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
    public sealed class CampaignChapterSixTests
    {
        private const string Data="Assets/CrowdPunch/Data/Campaign/";
        private const string Scenes="Assets/CrowdPunch/Scenes/Campaign/";

        [Test] public void Loop007_ChapterFiveSaveContinuesThroughSixAndUnlocksUnavailableSeven()
        {
            string path=Path.GetFullPath("Temp/CampaignTests/"+Guid.NewGuid()+".json");
            try
            {
                var catalog=AssetDatabase.LoadAssetAtPath<CampaignCatalog>(Data+"Campaign.asset");
                var progress=new CampaignProgress(path);
                for(int i=0;i<50;i++) progress.Complete(catalog.Get(i).id);
                progress=new CampaignProgress(path);
                Assert.That(progress.NextUnfinished(catalog),Is.EqualTo(50));
                Assert.That(catalog.Get(50).Available && progress.IsUnlocked(catalog,50),Is.True);
                for(int i=50;i<60;i++) progress.Complete(catalog.Get(i).id);
                progress=new CampaignProgress(path);
                Assert.That(progress.NextUnfinished(catalog),Is.EqualTo(60));
                Assert.That(progress.IsUnlocked(catalog,60),Is.True);
                Assert.That(catalog.Get(60).Available,Is.False);
            }
            finally { foreach(var suffix in new[]{"",".bak",".tmp"}) if(File.Exists(path+suffix)) File.Delete(path+suffix); }
        }

        [Test] public void Loop008_MovingBatteriesHasFiveLargeWavesAndExistingSafeguards()
        {
            var waves=AssetDatabase.FindAssets("t:EnemyWaveSettings",new[]{Data+"Waves"})
                .Select(g=>AssetDatabase.LoadAssetAtPath<EnemyWaveSettings>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(w=>w.name.StartsWith("CP58_")).OrderBy(w=>w.name).ToArray();
            Assert.That(waves.Select(w=>w.TotalEnemyCount),Is.EqualTo(new[]{76,96,100,118,138}));
            Assert.That(waves.Select(w=>w.EliteEnemies.Sum(e=>e.Count)),Is.EqualTo(new[]{1,0,2,0,0}));
            Assert.That(waves.Sum(w=>w.TotalEnemyCount+w.EliteEnemies.Sum(e=>e.Count)),Is.EqualTo(531));
            Assert.That(waves.All(w=>w.SpawnMode==EnemyWaveSpawnMode.AllAtOnce && w.DelayBeforeWave==3 && w.Duration==0),Is.True);
            Assert.That(waves[2].ArmoredAmmunitionProfile,Is.Not.Null);
            Assert.That(waves[3].WizardAmmunitionProfile,Is.Not.Null);
            Assert.That(waves[3].WaitForPersistentHazards,Is.True);
            Assert.That(waves.Where((w,i)=>i!=3).All(w=>!w.WaitForPersistentHazards),Is.True);
            var floor=AssetDatabase.LoadAssetAtPath<Mesh>(Data+"Layouts/Campaign_58_Floor.asset");
            Assert.That(floor.bounds.size.x,Is.EqualTo(100)); Assert.That(floor.bounds.size.z,Is.EqualTo(100));
        }

        [Test] public void Loop002_ChapterSixWiresSafeLayoutsExactWavesAndObjectiveSettings()
        {
            int[][] populations={new[]{12},new[]{19,24},new[]{16},new[]{16},new[]{16,22,21},new[]{14},new[]{22,26,26},new[]{77,96,102,118,138},new[]{14,18},new[]{10}};
            var setup=EditorSceneManager.GetSceneManagerSetup();
            try
            {
                for(int n=51;n<=60;n++)
                {
                    EditorSceneManager.OpenScene($"{Scenes}Campaign_{n:00}.unity");
                    Assert.That(UnityEngine.Object.FindFirstObjectByType<GauntletLevel>().OpeningHint,Is.Not.Empty);
                    var sub=UnityEngine.Object.FindFirstObjectByType<Unity.Scenes.SubScene>();
                    Assert.That(AssetDatabase.GetAssetPath(sub.SceneAsset),Is.EqualTo($"{Scenes}Campaign_{n:00}/Campaign_{n:00} Sub Scene.unity"));
                    EditorSceneManager.OpenScene(AssetDatabase.GetAssetPath(sub.SceneAsset));
                    var crowd=UnityEngine.Object.FindFirstObjectByType<EnemyWaveSequenceAuthoring>();
                    Assert.That(crowd.Waves.All(w=>AssetDatabase.GetAssetPath(w).StartsWith(Data)),Is.True);
                    Assert.That(crowd.Waves.Select(w=>w.TotalEnemyCount+w.EliteEnemies.Sum(e=>e.Count)),Is.EqualTo(populations[n-51]),$"Level {n}");
                    Assert.That(crowd.Waves.All(w=>w.Enemies.Sum(e=>e.MinimumCount)==w.TotalEnemyCount && w.Enemies.All(e=>e.Weight==0)),Is.True);
                    CampaignChapterTwoTests.AssertNavigationClearance(UnityEngine.Object.FindFirstObjectByType<NavigationArenaAuthoring>(),n);
                    if(n==51)
                    {
                        var shell=crowd.barricade.GetComponent<ShellTargetAuthoring>().settings;
                        Assert.That(shell.requiredExplosions,Is.EqualTo(3)); Assert.That(shell.exploderReplacementDelay,Is.EqualTo(4));
                        Assert.That(shell.coreMaxHealth,Is.GreaterThan(0));
                    }
                    if(n==52) Assert.That(crowd.Waves.All(w=>!w.WaitForPersistentHazards && w.WizardAmmunitionProfile==null),Is.True);
                    if(n==53)
                    {
                        Assert.That(crowd.barricade.settings.requiredHits,Is.EqualTo(4));
                        var cover=crowd.barricade.cover.settings;
                        Assert.That(cover.openingDegrees,Is.EqualTo(90)); Assert.That(cover.degreesPerSecond,Is.EqualTo(30));
                        Assert.That(cover.rotationMode,Is.EqualTo(CoverRotationMode.ReversePeriodically));
                        Assert.That(cover.reverseSeconds,Is.EqualTo(5)); Assert.That(cover.hitResponse,Is.EqualTo(CoverHitResponse.None));
                    }
                    if(n==54)
                    {
                        var track=crowd.barricade.GetComponent<TrackObjectAuthoring>();
                        var delta=track.destination.position-track.transform.position;
                        Assert.That(delta.magnitude,Is.EqualTo(10).Within(.001)); Assert.That(Mathf.Abs(delta.x),Is.GreaterThan(4));
                        Assert.That(track.settings.requiredNetHits,Is.EqualTo(6));
                        Assert.That(track.destination.GetComponentsInChildren<TrackSocketVisualAuthoring>().All(v=>v.trackObject==track),Is.True);
                        var marks=UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None).Where(r=>r.name=="Track rail" || r.name=="Direction arrow").ToArray();
                        Assert.That(marks.Length,Is.EqualTo(22)); Assert.That(marks.All(r=>r.bounds.max.y>-.99f),Is.True);
                    }
                    if(n==55)
                    {
                        Assert.That(crowd.Waves.All(w=>w.WizardAmmunitionProfile!=null && w.WaitForPersistentHazards),Is.True);
                        Assert.That(crowd.Waves[2].ArmoredAmmunitionProfile,Is.Not.Null);
                    }
                    if(n==56)
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
                    if(n==57)
                    {
                        var point=crowd.GetComponent<ProtectedPointAuthoring>();
                        Assert.That(point.zoneSize,Is.EqualTo(new Vector2(8,4))); Assert.That(point.zoneCenter,Is.EqualTo(new Vector2(0,-46)));
                        Assert.That(point.settings.breachThreshold,Is.EqualTo(1)); Assert.That(point.settings.maximumPlayerAttackers,Is.EqualTo(3));
                        foreach(var w in crowd.Waves)
                        {
                            Assert.That(w.EliteEnemies.Sum(e=>e.Count),Is.Zero);
                            Assert.That(w.BatchSize,Is.EqualTo(4)); Assert.That(w.BatchInterval,Is.EqualTo(3));
                            Assert.That(w.ReplenishWhileBossLives || w.WaitForPersistentHazards,Is.False);
                            Assert.That(w.WizardAmmunitionProfile,Is.Null); Assert.That(w.ArmoredAmmunitionProfile,Is.Null);
                        }
                        Assert.That(crowd.Waves.Skip(1).All(w=>w.DelayBeforeWave==5),Is.True);
                    }
                    var patches=UnityEngine.Object.FindObjectsByType<GroundHazardAuthoring>(FindObjectsSortMode.None);
                    Assert.That(patches.Length,Is.EqualTo(n==54||n==58||n==60?2:0));
                    foreach(var patch in patches)
                    {
                        Assert.That(patch.sequence,Is.EqualTo(crowd)); Assert.That(patch.shape,Is.EqualTo(GroundHazardShape.Rectangle));
                        Assert.That(patch.width,Is.EqualTo(3)); Assert.That(patch.depth,Is.EqualTo(8));
                        Assert.That(patch.operation,Is.EqualTo(GroundHazardOperation.Periodic));
                        Assert.That(patch.inactiveDuration,Is.EqualTo(4)); Assert.That(patch.warningDuration,Is.EqualTo(1.5f)); Assert.That(patch.activeDuration,Is.EqualTo(2.5f));
                        foreach(var range in crowd.Waves.SelectMany(w=>w.SpawnRectangles))
                        {
                            var d=new Vector2(Mathf.Abs(range.Center.x-patch.transform.position.x),Mathf.Abs(range.Center.z-patch.transform.position.z));
                            Assert.That(d.x>range.Width*.5f+patch.width*.5f+.5f || d.y>range.Depth*.5f+patch.depth*.5f+.5f,Is.True,$"Level {n} spawn bank overlaps hazard.");
                        }
                    }
                    if(patches.Length>0) Assert.That(patches.Select(p=>p.cycleOffset).OrderBy(x=>x),Is.EqualTo(new[]{0f,4f}));
                    if(n==60)
                    {
                        Assert.That(crowd.chickenBoss,Is.Not.Null);
                        Assert.That(AssetDatabase.GetAssetPath(crowd.chickenBoss.settings),Does.StartWith(Data));
                        var tuning=crowd.chickenBoss.settings.Bake();
                        Assert.That(tuning.Health,Is.EqualTo(2100)); Assert.That(tuning.ShotAim,Is.EqualTo(ChickenShotAim.MovementLead));
                        Assert.That(tuning.WindUp,Is.EqualTo(.8f)); Assert.That(tuning.ShotSpacing,Is.EqualTo(1.5f));
                        Assert.That(tuning.StageOnePattern,Is.EqualTo(ChickenPattern.Single));
                        Assert.That(tuning.StageTwoPattern,Is.EqualTo(ChickenPattern.Paired)); Assert.That(tuning.StageThreePattern,Is.EqualTo(ChickenPattern.Paired));
                        Assert.That(crowd.Waves.Single().ReplenishWhileBossLives,Is.True); Assert.That(crowd.Waves.Single().BossReplenishDelay,Is.EqualTo(4));
                    }
                }
            }
            finally { Restore(setup); }
        }

        [Test] public void Chicken006_RematchPreservesOriginalArenaGeometryAndLegacyTuning()
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
                Assert.That(Snapshot(Scenes+"Campaign_60/Campaign_60 Sub Scene.unity"),Is.EqualTo(Snapshot("Assets/CrowdPunch/Scenes/Gauntlets/Gauntlet_20/Gauntlet_20 Sub Scene.unity")));
                var intro=AssetDatabase.LoadAssetAtPath<ChickenBossSettings>(Data+"Settings/L20_Chicken.asset").Bake();
                Assert.That(intro.Health,Is.EqualTo(1800)); Assert.That(intro.ShotAim,Is.EqualTo(ChickenShotAim.PlayerPosition));
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
