using System;
using System.IO;
using System.Linq;
using CrowdPunch.Authoring;
using CrowdPunch.Components;
using CrowdPunch.Configuration;
using CrowdPunch.Mono.Levels;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CrowdPunch.Tests
{
    public sealed class CampaignChapterSevenTests
    {
        private const string Data="Assets/CrowdPunch/Data/Campaign/";
        private const string Scenes="Assets/CrowdPunch/Scenes/Campaign/";

        [Test] public void Loop007_ChapterSixSaveContinuesThroughSevenAndUnlocksUnavailableEight()
        {
            string path=Path.GetFullPath("Temp/CampaignTests/"+Guid.NewGuid()+".json");
            try
            {
                var catalog=AssetDatabase.LoadAssetAtPath<CampaignCatalog>(Data+"Campaign.asset");
                var progress=new CampaignProgress(path);
                for(int i=0;i<60;i++) progress.Complete(catalog.Get(i).id);
                progress=new CampaignProgress(path);
                Assert.That(progress.NextUnfinished(catalog),Is.EqualTo(60));
                Assert.That(catalog.Get(60).Available && progress.IsUnlocked(catalog,60),Is.True);
                for(int i=60;i<70;i++) progress.Complete(catalog.Get(i).id);
                progress=new CampaignProgress(path);
                Assert.That(progress.NextUnfinished(catalog),Is.EqualTo(70));
                Assert.That(progress.IsUnlocked(catalog,70),Is.True);
                Assert.That(catalog.Get(70).Available,Is.False);
            }
            finally { foreach(var suffix in new[]{"",".bak",".tmp"}) if(File.Exists(path+suffix)) File.Delete(path+suffix); }
        }

        [Test] public void Loop008_ShiftingFrontHasSixLargeWavesAndExistingSafeguards()
        {
            var waves=AssetDatabase.FindAssets("t:EnemyWaveSettings",new[]{Data+"Waves"})
                .Select(g=>AssetDatabase.LoadAssetAtPath<EnemyWaveSettings>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(w=>w.name.StartsWith("CP68_")).OrderBy(w=>w.name).ToArray();
            Assert.That(waves.Select(w=>w.TotalEnemyCount),Is.EqualTo(new[]{68,86,92,102,118,138}));
            Assert.That(waves.Select(w=>w.EliteEnemies.Sum(e=>e.Count)),Is.EqualTo(new[]{0,0,2,0,0,0}));
            Assert.That(waves.Sum(w=>w.TotalEnemyCount+w.EliteEnemies.Sum(e=>e.Count)),Is.EqualTo(606));
            Assert.That(waves.All(w=>w.SpawnMode==EnemyWaveSpawnMode.AllAtOnce && w.DelayBeforeWave==3 && w.Duration==0),Is.True);
            Assert.That(waves[2].ArmoredAmmunitionProfile,Is.Not.Null); Assert.That(waves[5].ArmoredAmmunitionProfile,Is.Not.Null);
            Assert.That(waves[4].WizardAmmunitionProfile,Is.Not.Null); Assert.That(waves[4].WaitForPersistentHazards,Is.True);
            Assert.That(waves.Where((w,i)=>i!=4).All(w=>!w.WaitForPersistentHazards),Is.True);
            var floor=AssetDatabase.LoadAssetAtPath<Mesh>(Data+"Layouts/Campaign_68_Floor.asset");
            Assert.That(floor.bounds.size.x,Is.EqualTo(100)); Assert.That(floor.bounds.size.z,Is.EqualTo(100));
        }

        [Test] public void Loop002_ChapterSevenWiresNavigableLayoutsSafeHazardsAndObjectiveSettings()
        {
            int[][] populations={new[]{13,18},new[]{16},new[]{19,24},new[]{16},new[]{21,24,26},new[]{14},new[]{23,26},new[]{68,86,94,102,118,138},new[]{12},new[]{10}};
            var setup=EditorSceneManager.GetSceneManagerSetup();
            try
            {
                for(int n=61;n<=70;n++)
                {
                    EditorSceneManager.OpenScene($"{Scenes}Campaign_{n:00}.unity");
                    Assert.That(UnityEngine.Object.FindFirstObjectByType<GauntletLevel>().OpeningHint,Is.Not.Empty);
                    var sub=UnityEngine.Object.FindFirstObjectByType<Unity.Scenes.SubScene>();
                    Assert.That(AssetDatabase.GetAssetPath(sub.SceneAsset),Is.EqualTo($"{Scenes}Campaign_{n:00}/Campaign_{n:00} Sub Scene.unity"));
                    EditorSceneManager.OpenScene(AssetDatabase.GetAssetPath(sub.SceneAsset));
                    var crowd=UnityEngine.Object.FindFirstObjectByType<EnemyWaveSequenceAuthoring>();
                    Assert.That(crowd.Waves.All(w=>AssetDatabase.GetAssetPath(w).StartsWith(Data)),Is.True);
                    Assert.That(crowd.Waves.Select(w=>w.TotalEnemyCount+w.EliteEnemies.Sum(e=>e.Count)),Is.EqualTo(populations[n-61]),$"Level {n}");
                    Assert.That(crowd.Waves.All(w=>w.Enemies.Sum(e=>e.MinimumCount)==w.TotalEnemyCount && w.Enemies.All(e=>e.Weight==0)),Is.True);
                    CampaignChapterTwoTests.AssertNavigationClearance(UnityEngine.Object.FindFirstObjectByType<NavigationArenaAuthoring>(),n);
                    if(n==61) Assert.That(crowd.Waves[0].ArmoredAmmunitionProfile,Is.Not.Null);
                    if(n==62) AssertOffsetGate(crowd.barricade);
                    if(n==63)
                    {
                        Assert.That(crowd.Waves[0].WizardAmmunitionProfile,Is.Not.Null);
                        Assert.That(crowd.Waves[0].WaitForPersistentHazards,Is.True);
                        Assert.That(crowd.Waves[1].WaitForPersistentHazards,Is.False);
                    }
                    if(n==64)
                    {
                        var shell=crowd.barricade.GetComponent<ShellTargetAuthoring>().settings;
                        Assert.That(shell.requiredExplosions,Is.EqualTo(4)); Assert.That(shell.exploderReplacementDelay,Is.EqualTo(4));
                        Assert.That(shell.coreMaxHealth,Is.GreaterThan(0));
                    }
                    if(n==65)
                    {
                        Assert.That(crowd.Waves[0].ArmoredAmmunitionProfile,Is.Not.Null);
                        Assert.That(crowd.Waves[1].WizardAmmunitionProfile,Is.Not.Null);
                        Assert.That(crowd.Waves[1].WaitForPersistentHazards,Is.True);
                        Assert.That(crowd.Waves[2].WaitForPersistentHazards,Is.False);
                    }
                    if(n==66)
                    {
                        Assert.That(crowd.barricade.settings.requiredHits,Is.EqualTo(4));
                        var cover=crowd.barricade.cover.settings;
                        Assert.That(cover.openingDegrees,Is.EqualTo(90)); Assert.That(cover.degreesPerSecond,Is.EqualTo(25));
                        Assert.That(cover.rotationMode,Is.EqualTo(CoverRotationMode.Continuous));
                        Assert.That(cover.hitResponse,Is.EqualTo(CoverHitResponse.Pause)); Assert.That(cover.hitPauseSeconds,Is.EqualTo(1));
                        Assert.That(crowd.barricade.cover.transform.position,Is.EqualTo(crowd.barricade.transform.position));
                    }
                    if(n==67) Assert.That(crowd.Waves[1].ArmoredAmmunitionProfile,Is.Not.Null);
                    if(n==69)
                    {
                        var track=crowd.barricade.GetComponent<TrackObjectAuthoring>();
                        var delta=track.destination.position-track.transform.position;
                        Assert.That(delta.magnitude,Is.EqualTo(10).Within(.001)); Assert.That(Mathf.Abs(delta.x),Is.LessThan(.001));
                        Assert.That(track.settings.requiredNetHits,Is.EqualTo(5));
                        Assert.That(track.destination.GetComponentsInChildren<TrackSocketVisualAuthoring>().All(v=>v.trackObject==track),Is.True);
                    }
                    var patches=UnityEngine.Object.FindObjectsByType<GroundHazardAuthoring>(FindObjectsSortMode.None);
                    Assert.That(patches.Length,Is.EqualTo(n==62||n==64||n==67||n==70?2:n==65?1:n==68?3:0));
                    foreach(var patch in patches)
                    {
                        Assert.That(patch.sequence,Is.EqualTo(crowd)); Assert.That(patch.shape,Is.EqualTo(GroundHazardShape.Rectangle));
                        Assert.That(patch.width,Is.EqualTo(3)); Assert.That(patch.depth,Is.EqualTo(n==64?6:8));
                        Assert.That(patch.operation,Is.EqualTo(n==64?GroundHazardOperation.AlwaysActive:GroundHazardOperation.Periodic));
                        Assert.That(patch.inactiveDuration,Is.EqualTo(4)); Assert.That(patch.warningDuration,Is.EqualTo(1.5f)); Assert.That(patch.activeDuration,Is.EqualTo(2.5f));
                        foreach(var range in crowd.Waves.SelectMany(w=>w.SpawnRectangles))
                        {
                            var d=new Vector2(Mathf.Abs(range.Center.x-patch.transform.position.x),Mathf.Abs(range.Center.z-patch.transform.position.z));
                            Assert.That(d.x>range.Width*.5f+patch.width*.5f+.5f || d.y>range.Depth*.5f+patch.depth*.5f+.5f,Is.True,$"Level {n} spawn bank overlaps hazard.");
                        }
                    }
                    if(patches.Length==2 && n!=64) Assert.That(patches.Select(p=>p.cycleOffset).OrderBy(x=>x),Is.EqualTo(new[]{0f,4f}));
                    if(n==68) Assert.That(patches.Select(p=>p.cycleOffset).OrderBy(x=>x),Is.EqualTo(new[]{0f,8f/3,16f/3}));
                    if(n==70)
                    {
                        Assert.That(crowd.rollingBoss,Is.Not.Null);
                        Assert.That(AssetDatabase.GetAssetPath(crowd.rollingBoss.settings),Does.StartWith(Data));
                        var tuning=crowd.rollingBoss.settings.Bake();
                        Assert.That(tuning.Health,Is.EqualTo(600)); Assert.That(tuning.Aim,Is.EqualTo(RollingAim.Reflect));
                        Assert.That(tuning.PauseDurations,Is.EqualTo(new Unity.Mathematics.float3(2.5f,1.8f,1.2f)));
                        var original=AssetDatabase.LoadAssetAtPath<RollingBossSettings>(Data+"Settings/L30_Rolling.asset").Bake();
                        Assert.That(tuning.RollSpeeds,Is.EqualTo(original.RollSpeeds));
                        Assert.That(crowd.Waves.Single().ReplenishWhileBossLives,Is.True); Assert.That(crowd.Waves.Single().BossReplenishDelay,Is.EqualTo(4));
                    }
                }
            }
            finally { Restore(setup); }
        }

        private static void AssertOffsetGate(BarricadeAuthoring gate)
        {
            Assert.That(gate.size.x,Is.EqualTo(2)); Assert.That(gate.settings.requiredHits,Is.EqualTo(4));
            Assert.That(gate.transform.position.x,Is.EqualTo(8));
            Assert.That(gate.exit.position.x,Is.EqualTo(8)); Assert.That(gate.exit.position.z,Is.GreaterThan(gate.transform.position.z));
            var sides=UnityEngine.Object.FindObjectsByType<SolidObstacleAuthoring>(FindObjectsSortMode.None).Where(o=>o.name.StartsWith("Gate terrain")).OrderBy(o=>o.transform.position.x).ToArray();
            Assert.That(sides.Length,Is.EqualTo(2));
            Assert.That(sides[0].transform.position.x+sides[0].Size.x*.5f,Is.EqualTo(7));
            Assert.That(sides[1].transform.position.x-sides[1].Size.x*.5f,Is.EqualTo(9));
            Assert.That(sides[0].transform.position.x-sides[0].Size.x*.5f,Is.LessThanOrEqualTo(-18));
            Assert.That(sides[1].transform.position.x+sides[1].Size.x*.5f,Is.GreaterThanOrEqualTo(18));
            foreach(var side in sides)
            {
                float front=side.transform.position.z-side.Size.y*.5f;
                Assert.That(front,Is.GreaterThan(gate.transform.position.z-gate.size.z*.5f));
                Assert.That(front,Is.LessThan(gate.transform.position.z+gate.size.z*.5f));
            }
        }

        [Test] public void Roll006_RematchPreservesOriginalArenaGeometryAndLegacyTuning()
        {
            var setup=EditorSceneManager.GetSceneManagerSetup();
            try
            {
                string[] Snapshot(string path)
                {
                    var scene=EditorSceneManager.OpenScene(path);
                    return scene.GetRootGameObjects().Where(g=>g.GetComponent<GroundHazardAuthoring>()==null)
                        .SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).Select(t=>
                    {
                        var mesh=t.GetComponent<MeshCollider>();
                        return $"{t.name}|{t.position:R}|{t.rotation:R}|{t.lossyScale:R}|"+(mesh!=null?AssetDatabase.GetAssetPath(mesh.sharedMesh):"");
                    }).OrderBy(s=>s).ToArray();
                }
                Assert.That(Snapshot(Scenes+"Campaign_70/Campaign_70 Sub Scene.unity"),Is.EqualTo(Snapshot("Assets/CrowdPunch/Scenes/Gauntlets/Gauntlet_21/Gauntlet_21 Sub Scene.unity")));
                var intro=AssetDatabase.LoadAssetAtPath<RollingBossSettings>(Data+"Settings/L30_Rolling.asset").Bake();
                Assert.That(intro.Health,Is.EqualTo(500)); Assert.That(intro.Aim,Is.EqualTo(RollingAim.ReaimOnCollision));
            }
            finally { Restore(setup); }
        }
        [Test] public void Loop002_NotchedFloorsHaveUpwardFacesAndExactPolygonArea()
        {
            for(int level=61;level<=69;level++)
            {
                var mesh=AssetDatabase.LoadAssetAtPath<Mesh>($"{Data}Layouts/Campaign_{level}_Floor.asset");
                var vertices=mesh.vertices; int n=vertices.Length/2;
                float polygonArea=0,triangleArea=0; int faces=0;
                for(int i=0;i<n;i++)
                {
                    var a=vertices[i]; var b=vertices[(i+1)%n];
                    polygonArea+=a.x*b.z-b.x*a.z;
                }
                var indices=mesh.triangles;
                for(int i=0;i<indices.Length;i+=3)
                {
                    var a=vertices[indices[i]];var b=vertices[indices[i+1]];var c=vertices[indices[i+2]];
                    if(a.y!=mesh.bounds.max.y || b.y!=a.y || c.y!=a.y) continue;
                    float normal=Vector3.Cross(b-a,c-a).y;
                    Assert.That(normal,Is.GreaterThan(0),$"Level {level} reversed or degenerate floor triangle.");
                    triangleArea+=normal*.5f; faces++;
                }
                Assert.That(faces,Is.EqualTo(n-2));
                Assert.That(triangleArea,Is.EqualTo(Mathf.Abs(polygonArea)*.5f).Within(.001f),$"Level {level} floor overlaps the notches.");
            }
        }

        private static void Restore(SceneSetup[] setup)
        {
            if(setup.Any(s=>s.isLoaded && s.isActive)) EditorSceneManager.RestoreSceneManagerSetup(setup);
            else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        }
    }
}
