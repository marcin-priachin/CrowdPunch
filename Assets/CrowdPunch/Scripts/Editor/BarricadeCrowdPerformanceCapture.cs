using System;
using System.Diagnostics;
using System.IO;
using CrowdPunch.Components;
using CrowdPunch.Systems.Combat;
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
    public static class BarricadeCrowdPerformanceCapture
    {
        // Isolated repeated-system cost in the real baked collision world, not a whole-frame benchmark.
        // Run after the live check pauses, then exit Play Mode to discard the synthetic stress crowd.
        public static string Capture()
        {
            if (!EditorApplication.isPlaying || !EditorApplication.isPaused)
                throw new InvalidOperationException("Pause Gauntlet_13 in Play Mode first.");
            var world = World.DefaultGameObjectInjectionWorld;
            var em = world.EntityManager; em.CompleteAllTrackedJobs();
            using var query = em.CreateEntityQuery(typeof(Enemy), typeof(BarricadeCrowdMember));
            using var walls = em.CreateEntityQuery(typeof(Barricade));
            var wall = walls.GetSingletonEntity();
            var impact = world.GetOrCreateSystem<BarricadeImpactSystem>();
            var presentation = world.GetOrCreateSystem<BarricadePresentationSystem>();
            string report = "Editor managed-system timings, 20 warmups + 200 samples. All bodies launched; synthetic fixed positions.\n";
            foreach (int count in new[] { 16, 128 })
            {
                using (var originals = query.ToEntityArray(Allocator.Temp))
                {
                    Entity template = originals[0];
                    foreach (var e in originals) if (!em.HasComponent<ExplosiveEnemyState>(e)) { template = e; break; }
                    for (int i = originals.Length; i < count; i++) em.Instantiate(template);
                }
                using (var bodies = query.ToEntityArray(Allocator.Temp))
                    for (int i = 0; i < bodies.Length; i++)
                    {
                        var e = bodies[i];
                        var transform = em.GetComponentData<LocalTransform>(e);
                        transform.Position = new float3(-10 + (i % 8) * 2.8f, .05f, -10 + (i / 8) * 1.1f);
                        em.SetComponentData(e, transform);
                        em.SetComponentData(e, new EnemyLaunchState { Phase = EnemyLaunchPhase.Launched, LaunchSequence = 1, Owner = EnemyLaunchOwner.Player });
                        em.SetComponentData(e, new PhysicsVelocity { Linear = new float3(0,0,8) });
                        em.SetComponentEnabled<RespawnRequest>(e, false);
                    }
                var tuning = em.GetComponentData<Barricade>(wall); tuning.HitsRemaining = tuning.RequiredHits;
                em.SetComponentData(wall, tuning); em.SetComponentData(wall, new PhysicsCollider { Value = tuning.IntactCollider });
                world.GetExistingSystemManaged<PhysicsSystemGroup>().Update(); em.CompleteAllTrackedJobs();
                var samples = new double[200];
                for (int i = -20; i < samples.Length; i++)
                {
                    long begin = Stopwatch.GetTimestamp();
                    impact.Update(world.Unmanaged); em.CompleteAllTrackedJobs();
                    if (i >= 0) samples[i] = (Stopwatch.GetTimestamp() - begin) * 1000d / Stopwatch.Frequency;
                }
                Array.Sort(samples);
                double sum = 0; foreach (double sample in samples) sum += sample;
                report += $"{count} bodies: sweep mean {sum / samples.Length:F4} ms; p95 {samples[189]:F4} ms; max {samples[199]:F4} ms\n";
                var timer = Stopwatch.StartNew();
                for (int i = 0; i < 200; i++) presentation.Update(world.Unmanaged);
                em.CompleteAllTrackedJobs(); timer.Stop();
                report += $"26 visual entities: presentation mean {timer.Elapsed.TotalMilliseconds / 200:F4} ms\n";
            }
            Directory.CreateDirectory("Temp/BarricadeValidation");
            File.WriteAllText("Temp/BarricadeValidation/performance.txt", report);
            return report;
        }
    }
}
