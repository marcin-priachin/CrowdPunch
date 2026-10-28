using System;
using CrowdPunch.Components;
using CrowdPunch.Systems.Groups;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Profiling;
using Unity.Transforms;

namespace CrowdPunch.Systems.AI
{
    /// <summary>COMBAT-016: stable pursuit and two interception groups, with concentrated regional reserves.</summary>
    [BurstCompile]
    [UpdateInGroup(typeof(GamePrePhysicsGroup))]
    [UpdateAfter(typeof(InputBridge.PlayerBridgeSystem))]
    [UpdateAfter(typeof(Initialization.EnemyWaveSpawnSystem))]
    [UpdateBefore(typeof(EnemyChaseSystem))]
    public partial struct EnemyCrowdTacticsSystem : ISystem
    {
        private double nextPlanAt;
        private Entity settingsOwner;
        private CrowdParticipation participation;
        private Entity playerRoom;
        private static readonly ProfilerMarker Marker = new ProfilerMarker("CrowdPunch.CrowdTactics");

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerSnapshot>();
            state.RequireForUpdate<ArenaBounds>();
            state.RequireForUpdate<EnemyCrowdPressureSettings>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            using var marker = Marker.Auto();
            var player = SystemAPI.GetSingleton<PlayerSnapshot>();
            var settings = SystemAPI.GetSingleton<EnemyCrowdPressureSettings>();
            var arena = SystemAPI.GetSingleton<ArenaBounds>();
            Entity owner = SystemAPI.GetSingletonEntity<EnemyCrowdPressureSettings>();
            Entity currentRoom = Entity.Null;
            foreach (var (room, entity) in SystemAPI.Query<RefRO<BarricadeRoom>>().WithEntityAccess())
                if (BarricadeRoomMovement.Contains(room.ValueRO, player.Position)) { currentRoom = entity; break; }
            bool suspended = !player.IsAvailable || SystemAPI.HasSingleton<ProtectedPoint>();
            if (owner != settingsOwner || participation != settings.Participation || currentRoom != playerRoom || suspended)
            {
                nextPlanAt = 0;
                settingsOwner = owner;
                participation = settings.Participation;
                playerRoom = currentRoom;
            }
            double now = SystemAPI.Time.ElapsedTime;
            bool plan = !suspended && now >= nextPlanAt;
            var owners = SystemAPI.GetComponentLookup<EnemyWaveOwnership>(true);
            var rooms = SystemAPI.GetComponentLookup<BarricadeRoom>(true);
            using var candidates = new NativeList<Candidate>(Allocator.Temp);

            foreach (var (assignment, transform, launch, archetype, attempt, entity) in
                SystemAPI.Query<RefRW<EnemyCrowdAssignment>, RefRO<LocalTransform>, RefRO<EnemyLaunchState>,
                    RefRO<EnemyArchetype>, RefRO<EnemyContactAttemptState>>().WithAll<Enemy>()
                    .WithOptions(EntityQueryOptions.IgnoreComponentEnabledState).WithEntityAccess())
            {
                ref var order = ref assignment.ValueRW;
                bool eligible = !suspended && archetype.ValueRO.Value == EnemyArchetypeKind.Baseline
                    && launch.ValueRO.Phase == EnemyLaunchPhase.Active
                    && !SystemAPI.IsComponentEnabled<RespawnRequest>(entity)
                    && !SystemAPI.HasComponent<SnakeHead>(entity) && !SystemAPI.HasComponent<SnakeSegment>(entity);
                if (!eligible) { order = default; continue; }

                bool hasRoom = BarricadeRoomMovement.TryGetRoom(entity, owners, rooms, out var room);
                var bounds = hasRoom ? BarricadeRoomMovement.Bounds(room) : arena;
                bool playerInRoom = !hasRoom || BarricadeRoomMovement.Contains(room, player.Position);
                if (!playerInRoom)
                {
                    order = Reserve(entity, transform.ValueRO.Position, bounds, settings);
                    continue;
                }

                // Broken groups do not receive replacements until the next plan. An active attack keeps its commitment.
                if (order.Role == EnemyCrowdRole.Interception)
                {
                    if (now >= order.InterceptUntil)
                        order.Role = EnemyCrowdRole.Pursuit;
                    else if (math.distancesq(transform.ValueRO.Position.xz, order.Destination.xz) <= 1f)
                    {
                        if (order.HasFlankWaypoint != 0)
                        {
                            order.Destination = order.InterceptionDestination;
                            order.HasFlankWaypoint = 0;
                        }
                        else order.Role = EnemyCrowdRole.Pursuit;
                    }
                }
                if (!plan)
                {
                    if (order.Initialized == 0) order = Reserve(entity, transform.ValueRO.Position, bounds, settings);
                    continue;
                }

                bool committed = attempt.ValueRO.IsAttempting != 0 || attempt.ValueRO.IsWindingUp != 0;
                float distanceSq = math.distancesq(transform.ValueRO.Position.xz, player.Position.xz);
                float radius = math.max(0f, settings.EngagementRadius)
                    + (order.Initialized != 0 && order.Role != EnemyCrowdRole.Reserve ? math.max(0f, settings.DisengagementMargin) : 0f);
                if (settings.Participation == CrowdParticipation.WholeArena || distanceSq <= radius * radius || committed)
                    candidates.Add(new Candidate { Enemy = entity, Position = transform.ValueRO.Position,
                        Bounds = bounds, DistanceSq = committed ? -1f : distanceSq, Committed = committed });
                else if (order.Initialized == 0 || order.Role != EnemyCrowdRole.Reserve)
                    order = Reserve(entity, transform.ValueRO.Position, bounds, settings);
            }
            if (!plan) return;

            candidates.AsArray().Sort();
            int pursuitCount = EnemyCrowdFormation.PursuitCount(candidates.Length, settings);
            var orders = SystemAPI.GetComponentLookup<EnemyCrowdAssignment>();
            int committedCount = 0;
            while (committedCount < candidates.Length && candidates[committedCount].Committed) committedCount++;
            pursuitCount = math.max(pursuitCount, committedCount);
            if (candidates.Length - pursuitCount < 2 * math.max(1, settings.MinimumInterceptionGroupSize))
                pursuitCount = candidates.Length;
            for (int index = 0; index < pursuitCount; index++)
                orders[candidates[index].Enemy] = new EnemyCrowdAssignment { Role = EnemyCrowdRole.Pursuit, Initialized = 1 };

            // Assign sides by existing position, rather than asking half the crowd to cross through the player.
            float2 forward = math.normalizesafe(player.Velocity.xz, math.normalizesafe(player.Forward.xz, new float2(0, 1)));
            float2 right = new float2(forward.y, -forward.x);
            var interceptors = candidates.AsArray().GetSubArray(pursuitCount, candidates.Length - pursuitCount);
            for (int index = 0; index < interceptors.Length; index++)
            {
                var candidate = interceptors[index];
                candidate.DistanceSq = math.dot(candidate.Position.xz - player.Position.xz, right);
                interceptors[index] = candidate;
            }
            interceptors.Sort();
            for (int index = 0; index < interceptors.Length; index++)
            {
                var candidate = interceptors[index];
                int side = index < (interceptors.Length + 1) / 2 ? -1 : 1;
                EnemyCrowdFormation.InterceptionTargets(candidate.Enemy.Index, side, player, settings, candidate.Bounds,
                    out float3 flank, out float3 destination);
                bool needsFlank = math.dot(candidate.Position.xz - player.Position.xz, right) * side < settings.FlankDistance * .5f;
                orders[candidate.Enemy] = new EnemyCrowdAssignment
                {
                    Initialized = 1, Role = EnemyCrowdRole.Interception,
                    Destination = needsFlank ? flank : destination, InterceptionDestination = destination,
                    HasFlankWaypoint = (byte)(needsFlank ? 1 : 0),
                    InterceptUntil = now + math.max(.1f, settings.CommitmentSeconds)
                };
            }
            nextPlanAt = candidates.Length == 0 ? now
                : now + math.max(.1f, settings.CommitmentSeconds) + math.max(.1f, settings.CloseInSeconds);
        }

        private static EnemyCrowdAssignment Reserve(Entity entity, float3 position, in ArenaBounds bounds,
            in EnemyCrowdPressureSettings settings) => new EnemyCrowdAssignment
        {
            Initialized = 1, Role = EnemyCrowdRole.Reserve,
            Destination = EnemyCrowdFormation.ReserveDestination(entity.Index, position, bounds, settings)
        };

        private struct Candidate : IComparable<Candidate>
        {
            public Entity Enemy;
            public float3 Position;
            public ArenaBounds Bounds;
            public float DistanceSq;
            public bool Committed;
            public int CompareTo(Candidate other)
            {
                int distance = DistanceSq.CompareTo(other.DistanceSq);
                return distance != 0 ? distance : Enemy.Index.CompareTo(other.Enemy.Index);
            }
        }
    }
}
