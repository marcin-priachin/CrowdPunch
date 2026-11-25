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

        [Test] public void Loop007_ChapterSixSaveContinuesThroughSevenAndUnlocksEight()
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
                Assert.That(catalog.Get(70).Available,Is.True);
            }
            finally { foreach(var suffix in new[]{"",".bak",".tmp"}) if(File.Exists(path+suffix)) File.Delete(path+suffix); }
        }

        [Test] public void Loop008And011_ShiftingFrontHasSixBatchedPhasesAndExistingSafeguards()
        {
            var waves=AssetDatabase.FindAssets("t:EnemyWaveSettings",new[]{Data+"Waves"})
                .Select(g=>AssetDatabase.LoadAssetAtPath<EnemyWaveSettings>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(w=>w.name.StartsWith("CP68_")).OrderBy(w=>w.name).ToArray();
            Assert.That(waves.Select(CampaignEnemyRevisionTests.PhysicalPopulation),Is.EqualTo(new[]{40,48,56,68,64,70}));
            Assert.That(waves.Select(w=>w.EliteEnemies.Sum(e=>e.Count)),Is.EqualTo(new[]{0,0,2,0,0,0}));
            Assert.That(waves.Sum(w=>w.TotalEnemyCount+w.LinkedPairCount*2+w.EliteEnemies.Sum(e=>e.Count)),Is.EqualTo(328));
            Assert.That(waves.Sum(CampaignEnemyRevisionTests.PhysicalPopulation),Is.EqualTo(346));
            Assert.That(waves.Sum(w=>w.Enemies.Where(e=>e.Settings.name=="EnemySpawnSettings").Sum(e=>e.MinimumCount)),Is.EqualTo(133));
            Assert.That(waves[2].ArmoredAmmunitionProfile,Is.Not.Null); Assert.That(waves[5].ArmoredAmmunitionProfile,Is.Not.Null);
            Assert.That(waves[5].WizardAmmunitionProfile,Is.Not.Null); Assert.That(waves[5].WaitForPersistentHazards,Is.True);
            Assert.That(waves.Take(5).All(w=>!w.WaitForPersistentHazards),Is.True);
            var floor=AssetDatabase.LoadAssetAtPath<Mesh>(Data+"Layouts/Campaign_68_Floor.asset");
            Assert.That(floor.bounds.size.x,Is.EqualTo(100)); Assert.That(floor.bounds.size.z,Is.EqualTo(100));
        }

        [TestCase(61), TestCase(62), TestCase(63), TestCase(64), TestCase(65)]
        [TestCase(66), TestCase(67), TestCase(68), TestCase(69), TestCase(70)]
        public void Loop002_ChapterSevenWiresNavigableLayoutsSafeHazardsAndObjectiveSettings(int n)
        {
            int[][] populations={new[]{13,17},new[]{8},new[]{16,20},new[]{16},new[]{26,22,24},new[]{14},new[]{18,26},new[]{40,48,56,68,64,70},new[]{12},new[]{10}};
            var setup=EditorSceneManager.GetSceneManagerSetup();
            try
            {
                EditorSceneManager.OpenScene($"{Scenes}Campaign_{n:00}.unity");
                var sub=UnityEngine.Object.FindFirstObjectByType<Unity.Scenes.SubScene>();
                Assert.That(AssetDatabase.GetAssetPath(sub.SceneAsset),Is.EqualTo($"{Scenes}Campaign_{n:00}/Campaign_{n:00} Sub Scene.unity"));
                EditorSceneManager.OpenScene(AssetDatabase.GetAssetPath(sub.SceneAsset));
                var crowd=CampaignBarricadeRoomTests.FirstCrowd();
                Assert.That(crowd.Waves.All(w=>AssetDatabase.GetAssetPath(w).StartsWith(Data)),Is.True);
                Assert.That(crowd.Waves.Select(CampaignEnemyRevisionTests.PhysicalPopulation),Is.EqualTo(populations[n-61]),$"Level {n}");
                Assert.That(crowd.Waves.All(w=>w.Enemies.Sum(e=>e.MinimumCount)==w.TotalEnemyCount && w.Enemies.All(e=>e.Weight==0)),Is.True);
                CampaignChapterTwoTests.AssertNavigationClearance(UnityEngine.Object.FindFirstObjectByType<NavigationArenaAuthoring>(),n);
                if(n==61) Assert.That(crowd.Waves.All(w=>w.ArmoredAmmunitionProfile==null),Is.True);
                if(n==62) CampaignBarricadeRoomTests.AssertRooms(n);
                if(n==64)
                {
                    var shell=crowd.barricade.GetComponent<ShellTargetAuthoring>().settings;
                    Assert.That(shell.requiredExplosions,Is.EqualTo(4)); Assert.That(shell.exploderReplacementDelay,Is.EqualTo(4));
                    Assert.That(shell.coreMaxHealth,Is.GreaterThan(0));
                }
                if(n==65)
                {
                    Assert.That(crowd.Waves[2].ArmoredAmmunitionProfile,Is.Not.Null);
                    Assert.That(crowd.Waves[2].WizardAmmunitionProfile,Is.Not.Null);
                    Assert.That(crowd.Waves[2].WaitForPersistentHazards,Is.True);
                    Assert.That(crowd.Waves.Take(2).All(w=>!w.WaitForPersistentHazards),Is.True);
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
                    CampaignTrackRouteTests.AssertProgressiveTrack(track,n);
                    Assert.That(track.destination.GetComponentsInChildren<TrackSocketVisualAuthoring>().All(v=>v.trackObject==track),Is.True);
                }
                var patches=UnityEngine.Object.FindObjectsByType<GroundHazardAuthoring>(FindObjectsSortMode.None);
                bool revised=n is 63 or 65 or 67 or 68;
                Assert.That(patches.Length,Is.EqualTo(n==62?4:n==68?6:revised?3:n is 64 or 70?2:0));
                foreach(var patch in patches)
                {
                    Assert.That(UnityEngine.Object.FindObjectsByType<EnemyWaveSequenceAuthoring>(FindObjectsSortMode.None).Contains(patch.sequence),Is.True); Assert.That(patch.shape,Is.EqualTo(GroundHazardShape.Rectangle));
                    bool easier=n==62 && patch.sequence.barricade.exit==null;
                    if(easier){Assert.That(patch.width,Is.GreaterThan(0).And.LessThan(3)); Assert.That(patch.depth,Is.GreaterThan(0).And.LessThan(8));}
                    else {Assert.That(patch.width,Is.EqualTo(3)); Assert.That(patch.depth,Is.EqualTo(n==64||revised?6:8));}
                    Assert.That(patch.operation,Is.EqualTo(n==64?GroundHazardOperation.AlwaysActive:GroundHazardOperation.Periodic));
                    Assert.That(patch.inactiveDuration,Is.EqualTo(easier?5:4)); Assert.That(patch.warningDuration,Is.EqualTo(1.5f)); Assert.That(patch.activeDuration,Is.EqualTo(easier?2:2.5f));
                    if(revised) AssertHazardClearance(n,patch);
                    foreach(var range in patch.sequence.Waves.SelectMany(w=>w.SpawnRectangles))
                    {
                        var d=new Vector2(Mathf.Abs(range.Center.x-patch.transform.position.x),Mathf.Abs(range.Center.z-patch.transform.position.z));
                        Assert.That(d.x>range.Width*.5f+patch.width*.5f+.5f || d.y>range.Depth*.5f+patch.depth*.5f+.5f,Is.True,$"Level {n} spawn bank overlaps hazard.");
                    }
                }
                if(patches.Length==2 && n!=64) Assert.That(patches.Select(p=>p.cycleOffset).OrderBy(x=>x),Is.EqualTo(new[]{0f,4f}));
                if(revised)
                {
                    var offsets=n==68?new[]{0f,4f/3,8f/3,4f,16f/3,20f/3}:new[]{0f,8f/3,16f/3};
                    Assert.That(patches.Select(p=>p.cycleOffset).OrderBy(x=>x),Is.EqualTo(offsets));
                }
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
            finally { Restore(setup); }
        }

        private static void AssertHazardClearance(int level, GroundHazardAuthoring patch)
        {
            Assert.That(patch.GetComponent<GroundHazardAppearanceAuthoring>().style,Is.EqualTo(GroundHazardStyle.Vault));
            foreach(var rock in UnityEngine.Object.FindObjectsByType<SolidObstacleAuthoring>(FindObjectsSortMode.None).Where(o=>o.name=="Rock island"))
            {
                Vector3 d=patch.transform.position-rock.transform.position;
                var gap=new Vector2(
                    Mathf.Max(0,Mathf.Abs(d.x)-(patch.width+rock.footprint.x)*.5f),
                    Mathf.Max(0,Mathf.Abs(d.z)-(patch.depth+rock.footprint.y)*.5f));
                Assert.That(gap.magnitude,Is.GreaterThanOrEqualTo(4),$"Level {level}: {patch.name} is too close to the island at {rock.transform.position}.");
            }
            if(level==68)
                Assert.That(Mathf.Abs(patch.transform.position.x)-patch.width*.5f,Is.GreaterThanOrEqualTo(5),"Keep the central north-south lane clear.");
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
