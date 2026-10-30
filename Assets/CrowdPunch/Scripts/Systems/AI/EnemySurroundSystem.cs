using System;
using CrowdPunch.Components;
using CrowdPunch.Systems.Groups;
using CrowdPunch.Utilities;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Profiling;
using Unity.Transforms;

namespace CrowdPunch.Systems.AI
{
    /// <summary>COMBAT-016: exclusive surround positions, with open lanes preferred for approach.</summary>
    [BurstCompile, UpdateInGroup(typeof(GamePrePhysicsGroup))]
    [UpdateAfter(typeof(EnemyCrowdTacticsSystem)), UpdateBefore(typeof(EnemyChaseSystem))]
    public partial struct EnemySurroundSystem : ISystem
    {
        private double nextUpdateAt;
        private float2 previousPlayer;
        private Entity settingsOwner;
        private bool formed;
        private float baseAngle;
        private static readonly ProfilerMarker Marker = new ProfilerMarker("CrowdPunch.Surround");

        [BurstCompile] public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerSnapshot>();
            state.RequireForUpdate<EnemyCrowdPressureSettings>();
            state.RequireForUpdate<ArenaBounds>();
        }

        [BurstCompile] public void OnUpdate(ref SystemState state)
        {
            using var marker = Marker.Auto();
            var player = SystemAPI.GetSingleton<PlayerSnapshot>();
            var settings = SystemAPI.GetSingleton<EnemyCrowdPressureSettings>();
            var arena = SystemAPI.GetSingleton<ArenaBounds>();
            var owner = SystemAPI.GetSingletonEntity<EnemyCrowdPressureSettings>();
            double now = SystemAPI.Time.ElapsedTime;
            float refreshDistance = settings.SurroundMovementRefreshDistance > 0 ? settings.SurroundMovementRefreshDistance : 1.25f;
            bool suspended = !player.IsAvailable || SystemAPI.HasSingleton<ProtectedPoint>();
            bool refresh = now >= nextUpdateAt || owner != settingsOwner
                || math.distancesq(previousPlayer, player.Position.xz) > refreshDistance * refreshDistance;
            if (suspended) { nextUpdateAt = 0; formed = false; }
            if (owner != settingsOwner) formed = false;
            settingsOwner = owner;
            var owners = SystemAPI.GetComponentLookup<EnemyWaveOwnership>(true);
            var rooms = SystemAPI.GetComponentLookup<BarricadeRoom>(true);
            var agents = SystemAPI.GetComponentLookup<NavigationAgent>(true);
            var directPursuers = SystemAPI.GetComponentLookup<DirectContactPursuit>(true);
            using var actors = new NativeList<Actor>(Allocator.Temp);

            foreach (var (surround, crowd, transform, launch, movement, attempt, entity) in
                SystemAPI.Query<RefRW<EnemySurroundAssignment>, RefRO<EnemyCrowdAssignment>, RefRO<LocalTransform>,
                    RefRO<EnemyLaunchState>, RefRO<EnemyMovementSettings>, RefRO<EnemyContactAttemptState>>()
                    .WithAll<Enemy>().WithOptions(EntityQueryOptions.IgnoreComponentEnabledState).WithEntityAccess())
            {
                bool hasRoom = BarricadeRoomMovement.TryGetRoom(entity, owners, rooms, out var room);
                bool eligible = !suspended && crowd.ValueRO.Initialized != 0 && crowd.ValueRO.Role == EnemyCrowdRole.Pursuit
                    && launch.ValueRO.Phase == EnemyLaunchPhase.Active && !SystemAPI.IsComponentEnabled<RespawnRequest>(entity)
                    && SystemAPI.GetComponent<EnemyArchetype>(entity).Value == EnemyArchetypeKind.Baseline
                    && !SystemAPI.HasComponent<SnakeHead>(entity) && !SystemAPI.HasComponent<SnakeSegment>(entity)
                    && (!directPursuers.HasComponent(entity) || !directPursuers.IsComponentEnabled(entity))
                    && (!hasRoom || BarricadeRoomMovement.Contains(room, player.Position));
                // Local pillar groups retain their dedicated positioning override.
                if (SystemAPI.HasComponent<PillarCrowdMember>(entity))
                {
                    var member = SystemAPI.GetComponent<PillarCrowdMember>(entity);
                    if (SystemAPI.HasComponent<FallingPillar>(member.Pillar))
                    {
                        var pillar = SystemAPI.GetComponent<FallingPillar>(member.Pillar);
                        if (SystemAPI.HasComponent<DinoBoss>(pillar.Boss)
                            && Initialization.PillarCrowdPlacement.Needed(pillar, SystemAPI.GetComponent<DinoBoss>(pillar.Boss),
                                SystemAPI.GetComponent<DinoTuning>(pillar.Boss))) eligible = false;
                    }
                }
                if (!eligible) { surround.ValueRW = default; continue; }
                if (!refresh) continue;
                actors.Add(new Actor
                {
                    Entity = entity, Position = transform.ValueRO.Position.xz,
                    Bounds = hasRoom ? BarricadeRoomMovement.Bounds(room) : arena,
                    Movement = movement.ValueRO, Previous = surround.ValueRO,
                    Radius = agents.HasComponent(entity) ? math.max(.2f, agents[entity].Radius) : .5f,
                    Committed = attempt.ValueRO.IsAttempting != 0 || attempt.ValueRO.IsWindingUp != 0,
                    DistanceSq = math.distancesq(transform.ValueRO.Position.xz, player.Position.xz)
                });
            }
            if (suspended || !refresh) return;
            previousPlayer = player.Position.xz;
            nextUpdateAt = actors.Length == 0 ? now
                : now + (settings.SurroundReassignmentSeconds > 0 ? settings.SurroundReassignmentSeconds : .5f);
            if (actors.Length == 0) return;
            actors.AsArray().Sort();
            if (!formed)
            {
                float2 offset = actors[0].Position - player.Position.xz;
                baseAngle = math.atan2(offset.y, offset.x);
                formed = true;
            }

            using var bodies = new NativeList<Body>(Allocator.Temp);
            float maxRadius = .5f;
            foreach (var (pose, launch, entity) in SystemAPI.Query<RefRO<LocalTransform>, RefRO<EnemyLaunchState>>()
                .WithAll<Enemy>().WithOptions(EntityQueryOptions.IgnoreComponentEnabledState).WithEntityAccess())
            {
                if (SystemAPI.IsComponentEnabled<RespawnRequest>(entity)
                    || launch.ValueRO.Phase != EnemyLaunchPhase.Active && launch.ValueRO.Phase != EnemyLaunchPhase.Recovering) continue;
                float radius = agents.HasComponent(entity) ? math.max(.2f, agents[entity].Radius) : .5f;
                maxRadius = math.max(maxRadius, radius);
                bodies.Add(new Body { Entity = entity, Position = pose.ValueRO.Position.xz, Radius = radius });
            }
            float cellSize = math.max(1f, maxRadius * 2f);
            using var occupied = new NativeParallelMultiHashMap<int2, Body>(math.max(1, bodies.Length), Allocator.Temp);
            foreach (var body in bodies) occupied.Add(Cell(body.Position, cellSize), body);
            using var reserved = new NativeParallelMultiHashMap<int2, Body>(actors.Length, Allocator.Temp);
            using var slots = new NativeList<Slot>(Allocator.Temp);
            float spacing = math.max(1f, math.max(actors[0].Radius * 2f,
                actors[0].Movement.SeparationDistanceMin));
            // Complete inner rings before opening outer rings. A small crowd gets a full-circle distribution too.
            for (int ring = 0; ring < 64 && slots.Length < actors.Length * 3 + 32; ring++)
            {
                float radius = Radius(actors[0], ring, spacing, player.Radius);
                int count = math.clamp((int)math.floor(2f * math.PI * radius / spacing), 1, 128);
                if (ring == 0 && actors.Length > 1) count = math.min(count, actors.Length);
                for (int index = 0; index < count; index++)
                {
                    float angle = baseAngle + (index + (ring % 2 == 0 ? 0f : .5f)) * (2f * math.PI / count);
                    slots.Add(new Slot { Direction = new float2(math.cos(angle), math.sin(angle)), Ring = ring });
                }
            }
            using var claimed = new NativeArray<byte>(slots.Length, Allocator.Temp);
            using var pending = new NativeList<Actor>(Allocator.Temp);
            var assignments = SystemAPI.GetComponentLookup<EnemySurroundAssignment>();
            var grid = SystemAPI.HasSingleton<NavigationGrid>() ? SystemAPI.GetSingleton<NavigationGrid>() : default;

            // Keep valid reservations first; bodies already near the player cannot have their place stolen by a queue.
            foreach (var actor in actors)
            {
                int index = actor.Previous.Slot;
                bool retained = actor.Previous.Assigned != 0 && index >= 0 && index < slots.Length
                    && math.distancesq(actor.Previous.Offset, Offset(actor, slots[index], spacing, player.Radius)) < .01f
                    && Available(actor, slots[index], claimed[index], player, spacing, cellSize, maxRadius, occupied, reserved, grid);
                if (retained && slots[index].Ring > 0)
                    for (int inner = 0; inner < index && slots[inner].Ring < slots[index].Ring; inner++)
                        if (Available(actor, slots[inner], claimed[inner], player, spacing, cellSize, maxRadius, occupied, reserved, grid))
                        { retained = false; break; }
                if (retained && !actor.Committed)
                {
                    float2 point = player.Position.xz + Offset(actor, slots[index], spacing, player.Radius);
                    if (Blocked(point, player.Position.xz, actor.Entity, actor.Radius, cellSize, maxRadius, occupied)) retained = false;
                }
                if (retained)
                    Assign(actor, index, slots[index], player, spacing, cellSize, reserved, claimed, assignments);
                else pending.Add(actor);
            }
            foreach (var actor in pending)
            {
                int best = -1, ring = -1; float bestDistance = float.MaxValue; bool bestClear = false;
                for (int index = 0; index < slots.Length; index++)
                {
                    if (ring >= 0 && slots[index].Ring != ring) break;
                    if (!Available(actor, slots[index], claimed[index], player, spacing, cellSize, maxRadius, occupied, reserved, grid)) continue;
                    float2 point = player.Position.xz + Offset(actor, slots[index], spacing, player.Radius);
                    bool clear = !Blocked(point, player.Position.xz, actor.Entity, actor.Radius, cellSize, maxRadius, occupied);
                    float distance = math.distancesq(actor.Position, point);
                    if (best < 0 || clear && !bestClear || clear == bestClear && distance < bestDistance)
                    { best = index; ring = slots[index].Ring; bestDistance = distance; bestClear = clear; }
                }
                if (best >= 0)
                    Assign(actor, best, slots[best], player, spacing, cellSize, reserved, claimed, assignments);
                else assignments[actor.Entity] = new EnemySurroundAssignment
                { Assigned = 1, Slot = -1, Offset = actor.Position - player.Position.xz };
            }
        }

        private static float Radius(in Actor actor, int ring, float spacing, float playerRadius) =>
            math.max(actor.Movement.SurroundDistance, playerRadius + actor.Radius + .5f)
                + ring * math.max(spacing, actor.Movement.SurroundRingSpacing);
        private static float2 Offset(in Actor actor, in Slot slot, float spacing, float playerRadius) =>
            slot.Direction * Radius(actor, slot.Ring, spacing, playerRadius);
        private static int2 Cell(float2 point, float size) => (int2)math.floor(point / size);

        private static bool Available(in Actor actor, in Slot slot, byte claimed, in PlayerSnapshot player,
            float spacing, float cellSize, float maxRadius, in NativeParallelMultiHashMap<int2, Body> occupied,
            in NativeParallelMultiHashMap<int2, Body> reserved, in NavigationGrid grid)
        {
            if (claimed != 0) return false;
            float2 point = player.Position.xz + Offset(actor, slot, spacing, player.Radius);
            float2 extent = math.max(float2.zero, actor.Bounds.Extents.xz - actor.Radius - .25f);
            if (math.any(point < actor.Bounds.Center.xz - extent) || math.any(point > actor.Bounds.Center.xz + extent)) return false;
            if (Blocked(point, point, actor.Entity, actor.Radius, cellSize, maxRadius, occupied)) return false;
            if (Blocked(point, point, actor.Entity, spacing * .45f, cellSize, spacing * .45f, reserved)) return false;
            if (!grid.Data.IsCreated) return true;
            ref var g = ref grid.Data.Value;
            int cls = NavigationGeometry.ClearanceClass(ref g, actor.Radius);
            int target = NavigationGeometry.Region(ref g, NavigationGeometry.Anchor(ref g, point, cls), cls);
            if (target == 0) return false;
            int current = NavigationGeometry.Region(ref g, NavigationGeometry.Anchor(ref g, actor.Position, cls), cls);
            return current == 0 || current == target;
        }

        private static void Assign(in Actor actor, int index, in Slot slot, in PlayerSnapshot player,
            float spacing, float cellSize,
            NativeParallelMultiHashMap<int2, Body> reserved, NativeArray<byte> claimed,
            ComponentLookup<EnemySurroundAssignment> assignments)
        {
            float2 offset = Offset(actor, slot, spacing, player.Radius);
            float2 point = player.Position.xz + offset;
            claimed[index] = 1;
            reserved.Add(Cell(point, cellSize), new Body { Entity = actor.Entity, Position = point, Radius = spacing * .45f });
            assignments[actor.Entity] = new EnemySurroundAssignment
            { Assigned = 1, Slot = index, Offset = offset };
        }

        private static bool Blocked(float2 start, float2 end, Entity self, float radius, float cellSize,
            float maxRadius, in NativeParallelMultiHashMap<int2, Body> bodies)
        {
            float2 delta = end - start;
            float lengthSq = math.lengthsq(delta);
            int steps = math.max(1, (int)math.ceil(math.sqrt(lengthSq) / cellSize));
            int reach = math.max(1, (int)math.ceil((radius + maxRadius) / cellSize));
            for (int step = 0; step <= steps; step++)
            {
                int2 cell = Cell(math.lerp(start, end, (float)step / steps), cellSize);
                for (int x = -reach; x <= reach; x++) for (int y = -reach; y <= reach; y++)
                {
                    if (!bodies.TryGetFirstValue(cell + new int2(x, y), out var body, out var iterator)) continue;
                    do
                    {
                        if (body.Entity == self) continue;
                        float t = lengthSq > .0001f ? math.saturate(math.dot(body.Position - start, delta) / lengthSq) : 0;
                        if (lengthSq > .0001f && (t <= 0 || t >= 1)) continue;
                        if (math.distancesq(start + delta * t, body.Position) < math.square(radius + body.Radius)) return true;
                    } while (bodies.TryGetNextValue(out body, ref iterator));
                }
            }
            return false;
        }

        private struct Slot { public float2 Direction; public int Ring; }
        private struct Body { public Entity Entity; public float2 Position; public float Radius; }
        private struct Actor : IComparable<Actor>
        {
            public Entity Entity;
            public float2 Position;
            public float Radius, DistanceSq;
            public ArenaBounds Bounds;
            public EnemyMovementSettings Movement;
            public EnemySurroundAssignment Previous;
            public bool Committed;
            public int CompareTo(Actor other)
            {
                int priority = other.Committed.CompareTo(Committed);
                if (priority != 0) return priority;
                int distance = DistanceSq.CompareTo(other.DistanceSq);
                return distance != 0 ? distance : Entity.Index.CompareTo(other.Entity.Index);
            }
        }
    }
}
