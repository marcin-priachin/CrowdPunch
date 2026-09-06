// Temporary editor-only validation probe; removed after the task's checks finish.
using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.TestTools.TestRunner.Api;
using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Transforms;
using CrowdPunch.Components;
using CrowdPunch.Mono.Levels;
using CrowdPunch.Mono.Player;

[InitializeOnLoad]
internal static class DeliberateShotProbe
{
    private const string Root = "Temp/DeliberateShots/";
    private static double freezeAt;
    static DeliberateShotProbe() { EditorApplication.update += Poll; }
    private static void Poll()
    {
        if (freezeAt > 0 && EditorApplication.isPlaying)
        {
            UnityEngine.Object.FindFirstObjectByType<PlayerHealth>(FindObjectsInactive.Include)?.ResetHealth();
            if (EditorApplication.timeSinceStartup >= freezeAt)
            {
                freezeAt = 0;
                EditorApplication.isPaused = true;
                Snapshot(); Capture("after");
            }
        }
        if (EditorApplication.isCompiling || !File.Exists(Root + "command.txt")) return;
        string command = File.ReadAllText(Root + "command.txt").Trim();
        File.Delete(Root + "command.txt");
        try
        {
            if (command == "start")
            {
                EditorSceneManager.OpenScene("Assets/CrowdPunch/Scenes/Bootstrap.unity");
                EditorApplication.isPlaying = true;
            }
            else if (command == "stop") EditorApplication.isPlaying = false;
            else if (command == "tests")
            {
                var api = ScriptableObject.CreateInstance<TestRunnerApi>();
                api.RegisterCallbacks(new Results());
                api.Execute(new ExecutionSettings(new Filter { testMode = TestMode.EditMode, assemblyNames = new[] { "Assembly-CSharp-Editor" } }));
            }
            else if (command.StartsWith("level"))
            {
                EditorApplication.isPaused = false;
                UnityEngine.Object.FindFirstObjectByType<GauntletSequence>().SelectLevel(int.Parse(command.Substring(5)));
                freezeAt = EditorApplication.timeSinceStartup + 12;
            }
            else if (command == "pause") { EditorApplication.isPaused = true; Snapshot(); }
            else if (command == "resume") EditorApplication.isPaused = false;
            else if (command == "snapshot") Snapshot();
            else if (command == "capture") Capture("after");
            File.AppendAllText(Root + "probe.log", DateTime.Now + " OK " + command + "\n");
        }
        catch (Exception exception) { File.AppendAllText(Root + "probe.log", exception + "\n"); }
    }
    private static void Snapshot()
    {
        var world = World.DefaultGameObjectInjectionWorld;
        if (world == null) { File.AppendAllText(Root + "probe.log", "No world\n"); return; }
        var m = world.EntityManager;
        using var query = m.CreateEntityQuery(typeof(Enemy), typeof(EnemyArchetype), typeof(EnemyLaunchState));
        using var enemies = query.ToEntityArray(Allocator.Temp);
        int[] counts = new int[5];
        int launched = 0, preparing = 0;
        foreach (var e in enemies)
        {
            counts[(int)m.GetComponentData<EnemyArchetype>(e).Value]++;
            if (m.GetComponentData<EnemyLaunchState>(e).Phase == EnemyLaunchPhase.Launched) launched++;
            if (m.GetComponentData<EnemyContactAttemptState>(e).IsWindingUp != 0) preparing++;
        }
        var p = UnityEngine.Object.FindFirstObjectByType<PlayerHealth>(FindObjectsInactive.Include);
        File.AppendAllText(Root + "probe.log", "t=" + Time.time + " counts=" + string.Join(",", counts) + " launched=" + launched + " preparing=" + preparing + " playerHP=" + (p == null ? -1 : p.CurrentHealth) + "\n");
    }
    private static void Capture(string name)
    {
        var camera = Camera.main;
        var rt = RenderTexture.GetTemporary(1280, 720, 24);
        var old = camera.targetTexture;
        var active = RenderTexture.active;
        var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); tex.Apply();
            File.WriteAllBytes(Root + name + ".png", tex.EncodeToPNG());
        }
        finally { camera.targetTexture = old; RenderTexture.active = active; RenderTexture.ReleaseTemporary(rt); UnityEngine.Object.DestroyImmediate(tex); }
    }
    private sealed class Results : ICallbacks
    {
        public void RunStarted(ITestAdaptor test) { }
        public void TestStarted(ITestAdaptor test) { }
        public void TestFinished(ITestResultAdaptor result) { }
        public void RunFinished(ITestResultAdaptor result)
        {
            TestRunnerApi.SaveResultToFile(result, Root + "tests.xml");
            File.AppendAllText(Root + "probe.log", "Tests passed=" + result.PassCount + " failed=" + result.FailCount + "\n");
        }
    }
}
