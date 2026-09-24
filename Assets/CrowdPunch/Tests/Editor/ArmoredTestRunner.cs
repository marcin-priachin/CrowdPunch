using System.IO;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace CrowdPunch.Tests
{
    public static class ArmoredTestRunner
    {
        private static TestRunnerApi api;
        public static void Run()
        {
            Directory.CreateDirectory("Temp/ArmoredValidation");
            api=ScriptableObject.CreateInstance<TestRunnerApi>(); api.RegisterCallbacks(new Results());
            api.Execute(new ExecutionSettings(new Filter { testMode=TestMode.EditMode,
                testNames=new[]{"CrowdPunch.Tests.ArmoredEnemyTests", "CrowdPunch.Tests.ArmoredShieldIndicatorTests", "CrowdPunch.Tests.EnemyAnimationTests", "CrowdPunch.Tests.CombatFeedbackTests", "CrowdPunch.Tests.EnemyPrefabCollisionContactTests", "CrowdPunch.Tests.ElitePunchGeometryTests", "CrowdPunch.Tests.EnemyArenaDistributionTests", "CrowdPunch.Tests.NavigationSystemTests", "CrowdPunch.Tests.BossEncounterTests","CrowdPunch.Tests.GauntletProgressionTests",
                    "CrowdPunch.Tests.LaunchedEnemyPlayerImpactTests","CrowdPunch.Tests.DasherObstacleRedirectTests"} }));
        }
        private sealed class Results : ICallbacks
        {
            public void RunStarted(ITestAdaptor tests) { }
            public void TestStarted(ITestAdaptor test) { }
            public void TestFinished(ITestResultAdaptor result) { }
            public void RunFinished(ITestResultAdaptor result)
            {
                TestRunnerApi.SaveResultToFile(result,"Temp/ArmoredValidation/editmode-results.xml");
                File.WriteAllText("Temp/ArmoredValidation/editmode-summary.txt",$"Passed {result.PassCount}; failed {result.FailCount}; skipped {result.SkipCount}\n{result.Message}\n");
                Debug.Log($"Armored regression tests: {result.PassCount} passed, {result.FailCount} failed.");
            }
        }
    }
}
