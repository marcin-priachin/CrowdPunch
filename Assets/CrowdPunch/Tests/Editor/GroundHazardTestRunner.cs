using System.IO;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace CrowdPunch.Tests
{
    public static class GroundHazardTestRunner
    {
        private static TestRunnerApi api;
        public static void Run()
        {
            Directory.CreateDirectory("Temp/GroundHazardValidation");
            api = ScriptableObject.CreateInstance<TestRunnerApi>(); api.RegisterCallbacks(new Results());
            api.Execute(new ExecutionSettings(new Filter { testMode = TestMode.EditMode, testNames = new[] {
                "CrowdPunch.Tests.GroundHazardTests", "CrowdPunch.Tests.GroundHazardNavigationTests",
                "CrowdPunch.Tests.NavigationTests", "CrowdPunch.Tests.NavigationSystemTests",
                "CrowdPunch.Tests.TrailInteractionTests", "CrowdPunch.Tests.ArmoredEnemyTests", "CrowdPunch.Tests.GauntletProgressionTests" } }));
        }
        private sealed class Results : ICallbacks
        {
            public void RunStarted(ITestAdaptor tests) { }
            public void TestStarted(ITestAdaptor test) { }
            public void TestFinished(ITestResultAdaptor result) { }
            public void RunFinished(ITestResultAdaptor result)
            {
                TestRunnerApi.SaveResultToFile(result,"Temp/GroundHazardValidation/editmode-results.xml");
                File.WriteAllText("Temp/GroundHazardValidation/editmode-summary.txt",$"Passed {result.PassCount}; failed {result.FailCount}; skipped {result.SkipCount}\n{result.Message}\n");
            }
        }
    }
}
