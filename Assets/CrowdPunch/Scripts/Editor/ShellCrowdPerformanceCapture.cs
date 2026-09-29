using System;
using System.Diagnostics;
using System.IO;
using CrowdPunch.Components;
using CrowdPunch.Systems.Combat;
using CrowdPunch.Systems.Lifetime;
using CrowdPunch.Systems.Presentation;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Transforms;
using UnityEditor;

namespace CrowdPunch.Editor
{
    public static class ShellCrowdPerformanceCapture
    {
        // Synthetic repeated-system costs, not whole-frame or target-hardware performance.
        public static string Capture()
        {
            if (!EditorApplication.isPlaying || !EditorApplication.isPaused)
                throw new InvalidOperationException("Pause Gauntlet_15 before profiling. Exit Play Mode afterward to discard stress entities.");
            var world = World.DefaultGameObjectInjectionWorld;
            var em = world.EntityManager; em.CompleteAllTrackedJobs();
            using var query = em.CreateEntityQuery(typeof(Enemy), typeof(BarricadeCrowdMember));
            using var targets = em.CreateEntityQuery(typeof(ShellTarget));
            if (targets.CalculateEntityCount() != 1) throw new InvalidOperationException("Load the shell level.");
            var impact = world.GetOrCreateSystem<BarricadeImpactSystem>();
            var replenish = world.GetOrCreateSystem<ShellExploderReplenishmentSystem>();
            var visual = world.GetOrCreateSystem<ShellPresentationSystem>();
            string report = "Shell Editor system timings: 20 warmups + 200 samples; fixed synthetic launched crowd.\n";
            foreach (int count in new[] { 14, 128 })
            {
                using (var originals = query.ToEntityArray(Allocator.Temp))
                {
                    Entity template = Entity.Null;
                    foreach (var e in originals) if (!em.HasComponent<ExplosiveEnemyState>(e)) { template = e; break; }
                    for (int i = originals.Length; i < count; i++) em.Instantiate(template);
                }
                using (var bodies = query.ToEntityArray(Allocator.Temp))
                    for (int i = 0; i < bodies.Length; i++)
                    {
                        var e = bodies[i]; var t = em.GetComponentData<LocalTransform>(e);
                        t.Position = new float3(-10 + (i % 8) * 2.8f, .05f, -10 + (i / 8) * 1.1f); em.SetComponentData(e, t);
                        em.SetComponentData(e, new EnemyLaunchState { Phase = EnemyLaunchPhase.Launched, LaunchSequence = 1, Owner = EnemyLaunchOwner.Player });
                        em.SetComponentData(e, new PhysicsVelocity { Linear = new float3(0,0,8) });
                        em.SetComponentEnabled<RespawnRequest>(e, false);
                    }
                world.GetExistingSystemManaged<PhysicsSystemGroup>().Update(); em.CompleteAllTrackedJobs();
                report += count + " roots: " + Measure(world, impact, "sweep") + "; "
                    + Measure(world, replenish, "exploder supply") + "; " + Measure(world, visual, "shell visuals") + "\n";
            }
            Directory.CreateDirectory("Temp/ShellValidation"); File.WriteAllText("Temp/ShellValidation/performance.txt", report);
            return report;
        }
        private static string Measure(World world, SystemHandle system, string name)
        {
            var samples = new double[200];
            for (int i = -20; i < samples.Length; i++)
            {
                long begin = Stopwatch.GetTimestamp(); system.Update(world.Unmanaged); world.EntityManager.CompleteAllTrackedJobs();
                if (i >= 0) samples[i] = (Stopwatch.GetTimestamp() - begin) * 1000d / Stopwatch.Frequency;
            }
            Array.Sort(samples); double sum = 0; foreach (double sample in samples) sum += sample;
            return $"{name} mean {sum / samples.Length:F4} ms, p95 {samples[189]:F4} ms";
        }
    }
}
