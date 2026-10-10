using System;
using System.IO;
using System.Linq;
using CrowdPunch.Authoring;
using CrowdPunch.Configuration;
using CrowdPunch.Mono.Levels;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CrowdPunch.Tests
{
    public sealed class CampaignTests
    {
        private string path;
        private CampaignCatalog catalog;
        [SetUp] public void SetUp()
        {
            path=Path.GetFullPath("Temp/CampaignTests/"+Guid.NewGuid()+".json");
            catalog=AssetDatabase.LoadAssetAtPath<CampaignCatalog>(CrowdPunch.Editor.CampaignBuildRegistration.CatalogPath);
        }
        [TearDown] public void TearDown()
        {
            foreach(var file in new[]{path,path+".bak",path+".tmp"}) if(File.Exists(file)) File.Delete(file);
        }
        [Test] public void Loop007_SaveReloadReplayAndChapterUnlockPreserveProgress()
        {
            var progress=new CampaignProgress(path);
            Assert.That(progress.NextUnfinished(catalog),Is.Zero);
            Assert.That(progress.IsUnlocked(catalog,1),Is.False);
            for(int i=0;i<10;i++) Assert.That(progress.Complete(catalog.Get(i).id),Is.True);
            var reloaded=new CampaignProgress(path);
            Assert.That(reloaded.NextUnfinished(catalog),Is.EqualTo(10));
            Assert.That(reloaded.IsUnlocked(catalog,10),Is.True);
            Assert.That(catalog.Get(10).Available,Is.True,"Chapter Two is now installed.");
            string backupBeforeReplay=File.ReadAllText(path+".bak");
            Assert.That(reloaded.Complete(catalog.Get(0).id),Is.True);
            Assert.That(File.ReadAllText(path+".bak"),Is.EqualTo(backupBeforeReplay));
            Assert.That(new CampaignProgress(path).NextUnfinished(catalog),Is.EqualTo(10));
            Assert.That(reloaded.Reset(),Is.True);
            Assert.That(new CampaignProgress(path).NextUnfinished(catalog),Is.Zero);
        }
        [Test] public void Loop007_CorruptPrimaryRecoversBackupWithoutInventingCompletion()
        {
            var progress=new CampaignProgress(path);
            progress.Complete(catalog.Get(0).id); progress.Complete(catalog.Get(1).id);
            File.WriteAllText(path,"broken json");
            var restored=new CampaignProgress(path);
            Assert.That(restored.IsComplete(catalog.Get(0).id),Is.True);
            Assert.That(restored.NextUnfinished(catalog),Is.EqualTo(1));
        }
        [Test] public void Loop007_BriefBackupLockDoesNotLoseCompletion()
        {
            var progress=new CampaignProgress(path);
            Assert.That(progress.Complete(catalog.Get(0).id),Is.True);
            Assert.That(progress.Complete(catalog.Get(1).id),Is.True);
            var locked=File.Open(path+".bak",FileMode.Open,FileAccess.Read,FileShare.None);
            var release=System.Threading.Tasks.Task.Run(()=>
            {
                System.Threading.Thread.Sleep(25);
                locked.Dispose();
            });
            try
            {
                Assert.That(progress.Complete(catalog.Get(2).id),Is.True);
                Assert.That(new CampaignProgress(path).NextUnfinished(catalog),Is.EqualTo(3));
            }
            finally { release.Wait(); locked.Dispose(); }
        }
        [Test] public void Loop007_OutOfOrderCompletionDoesNotSkipLockedLevels()
        {
            var progress=new CampaignProgress(path);
            progress.Complete(catalog.Get(9).id);
            Assert.That(progress.NextUnfinished(catalog),Is.Zero);
            Assert.That(progress.IsUnlocked(catalog,10),Is.False);
        }
        [Test] public void Loop007_AllEightyCompletedReturnsEndSentinel()
        {
            var progress=new CampaignProgress(path);
            foreach(var level in catalog.levels) progress.Complete(level.id);
            Assert.That(new CampaignProgress(path).NextUnfinished(catalog),Is.EqualTo(80));
            Assert.That(progress.IsUnlocked(catalog,80),Is.False);
        }
        [Test] public void Loop008_ChapterOneAssetsAreIsolatedAndLargeCrowdIsAuthored()
        {
            Assert.That(catalog.Count,Is.EqualTo(80));
            Assert.That(catalog.levels.Select(l=>l.id).Distinct().Count(),Is.EqualTo(80));
            Assert.That(catalog.levels.Count(l=>l.Available),Is.EqualTo(80));
            var waves=AssetDatabase.FindAssets("t:EnemyWaveSettings",new[]{"Assets/CrowdPunch/Data/Campaign/Waves"})
                .Select(g=>AssetDatabase.LoadAssetAtPath<EnemyWaveSettings>(AssetDatabase.GUIDToAssetPath(g))).ToArray();
            Assert.That(waves.Length,Is.EqualTo(155));
            foreach(var wave in waves)
            {
                Assert.That(wave.Enemies.All(e=>e.Settings!=null && e.Settings.EnemyPrefab!=null),Is.True,wave.name);
                Assert.That(wave.Enemies.Sum(e=>e.MinimumCount),Is.EqualTo(wave.TotalEnemyCount),wave.name);
                Assert.That(wave.Enemies.All(e=>AssetDatabase.GetAssetPath(e.Settings).StartsWith("Assets/CrowdPunch/Data/Settings/Enemies/")),Is.True);
            }
            var large=waves.Where(w=>w.name.StartsWith("CP08_")).OrderBy(w=>w.name).ToArray();
            Assert.That(large.Select(w=>w.TotalEnemyCount),Is.EqualTo(new[]{40,60,76,94}));
            Assert.That(large.Sum(w=>w.TotalEnemyCount),Is.EqualTo(270));
            var floor=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/CrowdPunch/Data/Campaign/Layouts/Campaign_08_Floor.asset");
            Assert.That(floor.bounds.size.x,Is.EqualTo(100)); Assert.That(floor.bounds.size.z,Is.EqualTo(100));
        }
        [Test] public void Loop006_SceneReferencesAndBossGeometryRemainValid()
        {
            var setup=EditorSceneManager.GetSceneManagerSetup();
            try
            {
                for(int i=0;i<10;i++)
                {
                    // Scene changes unload assets: resolve the catalog on each iteration.
                    string mainPath=$"Assets/CrowdPunch/Scenes/Campaign/Campaign_{i+1:00}.unity";
                    var main=EditorSceneManager.OpenScene(mainPath);
                    var marker=UnityEngine.Object.FindFirstObjectByType<GauntletLevel>();
                    Assert.That(marker,Is.Not.Null); Assert.That(marker.OpeningHint,Is.Not.Empty);
                    var reference=UnityEngine.Object.FindFirstObjectByType<Unity.Scenes.SubScene>();
                    Assert.That(AssetDatabase.GetAssetPath(reference.SceneAsset),Does.StartWith("Assets/CrowdPunch/Scenes/Campaign/"));
                    EditorSceneManager.OpenScene(AssetDatabase.GetAssetPath(reference.SceneAsset));
                    var crowd=UnityEngine.Object.FindFirstObjectByType<EnemyWaveSequenceAuthoring>();
                    Assert.That(crowd.Waves.All(w=>w!=null),Is.True);
                    Assert.That(crowd.Waves.All(w=>AssetDatabase.GetAssetPath(w).StartsWith("Assets/CrowdPunch/Data/Campaign/")),Is.True);
                    var wall=UnityEngine.Object.FindFirstObjectByType<BarricadeAuthoring>();
                    if(i==3 || i==5 || i==6)
                    {
                        Assert.That(wall.settings.requiredHits,Is.EqualTo(3));
                        Assert.That(crowd.barricade,Is.EqualTo(wall));
                        if(i!=6) Assert.That(wall.exit.position.z,Is.GreaterThan(wall.transform.position.z));
                    }
                    if(i==9)
                    {
                        var boss=UnityEngine.Object.FindFirstObjectByType<BossEncounterAuthoring>();
                        Assert.That(AssetDatabase.GetAssetPath(boss.settings),Does.StartWith("Assets/CrowdPunch/Data/Campaign/"));
                        var original=AssetDatabase.LoadAssetAtPath<BossEncounterSettings>("Assets/CrowdPunch/Data/Settings/BossEncounterSettings.asset");
                        Assert.That(boss.settings.Bake().RouteExtents,Is.EqualTo(original.Bake().RouteExtents));
                        Assert.That(boss.settings.Bake().Slam.Anticipation,Is.EqualTo(.8f));
                        Assert.That(boss.settings.Bake().OpeningDuration,Is.EqualTo(1.5f));
                    }
                }
            }
            finally
            {
                if(setup.Any(scene=>scene.isLoaded && scene.isActive)) EditorSceneManager.RestoreSceneManagerSetup(setup);
                else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            }
        }
    }
}
