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
    public sealed class CampaignChapterFiveTests
    {
        private const string Data="Assets/CrowdPunch/Data/Campaign/";
        private const string Scenes="Assets/CrowdPunch/Scenes/Campaign/";

        [Test] public void Loop007_ChapterFourSaveContinuesThroughFiveAndUnlocksSix()
        {
            string path=Path.GetFullPath("Temp/CampaignTests/"+Guid.NewGuid()+".json");
            try
            {
                var catalog=AssetDatabase.LoadAssetAtPath<CampaignCatalog>(Data+"Campaign.asset");
                var progress=new CampaignProgress(path);
                for(int i=0;i<40;i++) progress.Complete(catalog.Get(i).id);
                progress=new CampaignProgress(path);
                Assert.That(progress.NextUnfinished(catalog),Is.EqualTo(40));
                Assert.That(catalog.Get(40).Available && progress.IsUnlocked(catalog,40),Is.True);
                for(int i=40;i<50;i++) progress.Complete(catalog.Get(i).id);
                progress=new CampaignProgress(path);
                Assert.That(progress.NextUnfinished(catalog),Is.EqualTo(50));
                Assert.That(progress.IsUnlocked(catalog,50),Is.True);
                Assert.That(catalog.Get(50).Available,Is.True);
            }
            finally { foreach(var suffix in new[]{"",".bak",".tmp"}) if(File.Exists(path+suffix)) File.Delete(path+suffix); }
        }

        [Test] public void Loop008And011_FirebreakHasFiveBatchedPhasesAndExistingSafeguards()
        {
            var waves=AssetDatabase.FindAssets("t:EnemyWaveSettings",new[]{Data+"Waves"})
                .Select(g=>AssetDatabase.LoadAssetAtPath<EnemyWaveSettings>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(w=>w.name.StartsWith("CP48_")).OrderBy(w=>w.name).ToArray();
            Assert.That(waves.Select(CampaignEnemyRevisionTests.PhysicalPopulation),Is.EqualTo(new[]{48,56,64,72,80}));
            Assert.That(waves.Select(w=>w.EliteEnemies.Sum(e=>e.Count)),Is.EqualTo(new[]{0,0,2,0,0}));
            Assert.That(waves.Sum(CampaignEnemyRevisionTests.PhysicalPopulation),Is.EqualTo(320));
            Assert.That(waves.Sum(w=>w.Enemies.Where(e=>e.Settings.name=="EnemySpawnSettings").Sum(e=>e.MinimumCount)),Is.EqualTo(144));
            Assert.That(waves[2].ArmoredAmmunitionProfile,Is.Not.Null);
            Assert.That(waves[4].WizardAmmunitionProfile,Is.Not.Null);
            Assert.That(waves[4].WaitForPersistentHazards,Is.True);
            Assert.That(waves.Take(4).All(w=>!w.WaitForPersistentHazards),Is.True);
            var floor=AssetDatabase.LoadAssetAtPath<Mesh>(Data+"Layouts/Campaign_48_Floor.asset");
            Assert.That(floor.bounds.size.x,Is.EqualTo(100)); Assert.That(floor.bounds.size.z,Is.EqualTo(100));
        }

        [TestCase(41), TestCase(42), TestCase(43), TestCase(44), TestCase(45)]
        [TestCase(46), TestCase(47), TestCase(48), TestCase(49), TestCase(50)]
        public void Loop002_ChapterFiveHasSafeNavigationExactWavesAndWiredObjectives(int n)
        {
            int[][] populations={new[]{8,16},new[]{8},new[]{7,10},new[]{15},new[]{16,20,22},new[]{12},new[]{16,20,22},new[]{48,56,64,72,80},new[]{9,12},new[]{13}};
            var setup=EditorSceneManager.GetSceneManagerSetup();
            try
            {
                EditorSceneManager.OpenScene($"{Scenes}Campaign_{n:00}.unity");
                Assert.That(UnityEngine.Object.FindFirstObjectByType<GauntletLevel>().OpeningHint,Is.Not.Empty);
                var sub=UnityEngine.Object.FindFirstObjectByType<Unity.Scenes.SubScene>();
                Assert.That(AssetDatabase.GetAssetPath(sub.SceneAsset),Is.EqualTo($"{Scenes}Campaign_{n:00}/Campaign_{n:00} Sub Scene.unity"));
                EditorSceneManager.OpenScene(AssetDatabase.GetAssetPath(sub.SceneAsset));
                var crowd=CampaignBarricadeRoomTests.FirstCrowd();
                Assert.That(crowd.Waves.All(w=>AssetDatabase.GetAssetPath(w).StartsWith(Data)),Is.True);
                Assert.That(crowd.Waves.Select(CampaignEnemyRevisionTests.PhysicalPopulation),Is.EqualTo(populations[n-41]),$"Level {n}");
                Assert.That(crowd.Waves.All(w=>w.Enemies.Sum(e=>e.MinimumCount)==w.TotalEnemyCount && w.Enemies.All(e=>e.Weight==0)),Is.True);
                CampaignChapterTwoTests.AssertNavigationClearance(UnityEngine.Object.FindFirstObjectByType<NavigationArenaAuthoring>(),n);
                if(n==42) CampaignBarricadeRoomTests.AssertRooms(n);
                if(n==44)
                {
                    Assert.That(crowd.barricade.settings.requiredHits,Is.EqualTo(4));
                    var cover=crowd.barricade.cover.settings;
                    Assert.That(cover.openingDegrees,Is.EqualTo(85)); Assert.That(cover.degreesPerSecond,Is.EqualTo(30));
                    Assert.That(cover.rotationMode,Is.EqualTo(CoverRotationMode.RotateAndPause));
                    Assert.That(cover.rotateSeconds,Is.EqualTo(3)); Assert.That(cover.pauseSeconds,Is.EqualTo(1));
                    Assert.That(cover.hitResponse,Is.EqualTo(CoverHitResponse.None));
                }
                if(n==45) Assert.That(crowd.Waves.All(w=>w.ArmoredAmmunitionProfile!=null),Is.True);
                if(n==46)
                {
                    var track=crowd.barricade.GetComponent<TrackObjectAuthoring>();
                    CampaignTrackRouteTests.AssertProgressiveTrack(track,n);
                    Assert.That(track.destination.GetComponentsInChildren<TrackSocketVisualAuthoring>().All(v=>v.trackObject==track),Is.True);
                    var marks=UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None).Where(r=>r.name=="Track rail" || r.name=="Direction arrow").ToArray();
                    Assert.That(marks.Length,Is.EqualTo(18*(Array.IndexOf(CrowdPunch.Editor.GauntletProgressionBuilder.CampaignTrackLevels,n)+1))); Assert.That(marks.All(r=>r.bounds.max.y>-.99f),Is.True);
                }
                if(n==47)
                {
                    var point=crowd.GetComponent<ProtectedPointAuthoring>();
                    Assert.That(point.zoneSize,Is.EqualTo(new Vector2(8,4))); Assert.That(point.zoneCenter,Is.EqualTo(new Vector2(0,-46)));
                    Assert.That(point.settings.breachThreshold,Is.EqualTo(1)); Assert.That(point.settings.maximumPlayerAttackers,Is.EqualTo(3));
                    foreach(var w in crowd.Waves)
                    {
                        Assert.That(w.BatchSize,Is.EqualTo(4)); Assert.That(w.BatchInterval,Is.EqualTo(4));
                        Assert.That(w.ReplenishWhileBossLives || w.WaitForPersistentHazards,Is.False);
                        Assert.That(w.WizardAmmunitionProfile,Is.Null); Assert.That(w.ArmoredAmmunitionProfile,Is.Null);
                    }
                    Assert.That(crowd.Waves.Skip(1).All(w=>w.DelayBeforeWave==0),Is.True);
                }
                if(n==49) Assert.That(crowd.Waves[0].WizardAmmunitionProfile!=null && crowd.Waves[0].WaitForPersistentHazards,Is.True);
                var patches=UnityEngine.Object.FindObjectsByType<GroundHazardAuthoring>(FindObjectsSortMode.None);
                Assert.That(patches.Length,Is.EqualTo(n==42?3:n==48?5:n is 45 or 47 or 50?2:0));
                foreach(var patch in patches)
                {
                    Assert.That(UnityEngine.Object.FindObjectsByType<EnemyWaveSequenceAuthoring>(FindObjectsSortMode.None).Contains(patch.sequence),Is.True); Assert.That(patch.shape,Is.EqualTo(GroundHazardShape.Rectangle));
                    if(n==42 && patch.sequence.barricade.exit==null)
                    {
                        Assert.That(patch.width,Is.GreaterThan(0).And.LessThan(3)); Assert.That(patch.depth,Is.GreaterThan(0).And.LessThan(6));
                        Assert.That(patch.operation,Is.EqualTo(GroundHazardOperation.Periodic));
                    }
                    else
                    {
                        bool permanent=n==42 || n==48 && patch.name.EndsWith("outer pocket");
                        bool shortStrip=n==47 || n==45 && patch.name=="Southeast warning crossing";
                        Assert.That(patch.width,Is.EqualTo(3)); Assert.That(patch.depth,Is.EqualTo(permanent||shortStrip?6:8));
                        Assert.That(patch.operation,Is.EqualTo(permanent?GroundHazardOperation.AlwaysActive:GroundHazardOperation.Periodic));
                    }
                    if(n is 45 or 47 or 48)
                    {
                        Assert.That(patch.GetComponent<GroundHazardAppearanceAuthoring>().style,Is.EqualTo(GroundHazardStyle.Necropolis));
                        foreach(var rock in UnityEngine.Object.FindObjectsByType<SolidObstacleAuthoring>(FindObjectsSortMode.None).Where(o=>o.name=="Rock island"))
                        {
                            Vector3 d=patch.transform.position-rock.transform.position;
                            var gap=new Vector2(
                                Mathf.Max(0,Mathf.Abs(d.x)-(patch.width+rock.footprint.x)*.5f),
                                Mathf.Max(0,Mathf.Abs(d.z)-(patch.depth+rock.footprint.y)*.5f));
                            Assert.That(gap.magnitude,Is.GreaterThanOrEqualTo(4),$"Level {n}, {patch.name} is too close to {rock.name} at {rock.transform.position}.");
                        }
                    }
                    foreach(var range in patch.sequence.Waves.SelectMany(w=>w.SpawnRectangles))
                    {
                        var d=new Vector2(Mathf.Abs(range.Center.x-patch.transform.position.x),Mathf.Abs(range.Center.z-patch.transform.position.z));
                        Assert.That(d.x>range.Width*.5f+patch.width*.5f+.5f || d.y>range.Depth*.5f+patch.depth*.5f+.5f,Is.True,$"Level {n} spawn bank overlaps hazard.");
                    }
                }
                if(n is 45 or 47 or 48)
                {
                    float[] offsets=n==48?new[]{0f,8f/3f,16f/3f}:new[]{0f,4f};
                    Assert.That(patches.Where(p=>p.operation==GroundHazardOperation.Periodic).Select(p=>p.cycleOffset).OrderBy(x=>x),Is.EqualTo(offsets));
                }
                if(n==50)
                {
                    var boss=UnityEngine.Object.FindFirstObjectByType<BossEncounterAuthoring>();
                    Assert.That(AssetDatabase.GetAssetPath(boss.settings),Does.StartWith(Data));
                    var tuning=boss.settings.Bake();
                    Assert.That(tuning.Health,Is.EqualTo(360));
                    Assert.That(new[]{tuning.Slam.Anticipation,tuning.Lunge.Anticipation,tuning.Sweep.Anticipation},Is.EqualTo(new[]{.65f,.65f,.85f}));
                    Assert.That(new[]{tuning.Slam.Recovery,tuning.Lunge.Recovery,tuning.Sweep.Recovery},Is.EqualTo(new[]{2.8f,2.8f,3f}));
                    Assert.That(crowd.Waves.Single().ReplenishWhileBossLives,Is.True);
                    Assert.That(crowd.Waves.Single().BossReplenishDelay,Is.EqualTo(4));
                    Assert.That(patches.Select(p=>p.cycleOffset).OrderBy(x=>x),Is.EqualTo(new[]{0f,4f}));
                }
            }
            finally { Restore(setup); }
        }

        [Test] public void Boss002_RematchWarningOverridePreservesLegacyFloor()
        {
            var legacy=AssetDatabase.LoadAssetAtPath<BossEncounterSettings>("Assets/CrowdPunch/Data/Settings/BossEncounterSettings.asset");
            Assert.That(legacy.Bake().Slam.Anticipation,Is.EqualTo(Mathf.Max(.8f,legacy.slam.Anticipation)));
            var intro=AssetDatabase.LoadAssetAtPath<BossEncounterSettings>(Data+"Settings/L10_Gatekeeper.asset");
            Assert.That(intro.Bake().Slam.Anticipation,Is.EqualTo(.8f));
        }

        [Test] public void Loop002_RematchPreservesLegacyBossArenaGeometry()
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
                Assert.That(Snapshot(Scenes+"Campaign_50/Campaign_50 Sub Scene.unity"),Is.EqualTo(Snapshot("Assets/CrowdPunch/Scenes/Gauntlets/Gauntlet_11/Gauntlet_11 Sub Scene.unity")));
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
