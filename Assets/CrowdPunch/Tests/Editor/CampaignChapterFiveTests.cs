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

        [Test] public void Loop007_ChapterFourSaveContinuesThroughFiveAndUnlocksUnavailableSix()
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
                Assert.That(catalog.Get(50).Available,Is.False);
            }
            finally { foreach(var suffix in new[]{"",".bak",".tmp"}) if(File.Exists(path+suffix)) File.Delete(path+suffix); }
        }

        [Test] public void Loop008_FirebreakHasFiveLargeWavesAndExistingSafeguards()
        {
            var waves=AssetDatabase.FindAssets("t:EnemyWaveSettings",new[]{Data+"Waves"})
                .Select(g=>AssetDatabase.LoadAssetAtPath<EnemyWaveSettings>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(w=>w.name.StartsWith("CP48_")).OrderBy(w=>w.name).ToArray();
            Assert.That(waves.Select(w=>w.TotalEnemyCount),Is.EqualTo(new[]{70,86,92,110,128}));
            Assert.That(waves.Select(w=>w.EliteEnemies.Sum(e=>e.Count)),Is.EqualTo(new[]{0,0,2,0,0}));
            Assert.That(waves.Sum(w=>w.TotalEnemyCount+w.EliteEnemies.Sum(e=>e.Count)),Is.EqualTo(488));
            Assert.That(waves.All(w=>w.SpawnMode==EnemyWaveSpawnMode.AllAtOnce && w.DelayBeforeWave==3 && w.Duration==0),Is.True);
            Assert.That(waves[2].ArmoredAmmunitionProfile,Is.Not.Null);
            Assert.That(waves[3].WizardAmmunitionProfile,Is.Not.Null);
            Assert.That(waves[3].WaitForPersistentHazards,Is.True);
            foreach(var w in new[]{waves[1],waves[4]}) Assert.That(w.WaitForPersistentHazards,Is.False);
            var floor=AssetDatabase.LoadAssetAtPath<Mesh>(Data+"Layouts/Campaign_48_Floor.asset");
            Assert.That(floor.bounds.size.x,Is.EqualTo(100)); Assert.That(floor.bounds.size.z,Is.EqualTo(100));
        }

        [Test] public void Loop002_ChapterFiveHasSafeNavigationExactWavesAndWiredObjectives()
        {
            int[][] populations={new[]{14,18},new[]{16},new[]{19,21},new[]{15},new[]{16,20,21},new[]{12},new[]{22,26,27},new[]{70,86,94,110,128},new[]{15,20},new[]{13}};
            var setup=EditorSceneManager.GetSceneManagerSetup();
            try
            {
                for(int n=41;n<=50;n++)
                {
                    EditorSceneManager.OpenScene($"{Scenes}Campaign_{n:00}.unity");
                    Assert.That(UnityEngine.Object.FindFirstObjectByType<GauntletLevel>().OpeningHint,Is.Not.Empty);
                    var sub=UnityEngine.Object.FindFirstObjectByType<Unity.Scenes.SubScene>();
                    Assert.That(AssetDatabase.GetAssetPath(sub.SceneAsset),Is.EqualTo($"{Scenes}Campaign_{n:00}/Campaign_{n:00} Sub Scene.unity"));
                    EditorSceneManager.OpenScene(AssetDatabase.GetAssetPath(sub.SceneAsset));
                    var crowd=UnityEngine.Object.FindFirstObjectByType<EnemyWaveSequenceAuthoring>();
                    Assert.That(crowd.Waves.All(w=>AssetDatabase.GetAssetPath(w).StartsWith(Data)),Is.True);
                    Assert.That(crowd.Waves.Select(w=>w.TotalEnemyCount+w.EliteEnemies.Sum(e=>e.Count)),Is.EqualTo(populations[n-41]),$"Level {n}");
                    Assert.That(crowd.Waves.All(w=>w.Enemies.Sum(e=>e.MinimumCount)==w.TotalEnemyCount && w.Enemies.All(e=>e.Weight==0)),Is.True);
                    CampaignChapterTwoTests.AssertNavigationClearance(UnityEngine.Object.FindFirstObjectByType<NavigationArenaAuthoring>(),n);
                    if(n==42)
                    {
                        var gate=crowd.barricade;
                        Assert.That(gate.size.x,Is.EqualTo(2)); Assert.That(gate.settings.requiredHits,Is.EqualTo(4));
                        var sides=UnityEngine.Object.FindObjectsByType<SolidObstacleAuthoring>(FindObjectsSortMode.None).Where(o=>o.name.StartsWith("Gate terrain")).ToArray();
                        Assert.That(sides.Length,Is.EqualTo(2));
                        foreach(var side in sides)
                        {
                            Assert.That(Mathf.Abs(side.transform.position.x)-side.Size.x*.5f,Is.EqualTo(1));
                            float front=side.transform.position.z-side.Size.y*.5f;
                            Assert.That(front,Is.GreaterThan(gate.transform.position.z-gate.size.z*.5f));
                            Assert.That(front,Is.LessThan(gate.transform.position.z+gate.size.z*.5f));
                        }
                    }
                    if(n==44)
                    {
                        Assert.That(crowd.barricade.settings.requiredHits,Is.EqualTo(4));
                        var cover=crowd.barricade.cover.settings;
                        Assert.That(cover.openingDegrees,Is.EqualTo(85)); Assert.That(cover.degreesPerSecond,Is.EqualTo(30));
                        Assert.That(cover.rotationMode,Is.EqualTo(CoverRotationMode.RotateAndPause));
                        Assert.That(cover.rotateSeconds,Is.EqualTo(3)); Assert.That(cover.pauseSeconds,Is.EqualTo(1));
                        Assert.That(cover.hitResponse,Is.EqualTo(CoverHitResponse.None));
                    }
                    if(n==45) Assert.That(crowd.Waves.Take(2).All(w=>w.ArmoredAmmunitionProfile!=null),Is.True);
                    if(n==46)
                    {
                        var track=crowd.barricade.GetComponent<TrackObjectAuthoring>();
                        var delta=track.destination.position-track.transform.position;
                        Assert.That(delta.magnitude,Is.EqualTo(10).Within(.001)); Assert.That(Mathf.Abs(delta.x),Is.GreaterThan(4));
                        Assert.That(track.settings.requiredNetHits,Is.EqualTo(5));
                        Assert.That(track.destination.GetComponentsInChildren<TrackSocketVisualAuthoring>().All(v=>v.trackObject==track),Is.True);
                        var marks=UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None).Where(r=>r.name=="Track rail" || r.name=="Direction arrow").ToArray();
                        Assert.That(marks.Length,Is.EqualTo(22)); Assert.That(marks.All(r=>r.bounds.max.y>-.99f),Is.True);
                    }
                    if(n==47)
                    {
                        var point=crowd.GetComponent<ProtectedPointAuthoring>();
                        Assert.That(point.zoneSize,Is.EqualTo(new Vector2(8,4))); Assert.That(point.zoneCenter,Is.EqualTo(new Vector2(0,-46)));
                        Assert.That(point.settings.breachThreshold,Is.EqualTo(1)); Assert.That(point.settings.maximumPlayerAttackers,Is.EqualTo(3));
                        foreach(var w in crowd.Waves)
                        {
                            Assert.That(w.BatchSize,Is.EqualTo(4)); Assert.That(w.BatchInterval,Is.EqualTo(3));
                            Assert.That(w.ReplenishWhileBossLives || w.WaitForPersistentHazards,Is.False);
                            Assert.That(w.WizardAmmunitionProfile,Is.Null); Assert.That(w.ArmoredAmmunitionProfile,Is.Null);
                        }
                        Assert.That(crowd.Waves.Skip(1).All(w=>w.DelayBeforeWave==5),Is.True);
                    }
                    if(n==49) Assert.That(crowd.Waves[0].WizardAmmunitionProfile!=null && crowd.Waves[0].WaitForPersistentHazards,Is.True);
                    var patches=UnityEngine.Object.FindObjectsByType<GroundHazardAuthoring>(FindObjectsSortMode.None);
                    Assert.That(patches.Length,Is.EqualTo(n==42||n==48||n==50?2:n==45?1:0));
                    foreach(var patch in patches)
                    {
                        Assert.That(patch.sequence,Is.EqualTo(crowd)); Assert.That(patch.shape,Is.EqualTo(GroundHazardShape.Rectangle));
                        Assert.That(patch.width,Is.EqualTo(3)); Assert.That(patch.depth,Is.EqualTo(n==42||n==48?6:8));
                        Assert.That(patch.operation,Is.EqualTo(n==42||n==48?GroundHazardOperation.AlwaysActive:GroundHazardOperation.Periodic));
                        foreach(var range in crowd.Waves.SelectMany(w=>w.SpawnRectangles))
                        {
                            var d=new Vector2(Mathf.Abs(range.Center.x-patch.transform.position.x),Mathf.Abs(range.Center.z-patch.transform.position.z));
                            Assert.That(d.x>range.Width*.5f+patch.width*.5f+.5f || d.y>range.Depth*.5f+patch.depth*.5f+.5f,Is.True,$"Level {n} spawn bank overlaps hazard.");
                        }
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
