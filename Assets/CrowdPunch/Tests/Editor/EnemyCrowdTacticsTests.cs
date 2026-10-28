using CrowdPunch.Components;
using CrowdPunch.Configuration;
using CrowdPunch.Systems.AI;
using NUnit.Framework;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEditor;
using Unity.Collections;
using CrowdPunch.Utilities;
using EnemyArchetype = CrowdPunch.Components.EnemyArchetype;

namespace CrowdPunch.Tests
{
    public sealed class EnemyCrowdTacticsTests
    {
        private World world;
        private EntityManager em;
        private Entity player, tuning;
        private static readonly ArenaBounds Arena = new ArenaBounds { Extents = new float3(50, 8, 50) };

        [SetUp] public void Setup()
        {
            world = new World("COMBAT-016 grouped crowd tactics"); em = world.EntityManager;
            player = em.CreateEntity(typeof(PlayerSnapshot));
            em.SetComponentData(player, new PlayerSnapshot { IsAvailable = true, Forward = math.forward(), Velocity = new float3(0, 0, 12) });
            tuning = em.CreateEntity(typeof(EnemyCrowdPressureSettings));
            em.SetComponentData(tuning, new EnemyCrowdPressureSettings
            {
                PursuitShare = .6f, EngagementRadius = 35, DisengagementMargin = 10,
                PredictionSeconds = 1.25f, MaximumPredictionDistance = 16, FlankDistance = 10,
                CommitmentSeconds = 4, CloseInSeconds = 2, GroupRadius = 4, ReserveGroupCount = 6,
                MinimumInterceptionGroupSize = 4
            });
            em.SetComponentData(em.CreateEntity(typeof(ArenaBounds)), Arena);
        }
        [TearDown] public void Cleanup() => world.Dispose();

        private Entity Enemy(float3 position, EnemyArchetypeKind kind = EnemyArchetypeKind.Baseline)
        {
            var enemy = em.CreateEntity(typeof(Enemy), typeof(LocalTransform), typeof(RespawnRequest),
                typeof(EnemyLaunchState), typeof(EnemyArchetype), typeof(EnemyContactAttemptState),
                typeof(EnemyCrowdAssignment), typeof(DesiredMovement), typeof(NavigationIntent),
                typeof(WanderDestination), typeof(EnemyMovementSettings), typeof(EnemyContactDamageSettings),
                typeof(EnemySeparationDistance), typeof(EnemyArchetypeSeparationDistances));
            em.SetComponentEnabled<RespawnRequest>(enemy, false);
            em.SetComponentData(enemy, LocalTransform.FromPosition(position));
            em.SetComponentData(enemy, new EnemyArchetype { Value = kind });
            em.SetComponentData(enemy, new EnemyMovementSettings { MoveSpeed = 8, ChargeSpeedMultiplier = 1.25f,
                ChargeDistance = 100, SurroundDistance = 6, SurroundRingSpacing = .75f, WanderSpeed = 1.5f });
            em.SetComponentData(enemy, new EnemyContactAttemptState { SecondsRemaining = 10 });
            em.SetComponentData(enemy, new EnemyContactDamageSettings { AttemptDistance = 10, AttemptWindUpDuration = .65f,
                AttemptDuration = 1.1f, AttemptSpeedMultiplier = 1.25f });
            return enemy;
        }
        private Entity[] Crowd(int count)
        {
            var result = new Entity[count];
            for (int i = 0; i < count; i++)
            {
                float angle = i * 2.3999631f;
                result[i] = Enemy(new float3(math.cos(angle) * (10 + i * .25f), 0, math.sin(angle) * (10 + i * .25f)));
            }
            return result;
        }
        private void Tick(double time)
        {
            world.SetTime(new TimeData(time, .02f));
            world.GetOrCreateSystem<EnemyCrowdTacticsSystem>().Update(world.Unmanaged);
            world.GetOrCreateSystem<EnemyChaseSystem>().Update(world.Unmanaged);
            em.CompleteAllTrackedJobs();
        }
        private EnemyCrowdAssignment Order(Entity enemy) => em.GetComponentData<EnemyCrowdAssignment>(enemy);
        private Entity Interceptor(Entity[] crowd)
        {
            foreach (var enemy in crowd) if (Order(enemy).Role == EnemyCrowdRole.Interception) return enemy;
            Assert.Fail("No interception group created"); return Entity.Null;
        }

        [Test] public void Combat016_DefaultAssetExposesBothModesAndSixtyFortySplit()
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameRuntimeSettings>("Assets/CrowdPunch/Data/Settings/GameRuntimeSettings.asset");
            Assert.That(asset.CrowdPressure.Participation, Is.EqualTo(CrowdParticipation.Regional));
            Assert.That(asset.CrowdPressure.PursuitShare, Is.EqualTo(.6f));
            Assert.That(asset.CrowdPressure.EngagementRadius, Is.EqualTo(45f));
            Assert.That(System.Enum.GetValues(typeof(CrowdParticipation)).Length, Is.EqualTo(2));
        }

        [Test] public void Combat016_FiftyParticipantsFormThirtyPursuersAndTwoTenBodyInterceptionGroups()
        {
            var crowd = Crowd(50); Tick(0); int pursue = 0, left = 0, right = 0;
            foreach (var enemy in crowd)
            {
                var order = Order(enemy);
                if (order.Role == EnemyCrowdRole.Pursuit) pursue++;
                else if (order.InterceptionDestination.x < 0) left++;
                else right++;
                Assert.That(em.GetComponentData<EnemyContactAttemptState>(enemy).IsAttempting, Is.Zero);
            }
            Assert.That(pursue, Is.EqualTo(30)); Assert.That(left, Is.EqualTo(10)); Assert.That(right, Is.EqualTo(10));
        }

        [Test] public void Combat016_ChangingPlayerDirectionCannotRetargetCommittedInterception()
        {
            var crowd = Crowd(50); Tick(0); var enemy = Interceptor(crowd); var before = Order(enemy);
            em.SetComponentData(player, new PlayerSnapshot { IsAvailable = true, Position = new float3(8, 0, -10),
                Forward = -math.forward(), Velocity = new float3(0, 0, -12) });
            Tick(1); var after = Order(enemy);
            Assert.That(after.Role, Is.EqualTo(EnemyCrowdRole.Interception));
            Assert.That(after.Destination, Is.EqualTo(before.Destination));
            Assert.That(after.InterceptionDestination, Is.EqualTo(before.InterceptionDestination));
            Assert.That(after.InterceptUntil, Is.EqualTo(before.InterceptUntil));
        }

        [Test] public void Combat016_ArrivalClosesInAndTimeoutDoesNotImmediatelyRebuildFlanks()
        {
            var crowd = Crowd(50); Tick(0); var enemy = Interceptor(crowd);
            // A flank waypoint, when present, leads to the same frozen interception destination.
            em.SetComponentData(enemy, LocalTransform.FromPosition(Order(enemy).Destination)); Tick(.5);
            em.SetComponentData(enemy, LocalTransform.FromPosition(Order(enemy).InterceptionDestination)); Tick(1);
            Assert.That(Order(enemy).Role, Is.EqualTo(EnemyCrowdRole.Pursuit));
            Tick(4.1);
            foreach (var body in crowd) Assert.That(Order(body).Role, Is.EqualTo(EnemyCrowdRole.Pursuit));
            Tick(5.9);
            foreach (var body in crowd) Assert.That(Order(body).Role, Is.EqualTo(EnemyCrowdRole.Pursuit));
            Tick(6.1); Assert.That(Order(Interceptor(crowd)).Role, Is.EqualTo(EnemyCrowdRole.Interception));
        }

        [Test] public void Combat016_RegionalHasHysteresisAndWholeArenaIncludesDistantBodies()
        {
            var near = Enemy(new float3(34, 0, 0)); var far = Enemy(new float3(-49, 0, -49)); Tick(0);
            Assert.That(Order(near).Role, Is.EqualTo(EnemyCrowdRole.Pursuit));
            Assert.That(Order(far).Role, Is.EqualTo(EnemyCrowdRole.Reserve));
            em.SetComponentData(near, LocalTransform.FromPosition(new float3(42, 0, 0))); Tick(6.1);
            Assert.That(Order(near).Role, Is.EqualTo(EnemyCrowdRole.Pursuit));
            em.SetComponentData(near, LocalTransform.FromPosition(new float3(46, 0, 0))); Tick(12.2);
            Assert.That(Order(near).Role, Is.EqualTo(EnemyCrowdRole.Reserve));
            var settings = em.GetComponentData<EnemyCrowdPressureSettings>(tuning);
            settings.Participation = CrowdParticipation.WholeArena; em.SetComponentData(tuning, settings); Tick(12.3);
            Assert.That(Order(near).Role, Is.EqualTo(EnemyCrowdRole.Pursuit));
            Assert.That(Order(far).Role, Is.EqualTo(EnemyCrowdRole.Pursuit));
        }

        [TestCase(1)] [TestCase(5)] [TestCase(12)]
        public void Combat016_SmallPopulationsKeepOneUsefulPursuingCrowd(int count)
        {
            var crowd = Crowd(count); Tick(0);
            foreach (var enemy in crowd) Assert.That(Order(enemy).Role, Is.EqualTo(EnemyCrowdRole.Pursuit));
        }

        [TestCase(EnemyLaunchPhase.Launched)] [TestCase(EnemyLaunchPhase.Recovering)] [TestCase(EnemyLaunchPhase.Defeated)]
        public void Combat016_LifecycleInvalidatesOrdersAndDoesNotRefillOpenedFlank(EnemyLaunchPhase phase)
        {
            var crowd = Crowd(50); Tick(0); var enemy = Interceptor(crowd);
            em.SetComponentData(enemy, new EnemyLaunchState { Phase = phase }); Tick(1);
            Assert.That(Order(enemy).Initialized, Is.Zero);
            Assert.That(em.GetComponentData<DesiredMovement>(enemy).Speed, Is.Zero);
            em.SetComponentData(enemy, new EnemyLaunchState { Phase = EnemyLaunchPhase.Active }); Tick(2);
            Assert.That(Order(enemy).Role, Is.EqualTo(EnemyCrowdRole.Reserve));
            Assert.That(Order(enemy).InterceptUntil, Is.Zero);
        }

        [Test] public void Combat016_PooledBodiesAndOtherArchetypesDoNotJoinBaselineGroups()
        {
            var pooled = Enemy(float3.zero); em.SetComponentEnabled<RespawnRequest>(pooled, true);
            var ranged = Enemy(float3.zero, EnemyArchetypeKind.Ranged);
            var snake = Enemy(float3.zero); em.AddComponent<SnakeHead>(snake);
            Tick(0);
            foreach (var enemy in new[] { pooled, ranged, snake }) Assert.That(Order(enemy).Initialized, Is.Zero);
        }

        [Test] public void Combat016_ReserveNeighborsGatherInsteadOfReceivingUnrelatedMapSlots()
        {
            var crowd = new Entity[24];
            for (int i = 0; i < crowd.Length; i++) crowd[i] = Enemy(new float3(-46 + i % 3, 0, -45));
            Tick(0); float3 first = Order(crowd[0]).Destination;
            foreach (var enemy in crowd)
            {
                Assert.That(Order(enemy).Role, Is.EqualTo(EnemyCrowdRole.Reserve));
                Assert.That(math.distance(first.xz, Order(enemy).Destination.xz), Is.LessThanOrEqualTo(8.01f));
                Assert.That(em.GetComponentData<NavigationIntent>(enemy).Kind, Is.EqualTo(NavigationGoalKind.Coverage));
            }
        }

        [Test] public void Combat016_PredictionIsBoundedAtDashSpeedAndTargetsStayInsideArena()
        {
            var snapshot = em.GetComponentData<PlayerSnapshot>(player); snapshot.Position = new float3(49, 0, 49);
            snapshot.Velocity = new float3(100, 0, 0);
            var settings = em.GetComponentData<EnemyCrowdPressureSettings>(tuning);
            EnemyCrowdFormation.InterceptionTargets(1, 1, snapshot, settings, Arena, out var flank, out var goal);
            foreach (var point in new[] { flank, goal })
            { Assert.That(point.x, Is.InRange(-49.25f, 49.25f)); Assert.That(point.z, Is.InRange(-49.25f, 49.25f)); }
            snapshot.Position = float3.zero; settings.GroupRadius = 0; settings.FlankDistance = 0;
            EnemyCrowdFormation.InterceptionTargets(1, 1, snapshot, settings, Arena, out _, out goal);
            Assert.That(goal.x, Is.EqualTo(16).Within(.001f));
        }

        [Test] public void Combat016_CommittedContactAttackSurvivesAPlanningBoundary()
        {
            var crowd = Crowd(50); Tick(0); var enemy = Interceptor(crowd);
            em.SetComponentData(enemy, new EnemyContactAttemptState { IsAttempting = 1, SecondsRemaining = 10,
                CommittedDirection = -math.forward() });
            Tick(6.1);
            Assert.That(Order(enemy).Role, Is.EqualTo(EnemyCrowdRole.Pursuit));
            Assert.That(em.GetComponentData<EnemyContactAttemptState>(enemy).IsAttempting, Is.EqualTo(1));
            Assert.That(em.GetComponentData<NavigationIntent>(enemy).Mode, Is.EqualTo(NavigationMode.Committed));
        }

        [Test] public void Combat016_InterceptionUsesObstacleNavigationWithoutMovingPhysicsTransforms()
        {
            var crowd = Crowd(50); Tick(0); var enemy = Interceptor(crowd);
            float3 start = em.GetComponentData<LocalTransform>(enemy).Position;
            float2 midpoint = (start.xz + Order(enemy).Destination.xz) * .5f;
            using var obstacles = new NativeArray<NavigationRectangle>(new[] {
                new NavigationRectangle { Minimum = midpoint - 1.5f, Maximum = midpoint + 1.5f }
            }, Allocator.Temp);
            using var blob = NavigationGridConstruction.Build(new float2(-50), new float2(50), 1,
                new float3(.2f, .6f, 1.2f), obstacles, Allocator.Persistent);
            var grid = em.CreateEntity(typeof(NavigationGrid), typeof(NavigationRuntimeSettings), typeof(NavigationDiagnostics));
            em.SetComponentData(grid, new NavigationGrid { Data = blob, ParticipationAnchor = start.xz });
            em.SetComponentData(grid, new NavigationRuntimeSettings { Enabled = 1, DirectInterval = .2f,
                WaypointArrival = .2f, LookAhead = .5f, DestinationThreshold = 1, RepathCooldown = .2f,
                StuckDuration = 100, MinimumProgress = .2f, FailureDelay = 3, SmoothingChecks = 4,
                GlobalBudget = 4000, SearchLimit = 10000, ConcurrentSearches = 1, MaxPathLength = 256, MaxRequests = 16 });
            em.AddComponentData(enemy, new NavigationAgent { Radius = .2f });
            em.AddComponent<NavigationPathState>(enemy); em.AddBuffer<NavigationWaypoint>(enemy);
            var nav = world.GetOrCreateSystem<EnemyNavigationSystem>();
            for (int i = 0; i < 10; i++)
            {
                world.SetTime(new TimeData(i * .02, .02f)); nav.Update(world.Unmanaged); em.CompleteAllTrackedJobs();
            }
            var path = em.GetComponentData<NavigationPathState>(enemy);
            Assert.That(path.TravelState, Is.EqualTo(NavigationTravelState.Path));
            Assert.That(em.GetBuffer<NavigationWaypoint>(enemy).Length, Is.GreaterThan(0));
            Assert.That(em.GetComponentData<DesiredMovement>(enemy).Speed, Is.GreaterThan(0));
            Assert.That(em.GetComponentData<LocalTransform>(enemy).Position, Is.EqualTo(start));
        }
    }
}
