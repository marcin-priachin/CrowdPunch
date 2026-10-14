using System.IO;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
namespace CrowdPunch.Tests
{
    public static class FinaleTestRunner
    {
        private static TestRunnerApi api;
        public static void Run()
        {
            Directory.CreateDirectory("Temp/FinaleValidation");
            api=ScriptableObject.CreateInstance<TestRunnerApi>(); api.RegisterCallbacks(new Results());
            api.Execute(new ExecutionSettings(new Filter {testMode=TestMode.EditMode,testNames=new[]{
                "CrowdPunch.Tests.FinaleTests","CrowdPunch.Tests.BarricadeTests","CrowdPunch.Tests.ShellTargetTests",
                "CrowdPunch.Tests.RotatingCoverTests","CrowdPunch.Tests.TrackObjectTests","CrowdPunch.Tests.CampaignTests",
                "CrowdPunch.Tests.CampaignChapterEightTests.Loop007_ChapterSevenSaveReachesFinaleAndKeepsReplayUnlocked"}}));
        }
        private sealed class Results:ICallbacks
        {
            public void RunStarted(ITestAdaptor t) {} public void TestStarted(ITestAdaptor t) {} public void TestFinished(ITestResultAdaptor r) {}
            public void RunFinished(ITestResultAdaptor r) {
                TestRunnerApi.SaveResultToFile(r,"Temp/FinaleValidation/editmode-results.xml");
                File.WriteAllText("Temp/FinaleValidation/editmode-summary.txt",$"Passed {r.PassCount}; failed {r.FailCount}; skipped {r.SkipCount}\n{r.Message}\n");
                Debug.Log($"Finale tests: {r.PassCount} passed, {r.FailCount} failed.");
            }
        }
    }
}
