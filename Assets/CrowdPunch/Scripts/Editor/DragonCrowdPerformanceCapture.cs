using System;
using System.IO;
using System.Text;
using CrowdPunch.Components;
using CrowdPunch.Mono.Player;
using CrowdPunch.Systems.AI;
using CrowdPunch.Systems.Combat;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEditor;
using UnityEngine;

namespace CrowdPunch.Editor
{
    [InitializeOnLoad]
    public static class DragonCrowdPerformanceCapture
    {
        private static bool running;
        private static double since;
        static DragonCrowdPerformanceCapture() => EditorApplication.update += Tick;
        public static void Start()
        {
            if (!EditorApplication.isPlaying) throw new InvalidOperationException("Launch legacy Gauntlet_26 first.");
            var world = World.DefaultGameObjectInjectionWorld; var em = world.EntityManager; em.CompleteAllTrackedJobs();
            using var q = em.CreateEntityQuery(typeof(Enemy), typeof(EnemyWaveOwnership));
            using var roots = q.ToEntityArray(Allocator.Temp); Entity dragon = Entity.Null, baseline = Entity.Null;
            foreach (var e in roots) if (em.HasComponent<DragonTuning>(e)) dragon = e; else baseline = e;
            if (dragon == Entity.Null || baseline == Entity.Null) throw new InvalidOperationException("Wait for the first wave.");
            for (int i = 0; i < 250; i++)
            {
                Entity e = em.Instantiate(i % 25 == 0 ? dragon : baseline); em.RemoveComponent<EnemyWaveOwnership>(e);
                em.SetComponentData(e, LocalTransform.FromPosition(new float3(-13 + i % 20 * 1.3f, 0, -8 + i / 20 * 1.3f)));
                em.SetComponentData(e, new PhysicsVelocity()); em.SetComponentData(e, new EnemyLaunchState());
                em.SetComponentData(e, new Health { Current = 1000000, Max = 1000000 });
                em.SetComponentEnabled<RespawnRequest>(e, false); em.SetComponentEnabled<DeathRequest>(e, false);
                if (em.HasComponent<DragonTuning>(e))
                {
                    var tuning = em.GetComponentData<DragonTuning>(e); tuning.EnemyMode = DragonEnemyFlameMode.BurstsAndLaunches;
                    tuning.Normal = tuning.Launched; em.SetComponentData(e, tuning);
                    em.SetComponentData(e, new DragonFlameState { Initialized = 1, Generation = em.GetComponentData<EnemyLifetime>(e).Generation,
                        Phase = DragonFlamePhase.Active, Remaining = 10000 }); em.GetBuffer<DragonFlameClock>(e).Clear();
                }
            }
            foreach (var root in roots) em.DestroyEntity(root);
            running = true; since = world.Time.ElapsedTime; FeedbackTimeController.SetPaused(false); EditorApplication.isPaused = false;
        }
        private static void Tick()
        {
            if (!running || !EditorApplication.isPlaying || EditorApplication.isPaused) return;
            var health = UnityEngine.Object.FindFirstObjectByType<PlayerHealth>(FindObjectsInactive.Include);
            if (health != null) { health.gameObject.SetActive(true); health.Restore(health.MaxHealth); }
            var world = World.DefaultGameObjectInjectionWorld;
            if (world.Time.ElapsedTime - since < 3) return;
            running = false; EditorApplication.isPaused = true;
            var em = world.EntityManager; em.CompleteAllTrackedJobs();
            using var q = em.CreateEntityQuery(typeof(Enemy)); using var dq = em.CreateEntityQuery(typeof(DragonTuning));
            var report = new StringBuilder(); report.AppendLine($"Unity {Application.unityVersion}; {q.CalculateEntityCount()} enemies, {dq.CalculateEntityCount()} Dragons; three seconds advancing solver/animation simulation.");
            report.AppendLine("20 warmups / 120 completed updates, calling-thread allocations. Frozen physics; synthetic 0.41s clock advances exercise due damage ticks. Editor CPU system measurement, not standalone FPS/GPU or a hardware acceptance target.");
            var handles = new[] { world.GetExistingSystem<DragonPositioningSystem>(), world.GetExistingSystem<DragonFlameCycleSystem>(),
                world.GetExistingSystem<DragonFlameAvoidanceSystem>(), world.GetExistingSystem<DragonFlameDamageSystem>() };
            var names = new[] { "Positioning", "Flame cycle/head samples", "Avoidance", "Damage/spatial index/due ticks" };
            var originalTime = world.Time;
            try
            {
                double now = originalTime.ElapsedTime;
                for (int n = 0; n < handles.Length; n++)
                {
                    for (int i = 0; i < 20; i++) { world.SetTime(new TimeData(now += .41, .02f)); handles[n].Update(world.Unmanaged); em.CompleteAllTrackedJobs(); }
                    long allocated = GC.GetAllocatedBytesForCurrentThread(), start = System.Diagnostics.Stopwatch.GetTimestamp();
                    for (int i = 0; i < 120; i++) { world.SetTime(new TimeData(now += .41, .02f)); handles[n].Update(world.Unmanaged); em.CompleteAllTrackedJobs(); }
                    double ms = (System.Diagnostics.Stopwatch.GetTimestamp() - start) * 1000d / System.Diagnostics.Stopwatch.Frequency / 120;
                    report.AppendLine($"{names[n]}: {ms:F4} ms/update; {GC.GetAllocatedBytesForCurrentThread() - allocated} bytes / 120 updates");
                }
            }
            finally { world.SetTime(originalTime); }
            Directory.CreateDirectory("Temp/DragonValidation"); File.WriteAllText("Temp/DragonValidation/performance.txt", report.ToString());
            Debug.Log(report.ToString());
        }
    }
}
