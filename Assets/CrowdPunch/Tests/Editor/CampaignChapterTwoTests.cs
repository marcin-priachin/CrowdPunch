using System;
using System.IO;
using System.Linq;
using CrowdPunch.Authoring;
using CrowdPunch.Components;
using CrowdPunch.Configuration;
using CrowdPunch.Mono.Levels;
using CrowdPunch.Utilities;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CrowdPunch.Tests
{
    public sealed class CampaignChapterTwoTests
    {
        private const string Data = "Assets/CrowdPunch/Data/Campaign/";
        private const string Scenes = "Assets/CrowdPunch/Scenes/Campaign/";

        [Test]
        public void Loop007_ExistingChapterOneSaveContinuesIntoChapterTwoAndUnlocksFutureChapter()
        {
            string path = Path.GetFullPath("Temp/CampaignTests/"+Guid.NewGuid()+".json");
            try
            {
                var catalog = AssetDatabase.LoadAssetAtPath<CampaignCatalog>(Data+"Campaign.asset");
                var progress = new CampaignProgress(path);
                for (int i=0;i<10;i++) progress.Complete(catalog.Get(i).id);
                progress = new CampaignProgress(path);
                Assert.That(progress.NextUnfinished(catalog),Is.EqualTo(10));
                Assert.That(catalog.Get(10).Available && progress.IsUnlocked(catalog,10),Is.True);
                Assert.That(progress.IsUnlocked(catalog,11),Is.False);
                for (int i=10;i<20;i++) progress.Complete(catalog.Get(i).id);
                progress.Complete(catalog.Get(9).id);
                progress = new CampaignProgress(path);
                Assert.That(progress.NextUnfinished(catalog),Is.EqualTo(20));
                Assert.That(progress.IsUnlocked(catalog,20),Is.True);
                Assert.That(catalog.Get(20).Available,Is.False);
                Assert.That(progress.IsComplete("cp-001") && progress.IsComplete("cp-020"),Is.True);
            }
            finally { foreach (string suffix in new[]{"",".bak",".tmp"}) if(File.Exists(path+suffix)) File.Delete(path+suffix); }
        }

        [Test]
        public void Loop008_CrowdReactionUsesFullFootprintAndFiniteMixedCrowds()
        {
            var waves = AssetDatabase.FindAssets("t:EnemyWaveSettings",new[]{Data+"Waves"})
                .Select(g=>AssetDatabase.LoadAssetAtPath<EnemyWaveSettings>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(w=>w.name.StartsWith("CP18_")).OrderBy(w=>w.name).ToArray();
            Assert.That(waves.Select(w=>w.TotalEnemyCount),Is.EqualTo(new[]{52,70,88,108}));
            Assert.That(waves.Sum(w=>w.TotalEnemyCount),Is.EqualTo(318));
            Assert.That(waves.All(w=>w.SpawnMode==EnemyWaveSpawnMode.AllAtOnce && w.DelayBeforeWave==3 && w.Duration==0),Is.True);
            Assert.That(waves.All(w=>w.Enemies.All(e=>e.Settings!=null && e.Weight==0)
                && w.Enemies.Sum(e=>e.MinimumCount)==w.TotalEnemyCount),Is.True);
            var floor = AssetDatabase.LoadAssetAtPath<Mesh>(Data+"Layouts/Campaign_18_Floor.asset");
            Assert.That(floor.bounds.size.x,Is.EqualTo(100));
            Assert.That(floor.bounds.size.z,Is.EqualTo(100));
        }

        [Test]
        public void Loop002_ChapterTwoScenesWireIndependentSettingsAndValidObjectiveReferences()
        {
            var setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                for (int n=11;n<=20;n++)
                {
                    EditorSceneManager.OpenScene($"{Scenes}Campaign_{n:00}.unity");
                    Assert.That(UnityEngine.Object.FindFirstObjectByType<GauntletLevel>().OpeningHint,Is.Not.Empty);
                    var reference = UnityEngine.Object.FindFirstObjectByType<Unity.Scenes.SubScene>();
                    Assert.That(AssetDatabase.GetAssetPath(reference.SceneAsset),Is.EqualTo($"{Scenes}Campaign_{n:00}/Campaign_{n:00} Sub Scene.unity"));
                    EditorSceneManager.OpenScene(AssetDatabase.GetAssetPath(reference.SceneAsset));
                    var crowd = UnityEngine.Object.FindFirstObjectByType<EnemyWaveSequenceAuthoring>();
                    Assert.That(crowd.Waves.All(w=>w!=null && AssetDatabase.GetAssetPath(w).StartsWith(Data)),Is.True);
                    Assert.That(crowd.Waves.All(w=>w.Enemies.All(e=>e.Settings!=null)),Is.True);
                    AssertNavigationClearance(UnityEngine.Object.FindFirstObjectByType<NavigationArenaAuthoring>(),n);
                    var wall = UnityEngine.Object.FindFirstObjectByType<BarricadeAuthoring>();
                    if (wall!=null)
                    {
                        Assert.That(wall.transform.position.y-wall.size.y*.5f,Is.EqualTo(-1).Within(.001f),"Objective bottom must meet the floor.");
                        Assert.That(crowd.barricade,Is.EqualTo(wall));
                        Assert.That(AssetDatabase.GetAssetPath(wall.settings),Does.StartWith(Data));
                        Assert.That(wall.settings.replenishDelay,Is.EqualTo(4));
                        foreach (var visual in UnityEngine.Object.FindObjectsByType<BarricadeVisualAuthoring>(FindObjectsSortMode.None))
                            Assert.That(visual.barricade,Is.EqualTo(wall));
                    }
                    if (n==13)
                    {
                        // BARRICADE-006: the only crossing into the exit area is the gate.
                        Assert.That(wall.size.x,Is.EqualTo(2));
                        var floor=AssetDatabase.LoadAssetAtPath<Mesh>(Data+"Layouts/Campaign_13_Floor.asset");
                        var terrain=UnityEngine.Object.FindObjectsByType<SolidObstacleAuthoring>(FindObjectsSortMode.None)
                            .Where(o=>o.name.StartsWith("Gate terrain")).OrderBy(o=>o.transform.position.x).ToArray();
                        Assert.That(terrain.Length,Is.EqualTo(2));
                        Assert.That(terrain[0].transform.position.x+terrain[0].Size.x*.5f,Is.EqualTo(-wall.size.x*.5f));
                        Assert.That(terrain[1].transform.position.x-terrain[1].Size.x*.5f,Is.EqualTo(wall.size.x*.5f));
                        Assert.That(terrain[0].transform.position.x-terrain[0].Size.x*.5f,Is.LessThan(floor.bounds.min.x));
                        Assert.That(terrain[1].transform.position.x+terrain[1].Size.x*.5f,Is.GreaterThan(floor.bounds.max.x));
                        foreach(var solid in terrain)
                        {
                            Assert.That(solid.height,Is.GreaterThanOrEqualTo(wall.size.y));
                            float front=solid.transform.position.z-solid.Size.y*.5f;
                            Assert.That(front,Is.GreaterThan(wall.transform.position.z-wall.size.z*.5f),"Terrain must not shield the gate's front face.");
                            Assert.That(front,Is.LessThan(wall.transform.position.z+wall.size.z*.5f),"Terrain must overlap the gate to seal the side routes.");
                            Assert.That(solid.Size.y,Is.GreaterThanOrEqualTo(wall.size.z));
                        }
                        Assert.That(wall.exit.position.z-wall.exitRadius,
                            Is.GreaterThan(wall.transform.position.z+terrain[0].Size.y*.5f));
                    }
                    if (n==12)
                    {
                        var shell = wall.GetComponent<ShellTargetAuthoring>();
                        Assert.That(shell.settings.requiredExplosions,Is.EqualTo(3));
                        var baseline = AssetDatabase.LoadAssetAtPath<EnemySpawnSettings>("Assets/CrowdPunch/Data/Settings/Enemies/EnemySpawnSettings.asset");
                        Assert.That(shell.settings.coreMaxHealth,Is.EqualTo(baseline.Health.Max));
                        Assert.That(shell.settings.exploderReplacementDelay,Is.EqualTo(4));
                        Assert.That(crowd.Waves.Single().TotalEnemyCount,Is.EqualTo(10));
                    }
                    if (n==16 || n==19)
                    {
                        var track = wall.GetComponent<TrackObjectAuthoring>();
                        Assert.That(track.settings.requiredNetHits,Is.EqualTo(5));
                        Assert.That(AssetDatabase.GetAssetPath(track.settings),Does.StartWith(Data));
                        Assert.That(track.destination.position.z-wall.transform.position.z,Is.EqualTo(10));
                        Assert.That(track.transform.position.x,Is.EqualTo(n==19?5:0));
                        var socket = track.destination.GetComponentsInChildren<TrackSocketVisualAuthoring>();
                        Assert.That(socket.Length,Is.EqualTo(3));
                        Assert.That(socket.All(v=>v.trackObject==track),Is.True);
                        var markings=UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None)
                            .Where(r=>r.name=="Track rail" || r.name=="Direction arrow").ToArray();
                        Assert.That(markings.Length,Is.EqualTo(22));
                        Assert.That(markings.All(r=>r.bounds.max.y>-.99f),Is.True,"Track markings must remain above the floor after copying.");
                        Assert.That(crowd.Waves.Single().TotalEnemyCount,Is.EqualTo(n==19?10:8));
                    }
                    if (n==17)
                    {
                        Assert.That(wall.cover.target,Is.EqualTo(wall));
                        Assert.That(wall.cover.settings.openingDegrees,Is.EqualTo(90));
                        Assert.That(wall.cover.settings.degreesPerSecond,Is.EqualTo(25));
                        Assert.That(wall.settings.requiredHits,Is.EqualTo(3));
                    }
                    if (n==20)
                    {
                        var chicken = UnityEngine.Object.FindFirstObjectByType<ChickenBossAuthoring>();
                        Assert.That(crowd.chickenBoss,Is.EqualTo(chicken));
                        Assert.That(chicken.projectilePrefab,Is.Not.Null);
                        Assert.That(AssetDatabase.GetAssetPath(chicken.settings),Does.StartWith(Data));
                        var original = AssetDatabase.LoadAssetAtPath<ChickenBossSettings>("Assets/CrowdPunch/Data/Settings/ChickenBossSettings.asset");
                        Assert.That(JsonUtility.ToJson(chicken.settings),Is.EqualTo(JsonUtility.ToJson(original)));
                        Assert.That(crowd.Waves.Single().TotalEnemyCount,Is.EqualTo(6));
                        Assert.That(crowd.Waves.Single().BossReplenishDelay,Is.EqualTo(4));
                    }
                }
            }
            finally
            {
                if (setup.Any(s=>s.isLoaded && s.isActive)) EditorSceneManager.RestoreSceneManagerSetup(setup);
                else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            }
        }

        private static void AssertNavigationClearance(NavigationArenaAuthoring navigation, int number)
        {
            Assert.That(navigation.settings,Is.Not.Null);
            var arena=navigation.GetComponent<ArenaAuthoring>();
            var rectangles=new System.Collections.Generic.List<NavigationRectangle>();
            void Add(Vector3 p, Vector2 half) => rectangles.Add(new NavigationRectangle
            {
                Minimum=new float2(p.x-half.x,p.z-half.y), Maximum=new float2(p.x+half.x,p.z+half.y)
            });
            foreach(var obstacle in arena.GetComponentsInChildren<SolidObstacleAuthoring>()) Add(obstacle.transform.position,obstacle.Size*.5f);
            foreach(var cover in arena.GetComponentsInChildren<RotatingCoverAuthoring>())
                Add(cover.transform.position,Vector2.one*(Mathf.Max(3,cover.settings.radius)+Mathf.Clamp(cover.settings.thickness,.1f,1)));
            foreach(var shell in arena.GetComponentsInChildren<ShellTargetAuthoring>())
            {
                var wall=shell.GetComponent<BarricadeAuthoring>();
                Add(shell.transform.position,new Vector2(wall.size.x,wall.size.z)*.5f);
            }
            var center=((float3)arena.transform.position).xz+arena.SpacingCenterOffset.xz;
            using var obstacles=new NativeArray<NavigationRectangle>(rectangles.ToArray(),Allocator.Temp);
            var blob=NavigationGridConstruction.Build(center-arena.SpacingSize.xz*.5f,center+arena.SpacingSize.xz*.5f,
                navigation.settings.cellSize,(float3)navigation.settings.clearanceRadii+navigation.settings.clearanceMargin,obstacles,Allocator.Persistent);
            try
            {
                Assert.That(NavigationGeometry.TryResolveParticipationAnchor(ref blob.Value,navigation.overrideParticipationAnchor,
                    navigation.participationAnchor,out _),Is.True,$"Level {number} navigation anchor must clear every radius class.");
            }
            finally { blob.Dispose(); }
        }

        [Test]
        public void Chicken006_CampaignPreservesLegacyArenaTransformsAndCollisionMeshes()
        {
            var setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                string[] Snapshot(string path)
                {
                    var scene = EditorSceneManager.OpenScene(path);
                    return scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true))
                        .Select(t=>
                        {
                            var mesh=t.GetComponent<MeshCollider>();
                            return $"{t.name}|{t.position:R}|{t.rotation:R}|{t.lossyScale:R}|"+
                                (mesh!=null ? AssetDatabase.GetAssetPath(mesh.sharedMesh) : "");
                        })
                        .OrderBy(s=>s).ToArray();
                }
                var original = Snapshot("Assets/CrowdPunch/Scenes/Gauntlets/Gauntlet_20/Gauntlet_20 Sub Scene.unity");
                Assert.That(Snapshot(Scenes+"Campaign_20/Campaign_20 Sub Scene.unity"),Is.EqualTo(original));
            }
            finally
            {
                if (setup.Any(s=>s.isLoaded && s.isActive)) EditorSceneManager.RestoreSceneManagerSetup(setup);
                else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            }
        }
    }
}
