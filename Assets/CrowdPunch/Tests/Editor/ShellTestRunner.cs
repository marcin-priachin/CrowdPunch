using System.IO;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace CrowdPunch.Tests
{
    public static class ShellTestRunner
    {
        private static TestRunnerApi api;
        public static void Run()
        {
            Directory.CreateDirectory("Temp/ShellValidation");
            api=ScriptableObject.CreateInstance<TestRunnerApi>(); api.RegisterCallbacks(new Results());
            api.Execute(new ExecutionSettings(new Filter { testMode=TestMode.EditMode,
                testNames=new[]{"CrowdPunch.Tests.ShellTargetTests","CrowdPunch.Tests.GauntletProgressionTests",
                    "CrowdPunch.Tests.BarricadeTests","CrowdPunch.Tests.RotatingCoverTests"} }));
        }
        private sealed class Results : ICallbacks
        {
            public void RunStarted(ITestAdaptor tests) { }
            public void TestStarted(ITestAdaptor test) { }
            public void TestFinished(ITestResultAdaptor result) { }
            public void RunFinished(ITestResultAdaptor result)
            {
                TestRunnerApi.SaveResultToFile(result,"Temp/ShellValidation/editmode-results.xml");
                File.WriteAllText("Temp/ShellValidation/editmode-summary.txt",$"Passed {result.PassCount}; failed {result.FailCount}; skipped {result.SkipCount}\n{result.Message}\n");
                Debug.Log($"Shell regression tests: {result.PassCount} passed, {result.FailCount} failed.");
            }
        }
    }
}

