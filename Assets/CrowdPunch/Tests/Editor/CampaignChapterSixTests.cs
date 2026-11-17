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

        [Test] public void Loop007_ChapterFiveSaveContinuesThroughSixAndUnlocksSeven()
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
                Assert.That(catalog.Get(60).Available,Is.True);
            }
            finally { foreach(var suffix in new[]{"",".bak",".tmp"}) if(File.Exists(path+suffix)) File.Delete(path+suffix); }
        }

        [Test] public void Loop008And011_MovingBatteriesHasFiveBatchedPhasesAndExistingSafeguards()
        {
            var waves=AssetDatabase.FindAssets("t:EnemyWaveSettings",new[]{Data+"Waves"})
                .Select(g=>AssetDatabase.LoadAssetAtPath<EnemyWaveSettings>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(w=>w.name.StartsWith("CP58_")).OrderBy(w=>w.name).ToArray();
            Assert.That(waves.Select(CampaignEnemyRevisionTests.PhysicalPopulation),Is.EqualTo(new[]{48,56,64,72,88}));
            Assert.That(waves.Select(w=>w.EliteEnemies.Sum(e=>e.Count)),Is.EqualTo(new[]{0,0,2,0,0}));
            Assert.That(waves.Sum(CampaignEnemyRevisionTests.PhysicalPopulation),Is.EqualTo(328));
            Assert.That(waves.Sum(w=>w.Enemies.Where(e=>e.Settings.name=="EnemySpawnSettings").Sum(e=>e.MinimumCount)),Is.EqualTo(146));
            Assert.That(waves[2].ArmoredAmmunitionProfile,Is.Not.Null);
            Assert.That(waves[4].WizardAmmunitionProfile,Is.Not.Null);
            Assert.That(waves[4].WaitForPersistentHazards,Is.True);
            Assert.That(waves.Take(4).All(w=>!w.WaitForPersistentHazards),Is.True);
            var floor=AssetDatabase.LoadAssetAtPath<Mesh>(Data+"Layouts/Campaign_58_Floor.asset");
            Assert.That(floor.bounds.size.x,Is.EqualTo(100)); Assert.That(floor.bounds.size.z,Is.EqualTo(100));
        }

        [TestCase(51), TestCase(52), TestCase(53), TestCase(54), TestCase(55)]
        [TestCase(56), TestCase(57), TestCase(58), TestCase(59), TestCase(60)]
        public void Loop002_ChapterSixWiresSafeLayoutsExactWavesAndObjectiveSettings(int n)
        {
            int[][] populations={new[]{12},new[]{7,14},new[]{16},new[]{16},new[]{20,22,24},new[]{8},new[]{16,20,22},new[]{48,56,64,72,88},new[]{10,10},new[]{10}};
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
                Assert.That(crowd.Waves.Select(CampaignEnemyRevisionTests.PhysicalPopulation),Is.EqualTo(populations[n-51]),$"Level {n}");
                Assert.That(crowd.Waves.All(w=>w.Enemies.Sum(e=>e.MinimumCount)==w.TotalEnemyCount && w.Enemies.All(e=>e.Weight==0)),Is.True);
                CampaignChapterTwoTests.AssertNavigationClearance(UnityEngine.Object.FindFirstObjectByType<NavigationArenaAuthoring>(),n);
                if(n is 53 or 54)
                {
                    var support=crowd.Waves.Single();
                    string shooter=n==53?"MushroomShooterSpawnSettings":"SporellaSpawnSettings";
                    Assert.That(support.Enemies.ToDictionary(e=>e.Settings.name,e=>e.MinimumCount),Is.EquivalentTo(
                        new System.Collections.Generic.Dictionary<string,int> {
                            {"EnemySpawnSettings",13},{"ExplosiveEnemySpawnSettings",2},{shooter,1}
                        }));
                    Assert.That(support.ReplenishWhileBossLives,Is.True);
                    Assert.That(support.BossReplenishDelay,Is.EqualTo(4));
                    Assert.That(support.BatchSize,Is.EqualTo(4)); Assert.That(support.BatchInterval,Is.EqualTo(1.5f));
                }
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
                    CampaignTrackRouteTests.AssertProgressiveTrack(track,n);
                    Assert.That(track.destination.GetComponentsInChildren<TrackSocketVisualAuthoring>().All(v=>v.trackObject==track),Is.True);
                    var marks=UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None).Where(r=>r.name=="Track rail" || r.name=="Direction arrow").ToArray();
                    Assert.That(marks.Length,Is.EqualTo(18*(Array.IndexOf(CrowdPunch.Editor.GauntletProgressionBuilder.CampaignTrackLevels,n)+1))); Assert.That(marks.All(r=>r.bounds.max.y>-.99f),Is.True);
                }
                if(n==55)
                {
                    Assert.That(crowd.Waves.Take(2).All(w=>w.WizardAmmunitionProfile==null && !w.WaitForPersistentHazards),Is.True);
                    Assert.That(crowd.Waves[2].WizardAmmunitionProfile!=null && crowd.Waves[2].WaitForPersistentHazards,Is.True);
                    Assert.That(crowd.Waves[2].ArmoredAmmunitionProfile,Is.Not.Null);
                }
                if(n==56) CampaignBarricadeRoomTests.AssertRooms(n);
                if(n==57)
                {
                    var point=crowd.GetComponent<ProtectedPointAuthoring>();
                    Assert.That(point.zoneSize,Is.EqualTo(new Vector2(8,4))); Assert.That(point.zoneCenter,Is.EqualTo(new Vector2(0,-46)));
                    Assert.That(point.settings.breachThreshold,Is.EqualTo(1)); Assert.That(point.settings.maximumPlayerAttackers,Is.EqualTo(3));
                    foreach(var w in crowd.Waves)
                    {
                        Assert.That(w.EliteEnemies.Sum(e=>e.Count),Is.Zero);
                        Assert.That(w.BatchSize,Is.EqualTo(4)); Assert.That(w.BatchInterval,Is.EqualTo(4));
                        Assert.That(w.ReplenishWhileBossLives || w.WaitForPersistentHazards,Is.False);
                        Assert.That(w.WizardAmmunitionProfile,Is.Null); Assert.That(w.ArmoredAmmunitionProfile,Is.Null);
                    }
                    Assert.That(crowd.Waves.Skip(1).All(w=>w.DelayBeforeWave==0),Is.True);
                }
                var patches=UnityEngine.Object.FindObjectsByType<GroundHazardAuthoring>(FindObjectsSortMode.None);
                Assert.That(patches.Length,Is.EqualTo(n==58?5:n is 54 or 55 or 57?3:n==60?2:0));
                foreach(var patch in patches)
                {
                    Assert.That(patch.sequence,Is.EqualTo(crowd)); Assert.That(patch.shape,Is.EqualTo(GroundHazardShape.Rectangle));
                    Assert.That(patch.width,Is.EqualTo(3));
                    Assert.That(patch.depth,Is.EqualTo(n==54?4:n is 55 or 57?6:8));
                    Assert.That(patch.operation,Is.EqualTo(GroundHazardOperation.Periodic));
                    Assert.That(patch.inactiveDuration,Is.EqualTo(4)); Assert.That(patch.warningDuration,Is.EqualTo(1.5f)); Assert.That(patch.activeDuration,Is.EqualTo(2.5f));
                    if(n is 54 or 55 or 57 or 58)
                    {
                        Assert.That(patch.GetComponent<GroundHazardAppearanceAuthoring>().style,Is.EqualTo(GroundHazardStyle.Canyon));
                        AssertHazardClearance(n,patch);
                    }
                    foreach(var range in crowd.Waves.SelectMany(w=>w.SpawnRectangles))
                    {
                        var d=new Vector2(Mathf.Abs(range.Center.x-patch.transform.position.x),Mathf.Abs(range.Center.z-patch.transform.position.z));
                        Assert.That(d.x>range.Width*.5f+patch.width*.5f+.5f || d.y>range.Depth*.5f+patch.depth*.5f+.5f,Is.True,$"Level {n} spawn bank overlaps hazard.");
                    }
                }
                if(patches.Length>0)
                {
                    float[] offsets=n switch {54=>new[]{0f,2f,4f},55 or 57=>new[]{0f,8f/3f,16f/3f},58=>new[]{0f,1.6f,3.2f,4.8f,6.4f},_=>new[]{0f,4f}};
                    Assert.That(patches.Select(p=>p.cycleOffset).OrderBy(x=>x),Is.EqualTo(offsets));
                }
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
            finally { Restore(setup); }
        }

        private static void AssertHazardClearance(int level, GroundHazardAuthoring patch)
        {
            Vector2 center=new(patch.transform.position.x,patch.transform.position.z);
            Vector2 halfSize=new(patch.width*.5f,patch.depth*.5f);
            void Check(Vector2 min, Vector2 max, string obstacle, float clearance=4)
            {
                Vector2 gap=Vector2.Max(Vector2.zero,Vector2.Max(min-center-halfSize,center-halfSize-max));
                Assert.That(gap.magnitude,Is.GreaterThanOrEqualTo(clearance),$"Level {level}: {patch.name} is too close to {obstacle}.");
            }
            foreach(var rock in UnityEngine.Object.FindObjectsByType<SolidObstacleAuthoring>(FindObjectsSortMode.None).Where(o=>o.name=="Rock island"))
            {
                Vector2 position=new(rock.transform.position.x,rock.transform.position.z);
                Vector2 extent=(Vector2)rock.footprint*.5f;
                Check(position-extent,position+extent,$"rock at {position}");
            }
            if(level==54)
            {
                var track=UnityEngine.Object.FindFirstObjectByType<TrackObjectAuthoring>();
                var route=new[]{track.transform}.Concat(track.corners).Append(track.destination)
                    .Select(t=>new Vector2(t.position.x,t.position.z)).ToArray();
                Vector3 size=track.GetComponent<BarricadeAuthoring>().size;
                Vector2 carrierHalfSize=new(size.x*.5f,size.z*.5f);
                bool coversShootingApproach=false;
                for(int i=1;i<route.Length;i++)
                {
                    Vector2 start=route[i-1], end=route[i];
                    Check(Vector2.Min(start,end)-carrierHalfSize,Vector2.Max(start,end)+carrierHalfSize,$"carrier sweep on leg {i}",1.25f);
                    if(i==1) continue;
                    Vector2 forward=(end-start).normalized;
                    Vector2 offset=center-start;
                    float along=Vector2.Dot(offset,forward);
                    coversShootingApproach |= along>=-6 && along<=-4 && (offset-forward*along).sqrMagnitude<=.25f;
                }
                Assert.That(coversShootingApproach,Is.True,$"{patch.name} should interrupt the shooting approach to a turn.");
            }
            if(level==57)
                Assert.That(Mathf.Abs(center.x)-halfSize.x,Is.GreaterThan(4),"Leave a clear centre lane to the protected zone.");
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
