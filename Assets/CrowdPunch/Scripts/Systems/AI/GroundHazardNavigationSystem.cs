using CrowdPunch.Components;
using CrowdPunch.Systems.Groups;
using CrowdPunch.Systems.Movement;
using CrowdPunch.Utilities;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Profiling;
using Unity.Jobs;

namespace CrowdPunch.Systems.AI
{
    // Dynamic hazard overlay reuses the bounded terrain A* storage. It owns only voluntary
    // movement intent; patches never enter the collision world or alter launched velocity.
    [BurstCompile, UpdateInGroup(typeof(GamePrePhysicsGroup))]
    [UpdateAfter(typeof(EnemyNavigationSystem)), UpdateAfter(typeof(GroundHazardCycleSystem))]
    [UpdateBefore(typeof(EnemyMovementSystem)), UpdateBefore(typeof(DasherMovementSystem))]
    public partial struct GroundHazardNavigationSystem : ISystem
    {
        private NavigationSearchStorage storage;
        private NativeQueue<NavigationSearchRequest> requests;
        private BlobAssetReference<NavigationGridBlob> boundGrid;
        private uint revision, previousSignature;
        private uint edgeRevision;
        private NativeArray<byte> hazardEdges;
        private static readonly ProfilerMarker Marker = new ProfilerMarker("CrowdPunch.GroundHazardNavigation");
        public void OnCreate(ref SystemState state) => requests = new NativeQueue<NavigationSearchRequest>(Allocator.Persistent);
        public void OnDestroy(ref SystemState state) { storage.Dispose(); requests.Dispose(); if (hazardEdges.IsCreated) hazardEdges.Dispose(); }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            using var marker = Marker.Auto();
            var policy = SystemAPI.HasSingleton<GroundHazardPolicy>() ? SystemAPI.GetSingleton<GroundHazardPolicy>() : default;
            using var hazards = new NativeList<GroundHazard>(Allocator.TempJob);
            uint signature = 0;
            foreach (var (patch, status, entity) in SystemAPI.Query<RefRO<GroundHazard>, RefRO<GroundHazardState>>().WithEntityAccess())
            {
                if (status.ValueRO.Introduced == 0 || status.ValueRO.Phase == GroundHazardPhase.Inactive ||
                    status.ValueRO.Phase == GroundHazardPhase.Warning && policy.Avoidance == GroundHazardAvoidance.ActiveOnly) continue;
                hazards.Add(patch.ValueRO);
                signature ^= math.hash(new uint4((uint)entity.Index, (uint)entity.Version, status.ValueRO.RunGeneration, (uint)status.ValueRO.WaveIndex + 1));
            }
            if (signature != previousSignature) { previousSignature = signature; revision++; CancelSearches(); }
            if (hazards.Length == 0 || !SystemAPI.HasSingleton<NavigationGrid>())
            {
                CancelSearches();
                // Keep bounded scratch through inactive portions of a cycle. Release it on
                // arena unload, rather than allocating the full grid on every activation.
                if (!SystemAPI.HasSingleton<NavigationGrid>() && storage.Slots.IsCreated)
                { storage.Dispose(); storage = default; boundGrid = default; if (hazardEdges.IsCreated) hazardEdges.Dispose(); hazardEdges = default; }
                return;
            }
            var grid = SystemAPI.GetSingleton<NavigationGrid>();
            var settings = SystemAPI.GetSingleton<NavigationRuntimeSettings>();
            ref var g = ref grid.Data.Value;
            if (boundGrid != grid.Data)
            {
                storage.Dispose(); storage = new NavigationSearchStorage(g.Size.x * g.Size.y, settings.ConcurrentSearches);
                boundGrid = grid.Data; revision++; CancelSearches();
                if (hazardEdges.IsCreated) hazardEdges.Dispose();
                hazardEdges = new NativeArray<byte>(g.Size.x * g.Size.y * 3,Allocator.Persistent);
            }
            var patches = hazards.AsArray(); double now = SystemAPI.Time.ElapsedTime;
            if (edgeRevision != revision)
            {
                new EdgeOverlay { Grid = grid.Data, Hazards = patches, Edges = hazardEdges }.Schedule(hazardEdges.Length,64,state.Dependency).Complete();
                edgeRevision = revision;
            }
            state.Dependency = new RouteIntentJob
            {
                Grid = grid.Data, patches = patches, settings = settings, policy = policy, revision = revision, now = now, requests = requests,
                dashers = SystemAPI.GetComponentLookup<DasherState>(true),
                wizards = SystemAPI.GetComponentLookup<WizardCastState>(true),
                wizardSettings = SystemAPI.GetComponentLookup<WizardSettings>(true),
                reservations = SystemAPI.GetComponentLookup<ElitePunchReservation>(true),
                terrainRoutes = SystemAPI.GetComponentLookup<NavigationPathState>(true)
            }.Schedule(state.Dependency);
            state.Dependency.Complete();
            for (int i = 0; i < storage.Slots.Length; i++)
            {
                var slot = storage.Slots[i];
                if (slot.Active != 0 && !Valid(state.EntityManager, slot.Request)) { slot.Active = 0; storage.Slots[i] = slot; }
                if (slot.Active != 0) continue;
                int scanned = 0;
                while (requests.Count > 0 && scanned++ < settings.MaxRequests)
                { var request = requests.Dequeue(); if (Valid(state.EntityManager, request)) { storage.Begin(i, request, ref g); break; } }
            }
            storage.ExpandBudget(ref g, settings.GlobalBudget, settings.SearchLimit, default, hazardEdges);
            for (int i = 0; i < storage.Slots.Length; i++)
            {
                var slot = storage.Slots[i];
                if (slot.Active == 0 || slot.Result == NavigationSearchResult.Running) continue;
                if (Valid(state.EntityManager, slot.Request))
                {
                    var r = state.EntityManager.GetComponentData<GroundHazardRoute>(slot.Request.Enemy);
                    var path = state.EntityManager.GetBuffer<GroundHazardWaypoint>(slot.Request.Enemy); path.Clear();
                    // Exhausting a resource bound is not proof of no route. Both policies wait
                    // on limits; crossing is permitted only after an exhaustive unreachable result.
                    r.Pending = 0; r.Waypoint = 0; r.Failed = slot.Result == NavigationSearchResult.Unreachable ? (byte)1 : (byte)2;
                    r.RetryAt = now + settings.FailureDelay;
                    if (slot.Result == NavigationSearchResult.Found)
                    {
                        int length = 0, cell = slot.End;
                        while (cell >= 0 && length < settings.MaxPathLength) { length++; cell = storage.GetNode(i, cell).Parent; }
                        if (cell < 0 && length + 1 <= settings.MaxPathLength)
                        {
                            path.ResizeUninitialized(length + 1); path[length] = new GroundHazardWaypoint { Position = slot.Request.Destination }; cell = slot.End;
                            for (int p = length - 1; p >= 0; p--) { path[p] = new GroundHazardWaypoint { Position = NavigationGeometry.Center(ref g, cell) }; cell = storage.GetNode(i, cell).Parent; }
                            r.Failed = 0;
                        }
                    }
                    state.EntityManager.SetComponentData(slot.Request.Enemy, r);
                }
                slot.Active = 0; storage.Slots[i] = slot;
            }
        }
        private void CancelSearches()
        {
            requests.Clear();
            if (!storage.Slots.IsCreated) return;
            for (int i = 0; i < storage.Slots.Length; i++) { var s = storage.Slots[i]; s.Active = 0; storage.Slots[i] = s; }
        }
        [BurstCompile, WithAll(typeof(Enemy)), WithNone(typeof(RespawnRequest))]
        private partial struct RouteIntentJob : IJobEntity
        {
            public BlobAssetReference<NavigationGridBlob> Grid;
            [ReadOnly] public NativeArray<GroundHazard> patches;
            [ReadOnly] public ComponentLookup<DasherState> dashers;
            [ReadOnly] public ComponentLookup<WizardCastState> wizards;
            [ReadOnly] public ComponentLookup<WizardSettings> wizardSettings;
            [ReadOnly] public ComponentLookup<ElitePunchReservation> reservations;
            [ReadOnly] public ComponentLookup<NavigationPathState> terrainRoutes;
            public NativeQueue<NavigationSearchRequest> requests;
            public NavigationRuntimeSettings settings;
            public GroundHazardPolicy policy;
            public uint revision;
            public double now;
            private void Execute(Entity entity, in LocalTransform pose, in NavigationIntent intent,
                ref DesiredMovement movement, ref GroundHazardRoute route, DynamicBuffer<GroundHazardWaypoint> path,
                in NavigationAgent agent, in EnemyLaunchState launch, in EnemyMovementSettings movementSettings)
            {
                ref var g = ref Grid.Value;
                ref var r = ref route; var goal = intent; float2 position = pose.Position.xz;
                if (launch.Phase != EnemyLaunchPhase.Active || goal.Mode == NavigationMode.Committed ||
                    dashers.HasComponent(entity) && dashers[entity].Phase != DasherPhase.Positioning ||
                    wizards.HasComponent(entity) && wizards[entity].StopsMovement(wizardSettings[entity]) ||
                    reservations.HasComponent(entity) && reservations[entity].Owner != Entity.Null)
                { r.Version++; r.Pending = 0; path.Clear(); return; }
                int cls = NavigationGeometry.ClearanceClass(ref g, agent.Radius);
                if (cls < 0) { movement = default; return; }
                float radius = g.Radii[cls];
                float braking = movementSettings.BrakingAcceleration;
                if (r.Revision != revision)
                { r = new GroundHazardRoute { Version = r.Version + 1, Revision = revision, ProgressAt = now, LastPosition = position }; path.Clear(); }
                if (!GroundHazardGeometry.Clear(patches, position, position, radius))
                {
                    r.Version++; r.Pending = 0; r.Failed = 0; path.Clear();
                    movement = Escape(ref g, patches, position, radius, math.max(1.5f, goal.Speed));
                    return;
                }
                if (goal.Mode != NavigationMode.Travel)
                {
                    movement = Safe(ref g, patches, position, movement.Direction, movement.Speed, radius,
                        braking, settings.LookAhead);
                    return;
                }
                float2 destination = goal.Destination.xz;
                if (terrainRoutes.HasComponent(entity))
                {
                    var terrainRoute = terrainRoutes[entity];
                    if (terrainRoute.Initialized != 0) destination = terrainRoute.ResolvedGoal;
                }
                float brakingDistance = math.max(settings.LookAhead, math.square(movement.Speed) /
                    (2 * math.max(.1f, braking)) + .15f);
                if (GroundHazardGeometry.Clear(patches, position, destination, radius) &&
                    GroundHazardGeometry.Clear(patches, position, position + movement.Direction.xz * brakingDistance, radius))
                {
                    r.Version++; r.Pending = 0; path.Clear();
                    movement = Safe(ref g, patches, position, movement.Direction, movement.Speed, radius,
                        braking, settings.LookAhead);
                    return;
                }
                if (math.distancesq(r.Goal, destination) > math.square(settings.DestinationThreshold))
                { r.Version++; r.Pending = 0; r.Failed = 0; path.Clear(); r.RetryAt = now; }
                if (math.distancesq(position, r.LastPosition) >= math.square(settings.MinimumProgress))
                { r.ProgressAt = now; r.LastPosition = position; }
                else if (path.Length > 0 && now - r.ProgressAt > settings.StuckDuration)
                { r.Version++; r.Pending = 0; path.Clear(); r.RetryAt = now; r.ProgressAt = now; }
                bool following = false;
                if (path.Length > 0)
                {
                    while (r.Waypoint < path.Length - 1 && math.distance(position, path[r.Waypoint].Position) <= settings.WaypointArrival) r.Waypoint++;
                    for (int p = math.min(path.Length - 1, r.Waypoint + settings.SmoothingChecks); p > r.Waypoint; p--)
                        if (Segment(ref g, patches, position, path[p].Position, radius)) { r.Waypoint = p; break; }
                    float2 target = path[r.Waypoint].Position;
                    following = Segment(ref g, patches, position, target, radius);
                    if (!following) { path.Clear(); r.Version++; r.Pending = 0; r.RetryAt = now; }
                    else
                    {
                        float3 direction = new float3(target.x - position.x, 0, target.y - position.y);
                        float speed = goal.Speed;
                        if (r.Waypoint == path.Length - 1) speed = math.min(speed, math.sqrt(2 * math.max(.1f, braking) * math.max(0, math.distance(position, target) - .1f)));
                        movement = Safe(ref g, patches, position, math.normalizesafe(direction) + goal.Separation, speed, radius, braking, settings.LookAhead);
                        if (movement.Speed == 0) movement = Safe(ref g, patches, position, direction, speed, radius, braking, settings.LookAhead);
                    }
                }
                if (!following && r.Pending == 0 && now >= r.RetryAt && requests.Count < settings.MaxRequests)
                {
                    r.Goal = destination; r.Failed = 0;
                    int start = NavigationGeometry.Anchor(ref g, position, cls);
                    if (start >= 0 && GroundHazardGeometry.Clear(patches, position, NavigationGeometry.Center(ref g, start), radius) &&
                        ResolveGoal(ref g, patches, position, destination, cls, out float2 resolved, out int end))
                    {
                        r.Pending = 1;
                        requests.Enqueue(new NavigationSearchRequest { Enemy = entity, Version = r.Version, Start = start, Goal = end, Clearance = cls, Destination = resolved });
                    }
                    else { r.Failed = 2; r.RetryAt = now + settings.FailureDelay; }
                }
                if (!following && !(r.Failed == 1 && policy.Fallback == GroundHazardFallback.CrossAsLastResort)) movement = default;
            }
        }
        [BurstCompile]
        private struct EdgeOverlay : IJobParallelFor
        {
            public BlobAssetReference<NavigationGridBlob> Grid;
            [ReadOnly] public NativeArray<GroundHazard> Hazards;
            [WriteOnly] public NativeArray<byte> Edges;
            public void Execute(int index)
            {
                ref var g = ref Grid.Value; int count = g.Size.x * g.Size.y, cell = index % count, cls = index / count;
                byte bits = g.Edges[index]; float2 start = NavigationGeometry.Center(ref g,cell);
                for (int d = 0; d < 8; d++)
                    if ((bits & (1 << d)) != 0 && !GroundHazardGeometry.Clear(Hazards,start,
                        NavigationGeometry.Center(ref g,NavigationGeometry.Neighbor(ref g,cell,d)),g.Radii[cls])) bits &= (byte)~(1 << d);
                Edges[index] = bits;
            }
        }
        private static bool Valid(EntityManager em, NavigationSearchRequest request)
            => em.HasComponent<GroundHazardRoute>(request.Enemy) && em.GetComponentData<GroundHazardRoute>(request.Enemy).Version == request.Version &&
                em.GetComponentData<EnemyLaunchState>(request.Enemy).Phase == EnemyLaunchPhase.Active && !em.IsComponentEnabled<RespawnRequest>(request.Enemy);
        private static bool Segment(ref NavigationGridBlob g, NativeArray<GroundHazard> hazards, float2 a, float2 b, float radius)
            => NavigationGeometry.Segment(ref g, a, b, radius) && GroundHazardGeometry.Clear(hazards, a, b, radius);

        private static bool ResolveGoal(ref NavigationGridBlob g, NativeArray<GroundHazard> hazards, float2 position, float2 requested,
            int cls, out float2 goal, out int end)
        {
            goal = requested; end = NavigationGeometry.Anchor(ref g, requested, cls);
            if (end >= 0 && Segment(ref g, hazards, requested, NavigationGeometry.Center(ref g, end), g.Radii[cls])) return true;
            int start = NavigationGeometry.Anchor(ref g, position, cls), region = NavigationGeometry.Region(ref g, start, cls);
            float best = float.MaxValue;
            int2 center = math.clamp((int2)math.floor((requested - g.Minimum) / g.CellSize), int2.zero, g.Size - 1);
            for (int z = -10; z <= 10; z++) for (int x = -10; x <= 10; x++)
            {
                int2 c = center + new int2(x, z); if (math.any(c < 0) || math.any(c >= g.Size)) continue;
                int cell = c.y * g.Size.x + c.x;
                if (NavigationGeometry.Region(ref g, cell, cls) != region || region == 0) continue;
                float2 candidate = NavigationGeometry.Center(ref g, cell);
                if (!GroundHazardGeometry.Clear(hazards, candidate, candidate, g.Radii[cls])) continue;
                float score = math.distancesq(candidate, requested) + .001f * math.distancesq(candidate, position);
                if (score < best) { best = score; goal = candidate; end = cell; }
            }
            return best < float.MaxValue;
        }
        public static DesiredMovement Escape(ref NavigationGridBlob g, NativeArray<GroundHazard> hazards, float2 position, float radius, float speed)
        {
            float best = float.MaxValue; float2 selected = default;
            for (int i = 0; i < 32; i++)
            {
                float a = i * (math.PI * 2 / 32); float2 direction = new float2(math.cos(a), math.sin(a));
                float distance = 0;
                foreach (var h in hazards)
                    if (GroundHazardGeometry.Overlaps(h,position,radius))
                        distance = math.max(distance,GroundHazardGeometry.ExitDistance(h,position,direction,radius) + .1f);
                if (distance >= best) continue;
                float2 target = position + direction * distance;
                if (!NavigationGeometry.Segment(ref g, position, target, radius) || !GroundHazardGeometry.Clear(hazards, target, target, radius)) continue;
                bool clear = true;
                foreach (var h in hazards)
                    if (!GroundHazardGeometry.Overlaps(h, position, radius) && GroundHazardGeometry.Intersects(h, position, target, radius)) { clear = false; break; }
                if (clear) { best = distance; selected = direction; }
            }
            return best < float.MaxValue ? new DesiredMovement { Direction = new float3(selected.x,0,selected.y),Speed = speed } : default;
        }
        private static DesiredMovement Safe(ref NavigationGridBlob g, NativeArray<GroundHazard> hazards, float2 position, float3 direction,
            float speed, float radius, float braking, float lookAhead)
        {
            direction = math.normalizesafe(new float3(direction.x, 0, direction.z));
            float probe = math.max(lookAhead, speed * speed / (2 * math.max(.1f, braking)) + .15f);
            if (!Segment(ref g, hazards, position, position + direction.xz * probe, radius))
            {
                float safe = 0;
                for (int i = 0; i < 8; i++)
                {
                    float distance = (safe + probe) * .5f;
                    if (Segment(ref g, hazards, position, position + direction.xz * distance, radius)) safe = distance; else probe = distance;
                }
                speed = math.min(speed, math.sqrt(2 * math.max(.1f, braking) * math.max(0, safe - .05f)));
            }
            return new DesiredMovement { Direction = direction, Speed = speed };
        }
    }
}
