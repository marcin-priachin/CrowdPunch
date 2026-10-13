using CrowdPunch.Components;
using CrowdPunch.Systems.AI;
using CrowdPunch.Systems.Groups;
using CrowdPunch.Utilities;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace CrowdPunch.Systems.Physics
{
    [BurstCompile, UpdateInGroup(typeof(GamePrePhysicsGroup))]
    [UpdateAfter(typeof(NinjaDecisionSystem)), UpdateAfter(typeof(AI.GroundHazardCycleSystem))]
    [UpdateAfter(typeof(Movement.EnemyMovementSystem)), UpdateBefore(typeof(Movement.NinjaMovementSystem))]
    public partial struct NinjaTeleportSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NinjaState>(); state.RequireForUpdate<PlayerSnapshot>();
            state.RequireForUpdate<PhysicsWorldSingleton>(); state.RequireForUpdate<ArenaBounds>();
        }
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var player = SystemAPI.GetSingleton<PlayerSnapshot>();
            if (!player.IsAvailable) return;
            bool pending = false;
            foreach (var ninja in SystemAPI.Query<RefRO<NinjaState>>().WithNone<RespawnRequest>())
                if (ninja.ValueRO.Phase == NinjaPhase.TeleportPending) { pending = true; break; }
            if (!pending) return;
            var grid = SystemAPI.HasSingleton<NavigationGrid>() ? SystemAPI.GetSingleton<NavigationGrid>() : default;
            var bounds = SystemAPI.GetSingleton<ArenaBounds>();
            var world = SystemAPI.GetSingleton<PhysicsWorldSingleton>().CollisionWorld;
            var enemies = SystemAPI.GetComponentLookup<Enemy>(true);
            var owners = SystemAPI.GetComponentLookup<EnemyWaveOwnership>(true);
            var rooms = SystemAPI.GetComponentLookup<BarricadeRoom>(true);
            using var occupied = new NativeList<NinjaTeleportOccupant>(Allocator.Temp);
            using var hazards = new NativeList<GroundHazard>(Allocator.Temp);
            foreach (var (transform, agent, entity) in SystemAPI.Query<RefRO<LocalTransform>, RefRO<NavigationAgent>>()
                         .WithAll<Enemy>().WithNone<RespawnRequest>().WithEntityAccess())
                occupied.Add(new NinjaTeleportOccupant { Entity = entity, Position = transform.ValueRO.Position,
                    Radius = agent.ValueRO.Radius, HalfHeight = math.max(1, agent.ValueRO.Radius) });
            foreach (var (warning, entity) in SystemAPI.Query<RefRO<EnemySpawnWarning>>().WithOptions(EntityQueryOptions.IncludeDisabledEntities).WithEntityAccess())
                occupied.Add(new NinjaTeleportOccupant { Entity = entity, Position = warning.ValueRO.Position,
                    Radius = warning.ValueRO.Radius, PlanarReservation = 1 });
            foreach (var (hazard, status) in SystemAPI.Query<RefRO<GroundHazard>, RefRO<GroundHazardState>>())
                if (status.ValueRO.Introduced != 0 && status.ValueRO.Phase != GroundHazardPhase.Inactive) hazards.Add(hazard.ValueRO);
            foreach (var zone in SystemAPI.Query<RefRO<WizardZone>>())
                if (zone.ValueRO.Active != 0)
                    hazards.Add(new GroundHazard { Shape = GroundHazardShape.Circle, Position = zone.ValueRO.Position, Radius = zone.ValueRO.Radius });
            foreach (var trail in SystemAPI.Query<RefRO<TrailSection>>())
            {
                var section = trail.ValueRO;
                if (section.DamagesEnemies == 0 || section.ExpiresAt <= SystemAPI.Time.ElapsedTime) continue;
                float2 delta = section.End.xz - section.Start.xz;
                hazards.Add(new GroundHazard { Shape = GroundHazardShape.Rectangle, Position = (section.Start + section.End) * .5f,
                    HalfSize = new float2((math.length(delta) + section.Width) * .5f, section.Width * .5f), Angle = -math.atan2(delta.y, delta.x) });
            }
            foreach (var (ninja, tuning, launch, transform, velocity, agent, entity) in
                     SystemAPI.Query<RefRW<NinjaState>, RefRO<NinjaTuning>, RefRO<EnemyLaunchState>, RefRW<LocalTransform>, RefRW<PhysicsVelocity>, RefRO<NavigationAgent>>()
                         .WithNone<RespawnRequest>().WithEntityAccess())
            {
                if (launch.ValueRO.Phase != EnemyLaunchPhase.Active || ninja.ValueRO.Phase != NinjaPhase.TeleportPending) continue;
                ref var n = ref ninja.ValueRW;
                float radius = agent.ValueRO.Radius;
                float3 origin = transform.ValueRO.Position;
                bool hasRoom = BarricadeRoomMovement.TryGetRoom(entity, owners, rooms, out var room);
                bool found = false;
                for (int attempt = 0; attempt <= 2 * math.clamp(tuning.ValueRO.RearSearchSteps, 1, 16); attempt++)
                {
                    float3 candidate = NinjaTeleportGeometry.Candidate(player, tuning.ValueRO, radius, attempt, origin.y);
                    if (hasRoom && (!BarricadeRoomMovement.Contains(room, candidate) || !BarricadeRoomMovement.Contains(room, player.Position))) continue;
                    if (!NinjaTeleportGeometry.Valid(grid, bounds, entity, origin, candidate, player, radius, occupied.AsArray(), hazards.AsArray())) continue;
                    float3 toward = player.Position - candidate; toward.y = 0;
                    float distance = math.length(toward);
                    float3 direction = math.normalizesafe(toward);
                    if (!NinjaSolidSweep.Clear(world, enemies, entity, candidate, radius)
                        || NinjaSolidSweep.Distance(world, enemies, entity, candidate, direction, distance, radius) < distance - .001f) continue;
                    n.DeparturePosition = origin;
                    n.PreviousPosition = candidate;
                    n.LockedDirection = direction;
                    n.DistanceTravelled = 0; n.HasHitPlayer = 0; n.HasDashSegment = 0;
                    n.TeleportSequence++;
                    n.Phase = NinjaPhase.ArrivalWarning;
                    n.SecondsRemaining = math.max(0, tuning.ValueRO.ArrivalWarningDuration);
                    transform.ValueRW.Position = candidate; // The sole discontinuity, after validating the full destination.
                    transform.ValueRW.Rotation = quaternion.LookRotationSafe(direction, math.up());
                    velocity.ValueRW = default;
                    if (SystemAPI.HasComponent<NavigationPathState>(entity))
                        SystemAPI.SetComponent(entity, default(NavigationPathState));
                    if (SystemAPI.HasBuffer<NavigationWaypoint>(entity)) SystemAPI.GetBuffer<NavigationWaypoint>(entity).Clear();
                    var occupants = occupied.AsArray();
                    for (int i = 0; i < occupants.Length; i++)
                        if (occupied[i].Entity == entity)
                            occupants[i] = new NinjaTeleportOccupant { Entity = entity, Position = candidate, Radius = radius, HalfHeight = 1 };
                    found = true;
                    break;
                }
                if (found) continue;
                n.Phase = NinjaPhase.Positioning;
                n.RetryAt = SystemAPI.Time.ElapsedTime + NinjaScheduling.RandomDelay(ref n,
                    tuning.ValueRO.DestinationRetryMinimum, tuning.ValueRO.DestinationRetryMaximum);
            }
        }
    }
}

