using System.IO;
using System.Text;
using CrowdPunch.Components;
using CrowdPunch.Configuration;
using CrowdPunch.Systems.Combat;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEditor;

namespace CrowdPunch.Editor
{
    public static class LinkedPairCrowdPerformanceCapture
    {
        public static string CaptureDamage()
        {
            var report = new StringBuilder("LINK-003/004 completed-update microbenchmark; 20 warmups + 120 samples. Includes indexing, swept contacts and damage, excludes physics/rendering.\n");
            foreach (int count in new[] { 200, 1000 }) {
                using var world = new World("Linked pair crowd benchmark"); var em = world.EntityManager;
                using var physics = new PhysicsWorld(0, 0, 0);
                em.SetComponentData(em.CreateEntity(typeof(PhysicsWorldSingleton)), new PhysicsWorldSingleton { PhysicsWorld = physics });
                var tuning = AssetDatabase.LoadAssetAtPath<LinkedPairSettings>("Assets/CrowdPunch/Data/Settings/LinkedPairSettings.asset").Tuning;
                tuning.EnemyDamage = 0; tuning.Response = LinkedPairResponse.DamageAndPush;
                var sequence = em.CreateEntity(typeof(EnemyWaveSequence)); em.SetComponentData(sequence, new EnemyWaveSequence { RunGeneration = 1 });
                for (int i = 0; i < count + 4; i++) {
                    var e = em.CreateEntity(typeof(Enemy), typeof(LocalTransform), typeof(EnemyLaunchState), typeof(Health), typeof(NavigationAgent),
                        typeof(EnemyLifetime), typeof(PhysicsVelocity), typeof(EnemyTier), typeof(EnemyDamageState), typeof(DamageRequest), typeof(DeathRequest),
                        typeof(EnemyHealthBarVisibility), typeof(ExternalImpulse), typeof(RespawnRequest), typeof(LinkedPairEndpoint));
                    em.SetComponentData(e, LocalTransform.FromPosition(new float3(i % 32 * 1.6f - 25, 0, i / 32 * 1.6f - 25)));
                    em.SetComponentData(e, new Health { Current = 100, Max = 100 }); em.SetComponentData(e, new EnemyLifetime { Generation = 1 });
                    em.SetComponentData(e, new NavigationAgent { Radius = .5f }); em.SetComponentEnabled<RespawnRequest>(e, false);
                    em.SetComponentEnabled<DamageRequest>(e, false); em.SetComponentEnabled<DeathRequest>(e, false); em.SetComponentEnabled<ExternalImpulse>(e, false);
                }
                using var q = em.CreateEntityQuery(typeof(Enemy)); using var bodies = q.ToEntityArray(Unity.Collections.Allocator.Temp);
                for (int i = 0; i < 2; i++) {
                    var a = bodies[i * 2]; var b = bodies[i * 2 + 1]; var record = em.CreateEntity(typeof(LinkedPair), typeof(LinkedPairTuning));
                    em.AddBuffer<LinkedPairClock>(record); em.SetComponentData(record, tuning);
                    float3 pos = new float3(-4, 0, i * 8 - 4);
                    em.SetComponentData(a, LocalTransform.FromPosition(pos)); em.SetComponentData(b, LocalTransform.FromPosition(pos + new float3(7, 0, 0)));
                    em.SetComponentData(a, new LinkedPairEndpoint { Pair = record }); em.SetComponentData(b, new LinkedPairEndpoint { Pair = record });
                    em.SetComponentData(a, new EnemyLaunchState { Phase = EnemyLaunchPhase.Launched, LaunchDamage = 20, Owner = EnemyLaunchOwner.Player });
                    em.SetComponentData(b, em.GetComponentData<EnemyLaunchState>(a));
                    em.SetComponentData(record, new LinkedPair { A = a, B = b, Leader = a, Sequence = sequence, RunGeneration = 1, LifetimeA = 1, LifetimeB = 1,
                        Phase = EnemyLaunchPhase.Launched, PreviousA = pos - new float3(0, 0, .4f), PreviousB = pos + new float3(7, 0, -.4f), HasPrevious = 1 });
                }
                var system = world.GetOrCreateSystem<LinkedPairDamageSystem>();
                for (int i = 0; i < 20; i++) { world.SetTime(new TimeData(i * .02, .02f)); system.Update(world.Unmanaged); em.CompleteAllTrackedJobs(); }
                var timer = System.Diagnostics.Stopwatch.StartNew();
                for (int i = 0; i < 120; i++) { world.SetTime(new TimeData(1 + i * .02, .02f)); system.Update(world.Unmanaged); em.CompleteAllTrackedJobs(); }
                report.AppendLine($"2 launched pairs + {count} ordinary enemies: {timer.Elapsed.TotalMilliseconds / 120:F6} ms/update.");
            }
            Directory.CreateDirectory("Temp/LinkedPairValidation"); File.WriteAllText("Temp/LinkedPairValidation/damage-performance.txt", report.ToString()); return report.ToString();
        }
    }
}
