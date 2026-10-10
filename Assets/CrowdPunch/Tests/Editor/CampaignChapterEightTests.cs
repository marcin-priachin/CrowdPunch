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
    public sealed class CampaignChapterEightTests
    {
        private const string Data="Assets/CrowdPunch/Data/Campaign/";
        private const string Scenes="Assets/CrowdPunch/Scenes/Campaign/";

        [Test] public void Loop007_ChapterSevenSaveReachesFinalSentinelAndKeepsReplayUnlocked()
        {
            string path=Path.GetFullPath("Temp/CampaignTests/"+Guid.NewGuid()+".json");
            try
            {
                var catalog=AssetDatabase.LoadAssetAtPath<CampaignCatalog>(Data+"Campaign.asset");
                var progress=new CampaignProgress(path);
                for(int i=0;i<70;i++) progress.Complete(catalog.Get(i).id);
                Assert.That(new CampaignProgress(path).NextUnfinished(catalog),Is.EqualTo(70));
                for(int i=70;i<80;i++) { Assert.That(catalog.Get(i).Available,Is.True); progress.Complete(catalog.Get(i).id); }
                progress=new CampaignProgress(path);
                Assert.That(progress.NextUnfinished(catalog),Is.EqualTo(80));
                Assert.That(progress.IsUnlocked(catalog,80),Is.False);
                Assert.That(progress.IsUnlocked(catalog,79),Is.True);
                progress.Complete(catalog.Get(70).id);
                Assert.That(new CampaignProgress(path).NextUnfinished(catalog),Is.EqualTo(80));
                Assert.That(EditorBuildSettings.scenes.Count(s=>s.enabled),Is.EqualTo(81));
            }
            finally { foreach(var suffix in new[]{"",".bak",".tmp"}) if(File.Exists(path+suffix)) File.Delete(path+suffix); }
        }

        [Test] public void Loop008_ClaimTheGroundHasSixFiniteWavesAnd658Enemies()
        {
            var waves=AssetDatabase.FindAssets("t:EnemyWaveSettings",new[]{Data+"Waves"})
                .Select(g=>AssetDatabase.LoadAssetAtPath<EnemyWaveSettings>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(w=>w.name.StartsWith("CP78_")).OrderBy(w=>w.name).ToArray();
            Assert.That(waves.Select(w=>w.TotalEnemyCount),Is.EqualTo(new[]{76,96,100,108,128,146}));
            Assert.That(waves.Select(w=>w.EliteEnemies.Sum(e=>e.Count)),Is.EqualTo(new[]{0,0,2,0,0,2}));
            Assert.That(waves.Sum(w=>w.TotalEnemyCount+w.EliteEnemies.Sum(e=>e.Count)),Is.EqualTo(658));
            Assert.That(waves.All(w=>w.SpawnMode==EnemyWaveSpawnMode.AllAtOnce && w.DelayBeforeWave==3 && w.Duration==0),Is.True);
            Assert.That(waves[2].ArmoredAmmunitionProfile,Is.Not.Null); Assert.That(waves[5].ArmoredAmmunitionProfile,Is.Not.Null);
            Assert.That(waves[3].WizardAmmunitionProfile,Is.Not.Null); Assert.That(waves[3].WaitForPersistentHazards,Is.True);
            var floor=AssetDatabase.LoadAssetAtPath<Mesh>(Data+"Layouts/Campaign_78_Floor.asset");
            Assert.That(floor.bounds.size.x,Is.EqualTo(100)); Assert.That(floor.bounds.size.z,Is.EqualTo(100));
        }

        [Test] public void Loop002_ChapterEightWiresNavigationObjectivesAndSafeHazards()
        {
            int[][] populations={new[]{16,20},new[]{16},new[]{21,24,26},new[]{18},new[]{22,26,27},new[]{12},new[]{26,28,30},new[]{76,96,102,108,128,148},new[]{16,21},new[]{12}};
            var setup=EditorSceneManager.GetSceneManagerSetup();
            try
            {
                for(int n=71;n<=80;n++)
                {
                    EditorSceneManager.OpenScene($"{Scenes}Campaign_{n:00}.unity");
                    Assert.That(UnityEngine.Object.FindFirstObjectByType<GauntletLevel>().OpeningHint,Is.Not.Empty);
                    var sub=UnityEngine.Object.FindFirstObjectByType<Unity.Scenes.SubScene>();
                    Assert.That(AssetDatabase.GetAssetPath(sub.SceneAsset),Is.EqualTo($"{Scenes}Campaign_{n:00}/Campaign_{n:00} Sub Scene.unity"));
                    EditorSceneManager.OpenScene(AssetDatabase.GetAssetPath(sub.SceneAsset));
                    var crowd=UnityEngine.Object.FindFirstObjectByType<EnemyWaveSequenceAuthoring>();
                    Assert.That(crowd.Waves.All(w=>AssetDatabase.GetAssetPath(w).StartsWith(Data)),Is.True);
                    Assert.That(crowd.Waves.Select(w=>w.TotalEnemyCount+w.EliteEnemies.Sum(e=>e.Count)),Is.EqualTo(populations[n-71]),$"Level {n}");
                    Assert.That(crowd.Waves.All(w=>w.Enemies.Sum(e=>e.MinimumCount)==w.TotalEnemyCount && w.Enemies.All(e=>e.Weight==0)),Is.True);
                    CampaignChapterTwoTests.AssertNavigationClearance(UnityEngine.Object.FindFirstObjectByType<NavigationArenaAuthoring>(),n);
                    if(n==72)
                    {
                        Assert.That(crowd.barricade.settings.requiredHits,Is.EqualTo(4));
                        var cover=crowd.barricade.cover.settings;
                        Assert.That(cover.openingDegrees,Is.EqualTo(100)); Assert.That(cover.degreesPerSecond,Is.EqualTo(30));
                        Assert.That(cover.rotationMode,Is.EqualTo(CoverRotationMode.ReversePeriodically));
                        Assert.That(cover.reverseSeconds,Is.EqualTo(6)); Assert.That(cover.hitResponse,Is.EqualTo(CoverHitResponse.None));
                    }
                    if(n==74)
                    {
                        var gate=crowd.barricade;
                        Assert.That(gate.size.x,Is.EqualTo(2)); Assert.That(gate.settings.requiredHits,Is.EqualTo(4));
                        Assert.That(gate.exit.position.z,Is.GreaterThan(gate.transform.position.z));
                        var sides=UnityEngine.Object.FindObjectsByType<SolidObstacleAuthoring>(FindObjectsSortMode.None).Where(o=>o.name.StartsWith("Gate terrain")).ToArray();
                        Assert.That(sides.Length,Is.EqualTo(2));
                        foreach(var side in sides)
                        {
                            Assert.That(Mathf.Abs(side.transform.position.x)-side.Size.x*.5f,Is.EqualTo(1));
                            Assert.That(Mathf.Abs(side.transform.position.x)+side.Size.x*.5f,Is.GreaterThanOrEqualTo(19));
                            float front=side.transform.position.z-side.Size.y*.5f;
                            Assert.That(front,Is.GreaterThan(gate.transform.position.z-gate.size.z*.5f));
                            Assert.That(front,Is.LessThan(gate.transform.position.z+gate.size.z*.5f));
                        }
                    }
                    if(n==75 || n==77)
                    {
                        var point=crowd.GetComponent<ProtectedPointAuthoring>();
                        Assert.That(point.zoneSize,Is.EqualTo(new Vector2(8,4))); Assert.That(point.zoneCenter,Is.EqualTo(new Vector2(0,-46)));
                        Assert.That(point.settings.breachThreshold,Is.EqualTo(1)); Assert.That(point.settings.maximumPlayerAttackers,Is.EqualTo(3));
                        for(int i=0;i<crowd.Waves.Count;i++)
                        {
                            var w=crowd.Waves[i];
                            Assert.That(w.EliteEnemies.Sum(e=>e.Count),Is.Zero);
                            Assert.That(w.BatchSize,Is.EqualTo(4)); Assert.That(w.BatchInterval,Is.EqualTo(3));
                            Assert.That(w.ReplenishWhileBossLives || w.WaitForPersistentHazards,Is.False);
                            Assert.That(w.WizardAmmunitionProfile,Is.Null);
                            Assert.That(w.ArmoredAmmunitionProfile!=null,Is.EqualTo(i==(n==75?1:0)));
                            if(i>0) Assert.That(w.DelayBeforeWave,Is.EqualTo(5));
                        }
                    }
                    if(n==76)
                    {
                        var shell=crowd.barricade.GetComponent<ShellTargetAuthoring>().settings;
                        Assert.That(shell.requiredExplosions,Is.EqualTo(3)); Assert.That(shell.exploderReplacementDelay,Is.EqualTo(4));
                        Assert.That(shell.coreMaxHealth,Is.GreaterThan(0));
                    }
                    var patches=UnityEngine.Object.FindObjectsByType<GroundHazardAuthoring>(FindObjectsSortMode.None);
                    Assert.That(patches.Length,Is.EqualTo(n==73||n==74||n==78||n==80?2:0));
                    foreach(var patch in patches)
                    {
                        Assert.That(patch.sequence,Is.EqualTo(crowd)); Assert.That(patch.shape,Is.EqualTo(GroundHazardShape.Rectangle));
                        Assert.That(patch.width,Is.EqualTo(3)); Assert.That(patch.depth,Is.EqualTo(n==74?6:8));
                        Assert.That(patch.operation,Is.EqualTo(n==74?GroundHazardOperation.AlwaysActive:GroundHazardOperation.Periodic));
                        Assert.That(patch.inactiveDuration,Is.EqualTo(4)); Assert.That(patch.warningDuration,Is.EqualTo(1.5f)); Assert.That(patch.activeDuration,Is.EqualTo(2.5f));
                        foreach(var range in crowd.Waves.SelectMany(w=>w.SpawnRectangles))
                        {
                            var d=new Vector2(Mathf.Abs(range.Center.x-patch.transform.position.x),Mathf.Abs(range.Center.z-patch.transform.position.z));
                            Assert.That(d.x>range.Width*.5f+patch.width*.5f+.5f || d.y>range.Depth*.5f+patch.depth*.5f+.5f,Is.True,$"Level {n} spawn bank overlaps hazard.");
                        }
                        if(n==80) foreach(var pillar in UnityEngine.Object.FindObjectsByType<FallingPillarAuthoring>(FindObjectsSortMode.None))
                        {
                            var d=new Vector2(Mathf.Max(0,Mathf.Abs(pillar.transform.position.x-patch.transform.position.x)-patch.width*.5f),Mathf.Max(0,Mathf.Abs(pillar.transform.position.z-patch.transform.position.z)-patch.depth*.5f));
                            Assert.That(d.magnitude,Is.GreaterThanOrEqualTo(crowd.dinoBoss.settings.pillarCrowdRadius+1));
                            Assert.That(pillar.boss,Is.EqualTo(crowd.dinoBoss));
                        }
                    }
                    if(patches.Length==2 && n!=74) Assert.That(patches.Select(p=>p.cycleOffset).OrderBy(x=>x),Is.EqualTo(new[]{0f,4f}));
                    if(n==80)
                    {
                        Assert.That(crowd.dinoBoss,Is.Not.Null);
                        var tuning=crowd.dinoBoss.settings.BakeBoss();
                        Assert.That(tuning.RequiredHits,Is.EqualTo(3)); Assert.That(tuning.EnemiesPerPillar,Is.EqualTo(2));
                        Assert.That(tuning.ChaseDurations,Is.EqualTo(new Unity.Mathematics.float3(6,4.5f,3.5f)));
                        Assert.That(tuning.WarningDuration,Is.EqualTo(1));
                        Assert.That(crowd.dinoBoss.settings.fallDirection,Is.EqualTo(PillarFallDirection.TowardBoss));
                        Assert.That(crowd.Waves.Single().ReplenishWhileBossLives,Is.True); Assert.That(crowd.Waves.Single().BossReplenishDelay,Is.EqualTo(4));
                    }
                }
            }
            finally { Restore(setup); }
        }

        [Test] public void Pillar006_RematchPreservesOriginalArenaAndPillarPositions()
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
                Assert.That(Snapshot(Scenes+"Campaign_80/Campaign_80 Sub Scene.unity"),Is.EqualTo(Snapshot("Assets/CrowdPunch/Scenes/Gauntlets/Gauntlet_22/Gauntlet_22 Sub Scene.unity")));
                var intro=AssetDatabase.LoadAssetAtPath<DinoBossSettings>(Data+"Settings/L40_Dino.asset").BakeBoss();
                Assert.That(intro.ChaseDurations,Is.EqualTo(new Unity.Mathematics.float3(7,5.5f,4)));
            }
            finally { Restore(setup); }
        }
        [Test] public void Loop002_NotchedFloorsHaveUpwardFacesAndExactPolygonArea()
        {
            for(int level=71;level<=79;level++)
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
