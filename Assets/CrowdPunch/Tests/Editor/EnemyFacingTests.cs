using CrowdPunch.Components;
using CrowdPunch.Systems.Physics;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace CrowdPunch.Tests
{
    public sealed class EnemyFacingTests
    {
        [TestCase(EnemyLaunchPhase.Active)]
        [TestCase(EnemyLaunchPhase.Recovering)]
        [TestCase(EnemyLaunchPhase.Launched)]
        [TestCase(EnemyLaunchPhase.Defeated)]
        public void Info004FacingFollowsLifecycleWithoutChangingMovement(EnemyLaunchPhase phase)
        {
            using var world = new World("Enemy facing test");
            var em = world.EntityManager;
            Entity player = em.CreateEntity(typeof(PlayerSnapshot));
            em.SetComponentData(player, new PlayerSnapshot { Position = new float3(8f, 20f, 3f), IsAvailable = true });
            Entity enemy = em.CreateEntity(typeof(Enemy), typeof(LocalTransform), typeof(PhysicsVelocity),
                typeof(EnemyLaunchState), typeof(RespawnRequest));
            em.SetComponentEnabled<RespawnRequest>(enemy, false);
            var initial = LocalTransform.FromPosition(new float3(2f, 1f, 3f));
            var velocity = new PhysicsVelocity { Linear = new float3(-3f, 2f, 1f), Angular = new float3(0f, 2f, 0f) };
            em.SetComponentData(enemy, initial);
            em.SetComponentData(enemy, velocity);
            em.SetComponentData(enemy, new EnemyLaunchState { Phase = phase });
            var system = world.GetOrCreateSystem<EnemyFacingSystem>();
            system.Update(world.Unmanaged);
            em.CompleteAllTrackedJobs();
            bool faces = phase == EnemyLaunchPhase.Active || phase == EnemyLaunchPhase.Recovering;
            var actual = em.GetComponentData<LocalTransform>(enemy);
            Assert.That(math.distance(math.forward(actual.Rotation), faces ? new float3(1f, 0f, 0f) : math.forward()), Is.LessThan(0.0001f));
            Assert.That(actual.Position, Is.EqualTo(initial.Position));
            Assert.That(em.GetComponentData<PhysicsVelocity>(enemy).Linear, Is.EqualTo(velocity.Linear));
            Assert.That(em.GetComponentData<PhysicsVelocity>(enemy).Angular.y, Is.EqualTo(faces ? 0f : 2f));

            // A moving player must update facing; no delta-time-dependent turn lag.
            em.SetComponentData(player, new PlayerSnapshot { Position = new float3(2f, -4f, -7f), IsAvailable = true });
            system.Update(world.Unmanaged);
            em.CompleteAllTrackedJobs();
            Assert.That(math.distance(math.forward(em.GetComponentData<LocalTransform>(enemy).Rotation),
                faces ? new float3(0f, 0f, -1f) : math.forward()), Is.LessThan(0.0001f));
        }

        [TestCase(true, false, false)]
        [TestCase(false, true, false)]
        [TestCase(false, false, true)]
        public void Info004SkipsRespawnUnavailablePlayerAndCoincidentPositions(bool respawning, bool unavailable, bool coincident)
        {
            using var world = new World("Enemy facing guard test");
            var em = world.EntityManager;
            Entity player = em.CreateEntity(typeof(PlayerSnapshot));
            em.SetComponentData(player, new PlayerSnapshot { Position = coincident ? new float3(0f, 10f, 0f) : new float3(5f, 0f, 0f), IsAvailable = !unavailable });
            Entity enemy = em.CreateEntity(typeof(Enemy), typeof(LocalTransform), typeof(PhysicsVelocity),
                typeof(EnemyLaunchState), typeof(RespawnRequest));
            em.SetComponentData(enemy, LocalTransform.Identity);
            em.SetComponentData(enemy, new EnemyLaunchState { Phase = EnemyLaunchPhase.Active });
            em.SetComponentEnabled<RespawnRequest>(enemy, respawning);
            world.GetOrCreateSystem<EnemyFacingSystem>().Update(world.Unmanaged);
            em.CompleteAllTrackedJobs();
            Assert.That(em.GetComponentData<LocalTransform>(enemy).Rotation, Is.EqualTo(quaternion.identity));
        }
    }
}
