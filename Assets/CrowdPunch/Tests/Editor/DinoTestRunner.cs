using System.IO;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
namespace CrowdPunch.Tests
{
    public static class DinoTestRunner
    {
        private static TestRunnerApi api;
        public static void Run()
        {
            Directory.CreateDirectory("Temp/DinoValidation");
            api=ScriptableObject.CreateInstance<TestRunnerApi>(); api.RegisterCallbacks(new Results());
            api.Execute(new ExecutionSettings(new Filter { testMode=TestMode.EditMode,
                testNames=new[] { "CrowdPunch.Tests.DinoPillarTests","CrowdPunch.Tests.RollingBossTests","CrowdPunch.Tests.ChickenBossTests",
                    "CrowdPunch.Tests.BossEncounterTests","CrowdPunch.Tests.LaunchedEnemyPlayerImpactTests",
                    "CrowdPunch.Tests.EnemyLaunchHomingTests","CrowdPunch.Tests.GauntletProgressionTests" } }));
        }
        private sealed class Results : ICallbacks
        {
            public void RunStarted(ITestAdaptor t) { } public void TestStarted(ITestAdaptor t) { } public void TestFinished(ITestResultAdaptor r) { }
            public void RunFinished(ITestResultAdaptor r)
            {
                TestRunnerApi.SaveResultToFile(r,"Temp/DinoValidation/editmode-results.xml");
                File.WriteAllText("Temp/DinoValidation/editmode-summary.txt",$"Passed {r.PassCount}; failed {r.FailCount}; skipped {r.SkipCount}\n{r.Message}\n");
                Debug.Log($"Dino and boss regression tests: {r.PassCount} passed, {r.FailCount} failed.");
            }
        }
    }
}
