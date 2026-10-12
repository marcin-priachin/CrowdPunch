using CrowdPunch.Components;
using CrowdPunch.Systems.AI;
using CrowdPunch.Systems.Movement;
using CrowdPunch.Utilities;
using NUnit.Framework;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace CrowdPunch.Tests
{
    public sealed class BarricadeRoomMovementTests
    {
        private World world;
        private EntityManager em;
        private Entity player, owner;
        private static readonly BarricadeRoom Room = new BarricadeRoom
        { Minimum = new float2(-10, -20), Maximum = new float2(10, 10), Activated = 1 };

        [SetUp] public void Setup()
        {
            world = new World("BARRICADE-008 spawn-room movement"); em = world.EntityManager;
            player = em.CreateEntity(typeof(PlayerSnapshot));
            em.SetComponentData(player, new PlayerSnapshot { IsAvailable = true, Position = new float3(0, 0, 40) });
            em.SetComponentData(em.CreateEntity(typeof(ArenaBounds)), new ArenaBounds
            { Center = new float3(0, 0, 130), Extents = new float3(20, 8, 160) });
            em.SetComponentData(em.CreateEntity(typeof(EnemyCrowdPressureSettings)), new EnemyCrowdPressureSettings
            { MaximumApproachingEnemies = 1 });
            owner = em.CreateEntity(typeof(BarricadeRoom)); em.SetComponentData(owner, Room);
            world.SetTime(new TimeData(1, .02f));
        }
        [TearDown] public void Cleanup() => world.Dispose();
        private Entity Enemy(float3 position, Entity sequence, EnemyArchetypeKind kind = EnemyArchetypeKind.Baseline)
        {
            var e = em.CreateEntity(typeof(Enemy), typeof(LocalTransform), typeof(RespawnRequest),
                typeof(EnemyLaunchState), typeof(EnemyArchetype), typeof(EnemyContactAttemptState),
                typeof(DesiredMovement), typeof(NavigationIntent), typeof(WanderDestination),
                typeof(EnemyMovementSettings), typeof(EnemyContactDamageSettings), typeof(EnemySeparationDistance),
                typeof(EnemyArchetypeSeparationDistances), typeof(PhysicsVelocity), typeof(PhysicsMass), typeof(EnemyWaveOwnership));
            em.SetComponentEnabled<RespawnRequest>(e, false);
            em.SetComponentData(e, LocalTransform.FromPosition(position));
            em.SetComponentData(e, new EnemyWaveOwnership { Sequence = sequence });
            em.SetComponentData(e, new EnemyArchetype { Value = kind });
            em.SetComponentData(e, new EnemyMovementSettings { MoveSpeed = 5, WanderSpeed = 2, Acceleration = 20,
                BrakingAcceleration = 30, StoppingDistance = .5f, SurroundDistance = 4, ChargeSpeedMultiplier = 2 });
            em.SetComponentData(e, new EnemyContactDamageSettings { AttemptDistance = 3, AttemptDuration = 1,
                AttemptWindUpDuration = .2f, AttemptSpeedMultiplier = 2 });
            if (kind == EnemyArchetypeKind.Explosive) em.AddComponent<ExplosiveEnemyState>(e);
            return e;
        }
        private void Chase()
        { world.GetOrCreateSystem<EnemyChaseSystem>().Update(world.Unmanaged); em.CompleteAllTrackedJobs(); }
        private void Move()
        { world.GetOrCreateSystem<EnemyMovementSystem>().Update(world.Unmanaged); em.CompleteAllTrackedJobs(); }
        private static void Inside(float3 position, BarricadeRoom room)
        { Assert.That(BarricadeRoomMovement.Contains(room, position), Is.True, "Outside original spawn room: " + position); }

        [Test] public void Barricade008_ReserveCrowdDistributesAcrossItsOwnRoomAndReplacesStaleArenaTargets()
        {
            var enemies = new Entity[96];
            for (int i = 0; i < enemies.Length; i++)
            {
                enemies[i] = Enemy(new float3(0, 0, -5), owner);
                em.SetComponentData(enemies[i], new WanderDestination { IsAssigned = 1, Position = new float3(0, 0, 250) });
            }
            Chase(); bool front = false, back = false, left = false, right = false;
            foreach (var e in enemies)
            {
                var goal = em.GetComponentData<NavigationIntent>(e); Inside(goal.Destination, Room);
                Assert.That(goal.Kind, Is.EqualTo(NavigationGoalKind.Coverage));
                front |= goal.Destination.z > 0; back |= goal.Destination.z < -10;
                left |= goal.Destination.x < -3; right |= goal.Destination.x > 3;
                var cached = em.GetComponentData<WanderDestination>(e).Position; Chase();
                Assert.That(em.GetComponentData<WanderDestination>(e).Position, Is.EqualTo(cached));
            }
            Assert.That(front && back && left && right, Is.True);
        }

        [TestCase(EnemyArchetypeKind.Baseline)] [TestCase(EnemyArchetypeKind.Explosive)] [TestCase(EnemyArchetypeKind.Armored)]
        public void Barricade008_NeighboringPlayerDoesNotPullSurvivorTowardGate(EnemyArchetypeKind kind)
        {
            em.SetComponentData(player, new PlayerSnapshot { IsAvailable = true, Position = new float3(0, 0, 11) });
            var e = Enemy(new float3(0, 0, 9), owner, kind);
            em.SetComponentData(e, new EnemyContactAttemptState { IsAttempting = 1, SecondsRemaining = 1,
                CommittedDirection = math.forward() });
            Chase(); var goal = em.GetComponentData<NavigationIntent>(e);
            Assert.That(goal.Kind, Is.EqualTo(NavigationGoalKind.Coverage)); Inside(goal.Destination, Room);
            Assert.That(em.GetComponentData<EnemyContactAttemptState>(e).IsAttempting, Is.Zero);
        }

        [Test] public void Barricade008_PreviousRoomSurvivorsDoNotConsumeNextRoomsPressureSlot()
        {
            em.SetComponentData(player, new PlayerSnapshot { IsAvailable = true, Position = new float3(0, 0, 11) });
            var old = Enemy(new float3(0, 0, 9.9f), owner);
            var nextOwner = em.CreateEntity(typeof(BarricadeRoom));
            var nextRoom = new BarricadeRoom { Minimum = new float2(-10, 10), Maximum = new float2(10, 50), Activated = 1 };
            em.SetComponentData(nextOwner, nextRoom);
            var next = Enemy(new float3(0, 0, 20), nextOwner);
            Chase(); Assert.That(em.GetComponentData<NavigationIntent>(old).Kind, Is.EqualTo(NavigationGoalKind.Coverage));
            var target = em.GetComponentData<NavigationIntent>(next);
            Assert.That(target.Kind, Is.EqualTo(NavigationGoalKind.Position)); Inside(target.Destination, nextRoom);
        }

        [Test] public void Barricade008_PlayerReturningToRoomRestoresHostilePursuit()
        {
            var e = Enemy(new float3(0, 0, -5), owner); Chase();
            em.SetComponentData(player, new PlayerSnapshot { IsAvailable = true, Position = new float3(0, 0, 9.9f) });
            Chase(); var goal = em.GetComponentData<NavigationIntent>(e);
            Assert.That(goal.Kind, Is.EqualTo(NavigationGoalKind.Position)); Inside(goal.Destination, Room);
            Assert.That(goal.Destination.z, Is.LessThanOrEqualTo(9.25f));
        }

        [Test] public void Barricade008_DisplacedRecoveredEnemyKeepsOwnershipAndReturns()
        {
            var e = Enemy(new float3(0, 0, 30), owner); Chase(); Move();
            Inside(em.GetComponentData<NavigationIntent>(e).Destination, Room);
            Assert.That(em.GetComponentData<PhysicsVelocity>(e).Linear.z, Is.LessThan(0));
            Assert.That(em.GetComponentData<LocalTransform>(e).Position.z, Is.EqualTo(30), "Movement cannot teleport physics bodies.");
        }

        [TestCase(2f)] [TestCase(5f)] [TestCase(15f)]
        public void Barricade008_WalkingAndChargeSteeringStopAtRoomBoundary(float speed)
        {
            var e = Enemy(new float3(0, 0, 5), owner);
            em.SetComponentData(e, new DesiredMovement { Direction = math.forward(), Speed = speed });
            for (int i = 0; i < 250; i++)
            {
                Move(); var pose = em.GetComponentData<LocalTransform>(e);
                pose.Position += em.GetComponentData<PhysicsVelocity>(e).Linear * .02f;
                em.SetComponentData(e, pose); Inside(pose.Position, Room);
            }
            Assert.That(em.GetComponentData<PhysicsVelocity>(e).Linear.z, Is.EqualTo(0).Within(.001f));
        }

        [TestCase(EnemyLaunchPhase.Launched)] [TestCase(EnemyLaunchPhase.Recovering)]
        public void Barricade008_PhysicsLaunchAndRecoveryAreNotStoppedAtRoomBoundary(EnemyLaunchPhase phase)
        {
            var e = Enemy(new float3(0, 0, 11), owner);
            em.SetComponentData(e, new EnemyLaunchState { Phase = phase });
            var velocity = new float3(1, 2, 24); em.SetComponentData(e, new PhysicsVelocity { Linear = velocity });
            Move(); Assert.That(em.GetComponentData<PhysicsVelocity>(e).Linear, Is.EqualTo(velocity));
        }

        [Test] public void Barricade008_NonRoomEncounterKeepsGlobalDistribution()
        {
            var e = Enemy(float3.zero, Entity.Null); Chase();
            Assert.That(em.GetComponentData<NavigationIntent>(e).Destination.z, Is.GreaterThan(Room.Maximum.y));
        }

        [Test] public void Barricade008_BlockedNavigationTargetsResolveWithinRoomEvenWhenWholeHallIsConnected()
        {
            using var obstacles = new NativeArray<NavigationRectangle>(new[] { new NavigationRectangle
            { Minimum = new float2(-4, -3), Maximum = new float2(4, 3) } }, Allocator.Temp);
            using var blob = NavigationGridConstruction.Build(new float2(-10, -20), new float2(10, 100), 1,
                new float3(.2f, .6f, 1.2f), obstacles, Allocator.Persistent);
            int unboundedOutside = 0;
            for (int seed = 0; seed < 64; seed++)
            {
                if (NavigationGeometry.ResolveGoal(ref blob.Value, new float2(0, -8), float2.zero, 0, seed,
                    NavigationGoalKind.Coverage, out var global) && global.y > 0) unboundedOutside++;
                Assert.That(NavigationGeometry.ResolveGoal(ref blob.Value, new float2(0, -8), float2.zero, 0, seed,
                    NavigationGoalKind.Coverage, new float2(-9, -19), new float2(9, 0), out var local), Is.True);
                Assert.That(local.y, Is.InRange(-19, 0)); Assert.That(local.x, Is.InRange(-9, 9));
            }
            Assert.That(unboundedOutside, Is.GreaterThan(0), "The setup must exercise the old cross-room fallback.");
        }

        [TestCase(false)] [TestCase(true)]
        public void Barricade008_PathSearchCannotDetourThroughAdjacentRoom(bool restricted)
        {
            using var obstacles = new NativeArray<NavigationRectangle>(new[] { new NavigationRectangle
            { Minimum = new float2(-1, -20), Maximum = new float2(1, 0) } }, Allocator.Temp);
            using var blob = NavigationGridConstruction.Build(new float2(-10, -20), new float2(10, 20), 1,
                new float3(.2f, .6f, 1.2f), obstacles, Allocator.Persistent);
            var storage = new NavigationSearchStorage(blob.Value.Size.x * blob.Value.Size.y, 1);
            try
            {
                storage.Begin(0, new NavigationSearchRequest
                {
                    Start = NavigationGeometry.Anchor(ref blob.Value, new float2(-5, -5), 0),
                    Goal = NavigationGeometry.Anchor(ref blob.Value, new float2(5, -5), 0), Clearance = 0,
                    RestrictTerritory = (byte)(restricted ? 1 : 0),
                    TerritoryMinimum = new float2(-9, -19), TerritoryMaximum = new float2(9, -1)
                }, ref blob.Value);
                storage.ExpandBudget(ref blob.Value, 2000, 2000);
                Assert.That(storage.Slots[0].Result, Is.EqualTo(restricted ? NavigationSearchResult.Unreachable : NavigationSearchResult.Found));
            }
            finally { storage.Dispose(); }
        }

        [Test] public void Barricade009_RangedSurvivorKeepsLocalCoverageAndCancelsOffRoomShot()
        {
            var e=Enemy(float3.zero,owner,EnemyArchetypeKind.Ranged);
            em.AddComponentData(e,new RangedEnemySettings { PreferredMinimumDistance=5, PreferredMaximumDistance=8, ApproachSpeed=4, EngagementRange=100 });
            em.AddComponent<RangedPositioningState>(e); em.AddComponentData(e,new NavigationAgent { Radius=.5f });
            em.AddComponentData(e,new RangedAttackState { Phase=RangedAttackPhase.WindUp, SecondsRemaining=.01f });
            Chase(); var coverage=em.GetComponentData<NavigationIntent>(e);
            world.GetOrCreateSystem<RangedEnemyPositioningSystem>().Update(world.Unmanaged); em.CompleteAllTrackedJobs();
            Assert.That(em.GetComponentData<NavigationIntent>(e).Destination,Is.EqualTo(coverage.Destination));
            Assert.That(em.GetComponentData<NavigationIntent>(e).Kind,Is.EqualTo(NavigationGoalKind.Coverage));
            world.GetOrCreateSystem<CrowdPunch.Systems.Combat.RangedEnemyAttackSystem>().Update(world.Unmanaged);
            Assert.That(em.GetComponentData<RangedAttackState>(e).Phase,Is.EqualTo(RangedAttackPhase.Cooldown));
            Assert.That(em.GetComponentData<RangedAttackState>(e).ProjectilesSpawned,Is.Zero);
        }

        [Test] public void Barricade009_RangedDistanceBandDestinationStaysInsideRoom()
        {
            var e=Enemy(new float3(0,0,8),owner,EnemyArchetypeKind.Ranged);
            em.AddComponentData(e,new RangedEnemySettings { PreferredMinimumDistance=5, PreferredMaximumDistance=8, ApproachSpeed=4,RetreatSpeed=4 });
            em.AddComponent<RangedPositioningState>(e); em.AddComponentData(e,new NavigationAgent { Radius=.5f });
            em.SetComponentData(player,new PlayerSnapshot { IsAvailable=true, Position=new float3(0,0,5) });
            Chase(); world.GetOrCreateSystem<RangedEnemyPositioningSystem>().Update(world.Unmanaged); em.CompleteAllTrackedJobs();
            var target=em.GetComponentData<NavigationIntent>(e).Destination; Inside(target,Room); Assert.That(target.z,Is.LessThanOrEqualTo(9.25f));
        }

        [TestCase(DasherPhase.Positioning)] [TestCase(DasherPhase.Preparing)] [TestCase(DasherPhase.Dashing)]
        public void Barricade009_DasherDoesNotCommitToOffRoomPlayer(DasherPhase phase)
        {
            var e=Enemy(new float3(0,0,5),owner,EnemyArchetypeKind.Dasher);
            em.AddComponentData(e,new DasherSettings { PreparationMinimumDistance=1,PreparationMaximumDistance=100 });
            em.AddComponentData(e,new DasherState { Phase=phase,SecondsRemaining=.01f });
            em.AddComponentData(e,new NavigationAgent { Radius=.5f }); Chase();
            world.GetOrCreateSystem<DasherDecisionSystem>().Update(world.Unmanaged); em.CompleteAllTrackedJobs();
            Assert.That(em.GetComponentData<DasherState>(e).Phase,Is.EqualTo(DasherPhase.Positioning));
            Assert.That(em.GetComponentData<NavigationIntent>(e).Kind,Is.EqualTo(NavigationGoalKind.Coverage));
            Inside(em.GetComponentData<NavigationIntent>(e).Destination,Room);
        }

        [TestCase(EnemyLaunchPhase.Active)] [TestCase(EnemyLaunchPhase.Launched)]
        public void Barricade009_DashStopsBeforeRoomEdgeButLaunchedDasherKeepsPhysics(EnemyLaunchPhase phase)
        {
            var e=Enemy(new float3(0,0,9.2f),owner,EnemyArchetypeKind.Dasher);
            em.AddComponentData(e,new DasherSettings { MaximumDistance=100,DashSpeed=24,RecoveryDuration=1 });
            em.AddComponentData(e,new DasherState { Phase=DasherPhase.Dashing,LockedDirection=math.forward(),PreviousPosition=new float3(0,0,9.2f) });
            em.SetComponentData(e,new EnemyLaunchState { Phase=phase }); em.SetComponentData(e,new PhysicsVelocity { Linear=new float3(0,0,24) });
            world.GetOrCreateSystem<DasherMovementSystem>().Update(world.Unmanaged); em.CompleteAllTrackedJobs();
            Assert.That(em.GetComponentData<PhysicsVelocity>(e).Linear.z,Is.EqualTo(phase==EnemyLaunchPhase.Active?0:24));
            Assert.That(em.GetComponentData<DasherState>(e).Phase,Is.EqualTo(phase==EnemyLaunchPhase.Active?DasherPhase.Recovering:DasherPhase.Dashing));
        }
    }
}
