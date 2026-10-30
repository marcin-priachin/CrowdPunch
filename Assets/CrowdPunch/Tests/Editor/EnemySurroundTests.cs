using System.Collections.Generic;
using CrowdPunch.Components;
using CrowdPunch.Systems.AI;
using CrowdPunch.Utilities;
using NUnit.Framework;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace CrowdPunch.Tests
{
    public sealed class EnemySurroundTests
    {
        private World world;
        private EntityManager em;
        private Entity player, arena;
        [SetUp] public void Setup()
        {
            world = new World("COMBAT-016 occupancy-aware surround"); em = world.EntityManager;
            player = em.CreateEntity(typeof(PlayerSnapshot));
            em.SetComponentData(player, new PlayerSnapshot { IsAvailable = true, Radius = .5f, Forward = math.forward() });
            arena = em.CreateEntity(typeof(ArenaBounds));
            em.SetComponentData(arena, new ArenaBounds { Extents = new float3(50, 8, 50) });
            em.SetComponentData(em.CreateEntity(typeof(EnemyCrowdPressureSettings)), new EnemyCrowdPressureSettings
                { SurroundMovementRefreshDistance = 1.25f, SurroundReassignmentSeconds = .5f });
        }
        [TearDown] public void Cleanup() => world.Dispose();
        private Entity Enemy(float3 position)
        {
            var e = em.CreateEntity(typeof(Enemy), typeof(LocalTransform), typeof(EnemyArchetype), typeof(EnemyLaunchState),
                typeof(RespawnRequest), typeof(EnemyCrowdAssignment), typeof(EnemySurroundAssignment),
                typeof(EnemyMovementSettings), typeof(EnemyContactAttemptState), typeof(EnemyContactDamageSettings),
                typeof(EnemySeparationDistance), typeof(EnemyArchetypeSeparationDistances), typeof(DesiredMovement),
                typeof(NavigationIntent), typeof(WanderDestination), typeof(NavigationAgent));
            em.SetComponentEnabled<RespawnRequest>(e, false);
            em.SetComponentData(e, LocalTransform.FromPosition(position));
            em.SetComponentData(e, new NavigationAgent { Radius = .5f });
            em.SetComponentData(e, new EnemyCrowdAssignment { Role = EnemyCrowdRole.Pursuit, Initialized = 1 });
            em.SetComponentData(e, new EnemyMovementSettings { MoveSpeed = 8, ChargeSpeedMultiplier = 1.25f,
                SurroundDistance = 4.5f, SurroundRingSpacing = 3, SeparationDistanceMin = 3, WanderSpeed = 1.5f });
            em.SetComponentData(e, new EnemyContactDamageSettings { AttemptDistance = 10, AttemptDuration = 1.1f,
                AttemptWindUpDuration = .65f, AttemptSpeedMultiplier = 1.5f, AttemptIntervalMin = .1f, AttemptIntervalMax = .5f });
            em.SetComponentData(e, new EnemyContactAttemptState { SecondsRemaining = 10 });
            return e;
        }
        private Entity[] Queue(int count)
        {
            var result = new Entity[count];
            for (int i = 0; i < count; i++) result[i] = Enemy(new float3(0, 0, 8 + i * 1.1f));
            return result;
        }
        private void Tick(double time)
        {
            world.SetTime(new TimeData(time, .02f));
            world.GetOrCreateSystem<EnemySurroundSystem>().Update(world.Unmanaged);
            world.GetOrCreateSystem<EnemyChaseSystem>().Update(world.Unmanaged);
            em.CompleteAllTrackedJobs();
        }
        private EnemySurroundAssignment Order(Entity e) => em.GetComponentData<EnemySurroundAssignment>(e);

        [Test] public void Combat016_OneSidedQueueReservesDistinctPositionsAllAroundPlayer()
        {
            var crowd = Queue(30); Tick(0); int inner = 0; bool left = false, right = false, behind = false;
            var used = new HashSet<int>();
            foreach (var e in crowd)
            {
                var order = Order(e); Assert.That(order.Assigned, Is.EqualTo(1));
                Assert.That(order.Slot, Is.GreaterThanOrEqualTo(0)); Assert.That(used.Add(order.Slot), Is.True);
                if (math.length(order.Offset) < 5)
                {
                    inner++; left |= order.Offset.x < -2; right |= order.Offset.x > 2; behind |= order.Offset.y < -2;
                }
            }
            Assert.That(inner, Is.EqualTo(9)); Assert.That(left && right && behind, Is.True);
        }

        [Test] public void Combat016_BlockedRearBodyChoosesFreeSideAndWalksAroundCrowdCentre()
        {
            var front = Enemy(new float3(0, 0, 4.5f)); var rear = Enemy(new float3(0, 0, 8)); Tick(0);
            Assert.That(math.distance(Order(front).Offset, new float2(0, 4.5f)), Is.LessThan(.01f));
            Assert.That(Order(rear).Offset.y, Is.LessThan(-4));
            var route = em.GetComponentData<NavigationIntent>(rear);
            Assert.That(math.abs(route.Destination.x), Is.GreaterThan(1));
            Assert.That(route.Destination.z, Is.GreaterThan(0), "Use an arc waypoint instead of crossing the player.");
            Assert.That(em.GetComponentData<EnemyContactAttemptState>(rear).IsWindingUp, Is.Zero);
        }

        [TestCase(3)] [TestCase(5)]
        public void Combat016_SmallCrowdUsesEvenAngularSpacingInsteadOfAdjacentSlots(int count)
        {
            var crowd = Queue(count); Tick(0); float2 sum = float2.zero;
            foreach (var e in crowd) { Assert.That(math.length(Order(e).Offset), Is.EqualTo(4.5f).Within(.001f)); sum += Order(e).Offset; }
            Assert.That(math.length(sum), Is.LessThan(.01f));
        }

        [Test] public void Combat016_ValidReservationsStayStableAndFollowPlayerTranslation()
        {
            var crowd = Queue(12); Tick(0); var before = new EnemySurroundAssignment[crowd.Length];
            for (int i = 0; i < crowd.Length; i++) before[i] = Order(crowd[i]);
            Tick(.6);
            for (int i = 0; i < crowd.Length; i++) Assert.That(Order(crowd[i]).Offset, Is.EqualTo(before[i].Offset));
            em.SetComponentData(player, new PlayerSnapshot { IsAvailable = true, Radius = .5f, Position = new float3(.2f, 0, .1f) });
            Tick(.61);
            var first = em.GetComponentData<NavigationIntent>(crowd[0]);
            Assert.That(first.Destination.x, Is.EqualTo(before[0].Offset.x + .2f).Within(.001f));
            Assert.That(first.Destination.z, Is.EqualTo(before[0].Offset.y + .1f).Within(.001f));
        }

        [Test] public void Combat016_InRangeBodyCanWindUpBeforeReachingItsSurroundPosition()
        {
            var e = Enemy(new float3(0, 0, 8));
            em.SetComponentData(e, new EnemyContactAttemptState()); Tick(0);
            Assert.That(math.distance(new float2(0, 8), Order(e).Offset), Is.GreaterThan(1.25f));
            Assert.That(em.GetComponentData<EnemyContactAttemptState>(e).IsWindingUp, Is.EqualTo(1));
            Assert.That(em.GetComponentData<EnemyContactAttemptState>(e).SecondsRemaining, Is.EqualTo(.65f));
            for (int step = 1; step <= 34; step++) Tick(step * .02);
            Assert.That(em.GetComponentData<EnemyContactAttemptState>(e).IsAttempting, Is.EqualTo(1));
            Assert.That(em.GetComponentData<NavigationIntent>(e).Mode, Is.EqualTo(NavigationMode.Committed));
        }

        [TestCase(false)] [TestCase(true)]
        public void Combat016_BlockedInRangeBodyCanBeginContactAttemptInOpenArenaOrCorner(bool corner)
        {
            float3 focus = corner ? new float3(48, 0, 48) : float3.zero;
            em.SetComponentData(player, new PlayerSnapshot { IsAvailable = true, Radius = .5f, Position = focus });
            var actor = Enemy(focus + new float3(0, 0, -8));
            var blocker = Enemy(focus + new float3(0, 0, -3));
            em.SetComponentData(blocker, new EnemyCrowdAssignment { Initialized = 1, Role = EnemyCrowdRole.Reserve });
            em.SetComponentData(actor, new EnemyContactAttemptState()); Tick(0);
            Assert.That(em.GetComponentData<EnemyContactAttemptState>(actor).IsWindingUp, Is.EqualTo(1));
            for (int step = 1; step <= 34; step++) Tick(step * .02);
            Assert.That(em.GetComponentData<EnemyContactAttemptState>(actor).IsAttempting, Is.EqualTo(1));
            Assert.That(em.GetComponentData<EnemyContactAttemptState>(actor).CommittedDirection, Is.EqualTo(math.forward()));
        }

        [Test] public void Combat016_RelaxedContactStillRequiresRangeAndCooldown()
        {
            var far = Enemy(new float3(0, 0, 11)); var cooling = Enemy(new float3(0, 0, 7));
            em.SetComponentData(far, new EnemyContactAttemptState());
            em.SetComponentData(cooling, new EnemyContactAttemptState { SecondsRemaining = 1 }); Tick(0);
            Assert.That(em.GetComponentData<EnemyContactAttemptState>(far).IsWindingUp, Is.Zero);
            Assert.That(em.GetComponentData<EnemyContactAttemptState>(cooling).IsWindingUp, Is.Zero);
            em.SetComponentData(cooling, new EnemyContactAttemptState()); Tick(.02);
            Assert.That(em.GetComponentData<EnemyContactAttemptState>(cooling).IsWindingUp, Is.EqualTo(1));
        }

        [Test] public void Combat016_OccupiedApproachRedirectsAnArrivedBodyToAnotherOpenAngle()
        {
            var actor = Enemy(new float3(0, 0, 4.5f)); Tick(0);
            var blocker = Enemy(new float3(0, 0, 2));
            em.SetComponentData(blocker, new EnemyCrowdAssignment { Initialized = 1, Role = EnemyCrowdRole.Reserve });
            Tick(.6);
            Assert.That(math.abs(Order(actor).Offset.x), Is.GreaterThan(2), "Seek a clear angle instead of waiting behind the blocker.");
            Assert.That(em.GetComponentData<EnemyContactAttemptState>(actor).IsWindingUp, Is.Zero);
        }

        [Test] public void Combat016_LaunchReleasesReservationAndAnOuterBodyCanTakeTheOpening()
        {
            var crowd = Queue(30); Tick(0);
            foreach (var e in crowd)
            {
                var p = Order(e).Offset; em.SetComponentData(e, LocalTransform.FromPosition(new float3(p.x, 0, p.y)));
                em.SetComponentData(e, new EnemyContactAttemptState { SecondsRemaining = 10 });
            }
            var removed = crowd[0]; float2 opened = Order(removed).Offset;
            em.SetComponentData(removed, new EnemyLaunchState { Phase = EnemyLaunchPhase.Launched }); Tick(.6);
            Assert.That(Order(removed).Assigned, Is.Zero);
            bool filled = false;
            foreach (var e in crowd) if (e != removed && Order(e).Assigned != 0 && math.distancesq(Order(e).Offset, opened) < .01f) filled = true;
            Assert.That(filled, Is.True);
        }

        [TestCase(EnemyLaunchPhase.Launched)] [TestCase(EnemyLaunchPhase.Recovering)] [TestCase(EnemyLaunchPhase.Defeated)]
        public void Combat016_LifecycleInvalidatesSurroundWithoutSteeringTheBody(EnemyLaunchPhase phase)
        {
            var e = Enemy(new float3(0, 0, 8)); Tick(0);
            em.SetComponentData(e, new EnemyLaunchState { Phase = phase }); Tick(.1);
            Assert.That(Order(e).Assigned, Is.Zero); Assert.That(em.GetComponentData<DesiredMovement>(e).Speed, Is.Zero);
            Assert.That(em.GetComponentData<LocalTransform>(e).Position, Is.EqualTo(new float3(0, 0, 8)));
        }

        [Test] public void Combat016_InterceptionAndReserveOrdersDoNotAcquireSurroundPositions()
        {
            var e = Enemy(new float3(0, 0, 8)); Tick(0);
            em.SetComponentData(e, new EnemyCrowdAssignment { Initialized = 1, Role = EnemyCrowdRole.Interception,
                Destination = new float3(12, 0, 16) }); Tick(.1);
            Assert.That(Order(e).Assigned, Is.Zero);
            Assert.That(em.GetComponentData<NavigationIntent>(e).Destination, Is.EqualTo(new float3(12, 0, 16)));
        }

        [Test] public void Combat016_ExistingCommittedContactDirectionSurvivesCrowdRedistribution()
        {
            var crowd = Queue(20);
            em.SetComponentData(crowd[8], new EnemyContactAttemptState { IsAttempting = 1, SecondsRemaining = 1,
                CommittedDirection = new float3(1, 0, 0) }); Tick(0);
            Assert.That(em.GetComponentData<DesiredMovement>(crowd[8]).Direction, Is.EqualTo(new float3(1, 0, 0)));
            Assert.That(em.GetComponentData<NavigationIntent>(crowd[8]).Mode, Is.EqualTo(NavigationMode.Committed));
        }

        [Test] public void Combat016_ArenaEdgePositionsAreDistinctAndRemainInsideTerritory()
        {
            em.SetComponentData(arena, new ArenaBounds { Extents = new float3(10, 8, 10) });
            em.SetComponentData(player, new PlayerSnapshot { IsAvailable = true, Position = new float3(8, 0, 8), Radius = .5f });
            var crowd = new Entity[10];
            for (int i = 0; i < crowd.Length; i++) crowd[i] = Enemy(new float3(7 - i * .5f, 0, 6));
            Tick(0); var used = new HashSet<int>();
            foreach (var e in crowd)
            {
                var order = Order(e); Assert.That(order.Slot, Is.GreaterThanOrEqualTo(0));
                var p = new float2(8) + order.Offset;
                Assert.That(p.x, Is.InRange(-9.25f, 9.25f)); Assert.That(p.y, Is.InRange(-9.25f, 9.25f));
                Assert.That(used.Add(order.Slot), Is.True);
            }
        }

        [Test] public void Combat016_StaticObstaclesCannotOwnSurroundPositions()
        {
            using var obstacles = new NativeArray<NavigationRectangle>(new[] {
                new NavigationRectangle { Minimum = new float2(-2, 2), Maximum = new float2(2, 6) }
            }, Allocator.Temp);
            using var blob = NavigationGridConstruction.Build(new float2(-50), new float2(50), 1,
                new float3(.6f, 1, 2), obstacles, Allocator.Persistent);
            em.SetComponentData(em.CreateEntity(typeof(NavigationGrid)), new NavigationGrid { Data = blob });
            var crowd = Queue(20); Tick(0);
            foreach (var e in crowd)
            {
                var p = Order(e).Offset; Assert.That(Order(e).Slot, Is.GreaterThanOrEqualTo(0));
                Assert.That(NavigationGeometry.Anchor(ref blob.Value, p, 0), Is.GreaterThanOrEqualTo(0));
            }
        }
    }
}
