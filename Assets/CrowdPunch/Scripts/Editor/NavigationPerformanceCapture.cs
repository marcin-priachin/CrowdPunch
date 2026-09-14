using System;
using System.IO;
using System.Text;
using CrowdPunch.Components;
using CrowdPunch.Systems.AI;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEditor;
namespace CrowdPunch.Editor
{
    public static class NavigationPerformanceCapture
    {
        [MenuItem("Crowd Punch/Navigation/Benchmark Current Crowd")]
        public static void Capture()
        {
            if (!EditorApplication.isPlaying) throw new InvalidOperationException("Enter the validation arena first.");
            var world = World.DefaultGameObjectInjectionWorld; var em = world.EntityManager;
            using var grid = em.CreateEntityQuery(typeof(NavigationGrid), typeof(NavigationRuntimeSettings));
            if (grid.CalculateEntityCount() != 1) throw new InvalidOperationException("Exactly one navigation arena is required.");
            using var crowd = em.CreateEntityQuery(typeof(Enemy), typeof(EnemyLaunchState));
            using var entities = crowd.ToEntityArray(Allocator.Temp);
            int active = 0, pathBytes = 0;
            foreach (var e in entities)
            {
                if (em.GetComponentData<EnemyLaunchState>(e).Phase == EnemyLaunchPhase.Active && !em.IsComponentEnabled<RespawnRequest>(e)) active++;
                if (em.HasBuffer<NavigationWaypoint>(e)) pathBytes += em.GetBuffer<NavigationWaypoint>(e).Capacity * 8;
            }
            var handles = new[]{world.GetExistingSystem<EnemyChaseSystem>(),world.GetExistingSystem<RangedEnemyPositioningSystem>(),
                world.GetExistingSystem<DasherDecisionSystem>(),world.GetExistingSystem<EnemyNavigationSystem>()};
            string[] names = { "Chase + separation", "Ranged positioning + separation", "Dasher decision + separation", "Navigation" };
            var settings = grid.GetSingleton<NavigationRuntimeSettings>(); var original = settings;
            var report = new StringBuilder(); report.AppendLine($"UTC {DateTime.UtcNow:O}; Editor {Application.unityVersion}; total {entities.Length}; active {active}; native path capacity {pathBytes} bytes");
            report.AppendLine("Completed-update microbenchmark, 20 warmups + 120 samples per system. Includes main-thread and job completion time; simulation time and physics are not advanced. Managed bytes cover calling thread only. This is not a whole-frame/standalone benchmark.");
            em.CompleteAllTrackedJobs();
            try
            {
                foreach (byte enabled in new byte[] { 1, 0 })
                {
                    settings.Enabled = enabled; em.SetComponentData(grid.GetSingletonEntity(), settings);
                    report.AppendLine("Assistance " + enabled);
                    for (int n = 0; n < handles.Length; n++)
                    {
                        for (int i = 0; i < 20; i++) { handles[n].Update(world.Unmanaged); em.CompleteAllTrackedJobs(); }
                        long allocated = GC.GetAllocatedBytesForCurrentThread(), start = System.Diagnostics.Stopwatch.GetTimestamp();
                        for (int i = 0; i < 120; i++) { handles[n].Update(world.Unmanaged); em.CompleteAllTrackedJobs(); }
                        double milliseconds = (System.Diagnostics.Stopwatch.GetTimestamp() - start) * 1000d / System.Diagnostics.Stopwatch.Frequency / 120;
                        long bytes = GC.GetAllocatedBytesForCurrentThread() - allocated;
                        report.AppendLine($"{names[n]}: {milliseconds:F6} ms/update, {bytes} managed bytes / 120 updates");
                    }
                }
            }
            finally { em.SetComponentData(grid.GetSingletonEntity(), original); }
            Directory.CreateDirectory("Library/NavigationValidation");
            File.WriteAllText($"Library/NavigationValidation/benchmark-{entities.Length}.txt", report.ToString()); Debug.Log(report.ToString());
        }
    }
}
