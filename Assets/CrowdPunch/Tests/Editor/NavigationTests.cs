using CrowdPunch.Components;
using CrowdPunch.Utilities;
using CrowdPunch.Systems.AI;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
namespace CrowdPunch.Tests
{
    public sealed class NavigationTests
    {
        private static BlobAssetReference<NavigationGridBlob> Grid(params NavigationRectangle[] obstacles)
        {
            using var rectangles = new NativeArray<NavigationRectangle>(obstacles, Allocator.Temp);
            return NavigationGridConstruction.Build(new float2(-10), new float2(10), 1, new float3(.2f, .6f, 1.2f), rectangles, Allocator.Persistent);
        }
        private static NavigationRectangle Box(float x0, float y0, float x1, float y1) => new NavigationRectangle { Minimum = new float2(x0, y0), Maximum = new float2(x1, y1) };
        [Test]
        public void ParticipationAnchorUsesTranslatedSpacingCentreAndIgnoresDisabledOverride()
        {
            // COMBAT-017: translated, differently sized spacing bounds define the origin.
            using var rectangles = new NativeArray<NavigationRectangle>(0, Allocator.Temp);
            using var blob = NavigationGridConstruction.Build(new float2(20, -14), new float2(42, 14),
                1, new float3(.62f, .97f, 1.62f), rectangles, Allocator.Persistent);
            Assert.True(NavigationGeometry.TryResolveParticipationAnchor(ref blob.Value, false, new float2(0, -14), out var anchor));
            Assert.AreEqual(new float2(31, 0), anchor);
        }
        [Test]
        public void BlockedParticipationCentreSelectsNearestCellClearForEveryClass()
        {
            using var blob = Grid(Box(-1, -1, 1, 1)); ref var g = ref blob.Value;
            Assert.True(NavigationGeometry.TryResolveParticipationAnchor(ref g, false, default, out var anchor));
            for (int cls = 0; cls < 3; cls++) Assert.GreaterOrEqual(NavigationGeometry.Anchor(ref g, anchor, cls), 0);
            for (int cell = 0; cell < 400; cell++)
            {
                var candidate = NavigationGeometry.Center(ref g, cell);
                bool clear = true;
                for (int cls = 0; cls < 3; cls++) clear &= NavigationGeometry.Anchor(ref g, candidate, cls) >= 0;
                if (clear) Assert.GreaterOrEqual(math.lengthsq(candidate), math.lengthsq(anchor));
            }
            Assert.True(NavigationGeometry.TryResolveParticipationAnchor(ref g, false, default, out var repeated));
            Assert.AreEqual(anchor, repeated);
        }
        [Test]
        public void ParticipationOverrideSelectsRegionAndInvalidOverrideIsNotRelocated()
        {
            using var blob = Grid(Box(-1, -10, 1, 10)); ref var g = ref blob.Value;
            var requested = new float2(5, 0);
            Assert.True(NavigationGeometry.TryResolveParticipationAnchor(ref g, true, requested, out var anchor));
            Assert.AreEqual(requested, anchor);
            var grid = new NavigationGrid { Data = blob, ParticipationAnchor = anchor };
            Assert.True(NavigationGeometry.SpawnAllowed(grid, new float2(6, 0), .2f));
            Assert.False(NavigationGeometry.SpawnAllowed(grid, new float2(-6, 0), .2f));
            requested = new float2(0, -14);
            Assert.False(NavigationGeometry.TryResolveParticipationAnchor(ref g, true, requested, out anchor));
            Assert.AreEqual(requested, anchor);
        }
        [Test]
        public void ParticipationAnchorFailsWhenNoPointFitsEveryClass()
        {
            // Small enemies fit this corridor; the largest configured class does not.
            using var blob = Grid(Box(-10, -10, -1, 10), Box(1, -10, 10, 10));
            Assert.False(NavigationGeometry.TryResolveParticipationAnchor(ref blob.Value, false, default, out _));
        }
        [Test]
        public void CoordinatesAndRasterisationUseWorldXZAndActualBounds()
        {
            using var blob = Grid(Box(-1, -2, 1, 2)); ref var g = ref blob.Value;
            Assert.AreEqual(0, NavigationGeometry.Cell(ref g, new float2(-9.5f)));
            Assert.AreEqual(-1, NavigationGeometry.Cell(ref g, new float2(10, 0)));
            Assert.AreEqual(new float2(-9.5f), NavigationGeometry.Center(ref g, 0));
            Assert.AreEqual(0, NavigationGeometry.Region(ref g, NavigationGeometry.Cell(ref g, new float2(.5f)), 0));
            Assert.Greater(NavigationGeometry.Region(ref g, NavigationGeometry.Cell(ref g, new float2(5)), 0), 0);
        }
        [Test]
        public void ClearanceAppliesToEdgesCornersAndDifferentRadii()
        {
            using var blob = Grid(Box(-3, -10, -1, 10), Box(1, -10, 3, 10)); ref var g = ref blob.Value;
            Assert.True(NavigationGeometry.Segment(ref g, new float2(0, -8), new float2(0, 8), .6f));
            Assert.False(NavigationGeometry.Segment(ref g, new float2(0, -8), new float2(0, 8), 1.2f));
            Assert.False(NavigationGeometry.Segment(ref g, new float2(-9.9f, 0), new float2(-9, 0), .2f));
            Assert.False(NavigationGeometry.Segment(ref g, new float2(-4, 0), new float2(4, 0), .2f));
            for (int c = 0; c < 3; c++) for (int cell = 0; cell < 400; cell++) for (int d = 4; d < 8; d++)
                        if ((g.Edges[c * 400 + cell] & (1 << d)) != 0)
                        { int2 o = NavigationGeometry.Offset(d); Assert.Greater(g.Regions[c * 400 + cell + o.x], 0); Assert.Greater(g.Regions[c * 400 + cell + o.y * 20], 0); }
        }
        [Test]
        public void AStarFindsDetourAndEverySmoothingShortcutIsSafe()
        {
            using var blob = Grid(Box(-1, -6, 1, 6)); ref var g = ref blob.Value;
            var storage = new NavigationSearchStorage(400, 1);
            try
            {
                int start = NavigationGeometry.Cell(ref g, new float2(-6, 0)), goal = NavigationGeometry.Cell(ref g, new float2(6, 0));
                Assert.False(NavigationGeometry.Segment(ref g, NavigationGeometry.Center(ref g, start), NavigationGeometry.Center(ref g, goal), g.Radii.x));
                storage.Begin(0, new NavigationSearchRequest { Start = start, Goal = goal, Clearance = 0 }, ref g);
                while (storage.Slots[0].Result == NavigationSearchResult.Running) storage.ExpandBudget(ref g, 7, 4096);
                Assert.AreEqual(NavigationSearchResult.Found, storage.Slots[0].Result);
                int cell = goal, steps = 0;
                while (cell != start)
                {
                    int parent = storage.GetNode(0, cell).Parent; Assert.GreaterOrEqual(parent, 0);
                    Assert.True(NavigationGeometry.Segment(ref g, NavigationGeometry.Center(ref g, parent), NavigationGeometry.Center(ref g, cell), g.Radii.x));
                    cell = parent; Assert.Less(++steps, 100);
                }
                Assert.Greater(steps, 12);
                // A tempting skip through the barrier is rejected even when both endpoint cells are valid.
                Assert.False(NavigationGeometry.Segment(ref g, new float2(-2, 5), new float2(2, 5), g.Radii.x));
            }
            finally { storage.Dispose(); }
        }
        [Test]
        public void RegionsRejectDisconnectedDestinationsAndSpawns()
        {
            using var blob = Grid(Box(-1, -10, 1, 10)); ref var g = ref blob.Value;
            int left = NavigationGeometry.Cell(ref g, new float2(-5, 0)), right = NavigationGeometry.Cell(ref g, new float2(5, 0));
            Assert.AreNotEqual(NavigationGeometry.Region(ref g, left, 0), NavigationGeometry.Region(ref g, right, 0));
            Assert.False(NavigationGeometry.ResolveGoal(ref g, new float2(-5, 0), new float2(5, 0), 0, 1, NavigationGoalKind.ExactSetup, out _));
            Assert.False(NavigationGeometry.SpawnAllowed(new NavigationGrid { Data = blob, ParticipationAnchor = new float2(-5, 0) }, new float2(5, 0), .2f));
            var storage = new NavigationSearchStorage(400, 1);
            try
            {
                storage.Begin(0, new NavigationSearchRequest { Start = left, Goal = right }, ref g);
                storage.ExpandBudget(ref g, 1000, 4096); Assert.AreEqual(NavigationSearchResult.Unreachable, storage.Slots[0].Result);
            }
            finally { storage.Dispose(); }
        }
        [Test]
        public void BudgetContinuationIsFairAndLimitIsNotUnreachable()
        {
            using var blob = Grid(); ref var g = ref blob.Value; var storage = new NavigationSearchStorage(400, 3);
            try
            {
                for (int i = 0; i < 3; i++) storage.Begin(i, new NavigationSearchRequest { Start = 21 + i, Goal = 378 - i }, ref g);
                for (int i = 0; i < 6; i++) Assert.AreEqual(1, storage.ExpandBudget(ref g, 1, 4096));
                for (int i = 0; i < 3; i++) { Assert.AreEqual(2, storage.Slots[i].Expanded); Assert.AreEqual(NavigationSearchResult.Running, storage.Slots[i].Result); }
                storage.ExpandBudget(ref g, 300, 4096);
                for (int i = 0; i < 3; i++) Assert.AreEqual(NavigationSearchResult.Found, storage.Slots[i].Result);
                storage.Begin(0, new NavigationSearchRequest { Start = 21, Goal = 378 }, ref g);
                storage.ExpandBudget(ref g, 5, 1); Assert.AreEqual(NavigationSearchResult.ExpansionLimit, storage.Slots[0].Result);
            }
            finally { storage.Dispose(); }
        }
        [Test]
        public void DestinationVersionsPoolingAndRestartRejectOldResults()
        {
            // COMBAT-010/013/014 and LOOP-006: route lifetime cannot outlive enemy motion ownership.
            using var world = new World("Navigation stale result tests"); var em = world.EntityManager;
            var e = em.CreateEntity(typeof(NavigationPathState), typeof(EnemyLaunchState), typeof(RespawnRequest));
            em.SetComponentEnabled<RespawnRequest>(e, false);
            em.SetComponentData(e, new EnemyLaunchState { Phase = EnemyLaunchPhase.Active });
            var state = new NavigationPathState { Version = 4, Initialized = 1, Pending = 1 }; em.SetComponentData(e, state);
            var request = new NavigationSearchRequest { Enemy = e, Version = 4 }; Assert.True(EnemyNavigationSystem.Valid(em, request));
            state.Version++; em.SetComponentData(e, state); Assert.False(EnemyNavigationSystem.Valid(em, request));
            request.Version = state.Version; Assert.True(EnemyNavigationSystem.Valid(em, request));
            em.SetComponentEnabled<RespawnRequest>(e, true); Assert.False(EnemyNavigationSystem.Valid(em, request));
            em.SetComponentEnabled<RespawnRequest>(e, false); state.Reset(); em.SetComponentData(e, state); Assert.False(EnemyNavigationSystem.Valid(em, request));
            Assert.AreEqual(0, state.Initialized); Assert.AreEqual(0, state.Pending); Assert.AreEqual(0, state.Waypoint);
            em.DestroyEntity(e); Assert.False(EnemyNavigationSystem.Valid(em, request));
        }
        [Test]
        public void HighSpeedSteeringBrakesForTerrainWithoutReducingEveryStepToACrawl()
        {
            using var blob = Grid(Box(-1, -6, 1, 6)); ref var g = ref blob.Value;
            var settings = new NavigationRuntimeSettings { LookAhead = .7f, WaypointArrival = .25f };
            var movement = EnemyNavigationSystem.SafeMovement(ref g, new float2(-5, 0), new float3(1, 0, 0), 30, .2f, settings, 10);
            Assert.That(movement.Speed, Is.InRange(4f, 9f));
            Assert.True(NavigationGeometry.Segment(ref g, new float2(-5, 0), new float2(-5 + movement.Speed * movement.Speed / 20, 0), .2f));
            movement = EnemyNavigationSystem.SafeMovement(ref g, new float2(-1.1f, 0), new float3(1, 0, 0), 30, .2f, settings, 10);
            Assert.AreEqual(0, movement.Speed);
        }

        [Test]
        public void ExplosiveDirectChargeCanReachClearPlayerOutsideSpacingGrid()
        {
            // ENEMY-010 / COMBAT-017: spacing bounds do not create an invisible wall for a direct explosive pursuit.
            using var clearBlob = Grid(); ref var clear = ref clearBlob.Value;
            var goal = NavigationIntent.Travel(new float3(15, 0, 0), 12.5f, 0f, float3.zero);
            Assert.True(EnemyNavigationSystem.TryGetOutOfGridDirectMovement(
                ref clear, new float2(9, 0), goal, .62f, 8f, out var movement));
            Assert.Greater(movement.Speed, 0f);
            Assert.That(movement.Direction.x, Is.EqualTo(1f).Within(.0001f));

            using var blockedBlob = Grid(Box(10, -2, 11, 2)); ref var blocked = ref blockedBlob.Value;
            Assert.False(EnemyNavigationSystem.TryGetOutOfGridDirectMovement(
                ref blocked, new float2(9, 0), goal, .62f, 8f, out _));
        }

        [Test]
        public void InvalidCoverageSlotsResolveStablyWithoutOneFallbackCell()
        {
            // COMBAT-016: terrain must retain distributed launch opportunities.
            using var blob = Grid(Box(-3, -3, 3, 3)); ref var g = ref blob.Value; float2 previous = default; int different = 0;
            for (int i = 0; i < 20; i++)
            {
                Assert.True(NavigationGeometry.ResolveGoal(ref g, new float2(-6), float2.zero, 0, i, NavigationGoalKind.Coverage, out var a));
                Assert.True(NavigationGeometry.ResolveGoal(ref g, new float2(-6), float2.zero, 0, i, NavigationGoalKind.Coverage, out var b));
                Assert.AreEqual(a, b); if (i > 0 && math.distance(previous, a) > .1f) different++; previous = a;
            }
            Assert.Greater(different, 10);
        }
    }
}
