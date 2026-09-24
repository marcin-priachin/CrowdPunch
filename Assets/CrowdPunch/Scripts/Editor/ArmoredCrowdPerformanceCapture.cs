using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using CrowdPunch.Components;
using CrowdPunch.Mono.Player;
using CrowdPunch.Systems.AI;
using CrowdPunch.Systems.Combat;
using CrowdPunch.Systems.Movement;
using CrowdPunch.Systems.Presentation;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEditor;
using UnityEngine;

namespace CrowdPunch.Editor
{
    // Repeatable Editor-only load probe; injected roots never belong to encounter accounting.
    [InitializeOnLoad]
    public static class ArmoredCrowdPerformanceCapture
    {
        private static readonly List<Entity> injected = new List<Entity>();
        private static double finishAt;
        private static bool running;
        static ArmoredCrowdPerformanceCapture() { EditorApplication.update += Tick; }

        public static void Start()
        {
            if (!EditorApplication.isPlaying || running) throw new InvalidOperationException("Enter Gauntlet_12 first.");
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            em.CompleteAllTrackedJobs();
            using var query = em.CreateEntityQuery(typeof(Enemy), typeof(EnemyArchetype));
            using var roots = query.ToEntityArray(Allocator.Temp);
            Entity armored = Entity.Null, baseline = Entity.Null;
            foreach (var e in roots)
            {
                if (em.GetComponentData<EnemyArchetype>(e).Value == EnemyArchetypeKind.Armored) armored = e;
                if (em.GetComponentData<EnemyArchetype>(e).Value == EnemyArchetypeKind.Baseline) baseline = e;
            }
            if (armored == Entity.Null || baseline == Entity.Null) throw new InvalidOperationException("Both live profiles required.");
            for (int i = roots.Length; i < 250; i++)
            {
                var e = em.Instantiate(i % 5 == 0 ? armored : baseline);
                injected.Add(e);
                em.RemoveComponent<EnemyWaveOwnership>(e);
                em.SetComponentData(e, LocalTransform.FromPosition(new float3(-11 + i % 20 * 1.15f, 0, -7 + i / 20 * 1.2f)));
                em.SetComponentData(e, new Health { Current = 10000, Max = 10000 });
                em.SetComponentData(e, new PhysicsVelocity());
                em.SetComponentData(e, new EnemyLaunchState());
                em.SetComponentEnabled<RespawnRequest>(e, false);
                em.SetComponentEnabled<DeathRequest>(e, false);
                em.GetBuffer<CollisionDamageHistory>(e).Clear();
                if (em.HasComponent<EnemyArmor>(e))
                { em.SetComponentData(e, EnemyArmor.Fresh); em.GetBuffer<ArmorHitHistory>(e).Clear(); }
            }
            running = true; finishAt = EditorApplication.timeSinceStartup + 5;
            EditorApplication.isPaused = false;
        }

        private static void Tick()
        {
            if (!running) return;
            if (!EditorApplication.isPlaying) { running = false; injected.Clear(); return; }
            var player = UnityEngine.Object.FindFirstObjectByType<PlayerHealth>(FindObjectsInactive.Include);
            if (player != null) { player.gameObject.SetActive(true); player.Restore(player.MaxHealth); }
            if (EditorApplication.timeSinceStartup < finishAt) return;
            running = false; EditorApplication.isPaused = true;
            var world = World.DefaultGameObjectInjectionWorld; var em = world.EntityManager;
            try { Capture(world); }
            finally
            {
                em.CompleteAllTrackedJobs();
                foreach (var e in injected) if (em.Exists(e)) em.DestroyEntity(e);
                injected.Clear();
            }
        }

        private static void Capture(World world)
        {
            var em = world.EntityManager; em.CompleteAllTrackedJobs();
            using var query = em.CreateEntityQuery(typeof(Enemy), typeof(EnemyLaunchState));
            using var roots = query.ToEntityArray(Allocator.Temp);
            int active = 0, armorCount = 0, histories = 0;
            foreach (var e in roots)
            {
                if (em.GetComponentData<EnemyLaunchState>(e).Phase == EnemyLaunchPhase.Active && !em.IsComponentEnabled<RespawnRequest>(e)) active++;
                if (em.HasBuffer<ArmorHitHistory>(e)) { armorCount++; histories += em.GetBuffer<ArmorHitHistory>(e).Length; }
            }
            var handles = new[] { world.GetExistingSystem<EnemyChaseSystem>(), world.GetExistingSystem<EnemyNavigationSystem>(),
                world.GetExistingSystem<EnemyMovementSystem>(), world.GetExistingSystem<EnemyLaunchCollisionSystem>(),
                world.GetExistingSystem<CollisionDamageHistoryCleanupSystem>(), world.GetExistingSystem<PunchAimAssistSystem>(),
                world.GetExistingSystem<EnemyReadabilitySystem>(), world.GetExistingSystem<EnemyAnimationSystem>() };
            string[] names = { "Chase + separation", "Navigation", "Movement", "Collision (settled contacts)", "Hit-history cleanup", "Aim-assist", "Readability", "Sampled animation" };
            var report = new StringBuilder();
            report.AppendLine($"UTC {DateTime.UtcNow:O}; Unity {Application.unityVersion}; roots {roots.Length}; active {active}; armored {armorCount}; armor history entries {histories}");
            report.AppendLine("250-body mixed crowd, five seconds of actual Editor simulation before capture. Completed-update microbenchmark: 20 warmups + 120 samples/system, job completion included. Physics/time frozen during measurement, settled contacts, no punch request. Not whole-frame FPS or a collision-burst/standalone benchmark. Allocations cover calling thread only.");
            for (int n = 0; n < handles.Length; n++)
            {
                for (int i = 0; i < 20; i++) { handles[n].Update(world.Unmanaged); em.CompleteAllTrackedJobs(); }
                long allocated = GC.GetAllocatedBytesForCurrentThread(), start = System.Diagnostics.Stopwatch.GetTimestamp();
                for (int i = 0; i < 120; i++) { handles[n].Update(world.Unmanaged); em.CompleteAllTrackedJobs(); }
                double milliseconds = (System.Diagnostics.Stopwatch.GetTimestamp() - start) * 1000d / System.Diagnostics.Stopwatch.Frequency / 120;
                report.AppendLine($"{names[n]}: {milliseconds:F6} ms/update, {GC.GetAllocatedBytesForCurrentThread() - allocated} managed bytes / 120 updates");
            }
            Directory.CreateDirectory("Temp/ArmoredValidation");
            File.WriteAllText("Temp/ArmoredValidation/performance.txt", report.ToString());
            Debug.Log(report.ToString());
        }
    }
}
