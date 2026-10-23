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
            bool launched = phase == EnemyLaunchPhase.Launched;
            float3 launchedForward = math.normalize(new float3(velocity.Linear.x, 0f, velocity.Linear.z));
            var actual = em.GetComponentData<LocalTransform>(enemy);
            Assert.That(math.distance(math.forward(actual.Rotation), faces ? new float3(1f, 0f, 0f) : launched ? launchedForward : math.forward()), Is.LessThan(0.0001f));
            Assert.That(actual.Position, Is.EqualTo(initial.Position));
            Assert.That(em.GetComponentData<PhysicsVelocity>(enemy).Linear, Is.EqualTo(velocity.Linear));
            Assert.That(em.GetComponentData<PhysicsVelocity>(enemy).Angular.y, Is.EqualTo(faces || launched ? 0f : 2f));

            // A moving player must update facing; no delta-time-dependent turn lag.
            em.SetComponentData(player, new PlayerSnapshot { Position = new float3(2f, -4f, -7f), IsAvailable = true });
            system.Update(world.Unmanaged);
            em.CompleteAllTrackedJobs();
            Assert.That(math.distance(math.forward(em.GetComponentData<LocalTransform>(enemy).Rotation),
                faces ? new float3(0f, 0f, -1f) : launched ? launchedForward : math.forward()), Is.LessThan(0.0001f));
            if (launched)
            {
                em.SetComponentData(player, new PlayerSnapshot { IsAvailable = false });
                velocity.Linear = new float3(3f, -2f, -4f);
                em.SetComponentData(enemy, velocity);
                system.Update(world.Unmanaged);
                em.CompleteAllTrackedJobs();
                Assert.That(math.distance(math.forward(em.GetComponentData<LocalTransform>(enemy).Rotation),
                    new float3(0.6f, 0f, -0.8f)), Is.LessThan(0.0001f));
                Assert.That(em.GetComponentData<PhysicsVelocity>(enemy).Linear, Is.EqualTo(velocity.Linear));
            }
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

        [Test]
        public void Info004ChargingBaselineFacesResolvedVelocityWithoutSteeringIt()
        {
            using var world = new World("Baseline charge facing");
            var em = world.EntityManager;
            var player = em.CreateEntity(typeof(PlayerSnapshot));
            em.SetComponentData(player, new PlayerSnapshot { Position = new float3(10, 0, 0), IsAvailable = true });
            var enemy = em.CreateEntity(typeof(Enemy), typeof(LocalTransform), typeof(PhysicsVelocity),
                typeof(EnemyLaunchState), typeof(RespawnRequest), typeof(EnemyContactAttemptState), typeof(DesiredMovement), typeof(EnemyArchetype));
            em.SetComponentEnabled<RespawnRequest>(enemy, false);
            em.SetComponentData(enemy, LocalTransform.Identity);
            em.SetComponentData(enemy, new EnemyArchetype { Value = EnemyArchetypeKind.Baseline });
            em.SetComponentData(enemy, new EnemyContactAttemptState { IsAttempting = 1, CommittedDirection = math.forward() });
            em.SetComponentData(enemy, new DesiredMovement { Direction = math.forward(), Speed = 4 });
            var system = world.GetOrCreateSystem<EnemyFacingSystem>();
            foreach (var direction in new[] { new float3(-3, 2, 1), new float3(3, 0, -4) })
            {
                em.SetComponentData(enemy, new PhysicsVelocity { Linear = direction });
                system.Update(world.Unmanaged);
                em.CompleteAllTrackedJobs();
                Assert.That(math.distance(math.forward(em.GetComponentData<LocalTransform>(enemy).Rotation),
                    math.normalize(new float3(direction.x, 0, direction.z))), Is.LessThan(.0001f));
                Assert.That(em.GetComponentData<PhysicsVelocity>(enemy).Linear, Is.EqualTo(direction));
                Assert.That(em.GetComponentData<EnemyContactAttemptState>(enemy).CommittedDirection, Is.EqualTo(math.forward()));
                em.SetComponentData(player, new PlayerSnapshot { Position = new float3(-10, 0, 0), IsAvailable = false });
            }
            em.SetComponentData(enemy, new EnemyContactAttemptState());
            em.SetComponentData(player, new PlayerSnapshot { Position = new float3(-10, 0, 0), IsAvailable = true });
            system.Update(world.Unmanaged);
            em.CompleteAllTrackedJobs();
            Assert.That(math.forward(em.GetComponentData<LocalTransform>(enemy).Rotation).x, Is.EqualTo(-1).Within(.0001f));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void Info004StationaryChargeFallsBackToMovementThenCommittedDirection(bool moving)
        {
            using var world = new World("Baseline charge initial facing");
            var em = world.EntityManager;
            em.AddComponentData(em.CreateEntity(), new PlayerSnapshot { Position = new float3(10, 0, 0), IsAvailable = true });
            em.AddComponent<ProtectedPoint>(em.CreateEntity());
            var enemy = em.CreateEntity(typeof(Enemy), typeof(LocalTransform), typeof(PhysicsVelocity),
                typeof(EnemyLaunchState), typeof(RespawnRequest), typeof(EnemyContactAttemptState), typeof(DesiredMovement), typeof(EnemyArchetype));
            em.SetComponentEnabled<RespawnRequest>(enemy, false);
            em.SetComponentData(enemy, LocalTransform.Identity);
            em.SetComponentData(enemy, new EnemyArchetype { Value = EnemyArchetypeKind.Baseline });
            em.SetComponentData(enemy, new EnemyContactAttemptState { IsAttempting = 1, CommittedDirection = new float3(0, 0, -1) });
            em.SetComponentData(enemy, new DesiredMovement { Direction = math.forward(), Speed = moving ? 4 : 0 });
            world.GetOrCreateSystem<EnemyFacingSystem>().Update(world.Unmanaged);
            em.CompleteAllTrackedJobs();
            Assert.That(math.distance(math.forward(em.GetComponentData<LocalTransform>(enemy).Rotation),
                new float3(0, 0, moving ? 1 : -1)), Is.LessThan(.0001f));
        }

        [Test]
        public void Enemy005DashingDasherFacesCommittedDashDirection()
        {
            using var world = new World("Dasher committed facing test");
            EntityManager em = world.EntityManager;
            Entity player = em.CreateEntity(typeof(PlayerSnapshot));
            em.SetComponentData(player, new PlayerSnapshot
            {
                Position = new float3(10f, 0f, 0f),
                IsAvailable = true
            });
            Entity dasher = em.CreateEntity(typeof(Enemy), typeof(LocalTransform), typeof(PhysicsVelocity),
                typeof(EnemyLaunchState), typeof(DasherState), typeof(RespawnRequest));
            em.SetComponentEnabled<RespawnRequest>(dasher, false);
            em.SetComponentData(dasher, LocalTransform.Identity);
            em.SetComponentData(dasher, new EnemyLaunchState { Phase = EnemyLaunchPhase.Active });
            em.SetComponentData(dasher, new DasherState
            {
                Phase = DasherPhase.Dashing,
                LockedDirection = new float3(0f, 0f, -1f)
            });

            world.GetOrCreateSystem<EnemyFacingSystem>().Update(world.Unmanaged);
            em.CompleteAllTrackedJobs();

            float3 forward = math.forward(em.GetComponentData<LocalTransform>(dasher).Rotation);
            Assert.That(math.distance(forward, new float3(0f, 0f, -1f)), Is.LessThan(0.0001f));
        }
    }
}
