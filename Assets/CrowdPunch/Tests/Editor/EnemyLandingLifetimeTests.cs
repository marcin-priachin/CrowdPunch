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
        [Test]
        public void Combat011DefeatedBodyStaysVisibleDuringImpact()
        {
            using var world = new World("Landing lifetime test");
            var em = world.EntityManager;
            em.CreateEntity(typeof(ArenaBounds));
            var enemy = em.CreateEntity(typeof(Enemy), typeof(RespawnRequest), typeof(DeathRequest),
                typeof(EnemyLaunchState), typeof(EnemyLandingAnimation), typeof(LocalTransform),
                typeof(Health), typeof(HealthBar), typeof(EnemyDamageState), typeof(EnemyRespawnSettings),
                typeof(PhysicsVelocity), typeof(KnockbackRecovery), typeof(DesiredMovement));
            em.SetComponentData(enemy, LocalTransform.FromPosition(new float3(2f, 1f, 3f)));
            em.SetComponentData(enemy, new EnemyLaunchState { Phase = EnemyLaunchPhase.Defeated, LaunchSequence = 1 });
            em.SetComponentData(enemy, new EnemyLandingAnimation { Duration = 1.4f });
            em.SetComponentEnabled<RespawnRequest>(enemy, false);
            em.SetComponentEnabled<KnockbackRecovery>(enemy, false);
            em.AddBuffer<CollisionDamageHistory>(enemy);
            world.SetTime(new TimeData(10d, 0.02f));
            world.GetOrCreateSystem<DefeatedEnemyLifecycleSystem>().Update(world.Unmanaged);
            Assert.That(em.GetComponentData<RespawnRequest>(enemy).PoolNotBefore, Is.EqualTo(11.4d).Within(0.0001d));
            Assert.That(em.IsComponentEnabled<DeathRequest>(enemy), Is.False);
            world.SetTime(new TimeData(11d, 0.02f));
            world.GetOrCreateSystem<EnemyRespawnSystem>().Update(world.Unmanaged);
            em.CompleteAllTrackedJobs();
            Assert.That(em.GetComponentData<RespawnRequest>(enemy).IsPooled, Is.Zero);
            Assert.That(em.GetComponentData<LocalTransform>(enemy).Position, Is.EqualTo(new float3(2f, 1f, 3f)));
            world.SetTime(new TimeData(11.5d, 0.02f));
            world.GetOrCreateSystem<EnemyRespawnSystem>().Update(world.Unmanaged);
            em.CompleteAllTrackedJobs();
            Assert.That(em.GetComponentData<RespawnRequest>(enemy).IsPooled, Is.EqualTo(1));
        }
    }
}
