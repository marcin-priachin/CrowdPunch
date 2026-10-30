using System.Collections.Generic;
using CrowdPunch.Components;
using CrowdPunch.Systems.AI;
using NUnit.Framework;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace CrowdPunch.Tests
{
    public sealed class DirectContactPursuitTests
    {
        private World world;
        private EntityManager em;
        private Entity player, tuning;
        [SetUp] public void Setup()
        {
            world = new World("COMBAT-016 timed direct contact pressure"); em = world.EntityManager;
            player = em.CreateEntity(typeof(PlayerSnapshot));
            em.SetComponentData(player, new PlayerSnapshot { IsAvailable = true, Radius = .5f, Forward = math.forward() });
            tuning = em.CreateEntity(typeof(EnemyCrowdPressureSettings)); Configure(2, 4);
            em.SetComponentData(em.CreateEntity(typeof(ArenaBounds)), new ArenaBounds { Extents = new float3(100, 8, 100) });
        }
        [TearDown] public void Cleanup() => world.Dispose();
        private void Configure(int minimum, int maximum, float seconds = 3) => em.SetComponentData(tuning,
            new EnemyCrowdPressureSettings { MinimumDirectContactPursuers = minimum, MaximumDirectContactPursuers = maximum,
                DirectContactPursuerResampleSeconds = seconds });
        private Entity Enemy(float z, EnemyCrowdRole role = EnemyCrowdRole.Pursuit)
        {
            var e = em.CreateEntity(typeof(Enemy), typeof(DirectContactPursuit), typeof(LocalTransform), typeof(EnemyArchetype),
                typeof(EnemyLaunchState), typeof(RespawnRequest), typeof(EnemyCrowdAssignment), typeof(EnemySurroundAssignment),
                typeof(EnemyMovementSettings), typeof(EnemyContactAttemptState), typeof(EnemyContactDamageSettings),
                typeof(EnemySeparationDistance), typeof(EnemyArchetypeSeparationDistances), typeof(DesiredMovement),
                typeof(NavigationIntent), typeof(WanderDestination));
            em.SetComponentEnabled<RespawnRequest>(e, false); em.SetComponentEnabled<DirectContactPursuit>(e, false);
            em.SetComponentData(e, LocalTransform.FromPosition(new float3(0, 0, z)));
            em.SetComponentData(e, new EnemyCrowdAssignment { Initialized = 1, Role = role, Destination = new float3(30, 0, 30) });
            em.SetComponentData(e, new EnemyMovementSettings { MoveSpeed = 8, ChargeSpeedMultiplier = 1.25f, SurroundDistance = 4.5f,
                SurroundRingSpacing = 3, SeparationDistanceMin = 3, WanderSpeed = 1.5f });
            em.SetComponentData(e, new EnemyContactAttemptState { SecondsRemaining = 10 });
            em.SetComponentData(e, new EnemyContactDamageSettings { AttemptDistance = 10, AttemptWindUpDuration = .65f,
                AttemptDuration = 1.1f, AttemptSpeedMultiplier = 1.5f });
            return e;
        }
        private Entity[] Crowd(int count)
        { var result = new Entity[count]; for (int i = 0; i < count; i++) result[i] = Enemy(5 + i); return result; }
        private bool Selected(Entity e) => em.IsComponentEnabled<DirectContactPursuit>(e);
        private int Count()
        { using var q = em.CreateEntityQuery(typeof(DirectContactPursuit)); return q.CalculateEntityCount(); }
        private void Tick(double time)
        {
            world.SetTime(new TimeData(time, .02f));
            world.GetOrCreateSystem<DirectContactPursuitSystem>().Update(world.Unmanaged);
            world.GetOrCreateSystem<EnemySurroundSystem>().Update(world.Unmanaged);
            world.GetOrCreateSystem<EnemyChaseSystem>().Update(world.Unmanaged); em.CompleteAllTrackedJobs();
        }

        [Test] public void Combat016_CountIsSampledInclusivelyAndHeldUntilTheNextInterval()
        {
            Crowd(12); var observed = new HashSet<int>(); bool changed = false; int previous = -1;
            for (int period = 0; period < 40; period++)
            {
                double start = period * 3.01; Tick(start); int count = Count(); observed.Add(count);
                Assert.That(count, Is.InRange(2, 4));
                changed |= previous >= 0 && count != previous; previous = count;
                Tick(start + .1); Assert.That(Count(), Is.EqualTo(count));
                Tick(start + 2.99); Assert.That(Count(), Is.EqualTo(count));
            }
            Assert.That(changed, Is.True); Assert.That(observed.Contains(2) && observed.Contains(4), Is.True);
        }

        [Test] public void Combat016_CloserNewcomerReplacesFartherPursuerBeforeNextCountDraw()
        {
            Configure(2, 2); var crowd = Crowd(8); Tick(0);
            Assert.That(Selected(crowd[0]) && Selected(crowd[1]), Is.True);
            var closer = Enemy(1); Tick(1);
            Assert.That(Count(), Is.EqualTo(2)); Assert.That(Selected(closer), Is.True);
            Assert.That(Selected(crowd[0]), Is.True); Assert.That(Selected(crowd[1]), Is.False);
        }

        [Test] public void Combat016_PlayerMovementImmediatelySelectsTheClosestExistingBodies()
        {
            Configure(2, 2); var crowd = Crowd(8); Tick(0);
            em.SetComponentData(player, new PlayerSnapshot { IsAvailable = true, Position = new float3(0, 0, 12) }); Tick(.1);
            Assert.That(Count(), Is.EqualTo(2));
            Assert.That(Selected(crowd[7]) && Selected(crowd[6]), Is.True);
            Assert.That(Selected(crowd[0]) || Selected(crowd[1]), Is.False);
        }

        [Test] public void Combat016_EnemyMovementChangesMembershipWithDeterministicEqualDistanceTies()
        {
            Configure(1, 1); var first = Enemy(5); var second = Enemy(6); Tick(0);
            em.SetComponentData(second, LocalTransform.FromPosition(new float3(0, 0, 4))); Tick(.1);
            Assert.That(Selected(second), Is.True); Assert.That(Selected(first), Is.False);
            em.SetComponentData(second, LocalTransform.FromPosition(new float3(0, 0, 5))); Tick(.2);
            Assert.That(Selected(first), Is.True); Assert.That(Selected(second), Is.False);
            Tick(.3); Assert.That(Selected(first), Is.True);
        }

        [TestCase(EnemyLaunchPhase.Launched)] [TestCase(EnemyLaunchPhase.Recovering)] [TestCase(EnemyLaunchPhase.Defeated)]
        public void Combat016_IneligibleMemberIsReplacedWithoutRerollingTheCount(EnemyLaunchPhase phase)
        {
            var crowd = Crowd(12); Tick(0); int count = Count();
            var removed = crowd[0]; Assert.That(Selected(removed), Is.True);
            em.SetComponentData(removed, new EnemyLaunchState { Phase = phase }); Tick(.1);
            Assert.That(Selected(removed), Is.False); Assert.That(Count(), Is.EqualTo(count));
            for (int i = 1; i < count; i++) Assert.That(Selected(crowd[i]), Is.True);
        }

        [Test] public void Combat016_SampledCountIsClampedToAvailablePopulationAndVacanciesFillImmediately()
        {
            Configure(5, 5); Crowd(2); Tick(0); Assert.That(Count(), Is.EqualTo(2));
            var e = Enemy(8); Tick(.1); Assert.That(Count(), Is.EqualTo(3)); Assert.That(Selected(e), Is.True);
            em.SetComponentEnabled<RespawnRequest>(e, true); Tick(.2);
            Assert.That(Selected(e), Is.False); Assert.That(Count(), Is.EqualTo(2));
        }

        [TestCase(0, 0, 0)] [TestCase(-5, -1, 0)] [TestCase(3, 3, 3)]
        [TestCase(int.MaxValue, int.MaxValue, 8)]
        public void Combat016_DisabledFixedAndLargeRangesAreHandled(int minimum, int maximum, int expected)
        { Configure(minimum, maximum); Crowd(8); Tick(0); Assert.That(Count(), Is.EqualTo(expected)); }

        [Test] public void Combat016_ReversedRangeAndInvalidDurationAreNormalized()
        {
            Configure(4, 2, 0); Crowd(8);
            for (int i = 0; i < 20; i++) { Tick(i * .11); Assert.That(Count(), Is.InRange(2, 4)); }
        }

        [TestCase(EnemyCrowdRole.Pursuit)] [TestCase(EnemyCrowdRole.Reserve)] [TestCase(EnemyCrowdRole.Interception)]
        public void Combat016_SelectedBodyDirectlyTargetsPlayerBetweenAttempts(EnemyCrowdRole role)
        {
            Configure(1, 1); var e = Enemy(8, role); Tick(0);
            Assert.That(Selected(e), Is.True);
            Assert.That(em.GetComponentData<EnemySurroundAssignment>(e).Assigned, Is.Zero);
            Assert.That(em.GetComponentData<NavigationIntent>(e).Destination, Is.EqualTo(float3.zero));
            Assert.That(em.GetComponentData<DesiredMovement>(e).Speed, Is.GreaterThan(0));
            Assert.That(em.GetComponentData<EnemyContactAttemptState>(e).IsAttempting, Is.Zero);
            Assert.That(em.GetComponentData<EnemyContactAttemptState>(e).IsWindingUp, Is.Zero);
        }

        [Test] public void Combat016_OrdinaryWindupAndCommittedDirectionRemainAuthoritative()
        {
            Configure(1, 1); var e = Enemy(8); em.SetComponentData(e, new EnemyContactAttemptState()); Tick(0);
            Assert.That(em.GetComponentData<EnemyContactAttemptState>(e).IsWindingUp, Is.EqualTo(1));
            Assert.That(em.GetComponentData<DesiredMovement>(e).Speed, Is.Zero);
            for (int i = 1; i <= 34; i++) Tick(i * .02);
            Assert.That(em.GetComponentData<EnemyContactAttemptState>(e).IsAttempting, Is.EqualTo(1));
            float3 direction = em.GetComponentData<EnemyContactAttemptState>(e).CommittedDirection;
            Configure(0, 0); em.SetComponentData(player, new PlayerSnapshot { IsAvailable = true, Position = new float3(10, 0, 0) }); Tick(.7);
            Assert.That(Selected(e), Is.False);
            Assert.That(em.GetComponentData<NavigationIntent>(e).Mode, Is.EqualTo(NavigationMode.Committed));
            Assert.That(em.GetComponentData<EnemyContactAttemptState>(e).CommittedDirection, Is.EqualTo(direction));
        }

        [Test] public void Combat016_ProtectedPointAndUnavailablePlayerSuppressDirectSelection()
        {
            Configure(2, 2); Crowd(4); Tick(0); Assert.That(Count(), Is.EqualTo(2));
            em.SetComponentData(player, new PlayerSnapshot()); Tick(.1); Assert.That(Count(), Is.Zero);
            em.SetComponentData(player, new PlayerSnapshot { IsAvailable = true }); Tick(.2); Assert.That(Count(), Is.EqualTo(2));
            var objective = em.CreateEntity(typeof(ProtectedPoint)); em.AddBuffer<ProtectedPointAttacker>(objective);
            Tick(.3); Assert.That(Count(), Is.Zero);
        }

        [Test] public void Combat016_OtherArchetypesAndOffRoomBodiesDoNotConsumeTheCount()
        {
            Configure(2, 2); var ranged = Enemy(1); em.SetComponentData(ranged, new EnemyArchetype { Value = EnemyArchetypeKind.Ranged });
            var snake = Enemy(2); em.AddComponent<SnakeHead>(snake);
            var room = em.CreateEntity(typeof(BarricadeRoom));
            em.SetComponentData(room, new BarricadeRoom { Minimum = new float2(-10, 20), Maximum = new float2(10, 40) });
            var offRoom = Enemy(25); em.AddComponentData(offRoom, new EnemyWaveOwnership { Sequence = room });
            var valid = Crowd(3); Tick(0);
            Assert.That(Selected(ranged) || Selected(snake) || Selected(offRoom), Is.False);
            Assert.That(Count(), Is.EqualTo(2)); Assert.That(Selected(valid[0]) && Selected(valid[1]), Is.True);
        }
    }
}
