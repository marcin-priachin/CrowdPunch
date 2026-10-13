using System.IO;
using System.Text;
using CrowdPunch.Components;
using CrowdPunch.Configuration;
using CrowdPunch.Systems.AI;
using CrowdPunch.Systems.Physics;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Transforms;
using UnityEditor;

namespace CrowdPunch.Editor
{
    public static class NinjaCrowdPerformanceCapture
    {
        public static string CaptureDecisions()
        {
            var report = new StringBuilder("Completed-update microbenchmark; 20 warmups + 120 samples. No simulation/rendering or FPS claim.\n");
            foreach (int count in new[] { 200, 1000 })
            {
                using var world = new World("Ninja decision benchmark"); var em = world.EntityManager;
                world.SetTime(new TimeData(10, .02f));
                em.SetComponentData(em.CreateEntity(typeof(PlayerSnapshot)), new PlayerSnapshot { IsAvailable = true, Forward = math.forward(), Radius = 1.5f });
                var tuning = AssetDatabase.LoadAssetAtPath<NinjaSettings>("Assets/CrowdPunch/Data/Settings/NinjaSettings.asset").Tuning;
                for (int i = 0; i < count + 3; i++)
                {
                    var e = em.CreateEntity(typeof(Enemy), typeof(LocalTransform), typeof(EnemyLaunchState), typeof(CrowdPunch.Components.EnemyArchetype), typeof(RespawnRequest));
                    em.SetComponentEnabled<RespawnRequest>(e, false);
                    em.SetComponentData(e, LocalTransform.FromPosition(new float3(i % 32 * 1.6f - 25, 0, i / 32 * 1.6f - 25)));
                    if (i >= 3) continue;
                    em.AddComponentData(e, tuning);
                    em.AddComponentData(e, new NinjaState { Initialized = 1, Generation = 1, ReadyAt = 1000, PeriodicAt = 1000, RandomState = (uint)i + 1 });
                    em.AddComponentData(e, new EnemyLifetime { Generation = 1 });
                    em.AddComponentData(e, new NavigationAgent { Radius = .45f });
                    em.AddComponentData(e, new EnemyMovementSettings { WanderSpeed = 1.5f, SeparationWeight = 1.4f });
                    em.AddComponentData(e, new EnemySeparationDistance { Value = 1.6f });
                    em.AddComponent<EnemyArchetypeSeparationDistances>(e);
                    em.AddComponent<DesiredMovement>(e); em.AddComponent<NavigationIntent>(e);
                }
                var system = world.GetOrCreateSystem<NinjaDecisionSystem>();
                for (int i = 0; i < 20; i++) { system.Update(world.Unmanaged); em.CompleteAllTrackedJobs(); }
                var timer = System.Diagnostics.Stopwatch.StartNew();
                for (int i = 0; i < 120; i++) { system.Update(world.Unmanaged); em.CompleteAllTrackedJobs(); }
                report.AppendLine($"3 positioning Ninjas + {count} ordinary enemies: {timer.Elapsed.TotalMilliseconds / 120:F6} ms/update.");
            }
            Directory.CreateDirectory("Temp/NinjaValidation"); File.WriteAllText("Temp/NinjaValidation/decision-performance.txt", report.ToString());
            return report.ToString();
        }

        // Use only a paused legacy preview. Temporary bodies are removed before returning.
        public static string CapturePhysics()
        {
            if (!EditorApplication.isPlaying || !EditorApplication.isPaused) throw new System.InvalidOperationException("Pause the Ninja legacy preview first.");
            var world = World.DefaultGameObjectInjectionWorld; var em = world.EntityManager; em.CompleteAllTrackedJobs();
            using var prefabs = em.CreateEntityQuery(new EntityQueryDesc { All = new[] { ComponentType.ReadOnly<NinjaState>(), ComponentType.ReadOnly<Prefab>() }, Options = EntityQueryOptions.IncludePrefab });
            Entity ninjaPrefab = prefabs.GetSingletonEntity();
            using var sequence = em.CreateEntityQuery(typeof(EnemyWaveSequence));
            Entity baselinePrefab = Entity.Null;
            foreach (var profile in em.GetBuffer<EnemyWaveProfile>(sequence.GetSingletonEntity()))
                if (profile.Profile.Archetype == EnemyArchetypeKind.Baseline) { baselinePrefab = profile.Profile.EnemyPrefab; break; }
            if (baselinePrefab == Entity.Null) throw new System.InvalidOperationException("No baked Baseline profile.");
            var oldTime = world.Time; world.SetTime(new TimeData(oldTime.ElapsedTime, .02f));
            var report = new StringBuilder("Actual baked capsules in a paused legacy arena; 10 warmups + 60 samples, reset fixture poses/velocities. Excludes rendering/full gameplay.\n");
            try
            {
                foreach (int count in new[] { 200, 1000 })
                {
                    var bodies = new NativeList<Entity>(Allocator.Temp);
                    var poses = new NativeList<LocalTransform>(Allocator.Temp);
                    try
                    {
                        for (int i = 0; i < count + 3; i++)
                        {
                            var e = em.Instantiate(i < 3 ? ninjaPrefab : baselinePrefab); bodies.Add(e);
                            em.AddComponentData(e, new EnemyTier { Value = EnemyCombatTier.Normal });
                            em.AddComponentData(e, new CrowdPunch.Components.EnemyArchetype { Value = i < 3 ? EnemyArchetypeKind.Ninja : EnemyArchetypeKind.Baseline });
                            em.SetComponentEnabled<RespawnRequest>(e, false);
                            poses.Add(LocalTransform.FromPosition(new float3(i % 32 * 1.1f - 17, 0, i / 32 * 1.1f - 17)));
                        }
                        double physicsMs = 0, collisionMs = 0;
                        var physics = world.GetExistingSystemManaged<PhysicsSystemGroup>(); var collision = world.GetOrCreateSystem<NinjaCollisionSystem>();
                        for (int sample = -10; sample < 60; sample++)
                        {
                            for (int i = 0; i < bodies.Length; i++)
                            {
                                var e = bodies[i]; em.SetComponentData(e, poses[i]);
                                em.SetComponentData(e, new EnemyLaunchState { Phase = i >= 3 && i < 35 ? EnemyLaunchPhase.Launched : EnemyLaunchPhase.Active,
                                    Owner = EnemyLaunchOwner.Player, LaunchSequence = 1, LaunchDamage = 20 });
                                em.SetComponentData(e, new PhysicsVelocity { Linear = i < 2 ? new float3(48, 0, 0) : i < 35 ? new float3(20, 0, 0) : float3.zero });
                                if (i < 3) em.SetComponentData(e, new NinjaState { Phase = i < 2 ? NinjaPhase.Dashing : NinjaPhase.Preparing,
                                    LockedDirection = new float3(1, 0, 0), HasDashSegment = i < 2 ? (byte)1 : (byte)0 });
                            }
                            var timer = System.Diagnostics.Stopwatch.StartNew(); physics.Update(); em.CompleteAllTrackedJobs();
                            double step = timer.Elapsed.TotalMilliseconds;
                            timer.Restart(); collision.Update(world.Unmanaged); em.CompleteAllTrackedJobs();
                            if (sample >= 0) { physicsMs += step; collisionMs += timer.Elapsed.TotalMilliseconds; }
                        }
                        report.AppendLine($"3 Ninjas + {count} Baselines (32 launched): physics including selective body-pair filter {physicsMs / 60:F6} ms/step; Ninja collision interpretation {collisionMs / 60:F6} ms/update.");
                    }
                    finally
                    {
                        foreach (var body in bodies) if (em.Exists(body)) em.DestroyEntity(body);
                        bodies.Dispose(); poses.Dispose();
                        world.GetExistingSystemManaged<CrowdPunch.Systems.Groups.GamePrePhysicsGroup>().RequestPhysicsRebuild();
                    }
                }
            }
            finally { world.SetTime(oldTime); }
            Directory.CreateDirectory("Temp/NinjaValidation"); File.WriteAllText("Temp/NinjaValidation/physics-performance.txt", report.ToString());
            return report.ToString();
        }
    }
}
