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
    public sealed class CampaignChapterThreeTests
    {
        private const string Data="Assets/CrowdPunch/Data/Campaign/";
        private const string Scenes="Assets/CrowdPunch/Scenes/Campaign/";

        [Test] public void Loop007_ChapterTwoSaveContinuesThroughThreeAndUnlocksUnavailableFour()
        {
            string path=Path.GetFullPath("Temp/CampaignTests/"+Guid.NewGuid()+".json");
            try
            {
                var catalog=AssetDatabase.LoadAssetAtPath<CampaignCatalog>(Data+"Campaign.asset");
                var progress=new CampaignProgress(path);
                for(int i=0;i<20;i++) progress.Complete(catalog.Get(i).id);
                progress=new CampaignProgress(path);
                Assert.That(progress.NextUnfinished(catalog),Is.EqualTo(20));
                Assert.That(catalog.Get(20).Available && progress.IsUnlocked(catalog,20),Is.True);
                for(int i=20;i<30;i++) progress.Complete(catalog.Get(i).id);
                progress=new CampaignProgress(path);
                Assert.That(progress.NextUnfinished(catalog),Is.EqualTo(30));
                Assert.That(progress.IsUnlocked(catalog,30),Is.True);
                Assert.That(catalog.Get(30).Available,Is.False);
            }
            finally { foreach(var suffix in new[]{"",".bak",".tmp"}) if(File.Exists(path+suffix)) File.Delete(path+suffix); }
        }

        [Test] public void Loop008_AuthoredLargeCrowdIncludesArmorSupplyAndAdditionalElites()
        {
            var waves=AssetDatabase.FindAssets("t:EnemyWaveSettings",new[]{Data+"Waves"})
                .Select(g=>AssetDatabase.LoadAssetAtPath<EnemyWaveSettings>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(w=>w.name.StartsWith("CP28_")).OrderBy(w=>w.name).ToArray();
            Assert.That(waves.Select(w=>w.TotalEnemyCount),Is.EqualTo(new[]{59,76,96,108}));
            Assert.That(waves.Select(w=>w.EliteEnemies.Sum(e=>e.Count)),Is.EqualTo(new[]{0,1,0,2}));
            Assert.That(waves.Sum(w=>w.TotalEnemyCount+w.EliteEnemies.Sum(e=>e.Count)),Is.EqualTo(342));
            Assert.That(waves.All(w=>w.SpawnMode==EnemyWaveSpawnMode.AllAtOnce && w.DelayBeforeWave==3 && w.Duration==0),Is.True);
            Assert.That(waves[0].ArmoredAmmunitionProfile,Is.Not.Null);
            Assert.That(waves[3].ArmoredAmmunitionProfile,Is.Not.Null);
            var floor=AssetDatabase.LoadAssetAtPath<Mesh>(Data+"Layouts/Campaign_28_Floor.asset");
            Assert.That(floor.bounds.size.x,Is.EqualTo(100)); Assert.That(floor.bounds.size.z,Is.EqualTo(100));
        }

        [Test] public void Loop002_ChapterThreeScenesHaveSafeNavigationObjectivesAndHazardSpawnBanks()
        {
            var setup=EditorSceneManager.GetSceneManagerSetup();
            try
            {
                for(int n=21;n<=30;n++)
                {
                    EditorSceneManager.OpenScene($"{Scenes}Campaign_{n:00}.unity");
                    Assert.That(UnityEngine.Object.FindFirstObjectByType<GauntletLevel>().OpeningHint,Is.Not.Empty);
                    var sub=UnityEngine.Object.FindFirstObjectByType<Unity.Scenes.SubScene>();
                    Assert.That(AssetDatabase.GetAssetPath(sub.SceneAsset),Is.EqualTo($"{Scenes}Campaign_{n:00}/Campaign_{n:00} Sub Scene.unity"));
                    EditorSceneManager.OpenScene(AssetDatabase.GetAssetPath(sub.SceneAsset));
                    var crowd=UnityEngine.Object.FindFirstObjectByType<EnemyWaveSequenceAuthoring>();
                    Assert.That(crowd.Waves.All(w=>AssetDatabase.GetAssetPath(w).StartsWith(Data)),Is.True);
                    CampaignChapterTwoTests.AssertNavigationClearance(UnityEngine.Object.FindFirstObjectByType<NavigationArenaAuthoring>(),n);
                    if(n==21) Assert.That(crowd.Waves.All(w=>w.ArmoredAmmunitionProfile!=null),Is.True);
                    if(n==22)
                    {
                        Assert.That(crowd.barricade.size.x,Is.EqualTo(2));
                        var gate=crowd.barricade;
                        var barriers=UnityEngine.Object.FindObjectsByType<SolidObstacleAuthoring>(FindObjectsSortMode.None).Where(o=>o.name.StartsWith("Gate terrain")).ToArray();
                        Assert.That(barriers.Length,Is.EqualTo(2));
                        foreach(var rock in barriers)
                        {
                            Assert.That(Mathf.Abs(rock.transform.position.x)-rock.Size.x*.5f,Is.EqualTo(1));
                            float front=rock.transform.position.z-rock.Size.y*.5f;
                            Assert.That(front,Is.GreaterThan(gate.transform.position.z-gate.size.z*.5f));
                            Assert.That(front,Is.LessThan(gate.transform.position.z+gate.size.z*.5f));
                        }
                    }
                    if(n==24)
                    {
                        var point=crowd.GetComponent<ProtectedPointAuthoring>();
                        Assert.That(point.zoneSize,Is.EqualTo(new Vector2(8,4)));
                        Assert.That(point.zoneCenter,Is.EqualTo(new Vector2(0,-46)));
                        Assert.That(point.settings.breachThreshold,Is.EqualTo(1));
                        Assert.That(AssetDatabase.GetAssetPath(point.settings),Does.StartWith(Data));
                        Assert.That(crowd.Waves.Select(w=>w.TotalEnemyCount),Is.EqualTo(new[]{12,16,20}));
                        foreach(var w in crowd.Waves)
                        {
                            Assert.That(w.BatchSize,Is.EqualTo(4)); Assert.That(w.BatchInterval,Is.EqualTo(4));
                            Assert.That(w.ArmoredAmmunitionProfile,Is.Null); Assert.That(w.ReplenishWhileBossLives,Is.False);
                        }
                        Assert.That(crowd.Waves.Skip(1).All(w=>w.DelayBeforeWave==5),Is.True);
                    }
                    if(n==26)
                    {
                        var track=crowd.barricade.GetComponent<TrackObjectAuthoring>();
                        Assert.That(track.transform.position.x,Is.EqualTo(5));
                        Assert.That(track.settings.requiredNetHits,Is.EqualTo(5));
                        Assert.That(track.destination.position.z-track.transform.position.z,Is.EqualTo(10));
                    }
                    var patches=UnityEngine.Object.FindObjectsByType<GroundHazardAuthoring>(FindObjectsSortMode.None);
                    Assert.That(patches.Length,Is.EqualTo(n==28?3:n==25||n==26||n==27?1:0));
                    foreach(var patch in patches)
                    {
                        Assert.That(patch.sequence,Is.EqualTo(crowd)); Assert.That(patch.firstWave,Is.Zero);
                        Assert.That(patch.damage,Is.EqualTo(12)); Assert.That(patch.damageInterval,Is.EqualTo(.75f));
                        var half=patch.shape==GroundHazardShape.Circle?Vector2.one*patch.radius:new Vector2(patch.width,patch.depth)*.5f;
                        if(patch.operation==GroundHazardOperation.Periodic)
                        {
                            Assert.That(patch.inactiveDuration,Is.EqualTo(4)); Assert.That(patch.warningDuration,Is.EqualTo(1.5f));
                            Assert.That(patch.activeDuration,Is.EqualTo(2.5f));
                        }
                        foreach(var range in crowd.Waves.SelectMany(w=>w.SpawnRectangles))
                        {
                            var separation=new Vector2(Mathf.Abs(range.Center.x-patch.transform.position.x),Mathf.Abs(range.Center.z-patch.transform.position.z));
                            Assert.That(separation.x>range.Width*.5f+half.x+.5f || separation.y>range.Depth*.5f+half.y+.5f,Is.True,$"Level {n} spawn bank intersects a hazard footprint.");
                        }
                    }
                    if(n==29) { Assert.That(crowd.barricade.cover.settings.openingDegrees,Is.EqualTo(100)); Assert.That(crowd.barricade.cover.settings.degreesPerSecond,Is.EqualTo(20)); }
                    if(n==30)
                    {
                        Assert.That(crowd.rollingBoss,Is.Not.Null);
                        Assert.That(AssetDatabase.GetAssetPath(crowd.rollingBoss.settings),Does.StartWith(Data));
                        Assert.That(JsonUtility.ToJson(crowd.rollingBoss.settings),Is.EqualTo(JsonUtility.ToJson(AssetDatabase.LoadAssetAtPath<RollingBossSettings>("Assets/CrowdPunch/Data/Settings/RollingBossSettings.asset"))));
                        Assert.That(crowd.Waves.Single().TotalEnemyCount,Is.EqualTo(6));
                        Assert.That(crowd.Waves.Single().ReplenishWhileBossLives,Is.True);
                    }
                }
            }
            finally { Restore(setup); }
        }

        [Test] public void Roll006_CampaignPreservesLegacyBossArenaGeometry()
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
                Assert.That(Snapshot(Scenes+"Campaign_30/Campaign_30 Sub Scene.unity"),Is.EqualTo(Snapshot("Assets/CrowdPunch/Scenes/Gauntlets/Gauntlet_21/Gauntlet_21 Sub Scene.unity")));
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
