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
    public sealed class CampaignChapterFourTests
    {
        private const string Data="Assets/CrowdPunch/Data/Campaign/";
        private const string Scenes="Assets/CrowdPunch/Scenes/Campaign/";

        [Test] public void Loop007_ChapterThreeSaveContinuesThroughFourAndUnlocksFive()
        {
            string path=Path.GetFullPath("Temp/CampaignTests/"+Guid.NewGuid()+".json");
            try
            {
                var catalog=AssetDatabase.LoadAssetAtPath<CampaignCatalog>(Data+"Campaign.asset");
                var progress=new CampaignProgress(path);
                for(int i=0;i<30;i++) progress.Complete(catalog.Get(i).id);
                progress=new CampaignProgress(path);
                Assert.That(progress.NextUnfinished(catalog),Is.EqualTo(30));
                Assert.That(catalog.Get(30).Available && progress.IsUnlocked(catalog,30),Is.True);
                for(int i=30;i<40;i++) progress.Complete(catalog.Get(i).id);
                progress=new CampaignProgress(path);
                Assert.That(progress.NextUnfinished(catalog),Is.EqualTo(40));
                Assert.That(progress.IsUnlocked(catalog,40),Is.True);
                Assert.That(catalog.Get(40).Available,Is.True);
            }
            finally { foreach(var suffix in new[]{"",".bak",".tmp"}) if(File.Exists(path+suffix)) File.Delete(path+suffix); }
        }

        [Test] public void Loop008_LargeCrowdUsesFullFootprintWithFiniteMixedWavesAndSafeguards()
        {
            var waves=AssetDatabase.FindAssets("t:EnemyWaveSettings",new[]{Data+"Waves"})
                .Select(g=>AssetDatabase.LoadAssetAtPath<EnemyWaveSettings>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(w=>w.name.StartsWith("CP38_")).OrderBy(w=>w.name).ToArray();
            Assert.That(waves.Select(w=>w.TotalEnemyCount),Is.EqualTo(new[]{48,54,72,88}));
            Assert.That(waves.Select(w=>w.EliteEnemies.Sum(e=>e.Count)),Is.EqualTo(new[]{0,2,0,0}));
            Assert.That(waves.Sum(w=>w.TotalEnemyCount+w.EliteEnemies.Sum(e=>e.Count)),Is.EqualTo(264));
            Assert.That(waves.All(w=>w.SpawnMode==EnemyWaveSpawnMode.Batched && w.BatchSize==8 && w.BatchInterval==6),Is.True);
            Assert.That(waves.Take(2).All(w=>w.ArmoredAmmunitionProfile!=null),Is.True);
            for(int phase=0; phase<waves.Length; phase++)
            {
                bool timed = phase % 2 == 0;
                Assert.That(waves[phase].ActivationMode, Is.EqualTo(timed ? EnemyWaveActivationMode.DurationElapsed
                    : EnemyWaveActivationMode.AllCurrentAndPreviousEnemiesDefeated));
                Assert.That(waves[phase].Duration, Is.EqualTo(timed ? 6f : 0f));
                Assert.That(waves[phase].DelayBeforeWave, Is.EqualTo(timed ? 3f : 0f));
            }
            Assert.That(waves.Take(3).All(w=>w.WizardAmmunitionProfile==null && !w.WaitForPersistentHazards),Is.True);
            Assert.That(waves[3].WizardAmmunitionProfile,Is.Not.Null);
            Assert.That(waves[3].WaitForPersistentHazards,Is.True);
            var floor=AssetDatabase.LoadAssetAtPath<Mesh>(Data+"Layouts/Campaign_38_Floor.asset");
            Assert.That(floor.bounds.size.x,Is.EqualTo(100)); Assert.That(floor.bounds.size.z,Is.EqualTo(100));
        }

        [TestCase(31,33)]
        [TestCase(34,34)]
        [TestCase(35,40)]
        public void Loop002_ChapterFourScenesWireObjectivesSafeNavigationAndFiniteDefense(int first, int last)
        {
            var setup=EditorSceneManager.GetSceneManagerSetup();
            try
            {
                for(int n=first;n<=last;n++)
                {
                    EditorSceneManager.OpenScene($"{Scenes}Campaign_{n:00}.unity");
                    var sub=UnityEngine.Object.FindFirstObjectByType<Unity.Scenes.SubScene>();
                    Assert.That(AssetDatabase.GetAssetPath(sub.SceneAsset),Is.EqualTo($"{Scenes}Campaign_{n:00}/Campaign_{n:00} Sub Scene.unity"));
                    EditorSceneManager.OpenScene(AssetDatabase.GetAssetPath(sub.SceneAsset));
                    var crowd=CampaignBarricadeRoomTests.FirstCrowd();
                    Assert.That(crowd.Waves.All(w=>AssetDatabase.GetAssetPath(w).StartsWith(Data)),Is.True);
                    CampaignChapterTwoTests.AssertNavigationClearance(UnityEngine.Object.FindFirstObjectByType<NavigationArenaAuthoring>(),n);
                    if(n==31) Assert.That(crowd.Waves.All(w=>w.WizardAmmunitionProfile!=null && w.WaitForPersistentHazards),Is.True);
                    if(n==33) Assert.That(crowd.Waves.All(w=>w.WizardAmmunitionProfile==null && !w.WaitForPersistentHazards),Is.True);
                    if(n==32 || n==39)
                    {
                        var track=crowd.barricade.GetComponent<TrackObjectAuthoring>();
                        CampaignTrackRouteTests.AssertProgressiveTrack(track,n);
                        Assert.That(track.destination.GetComponentsInChildren<TrackSocketVisualAuthoring>().All(v=>v.trackObject==track),Is.True);
                        var marks=UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None).Where(r=>r.name=="Track rail" || r.name=="Direction arrow").ToArray();
                        Assert.That(marks.Length,Is.EqualTo(18*(Array.IndexOf(CrowdPunch.Editor.GauntletProgressionBuilder.CampaignTrackLevels,n)+1)));
                        Assert.That(marks.All(r=>r.bounds.max.y>-.99f),Is.True);
                    }
                    if(n==34) CampaignBarricadeRoomTests.AssertRooms(n);
                    if(n==35)
                    {
                        Assert.That(crowd.barricade.settings.requiredHits,Is.EqualTo(4));
                        Assert.That(crowd.barricade.cover.settings.openingDegrees,Is.EqualTo(100));
                        Assert.That(crowd.barricade.cover.settings.degreesPerSecond,Is.EqualTo(25));
                    }
                    if(n==36)
                    {
                        var point=crowd.GetComponent<ProtectedPointAuthoring>();
                        Assert.That(point.zoneSize,Is.EqualTo(new Vector2(8,4)));
                        Assert.That(point.zoneCenter,Is.EqualTo(new Vector2(0,-46)));
                        Assert.That(point.settings.breachThreshold,Is.EqualTo(1));
                        Assert.That(crowd.Waves.Select(w=>w.TotalEnemyCount),Is.EqualTo(new[]{14,18,20}));
                        foreach(var w in crowd.Waves)
                        {
                            Assert.That(w.BatchSize,Is.EqualTo(4)); Assert.That(w.BatchInterval,Is.EqualTo(4));
                            Assert.That(w.ReplenishWhileBossLives || w.WaitForPersistentHazards,Is.False);
                            Assert.That(w.WizardAmmunitionProfile,Is.Null); Assert.That(w.ArmoredAmmunitionProfile,Is.Null);
                            Assert.That(w.EliteEnemies,Is.Empty);
                        }
                        Assert.That(crowd.Waves.Take(2).All(w=>w.ActivationMode==EnemyWaveActivationMode.DurationElapsed && w.Duration==6),Is.True);
                        Assert.That(crowd.Waves.Last().ActivationMode,Is.EqualTo(EnemyWaveActivationMode.AllCurrentAndPreviousEnemiesDefeated));
                    }
                    if(n==37) Assert.That(crowd.barricade.GetComponent<ShellTargetAuthoring>().settings.requiredExplosions,Is.EqualTo(3));
                    var patches=UnityEngine.Object.FindObjectsByType<GroundHazardAuthoring>(FindObjectsSortMode.None);
                    int expectedPatches = n switch { 34 or 37 => 1, 35 or 36 => 2, 38 => 4, _ => 0 };
                    Assert.That(patches.Length,Is.EqualTo(expectedPatches), $"Level {n}");
                    if(n==35 || n==36 || n==38)
                    {
                        Assert.That(patches.Select(p=>p.cycleOffset).OrderBy(offset=>offset),
                            Is.EqualTo(n==38 ? new[]{0f,2f,4f,6f} : new[]{0f,4f}));
                        var rocks=UnityEngine.Object.FindObjectsByType<SolidObstacleAuthoring>(FindObjectsSortMode.None);
                        foreach(var patch in patches)
                        {
                            var half = patch.shape==GroundHazardShape.Circle ? Vector2.one*patch.radius : new Vector2(patch.width,patch.depth)*.5f;
                            foreach(var rock in rocks)
                            {
                                var delta = rock.transform.position-patch.transform.position;
                                var gap = Vector2.Max(Vector2.zero,
                                    new Vector2(Mathf.Abs(delta.x),Mathf.Abs(delta.z))-half-rock.Size*.5f);
                                Assert.That(gap.magnitude,Is.GreaterThanOrEqualTo(4),$"Level {n}, {patch.name} clearance");
                            }
                        }
                    }
                    foreach(var patch in patches)
                    {
                        Assert.That(UnityEngine.Object.FindObjectsByType<EnemyWaveSequenceAuthoring>(FindObjectsSortMode.None).Contains(patch.sequence),Is.True);
                        var half=patch.shape==GroundHazardShape.Circle?Vector2.one*patch.radius:new Vector2(patch.width,patch.depth)*.5f;
                        foreach(var range in crowd.Waves.SelectMany(w=>w.SpawnRectangles))
                        {
                            var d=new Vector2(Mathf.Abs(range.Center.x-patch.transform.position.x),Mathf.Abs(range.Center.z-patch.transform.position.z));
                            Assert.That(d.x>range.Width*.5f+half.x+.5f || d.y>range.Depth*.5f+half.y+.5f,Is.True,$"Level {n} spawn bank overlaps hazard.");
                        }
                    }
                    if(n==40)
                    {
                        Assert.That(crowd.dinoBoss,Is.Not.Null);
                        Assert.That(AssetDatabase.GetAssetPath(crowd.dinoBoss.settings),Does.StartWith(Data));
                        Assert.That(crowd.dinoBoss.settings.requiredSuccessfulHits,Is.EqualTo(3));
                        Assert.That(crowd.dinoBoss.settings.enemiesPerPillar,Is.EqualTo(2));
                        var pillars=UnityEngine.Object.FindObjectsByType<FallingPillarAuthoring>(FindObjectsSortMode.None);
                        Assert.That(pillars.Length,Is.EqualTo(3)); Assert.That(pillars.All(p=>p.boss==crowd.dinoBoss),Is.True);
                        Assert.That(crowd.Waves.Single().TotalEnemyCount,Is.EqualTo(8));
                        Assert.That(crowd.Waves.Single().ReplenishWhileBossLives,Is.True);
                    }
                }
            }
            finally { Restore(setup); }
        }

        [Test] public void Pillar006_CampaignPreservesLegacyArenaAndPillarGeometry()
        {
            var setup=EditorSceneManager.GetSceneManagerSetup();
            try
            {
                string[] Snapshot(string path)
                {
                    var scene=EditorSceneManager.OpenScene(path);
                    return scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).Select(t=>
                    {
                        var mesh=t.GetComponent<MeshCollider>();
                        return $"{t.name}|{t.position:R}|{t.rotation:R}|{t.lossyScale:R}|"+(mesh!=null?AssetDatabase.GetAssetPath(mesh.sharedMesh):"");
                    }).OrderBy(s=>s).ToArray();
                }
                Assert.That(Snapshot(Scenes+"Campaign_40/Campaign_40 Sub Scene.unity"),Is.EqualTo(Snapshot("Assets/CrowdPunch/Scenes/Gauntlets/Gauntlet_22/Gauntlet_22 Sub Scene.unity")));
            }
            finally { Restore(setup); }
        }

        private static void Restore(SceneSetup[] setup)
        {
            if(setup.Any(s=>s.isLoaded && s.isActive)) EditorSceneManager.RestoreSceneManagerSetup(setup);
            else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        }
    }
}
