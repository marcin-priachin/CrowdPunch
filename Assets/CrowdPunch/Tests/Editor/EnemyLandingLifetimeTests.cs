using CrowdPunch.Components;
using CrowdPunch.Systems.Lifetime;
using NUnit.Framework;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace CrowdPunch.Tests
{
    public sealed class EnemyLandingLifetimeTests
    {
        [TestCase(0, 1.3333334f)]
        [TestCase(1, .16666667f)]
        public void Enemy013BombRetainsItsAuthoredDefeatVisualWithoutPriorLaunch(byte exploded, float duration)
        {
            using var world = new World("Bomb defeat visual");
            var em = world.EntityManager;
            var enemy = em.CreateEntity(typeof(Enemy), typeof(RespawnRequest), typeof(DeathRequest),
                typeof(EnemyLaunchState), typeof(EnemyLandingAnimation), typeof(ExplosiveEnemyState));
            em.SetComponentData(enemy, new EnemyLaunchState { Phase = EnemyLaunchPhase.Defeated });
            em.SetComponentData(enemy, new ExplosiveEnemyState { HasExploded = exploded });
            em.SetComponentData(enemy, new EnemyLandingAnimation { Duration = .16666667f, OrdinaryDefeatDuration = 1.3333334f });
            em.SetComponentEnabled<RespawnRequest>(enemy, false);
            world.SetTime(new TimeData(10, .02f));
            var system = world.GetOrCreateSystem<DefeatedEnemyLifecycleSystem>();
            system.Update(world.Unmanaged);
            Assert.That(em.GetComponentData<RespawnRequest>(enemy).PoolNotBefore, Is.EqualTo(10 + duration).Within(.0001));
            Assert.That(em.GetComponentData<EnemyLaunchState>(enemy).Phase, Is.EqualTo(EnemyLaunchPhase.Defeated));
            Assert.That(em.GetComponentData<ExplosiveEnemyState>(enemy).HasExploded, Is.EqualTo(exploded));
            world.SetTime(new TimeData(10.1, .02f));
            system.Update(world.Unmanaged);
            Assert.That(em.GetComponentData<RespawnRequest>(enemy).PoolNotBefore, Is.EqualTo(10 + duration).Within(.0001),
                "The death visual cannot extend its own pooling deadline each frame.");
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Combat011DefeatedBodyStaysVisibleDuringImpact(bool alienSpike)
        {
            using var world = new World("Landing lifetime test");
            var em = world.EntityManager;
            em.CreateEntity(typeof(ArenaBounds));
            var enemy = em.CreateEntity(typeof(Enemy), typeof(RespawnRequest), typeof(DeathRequest),
                typeof(EnemyLaunchState), typeof(EnemyLandingAnimation), typeof(LocalTransform),
                typeof(Health), typeof(HealthBar), typeof(EnemyDamageState), typeof(EnemyRespawnSettings), typeof(EnemyLifetime),
                typeof(PhysicsVelocity), typeof(KnockbackRecovery), typeof(DesiredMovement), typeof(EnemyGroundConstraint),
                typeof(EnemyMovementSettings));
            em.SetComponentData(enemy, LocalTransform.FromPosition(new float3(2f, 1f, 3f)));
            em.SetComponentData(enemy, new EnemyLaunchState { Phase = EnemyLaunchPhase.Defeated, LaunchSequence = 1 });
            em.SetComponentData(enemy, new EnemyLandingAnimation { Duration = 1.4f });
            em.SetComponentEnabled<RespawnRequest>(enemy, false);
            em.SetComponentEnabled<KnockbackRecovery>(enemy, false);
            em.AddSharedComponent(enemy, new PhysicsWorldIndex());
            em.AddBuffer<CollisionDamageHistory>(enemy);
            if (alienSpike)
            {
                em.AddComponent<AlienSpikeTuning>(enemy);
                em.SetComponentData(enemy, new PhysicsVelocity { Linear = new float3(3, 0, 4) });
            }
            world.SetTime(new TimeData(10d, 0.02f));
            world.GetOrCreateSystem<DefeatedEnemyLifecycleSystem>().Update(world.Unmanaged);
            Assert.That(em.GetComponentData<RespawnRequest>(enemy).PoolNotBefore, Is.EqualTo(11.4d).Within(0.0001d));
            Assert.That(em.IsComponentEnabled<DeathRequest>(enemy), Is.False);
            world.SetTime(new TimeData(10.8d, 0.02f));
            world.GetOrCreateSystem<EnemyRespawnSystem>().Update(world.Unmanaged);
            em.CompleteAllTrackedJobs();
            Assert.That(em.GetComponentData<RespawnRequest>(enemy).IsPooled, Is.Zero);
            Assert.That(em.GetSharedComponent<PhysicsWorldIndex>(enemy).Value, Is.Zero,
                "The visible landing must still participate in physics.");
            Assert.That(em.GetComponentData<LocalTransform>(enemy).Position, Is.EqualTo(new float3(2f, 1f, 3f)));
            if (alienSpike)
            {
                float3 sliding = em.GetComponentData<PhysicsVelocity>(enemy).Linear;
                Assert.That(math.length(sliding), Is.InRange(0.05f, 4.99f));
                Assert.That(math.distance(math.normalize(sliding), new float3(.6f, 0, .8f)), Is.LessThan(.0001f));
                var respawn = world.GetOrCreateSystem<EnemyRespawnSystem>();
                for (int tick = 1; tick <= 20; tick++)
                {
                    world.SetTime(new TimeData(10.8d + tick * .02d, .02f));
                    respawn.Update(world.Unmanaged);
                    em.CompleteAllTrackedJobs();
                }
                Assert.That(em.GetComponentData<PhysicsVelocity>(enemy).Linear, Is.EqualTo(float3.zero));
                Assert.That(em.GetComponentData<RespawnRequest>(enemy).IsPooled, Is.Zero);
            }
            world.SetTime(new TimeData(11.5d, 0.02f));
            world.GetOrCreateSystem<EnemyRespawnSystem>().Update(world.Unmanaged);
            em.CompleteAllTrackedJobs();
            Assert.That(em.GetComponentData<RespawnRequest>(enemy).IsPooled, Is.EqualTo(1));
            Assert.That(em.GetSharedComponent<PhysicsWorldIndex>(enemy).Value, Is.EqualTo(uint.MaxValue),
                "Hidden pooled bodies must not collide with one another.");

            // Exercise an actual respawn after pooling, including restoration for the next physics build.
            em.SetComponentData(enemy, new EnemyRespawnSettings { Enabled = 1 });
            var request = em.GetComponentData<RespawnRequest>(enemy);
            request.RespawnAt = 12;
            em.SetComponentData(enemy, request);
            world.SetTime(new TimeData(12.1d, 0.02f));
            world.GetOrCreateSystem<EnemyRespawnSystem>().Update(world.Unmanaged);
            em.CompleteAllTrackedJobs();
            Assert.That(em.IsComponentEnabled<RespawnRequest>(enemy), Is.False);
            Assert.That(em.GetSharedComponent<PhysicsWorldIndex>(enemy).Value, Is.Zero);
        }
    }
}
