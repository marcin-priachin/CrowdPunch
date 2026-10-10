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

        [Test] public void Loop007_ChapterThreeSaveContinuesThroughFourAndUnlocksUnavailableFive()
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
                Assert.That(catalog.Get(40).Available,Is.False);
            }
            finally { foreach(var suffix in new[]{"",".bak",".tmp"}) if(File.Exists(path+suffix)) File.Delete(path+suffix); }
        }

        [Test] public void Loop008_LargeCrowdUsesFullFootprintWithFiniteMixedWavesAndSafeguards()
        {
            var waves=AssetDatabase.FindAssets("t:EnemyWaveSettings",new[]{Data+"Waves"})
                .Select(g=>AssetDatabase.LoadAssetAtPath<EnemyWaveSettings>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(w=>w.name.StartsWith("CP38_")).OrderBy(w=>w.name).ToArray();
            Assert.That(waves.Select(w=>w.TotalEnemyCount),Is.EqualTo(new[]{70,86,100,120}));
            Assert.That(waves.Select(w=>w.EliteEnemies.Sum(e=>e.Count)),Is.EqualTo(new[]{0,0,2,0}));
            Assert.That(waves.Sum(w=>w.TotalEnemyCount+w.EliteEnemies.Sum(e=>e.Count)),Is.EqualTo(378));
            Assert.That(waves.All(w=>w.SpawnMode==EnemyWaveSpawnMode.AllAtOnce && w.DelayBeforeWave==3 && w.Duration==0),Is.True);
            Assert.That(waves[2].ArmoredAmmunitionProfile,Is.Not.Null);
            foreach(var w in new[]{waves[0],waves[3]})
            {
                Assert.That(w.WizardAmmunitionProfile,Is.Not.Null);
                Assert.That(w.WaitForPersistentHazards,Is.True);
            }
            Assert.That(waves[1].WizardAmmunitionProfile,Is.Null);
            Assert.That(waves[1].WaitForPersistentHazards,Is.False,"Trails must not delay a cleared wave.");
            var floor=AssetDatabase.LoadAssetAtPath<Mesh>(Data+"Layouts/Campaign_38_Floor.asset");
            Assert.That(floor.bounds.size.x,Is.EqualTo(100)); Assert.That(floor.bounds.size.z,Is.EqualTo(100));
        }

        [Test] public void Loop002_ChapterFourScenesWireObjectivesSafeNavigationAndFiniteDefense()
        {
            var setup=EditorSceneManager.GetSceneManagerSetup();
            try
            {
                for(int n=31;n<=40;n++)
                {
                    EditorSceneManager.OpenScene($"{Scenes}Campaign_{n:00}.unity");
                    Assert.That(UnityEngine.Object.FindFirstObjectByType<GauntletLevel>().OpeningHint,Is.Not.Empty);
                    var sub=UnityEngine.Object.FindFirstObjectByType<Unity.Scenes.SubScene>();
                    Assert.That(AssetDatabase.GetAssetPath(sub.SceneAsset),Is.EqualTo($"{Scenes}Campaign_{n:00}/Campaign_{n:00} Sub Scene.unity"));
                    EditorSceneManager.OpenScene(AssetDatabase.GetAssetPath(sub.SceneAsset));
                    var crowd=UnityEngine.Object.FindFirstObjectByType<EnemyWaveSequenceAuthoring>();
                    Assert.That(crowd.Waves.All(w=>AssetDatabase.GetAssetPath(w).StartsWith(Data)),Is.True);
                    CampaignChapterTwoTests.AssertNavigationClearance(UnityEngine.Object.FindFirstObjectByType<NavigationArenaAuthoring>(),n);
                    if(n==31) Assert.That(crowd.Waves.All(w=>w.WizardAmmunitionProfile!=null && w.WaitForPersistentHazards),Is.True);
                    if(n==33) Assert.That(crowd.Waves.All(w=>w.WizardAmmunitionProfile==null && !w.WaitForPersistentHazards),Is.True);
                    if(n==32 || n==39)
                    {
                        var track=crowd.barricade.GetComponent<TrackObjectAuthoring>();
                        var delta=track.destination.position-track.transform.position;
                        Assert.That(delta.magnitude,Is.EqualTo(10).Within(.001));
                        Assert.That(Mathf.Abs(delta.x),n==32?Is.GreaterThan(4):Is.LessThan(.01f));
                        Assert.That(track.settings.requiredNetHits,Is.EqualTo(5));
                        Assert.That(track.destination.GetComponentsInChildren<TrackSocketVisualAuthoring>().All(v=>v.trackObject==track),Is.True);
                        var marks=UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None).Where(r=>r.name=="Track rail" || r.name=="Direction arrow").ToArray();
                        Assert.That(marks.Length,Is.EqualTo(22));
                        Assert.That(marks.All(r=>r.bounds.max.y>-.99f),Is.True);
                    }
                    if(n==34)
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
                        Assert.That(crowd.Waves.Select(w=>w.TotalEnemyCount),Is.EqualTo(new[]{18,21,23}));
                        foreach(var w in crowd.Waves)
                        {
                            Assert.That(w.BatchSize,Is.EqualTo(4)); Assert.That(w.BatchInterval,Is.EqualTo(3));
                            Assert.That(w.ReplenishWhileBossLives || w.WaitForPersistentHazards,Is.False);
                            Assert.That(w.WizardAmmunitionProfile,Is.Null); Assert.That(w.ArmoredAmmunitionProfile,Is.Null);
                        }
                        Assert.That(crowd.Waves.Skip(1).All(w=>w.DelayBeforeWave==5),Is.True);
                    }
                    if(n==37) Assert.That(crowd.barricade.GetComponent<ShellTargetAuthoring>().settings.requiredExplosions,Is.EqualTo(3));
                    var patches=UnityEngine.Object.FindObjectsByType<GroundHazardAuthoring>(FindObjectsSortMode.None);
                    Assert.That(patches.Length,Is.EqualTo(n==34||n==37?1:0));
                    foreach(var patch in patches)
                    {
                        Assert.That(patch.sequence,Is.EqualTo(crowd));
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
