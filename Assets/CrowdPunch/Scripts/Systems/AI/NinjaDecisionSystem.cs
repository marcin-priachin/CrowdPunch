using CrowdPunch.Components;
using CrowdPunch.Systems.Groups;
using CrowdPunch.Utilities;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace CrowdPunch.Systems.AI
{
    [BurstCompile, UpdateInGroup(typeof(GamePrePhysicsGroup))]
    [UpdateAfter(typeof(EnemyChaseSystem)), UpdateBefore(typeof(EnemyNavigationSystem))]
    [UpdateBefore(typeof(Movement.EnemyMovementSystem))]
    public partial struct NinjaDecisionSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NinjaState>();
            state.RequireForUpdate<PlayerSnapshot>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var player = SystemAPI.GetSingleton<PlayerSnapshot>();
            double now = SystemAPI.Time.ElapsedTime;
            float dt = SystemAPI.Time.DeltaTime;
            var grid = SystemAPI.HasSingleton<NavigationGrid>() ? SystemAPI.GetSingleton<NavigationGrid>() : default;
            using var neighbors = new NativeList<EnemySeparationNeighbor>(Allocator.Temp);
            foreach (var (transform, launch, archetype) in SystemAPI.Query<RefRO<LocalTransform>, RefRO<EnemyLaunchState>, RefRO<EnemyArchetype>>()
                         .WithAll<Enemy>().WithNone<RespawnRequest>())
                if (launch.ValueRO.Phase == EnemyLaunchPhase.Active)
                    neighbors.Add(new EnemySeparationNeighbor { Position = transform.ValueRO.Position, Archetype = archetype.ValueRO.Value });
            int occupied = 0;
            foreach (var (ninja, launch) in SystemAPI.Query<RefRO<NinjaState>, RefRO<EnemyLaunchState>>().WithNone<RespawnRequest>())
                if (launch.ValueRO.Phase == EnemyLaunchPhase.Active && ninja.ValueRO.OccupiesAmbushSlot) occupied++;

            var owners = SystemAPI.GetComponentLookup<EnemyWaveOwnership>(true);
            var rooms = SystemAPI.GetComponentLookup<BarricadeRoom>(true);
            foreach (var (ninja, tuning, launch, transform, movement, navigation, entity) in
                     SystemAPI.Query<RefRW<NinjaState>, RefRO<NinjaTuning>, RefRO<EnemyLaunchState>, RefRO<LocalTransform>,
                         RefRW<DesiredMovement>, RefRW<NavigationIntent>>().WithAll<Enemy>().WithNone<RespawnRequest>().WithEntityAccess())
            {
                ref NinjaState n = ref ninja.ValueRW;
                NinjaTuning t = tuning.ValueRO;
                uint generation = SystemAPI.GetComponent<EnemyLifetime>(entity).Generation;
                if (n.Initialized == 0 || n.Generation != generation)
                {
                    n = new NinjaState { Initialized = 1, Generation = generation,
                        RandomState = math.hash(new uint3((uint)entity.Index + 1, (uint)entity.Version + 1, generation)) | 1u };
                    n.ReadyAt = n.PeriodicAt = now + math.max(0, t.InitialDelay);
                }
                bool wasSlot = n.OccupiesAmbushSlot;
                movement.ValueRW = default;
                navigation.ValueRW = default;
                if (launch.ValueRO.Phase != EnemyLaunchPhase.Active || !player.IsAvailable)
                {
                    if (n.WasInactive == 0 || n.ObservedLaunchSequence != launch.ValueRO.LaunchSequence)
                        NinjaScheduling.Interrupt(ref n, t, now);
                    n.ObservedLaunchSequence = launch.ValueRO.LaunchSequence;
                    n.WasInactive = 1;
                    if (wasSlot) occupied--;
                    continue;
                }
                if (n.WasInactive != 0)
                {
                    NinjaScheduling.RestartCooldown(ref n, t, now);
                    n.WasInactive = 0;
                }
                float3 delta = player.Position - transform.ValueRO.Position;
                delta.y = 0;
                float distance = math.length(delta);
                bool inside = distance <= t.ProximityDistance;
                if (inside && n.WasWithinProximity == 0) n.ProximityReady = 1;
                n.WasWithinProximity = inside ? (byte)1 : (byte)0;
                if (n.Phase == NinjaPhase.Preparing || n.Phase == NinjaPhase.ArrivalWarning || n.Phase == NinjaPhase.Recovering)
                {
                    n.SecondsRemaining -= dt;
                    if (n.SecondsRemaining <= .000001f)
                    {
                        if (n.Phase == NinjaPhase.Preparing) n.Phase = NinjaPhase.TeleportPending;
                        else if (n.Phase == NinjaPhase.ArrivalWarning) n.Phase = NinjaPhase.Dashing;
                        else n.Phase = NinjaPhase.Positioning;
                    }
                }
                if (n.Phase != NinjaPhase.Positioning) continue;

                bool hasRoom = BarricadeRoomMovement.TryGetRoom(entity, owners, rooms, out var room);
                bool inRoom = !hasRoom || BarricadeRoomMovement.Contains(room, player.Position);
                if (inRoom && distance <= t.EngagementDistance && NinjaScheduling.Ready(n, t, now))
                {
                    if (occupied < math.max(1, t.MaximumConcurrentAmbushes))
                    {
                        n.Phase = NinjaPhase.Preparing;
                        n.SecondsRemaining = math.max(0, t.PreparationDuration);
                        occupied++;
                        continue;
                    }
                    n.RetryAt = now + NinjaScheduling.RandomDelay(ref n, t.SlotRetryMinimum, t.SlotRetryMaximum);
                }

                float3 toward = math.normalizesafe(delta);
                float minimum = math.min(t.PreferredMinimumDistance, t.PreferredMaximumDistance);
                float maximum = math.max(t.PreferredMinimumDistance, t.PreferredMaximumDistance);
                float3 direction = distance < minimum ? -toward : distance > maximum ? toward : float3.zero;
                var motor = SystemAPI.GetComponent<EnemyMovementSettings>(entity);
                var spacing = SystemAPI.GetComponent<EnemySeparationDistance>(entity);
                var overrides = SystemAPI.GetComponent<EnemyArchetypeSeparationDistances>(entity);
                float3 separation = float3.zero;
                foreach (var other in neighbors)
                {
                    float3 away = transform.ValueRO.Position - other.Position; away.y = 0;
                    float length = math.length(away);
                    float limit = overrides.GetDistance(other.Archetype, spacing.Value);
                    if (length > .001f && length < limit)
                        separation += away / length * (1 - length / limit) * overrides.GetWeight(other.Archetype, motor.SeparationWeight);
                }
                float speed = distance < minimum ? t.RetreatSpeed : distance > maximum ? t.ApproachSpeed : motor.WanderSpeed;
                movement.ValueRW = new DesiredMovement { Direction = math.normalizesafe(direction + separation), Speed = speed };
                float radius = SystemAPI.GetComponent<NavigationAgent>(entity).Radius;
                float3 destination = NavigationGeometry.DistanceBandDestination(grid, transform.ValueRO.Position, player.Position, minimum, maximum, radius);
                destination.y = transform.ValueRO.Position.y;
                if (hasRoom)
                {
                    var bounds = BarricadeRoomMovement.Bounds(room);
                    destination.xz = math.clamp(destination.xz, bounds.Center.xz - bounds.Extents.xz, bounds.Center.xz + bounds.Extents.xz);
                }
                navigation.ValueRW = NavigationIntent.Travel(destination, speed, .35f, separation, NavigationGoalKind.DistanceBand);
                if (math.lengthsq(direction) < .0001f) navigation.ValueRW.Mode = NavigationMode.Hold;
            }
        }
    }
}
