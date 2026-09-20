using CrowdPunch.Components;
using CrowdPunch.Systems.AI;
using CrowdPunch.Systems.Combat;
using CrowdPunch.Utilities;
using NUnit.Framework;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace CrowdPunch.Tests
{
    public sealed class ElitePunchGeometryTests
    {
        [Test]
        public void Enemy009_ProjectileMovesBeyondLocalSamplesToClearLongObstacleBeforeEliteApproaches()
        {
            using var world = new World("Elite long-obstacle staging");
            Entity elite = CreateEliteAttempt(world, out Entity target);
            var em = world.EntityManager;
            em.SetComponentData(elite, LocalTransform.FromPosition(new float3(-4, 0, 0)));
            em.SetComponentData(target, LocalTransform.FromPosition(new float3(2, 0, 0)));
            using var playerQuery = em.CreateEntityQuery(typeof(PlayerSnapshot));
            em.SetComponentData(playerQuery.GetSingletonEntity(), new PlayerSnapshot
            { IsAvailable = true, Position = new float3(10, 0, 0) });
            var settings = em.GetComponentData<ElitePunchSettings>(elite);
            settings.CrowdCorridorRadius = 1.5f;
            settings.DesiredPunchDistance = 1f;
            em.SetComponentData(elite, settings);
            em.AddComponentData(elite, new NavigationAgent { Radius = .75f });
            em.AddComponentData(target, new NavigationAgent { Radius = .2f });
            em.AddComponentData(target, new EnemyMovementSettings { MoveSpeed = 5 });
            em.AddComponentData(target, new DesiredMovement());
            em.AddComponentData(target, new NavigationIntent());
            em.AddComponentData(target, new EnemyContactAttemptState());
            em.AddComponentData(target, new EnemyContactDamageSettings());
            using var obstacles = new NativeArray<NavigationRectangle>(new[]
            {
                new NavigationRectangle { Minimum = new float2(-1, -5), Maximum = new float2(1, 5) }
            }, Allocator.Temp);
            using var blob = NavigationGridConstruction.Build(new float2(-15), new float2(15), 1,
                new float3(.3f, .6f, 1.2f), obstacles, Allocator.Persistent);
            Entity arena = em.CreateEntity(typeof(NavigationGrid), typeof(NavigationRuntimeSettings));
            em.SetComponentData(arena, new NavigationGrid { Data = blob });
            em.SetComponentData(arena, new NavigationRuntimeSettings { Enabled = 1 });

            world.GetOrCreateSystemManaged<EliteCrowdSupportSystem>().Update();

            NavigationIntent intent = em.GetComponentData<NavigationIntent>(target);
            Assert.AreEqual(0, em.GetComponentData<ElitePunchReservation>(target).IsStaged);
            Assert.Greater(em.GetComponentData<DesiredMovement>(target).Speed, 0);
            Assert.Greater(math.abs(intent.Destination.z), 3f,
                "Two local 1.5 m rings cannot clear this wall; staging must keep searching outward.");
            float3 behind = ElitePunchSystem.DesiredPosition(intent.Destination, new float3(10, 0, 0), 1f);
            Assert.IsTrue(NavigationGeometry.Segment(ref blob.Value, new float2(-4, 0), behind.xz, 1.2f),
                "The chosen projectile position must already provide the elite's full clear approach lane.");

            Entity transientBlocker = em.CreateEntity(typeof(Enemy), typeof(EnemyTier), typeof(LocalTransform),
                typeof(EnemyLaunchState), typeof(DesiredMovement), typeof(EnemyMovementSettings),
                typeof(NavigationIntent), typeof(EnemyContactAttemptState), typeof(EnemyContactDamageSettings),
                typeof(ElitePunchReservation));
            em.SetComponentData(transientBlocker, new EnemyTier { Value = EnemyCombatTier.Normal });
            em.SetComponentData(transientBlocker, new EnemyLaunchState { Phase = EnemyLaunchPhase.Active });
            em.SetComponentData(transientBlocker, LocalTransform.FromPosition(
                math.lerp(new float3(-4, 0, 0), behind, .5f)));
            em.SetComponentData(transientBlocker, new EnemyMovementSettings { MoveSpeed = 5 });
            em.SetComponentData(target, LocalTransform.FromPosition(new float3(2, 0, 0)));
            em.SetComponentData(target, new DesiredMovement());

            world.GetOrCreateSystemManaged<EliteCrowdSupportSystem>().Update();

            Assert.Greater(em.GetComponentData<DesiredMovement>(target).Speed, 0,
                "A transient crowd blocker may delay IsStaged, but must not prevent obstacle-clear relocation.");
            Assert.AreEqual(0, em.GetComponentData<ElitePunchReservation>(target).IsStaged);
        }

        [TestCase(false, 8.5f)]
        [TestCase(true, 8.5f)]
        [TestCase(false, 12f)]
        public void Enemy009_StagingRequiresBodyClearanceAndNeverReportsBlockedFallbackReady(bool fullyBlocked, float targetX)
        {
            using var world = new World("Elite boundary staging");
            Entity elite = CreateEliteAttempt(world, out Entity target);
            var em = world.EntityManager;
            em.SetComponentData(elite, LocalTransform.FromPosition(new float3(5, 0, 0)));
            em.SetComponentData(target, LocalTransform.FromPosition(new float3(targetX, 0, 0)));
            using var playerQuery = em.CreateEntityQuery(typeof(PlayerSnapshot));
            em.SetComponentData(playerQuery.GetSingletonEntity(), new PlayerSnapshot { IsAvailable = true });
            var settings = em.GetComponentData<ElitePunchSettings>(elite);
            settings.CrowdCorridorRadius = 1.5f;
            em.SetComponentData(elite, settings);
            em.AddComponentData(elite, new NavigationAgent { Radius = .75f });
            em.AddComponentData(target, new NavigationAgent { Radius = .2f });
            em.AddComponentData(target, new EnemyMovementSettings { MoveSpeed = 5 });
            em.AddComponentData(target, new DesiredMovement());
            em.AddComponentData(target, new NavigationIntent());
            em.AddComponentData(target, new EnemyContactAttemptState());
            em.AddComponentData(target, new EnemyContactDamageSettings());
            using var obstacles = new NativeArray<NavigationRectangle>(fullyBlocked
                ? new[] { new NavigationRectangle { Minimum = new float2(-10), Maximum = new float2(10) } }
                : new NavigationRectangle[0], Allocator.Temp);
            using var blob = NavigationGridConstruction.Build(new float2(-10), new float2(10), 1,
                new float3(.3f, .6f, 1.2f), obstacles, Allocator.Persistent);
            Entity arena = em.CreateEntity(typeof(NavigationGrid), typeof(NavigationRuntimeSettings));
            em.SetComponentData(arena, new NavigationGrid { Data = blob });
            em.SetComponentData(arena, new NavigationRuntimeSettings { Enabled = 1 });
            var support = world.GetOrCreateSystemManaged<EliteCrowdSupportSystem>();

            support.Update();

            Assert.AreEqual(0, em.GetComponentData<ElitePunchReservation>(target).IsStaged);
            if (fullyBlocked)
            {
                Assert.AreEqual(0, em.GetComponentData<DesiredMovement>(target).Speed,
                    "No valid sample must remain unstaged even when movement is zero.");
                return;
            }
            Assert.Greater(em.GetComponentData<DesiredMovement>(target).Speed, 0);
            float3 destination = em.GetComponentData<NavigationIntent>(target).Destination;
            if (targetX > 10f)
            {
                Assert.Less(destination.x, 10f, "A recovered projectile outside spacing bounds must first re-enter.");
                em.SetComponentData(target, LocalTransform.FromPosition(destination));
                support.Update();
                Assert.AreEqual(0, em.GetComponentData<ElitePunchReservation>(target).IsStaged);
                Assert.Greater(em.GetComponentData<DesiredMovement>(target).Speed, 0);
                destination = em.GetComponentData<NavigationIntent>(target).Destination;
            }
            float3 behind = ElitePunchSystem.DesiredPosition(destination, float3.zero, settings.DesiredPunchDistance);
            Assert.IsTrue(NavigationGeometry.Segment(ref blob.Value, new float2(5, 0), behind.xz, 1.2f));
            em.SetComponentData(target, LocalTransform.FromPosition(destination));
            support.Update();
            Assert.AreEqual(1, em.GetComponentData<ElitePunchReservation>(target).IsStaged);
            Assert.AreEqual(0, em.GetComponentData<DesiredMovement>(target).Speed);
        }

        [Test]
        public void Enemy009_FailedCooldownApproachDoesNotPreventNextReservation()
        {
            using var world = new World("Elite cooldown retry");
            Entity elite = CreateEliteAttempt(world, out Entity target);
            var em = world.EntityManager;
            em.SetComponentData(elite, new ElitePunchState { Phase = ElitePunchPhase.Cooldown });
            em.SetComponentData(target, new ElitePunchReservation());
            em.SetComponentData(elite, new NavigationPathState { TravelState = NavigationTravelState.Failed });
            var system = world.GetOrCreateSystemManaged<ElitePunchSystem>();

            system.Update();
            Assert.AreEqual(ElitePunchPhase.SelectingTarget, em.GetComponentData<ElitePunchState>(elite).Phase);
            system.Update();

            Assert.AreEqual(target, em.GetComponentData<ElitePunchState>(elite).Target);
            Assert.AreEqual(elite, em.GetComponentData<ElitePunchReservation>(target).Owner);
            Assert.AreEqual(NavigationMode.Hold, em.GetComponentData<NavigationIntent>(elite).Mode,
                "Navigation must retire the failed speculative route before reserved setup.");
        }

        [Test]
        public void Enemy009_StagingWaitStillRetargetsWithoutSpendingSetupTimeout()
        {
            using var world = new World("Elite staging retarget");
            Entity elite = CreateEliteAttempt(world, out Entity target);
            var em = world.EntityManager;
            var system = world.GetOrCreateSystemManaged<ElitePunchSystem>();
            for (int i = 0; i < 100; i++) system.Update();
            Assert.AreEqual(0f, em.GetComponentData<ElitePunchState>(elite).SetupSeconds);
            Assert.AreEqual(target, em.GetComponentData<ElitePunchState>(elite).Target);

            Entity closer = CreateProjectile(em, 1f);
            for (int i = 0; i < 20; i++) system.Update();

            Assert.AreEqual(closer, em.GetComponentData<ElitePunchState>(elite).Target);
            Assert.AreEqual(Entity.Null, em.GetComponentData<ElitePunchReservation>(target).Owner);
            Assert.AreEqual(elite, em.GetComponentData<ElitePunchReservation>(closer).Owner);
            Assert.AreEqual(0f, em.GetComponentData<ElitePunchState>(elite).SetupSeconds);
        }

        [Test]
        public void Enemy009_FailedReservedApproachStillCancelsAndCoolsDown()
        {
            using var world = new World("Elite failed reserved setup");
            Entity elite = CreateEliteAttempt(world, out Entity target);
            var em = world.EntityManager;
            em.SetComponentData(target, new ElitePunchReservation { Owner = elite, IsStaged = 1 });
            em.SetComponentData(elite, new NavigationPathState { TravelState = NavigationTravelState.Failed });

            world.GetOrCreateSystemManaged<ElitePunchSystem>().Update();

            Assert.AreEqual(ElitePunchPhase.Cooldown, em.GetComponentData<ElitePunchState>(elite).Phase);
            Assert.AreEqual(Entity.Null, em.GetComponentData<ElitePunchState>(elite).Target);
            Assert.AreEqual(Entity.Null, em.GetComponentData<ElitePunchReservation>(target).Owner);
            Assert.AreEqual(0f, em.GetComponentData<DesiredMovement>(elite).Speed);
        }

        private static Entity CreateEliteAttempt(World world, out Entity target)
        {
            world.SetTime(new TimeData(1, .02f));
            var em = world.EntityManager;
            Entity player = em.CreateEntity(typeof(PlayerSnapshot));
            em.SetComponentData(player, new PlayerSnapshot { IsAvailable = true, Position = new float3(0, 0, 10) });
            Entity elite = em.CreateEntity(typeof(Enemy), typeof(ElitePunchState), typeof(ElitePunchSettings),
                typeof(DesiredMovement), typeof(LocalTransform), typeof(EnemyLaunchState),
                typeof(NavigationIntent), typeof(NavigationPathState), typeof(EnemyMovementSettings));
            em.SetComponentData(elite, LocalTransform.Identity);
            em.SetComponentData(elite, new EnemyLaunchState { Phase = EnemyLaunchPhase.Active });
            em.SetComponentData(elite, new ElitePunchSettings
            {
                AllowActiveTargets = 1, Cooldown = 1, RetargetInterval = .25f, MaximumSetupDuration = 1,
                DesiredPunchDistance = 1, PositionTolerance = .4f
            });
            target = CreateProjectile(em, 3f);
            em.SetComponentData(target, new ElitePunchReservation { Owner = elite });
            em.SetComponentData(elite, new ElitePunchState
            {
                Phase = ElitePunchPhase.Repositioning, Target = target, RetargetSeconds = .25f
            });
            return elite;
        }

        private static Entity CreateProjectile(EntityManager em, float distance)
        {
            Entity target = em.CreateEntity(typeof(Enemy), typeof(EnemyTier), typeof(EnemyArchetype),
                typeof(LocalTransform), typeof(EnemyLaunchState), typeof(Health), typeof(ElitePunchReservation));
            em.SetComponentData(target, new EnemyTier { Value = EnemyCombatTier.Normal });
            em.SetComponentData(target, new EnemyLaunchState { Phase = EnemyLaunchPhase.Active });
            em.SetComponentData(target, new Health { Current = 10, Max = 10 });
            em.SetComponentData(target, LocalTransform.FromPosition(new float3(0, 0, distance)));
            return target;
        }

        [TestCase(1.14f, -1f, 3f, true)]
        [TestCase(1.16f, 0f, 3f, false)]
        [TestCase(0f, 0f, -0.01f, false)]
        [TestCase(0f, 0f, 6.01f, false)]
        [TestCase(0f, 1.16f, 3f, false)]
        public void Player008_PunchMatchesFootprintWithoutHeightNarrowing(float x, float y, float z, bool expected)
        {
            PunchSpecification punch = new PunchSpecification
            {
                Origin = new float3(0f, 2f, 0f), Direction = new float3(0f, 0f, 1f),
                Radius = 1.15f, Range = 6f, Cause = EnemyLaunchCause.PlayerPunch
            };
            Assert.AreEqual(expected, PunchResolution.Contains(punch.Origin + new float3(x, y, z), punch));
        }

        [Test]
        public void ElitePunchRetainsCircularCrossSection()
        {
            PunchSpecification punch = new PunchSpecification
            {
                Direction = new float3(0f, 0f, 1f), Radius = 1.15f, Range = 6f,
                Cause = EnemyLaunchCause.ElitePunch
            };
            Assert.IsFalse(PunchResolution.Contains(new float3(1.14f, -1f, 3f), punch));
        }

        [Test]
        public void TacticProbabilityEndpointsAreDeterministic()
        {
            uint zeroSeed = 123u, oneSeed = 123u;
            Assert.AreEqual(ElitePunchTactic.CrowdShot, ElitePunchSystem.ChooseTactic(ref zeroSeed, 0f));
            Assert.AreEqual(ElitePunchTactic.ClearPath, ElitePunchSystem.ChooseTactic(ref oneSeed, 1f));
        }

        [Test]
        public void IntermediateTacticSelectionRepeatsFromSameSeed()
        {
            uint first = 9876u, second = 9876u;
            Assert.AreEqual(ElitePunchSystem.ChooseTactic(ref first, 0.42f), ElitePunchSystem.ChooseTactic(ref second, 0.42f));
            Assert.AreEqual(first, second);
        }

        [Test]
        public void DesiredPositionIsBehindTargetRelativeToPlayer()
        {
            float3 position = ElitePunchSystem.DesiredPosition(new float3(2f, 3f, 0f), new float3(10f, 0f, 0f), 1.5f);
            Assert.That(position.x, Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(position.y, Is.EqualTo(3f).Within(0.0001f));
            Assert.That(position.z, Is.EqualTo(0f).Within(0.0001f));
        }

        [Test]
        public void TargetSearchEligibilityUsesDedicatedPhaseSwitches()
        {
            ElitePunchSettings settings = new ElitePunchSettings
            {
                AllowActiveTargets = 1,
                AllowRecoveringTargets = 0,
                AllowLaunchedTargets = 0
            };
            Health living = new Health { Current = 1f, Max = 1f };
            Assert.IsTrue(ElitePunchSystem.CanSelectTarget(
                new EnemyLaunchState { Phase = EnemyLaunchPhase.Active }, living, settings));
            Assert.IsFalse(ElitePunchSystem.CanSelectTarget(
                new EnemyLaunchState { Phase = EnemyLaunchPhase.Recovering }, living, settings));
            Assert.IsFalse(ElitePunchSystem.CanSelectTarget(
                new EnemyLaunchState { Phase = EnemyLaunchPhase.Launched }, living, settings));
        }

        [Test]
        public void SetupSpeedBrakesIntoPositionTolerance()
        {
            Assert.AreEqual(0f, ElitePunchSystem.CalculateSetupSpeed(0.4f, 0.4f, 75f, 40f, 12f));
            Assert.That(ElitePunchSystem.CalculateSetupSpeed(1f, 0.4f, 75f, 40f, 12f), Is.EqualTo(12f));
            Assert.AreEqual(75f, ElitePunchSystem.CalculateSetupSpeed(100f, 0.4f, 75f, 40f, 12f));
        }

        [Test]
        public void SetupSpeedStaysAboveMovingTargetCatchupSpeed()
        {
            float speed = ElitePunchSystem.CalculateSetupSpeed(0.5f, 0.4f, 37.5f, 20f, 11f);
            Assert.AreEqual(11f, speed);
        }

        [Test]
        public void EnemyInsideShotCorridorMovesTowardNearestSide()
        {
            bool shouldExit = EliteCrowdSupportSystem.TryGetCorridorExitDirection(
                new float3(4f, 0f, 0.5f),
                float3.zero,
                new float3(10f, 0f, 0f),
                1.5f,
                out float3 direction);

            Assert.IsTrue(shouldExit);
            Assert.Greater(direction.z, 0f);
            Assert.That(direction.x, Is.EqualTo(0f).Within(0.0001f));
        }

        [Test]
        public void EnemyOutsideShotSegmentKeepsItsNormalIntent()
        {
            Assert.IsFalse(EliteCrowdSupportSystem.TryGetCorridorExitDirection(
                new float3(12f, 0f, 0f),
                float3.zero,
                new float3(10f, 0f, 0f),
                1.5f,
                out _));
        }

        [Test]
        public void ProjectileMovesBeforeWaitingAtClearStagingPosition()
        {
            DesiredMovement moving = EliteCrowdSupportSystem.GetStagingMovement(
                float3.zero,
                new float3(2f, 0f, 0f),
                5f,
                0.25f);
            DesiredMovement waiting = EliteCrowdSupportSystem.GetStagingMovement(
                new float3(1.9f, 0f, 0f),
                new float3(2f, 0f, 0f),
                5f,
                0.25f);

            Assert.Greater(moving.Speed, 0f);
            Assert.Greater(moving.Direction.x, 0f);
            Assert.AreEqual(0f, waiting.Speed);
        }

        [Test]
        public void EliteRoutesAroundTargetWhenDirectApproachCrossesIt()
        {
            float3 direction = ElitePunchSystem.GetCollisionAvoidingApproachDirection(
                new float3(4f, 0f, 0f),
                float3.zero,
                new float3(-1f, 0f, 0f),
                1f);

            Assert.Less(direction.x, 0f);
            Assert.That(math.abs(direction.z), Is.GreaterThan(0.01f));
        }

        [Test]
        public void EliteUsesDirectApproachWhenTargetDoesNotBlockIt()
        {
            float3 direction = ElitePunchSystem.GetCollisionAvoidingApproachDirection(
                new float3(-4f, 0f, 0f),
                float3.zero,
                new float3(-1f, 0f, 0f),
                1f);

            Assert.That(direction.x, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(direction.z, Is.EqualTo(0f).Within(0.0001f));
        }
    }
}
