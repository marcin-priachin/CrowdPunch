using CrowdPunch.Components;
using CrowdPunch.Systems.Groups;
using CrowdPunch.Utilities;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Profiling;

namespace CrowdPunch.Systems.AI
{
    [BurstCompile]
    [UpdateInGroup(typeof(GamePrePhysicsGroup))]
    [UpdateAfter(typeof(EnemyChaseSystem))]
    [UpdateAfter(typeof(RangedEnemyPositioningSystem))]
    [UpdateAfter(typeof(DasherDecisionSystem))]
    [UpdateAfter(typeof(ElitePunchSystem))]
    [UpdateAfter(typeof(EliteCrowdSupportSystem))]
    [UpdateBefore(typeof(Movement.EnemyMovementSystem))]
    [UpdateBefore(typeof(Movement.DasherMovementSystem))]
    public partial struct EnemyNavigationSystem : ISystem
    {
        private NavigationSearchStorage storage;
        private NativeQueue<NavigationSearchRequest> queue;
        private BlobAssetReference<NavigationGridBlob> boundGrid;
        private Entity gridEntity;
        private static readonly ProfilerMarker Marker = new ProfilerMarker("CrowdPunch.Navigation");
        public void OnCreate(ref SystemState state)
        {
            queue = new NativeQueue<NavigationSearchRequest>(Allocator.Persistent);
        }
        public void OnDestroy(ref SystemState state) { storage.Dispose(); queue.Dispose(); }
        [BurstCompile]
        public void OnUpdate(ref SystemState system)
        {
            using var marker = Marker.Auto();
            // Do not RequireForUpdate(grid): unloading must release old search state even with no grid.
            if (!SystemAPI.HasSingleton<NavigationGrid>())
            { if (storage.Slots.IsCreated) { storage.Dispose(); storage = default; } queue.Clear(); boundGrid = default; gridEntity = Entity.Null; return; }
            var grid = SystemAPI.GetSingleton<NavigationGrid>();
            var settings = SystemAPI.GetSingleton<NavigationRuntimeSettings>();
            var entity = SystemAPI.GetSingletonEntity<NavigationGrid>();
            ref var g = ref grid.Data.Value;
            var diagnostics = SystemAPI.GetSingleton<NavigationDiagnostics>();
            diagnostics.Direct = diagnostics.Following = diagnostics.Waiting = diagnostics.Expanded = 0;
            bool changed = boundGrid != grid.Data || gridEntity != entity;
            if (changed)
            {
                storage.Dispose(); storage = new NavigationSearchStorage(g.Size.x * g.Size.y, settings.ConcurrentSearches);
                boundGrid = grid.Data; gridEntity = entity; queue.Clear();
            }
            double now = SystemAPI.Time.ElapsedTime;
            foreach (var (intent, nav, agent, transform, launch, path, movement, enemy) in
                SystemAPI.Query<RefRO<NavigationIntent>, RefRW<NavigationPathState>, RefRO<NavigationAgent>, RefRO<LocalTransform>,
                    RefRO<EnemyLaunchState>, DynamicBuffer<NavigationWaypoint>, RefRW<DesiredMovement>>()
                    .WithAll<Enemy>().WithOptions(EntityQueryOptions.IgnoreComponentEnabledState).WithEntityAccess())
            {
                ref var n = ref nav.ValueRW;
                bool active = launch.ValueRO.Phase == EnemyLaunchPhase.Active && !SystemAPI.IsComponentEnabled<RespawnRequest>(enemy);
                if (changed || !active || settings.Enabled == 0)
                { if (n.Initialized != 0 || n.Pending != 0 || path.Length > 0) { n.Reset(); path.Clear(); } if (!active || settings.Enabled == 0) continue; }
                if (SystemAPI.HasComponent<DasherState>(enemy) && SystemAPI.GetComponent<DasherState>(enemy).Phase != DasherPhase.Positioning)
                { if (n.Initialized != 0) { n.Reset(); path.Clear(); } continue; }
                var goal = intent.ValueRO;
                if (goal.Mode != NavigationMode.Travel)
                {
                    if (n.Initialized != 0) { n.Reset(); path.Clear(); }
                    // Hold retains authored separation only; committed attacks keep their existing direction.
                    if (goal.Mode == NavigationMode.Hold)
                        movement.ValueRW = SafeMovement(ref g, transform.ValueRO.Position.xz, movement.ValueRO.Direction, movement.ValueRO.Speed,
                            agent.ValueRO.Radius + g.Margin, settings, 0);
                    continue;
                }
                float2 position = transform.ValueRO.Position.xz;
                int cls = NavigationGeometry.ClearanceClass(ref g, agent.ValueRO.Radius);
                if (cls < 0) { movement.ValueRW = default; continue; }
                float radius = g.Radii[cls];
                if (SystemAPI.HasComponent<ExplosiveEnemyState>(enemy)
                    && TryGetOutOfGridDirectMovement(ref g, position, goal, radius,
                        SystemAPI.GetComponent<EnemyMovementSettings>(enemy).BrakingAcceleration, out DesiredMovement directMovement))
                {
                    if (n.Initialized != 0) { n.Reset(); path.Clear(); }
                    movement.ValueRW = directMovement;
                    diagnostics.Direct++;
                    continue;
                }
                if (NavigationGeometry.Anchor(ref g, position, cls) < 0)
                {
                    // Physics crowd compression may push an active body inside the navigation margin.
                    // Escape uses actual collider clearance, only toward a class-valid nearby centre.
                    if (NavigationGeometry.TryEscape(ref g, position, agent.ValueRO.Radius, cls, out var escape))
                    {
                        var direction = math.normalizesafe(escape - position);
                        // TryEscape validates the complete ingress segment, including obstacle clearance.
                        // Ordinary SafeMovement rejects its outside-bounds starting point.
                        float escapeSpeed = math.min(math.min(goal.Speed, 1.5f),
                            math.sqrt(6f * math.max(0f, math.distance(position, escape) - .05f)));
                        movement.ValueRW = new DesiredMovement
                        { Direction = new float3(direction.x, 0, direction.y), Speed = escapeSpeed };
                        n.NextDirectAt = 0; n.ProgressAt = now;
                    }
                    else movement.ValueRW = default;
                    diagnostics.Waiting++; continue;
                }
                if (n.Initialized == 0)
                {
                    n.Reset(); n.Initialized = 1; n.RequestedGoal = goal.Destination.xz; n.Kind = goal.Kind;
                    n.ProgressPosition = position; n.LastPosition = position; n.ProgressAt = now; n.RequestOffset = Stagger(enemy, settings.Stagger);
                    n.NextRequestAt = now + Stagger(enemy, settings.Stagger);
                    Resolve(ref g, position, goal, cls, enemy, now, settings, ref n, ref diagnostics);
                }
                float threshold = goal.Kind == NavigationGoalKind.ExactSetup
                    ? math.min(settings.DestinationThreshold, math.max(.05f, goal.ArrivalDistance * .5f)) : settings.DestinationThreshold;
                bool goalChanged = math.distancesq(n.RequestedGoal, goal.Destination.xz) > threshold * threshold || n.Kind != goal.Kind;
                if (!goalChanged && n.TravelState == NavigationTravelState.Direct && math.distancesq(n.RequestedGoal, n.ResolvedGoal) < .0001f
                    && NavigationGeometry.Segment(ref g, position, goal.Destination.xz, radius))
                { n.RequestedGoal = goal.Destination.xz; n.ResolvedGoal = goal.Destination.xz; }
                if (goalChanged && now >= n.NextRequestAt)
                {
                    n.Version++; n.Pending = 0; n.RequestedGoal = goal.Destination.xz; n.Kind = goal.Kind;
                    // Keep an already safe route while the replacement is queued.
                    Resolve(ref g, position, goal, cls, enemy, now, settings, ref n, ref diagnostics);
                    n.NextDirectAt = 0; diagnostics.Replans++;
                }
                if (n.TravelState == NavigationTravelState.Failed && now >= n.NextRequestAt)
                    Resolve(ref g, position, goal, cls, enemy, now, settings, ref n, ref diagnostics);
                float arrival = math.max(.05f, goal.ArrivalDistance);
                if (n.TravelState == NavigationTravelState.Failed && now < n.NextRequestAt)
                { movement.ValueRW = default; diagnostics.Waiting++; continue; }
                bool arrived = math.distancesq(position, n.ResolvedGoal) <= arrival * arrival;
                if (arrived)
                {
                    n.Version += n.Pending; n.Pending = 0; path.Clear(); n.Waypoint = 0; n.TravelState = NavigationTravelState.Direct;
                    n.ProgressPosition = position; n.ProgressAt = now;
                    movement.ValueRW = SafeMovement(ref g, position, goal.Separation, goal.Speed, radius, settings, 0);
                    diagnostics.Direct++; continue;
                }
                if (now >= n.NextDirectAt)
                {
                    n.NextDirectAt = now + settings.DirectInterval;
                    if (NavigationGeometry.Segment(ref g, position, n.ResolvedGoal, radius))
                    { n.Version += n.Pending; n.Pending = 0; path.Clear(); n.Waypoint = 0; n.TravelState = NavigationTravelState.Direct; }
                    else if (n.TravelState == NavigationTravelState.Direct) n.TravelState = NavigationTravelState.Pending;
                }
                if (math.distancesq(position, n.ProgressPosition) >= settings.MinimumProgress * settings.MinimumProgress)
                { n.ProgressPosition = position; n.ProgressAt = now; }
                else if (now - n.ProgressAt >= settings.StuckDuration && now >= n.NextRequestAt && n.Pending == 0)
                { n.ProgressAt = now; path.Clear(); n.Waypoint = 0; n.TravelState = NavigationTravelState.Pending; diagnostics.Replans++; }
                // Teleports / recovery displacement invalidate stale corridors from the actual new position.
                if (math.distancesq(position, n.LastPosition) > math.max(4f, settings.LookAhead * settings.LookAhead * 16f))
                { n.Version++; n.Pending = 0; path.Clear(); n.Waypoint = 0; n.NextDirectAt = 0; n.TravelState = NavigationTravelState.Pending; }
                n.LastPosition = position;
                float2 target = n.ResolvedGoal; bool hasRoute = n.TravelState == NavigationTravelState.Direct;
                if (!hasRoute && path.Length > 0)
                {
                    while (n.Waypoint < path.Length - 1 && math.distance(position, path[n.Waypoint].Position) <= settings.WaypointArrival) n.Waypoint++;
                    int furthest = math.min(path.Length - 1, n.Waypoint + settings.SmoothingChecks);
                    for (int i = furthest; i > n.Waypoint; i--)
                        if (NavigationGeometry.Segment(ref g, position, path[i].Position, radius)) { n.Waypoint = i; break; }
                    target = path[math.min(n.Waypoint, path.Length - 1)].Position;
                    hasRoute = NavigationGeometry.Segment(ref g, position, target, radius);
                    if (!hasRoute) { path.Clear(); n.Waypoint = 0; diagnostics.Replans++; }
                    else n.TravelState = NavigationTravelState.Path;
                }
                if ((!hasRoute || n.PathVersion != n.Version) && n.TravelState != NavigationTravelState.Direct && n.Pending == 0 && now >= n.NextRequestAt)
                {
                    if (!NavigationGeometry.ResolveGoal(ref g, position, n.RequestedGoal, cls, enemy.Index, n.Kind, out var validGoal))
                    { Fail(now, settings, ref n, ref diagnostics, false); movement.ValueRW = default; continue; }
                    n.ResolvedGoal = validGoal;
                    int start = NavigationGeometry.Anchor(ref g, position, cls), end = NavigationGeometry.Anchor(ref g, validGoal, cls);
                    if (start >= 0 && end >= 0 && queue.Count < settings.MaxRequests)
                    {
                        n.Pending = 1;
                        queue.Enqueue(new NavigationSearchRequest { Enemy = enemy, Version = n.Version, Start = start, Goal = end, Clearance = cls, Destination = validGoal });
                    }
                    n.NextRequestAt = now + settings.RepathCooldown + Stagger(enemy, settings.Stagger);
                }
                if (hasRoute)
                {
                    float2 primary = math.normalizesafe(target - position);
                    float speed = goal.Speed;
                    float braking = SystemAPI.GetComponent<EnemyMovementSettings>(enemy).BrakingAcceleration;
                    if (n.TravelState == NavigationTravelState.Direct || n.Waypoint == path.Length - 1)
                        speed = math.min(speed, math.sqrt(2 * math.max(.1f, braking) * math.max(0, math.distance(position, target) - arrival * .5f)));
                    movement.ValueRW = SafeMovement(ref g, position, new float3(primary.x, 0, primary.y) + goal.Separation, speed, radius, settings, braking);
                    if (movement.ValueRO.Speed == 0)
                        movement.ValueRW = SafeMovement(ref g, position, new float3(primary.x, 0, primary.y), speed, radius, settings, braking);
                    if (n.TravelState == NavigationTravelState.Direct) diagnostics.Direct++; else diagnostics.Following++;
                }
                else { movement.ValueRW = default; diagnostics.Waiting++; }
            }
            if (settings.Enabled == 0) { queue.Clear(); for (int i = 0; i < storage.Slots.Length; i++) { var s = storage.Slots[i]; s.Active = 0; storage.Slots[i] = s; } }
            // Validate identity and goal generation at both admission and completion.
            for (int i = 0; i < storage.Slots.Length; i++)
            {
                var slot = storage.Slots[i];
                if (slot.Active != 0 && !Valid(system.EntityManager, slot.Request)) { slot.Active = 0; storage.Slots[i] = slot; diagnostics.StaleResults++; }
                if (storage.Slots[i].Active != 0) continue;
                int scanned = 0;
                while (queue.Count > 0 && scanned++ < settings.MaxRequests)
                {
                    var request = queue.Dequeue();
                    if (!Valid(system.EntityManager, request)) { diagnostics.StaleResults++; continue; }
                    storage.Begin(i, request, ref g); break;
                }
            }
            // The complete routing update is Burst compiled; no nested job or scratch copy is needed.
            diagnostics.Expanded = storage.ExpandBudget(ref g, settings.GlobalBudget, settings.SearchLimit);
            diagnostics.Searching = 0;
            for (int i = 0; i < storage.Slots.Length; i++)
            {
                var slot = storage.Slots[i]; if (slot.Active == 0) continue;
                if (slot.Result == NavigationSearchResult.Running) { diagnostics.Searching++; continue; }
                if (Valid(system.EntityManager, slot.Request))
                {
                    var n = system.EntityManager.GetComponentData<NavigationPathState>(slot.Request.Enemy);
                    var path = system.EntityManager.GetBuffer<NavigationWaypoint>(slot.Request.Enemy);
                    if (slot.Result == NavigationSearchResult.Found)
                    {
                        int length = 0, cell = slot.End;
                        while (cell >= 0 && length <= settings.MaxPathLength) { length++; cell = storage.GetNode(i, cell).Parent; }
                        if (length + 1 <= settings.MaxPathLength)
                        {
                            path.ResizeUninitialized(length + 1); path[length] = new NavigationWaypoint { Position = slot.Request.Destination }; cell = slot.End;
                            for (int p = length - 1; p >= 0; p--) { path[p] = new NavigationWaypoint { Position = NavigationGeometry.Center(ref g, cell) }; cell = storage.GetNode(i, cell).Parent; }
                            n.Pending = 0; n.Waypoint = 0; n.PathVersion = n.Version; n.TravelState = NavigationTravelState.Path;
                        }
                        else { path.Clear(); Fail(now, settings, ref n, ref diagnostics, true); }
                    }
                    else Fail(now, settings, ref n, ref diagnostics, slot.Result == NavigationSearchResult.ExpansionLimit);
                    system.EntityManager.SetComponentData(slot.Request.Enemy, n);
                }
                else diagnostics.StaleResults++;
                slot.Active = 0; storage.Slots[i] = slot;
            }
            diagnostics.Queued = queue.Count;
            SystemAPI.SetSingleton(diagnostics);
        }
        private static void Resolve(ref NavigationGridBlob g, float2 position, NavigationIntent intent, int cls, Entity enemy, double now,
            NavigationRuntimeSettings settings, ref NavigationPathState n, ref NavigationDiagnostics diagnostics)
        {
            if (NavigationGeometry.ResolveGoal(ref g, position, intent.Destination.xz, cls, enemy.Index, intent.Kind, out var resolved))
            { n.ResolvedGoal = resolved; n.TravelState = NavigationTravelState.Pending; }
            else { n.ResolvedGoal = position; Fail(now, settings, ref n, ref diagnostics, false); }
        }
        private static float Stagger(Entity e, float maximum) => (math.hash(new int2(e.Index, e.Version)) % 1024) / 1024f * maximum;
        public static bool TryGetOutOfGridDirectMovement(ref NavigationGridBlob g, float2 position,
            NavigationIntent goal, float radius, float braking, out DesiredMovement movement)
        {
            movement = default;
            if (goal.Mode != NavigationMode.Travel || goal.Kind != NavigationGoalKind.Position
                || NavigationGeometry.Cell(ref g, goal.Destination.xz) >= 0
                || !NavigationGeometry.ObstacleFreeSegment(ref g, position, goal.Destination.xz, radius))
                return false;

            float2 toGoal = goal.Destination.xz - position;
            float distance = math.length(toGoal);
            float arrival = math.max(.05f, goal.ArrivalDistance);
            if (distance <= arrival) return true;

            float speed = goal.Speed;
            if (braking > 0f)
                speed = math.min(speed, math.sqrt(2f * braking * math.max(0f, distance - arrival * .5f)));
            movement = new DesiredMovement
            {
                Direction = new float3(toGoal.x / distance, 0f, toGoal.y / distance),
                Speed = speed
            };
            return true;
        }
        private static void Fail(double now, NavigationRuntimeSettings s, ref NavigationPathState n, ref NavigationDiagnostics d, bool limited)
        { n.Pending = 0; n.TravelState = NavigationTravelState.Failed; n.NextRequestAt = now + s.FailureDelay + n.RequestOffset; n.NextDirectAt = n.NextRequestAt; if (limited) d.LimitReached++; else d.Failures++; }
        public static bool Valid(EntityManager em, NavigationSearchRequest request)
        {
            return em.Exists(request.Enemy) && em.HasComponent<NavigationPathState>(request.Enemy)
            && em.GetComponentData<NavigationPathState>(request.Enemy).Accepts(request.Version)
            && em.GetComponentData<EnemyLaunchState>(request.Enemy).Phase == EnemyLaunchPhase.Active
            && !em.IsComponentEnabled<RespawnRequest>(request.Enemy);
        }
        public static DesiredMovement SafeMovement(ref NavigationGridBlob g, float2 position, float3 direction, float speed, float radius,
            NavigationRuntimeSettings settings, float braking)
        {
            direction.y = 0; direction = math.normalizesafe(direction);
            if (speed <= 0 || math.lengthsq(direction) == 0) return default;
            float probe = math.max(settings.LookAhead, braking > 0 ? speed * speed / (2 * braking) + .15f : settings.LookAhead);
            if (!NavigationGeometry.Segment(ref g, position, position + direction.xz * probe, radius))
            {
                // Reduce speed before terrain, preserving direction. Never accept an unsafe short step.
                float safeDistance = 0f;
                for (int pass = 0; pass < 6; pass++)
                {
                    float candidate = (safeDistance + probe) * .5f;
                    if (NavigationGeometry.Segment(ref g, position, position + direction.xz * candidate, radius)) safeDistance = candidate;
                    else probe = candidate;
                }
                safeDistance = math.max(0f, safeDistance - .05f);
                if (safeDistance <= .02f) return default;
                speed = math.min(speed, math.sqrt(2 * math.max(.1f, braking) * safeDistance));
            }
            return new DesiredMovement { Direction = direction, Speed = speed };
        }
    }
}
