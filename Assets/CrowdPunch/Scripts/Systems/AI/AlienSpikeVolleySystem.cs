using CrowdPunch.Authoring;
using CrowdPunch.Components;
using CrowdPunch.Systems.Combat;
using CrowdPunch.Systems.Groups;
using CrowdPunch.Utilities;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace CrowdPunch.Systems.AI
{
    [BurstCompile]
    [UpdateInGroup(typeof(GamePrePhysicsGroup))]
    [UpdateAfter(typeof(EnemyChaseSystem)), UpdateAfter(typeof(EliteCrowdSupportSystem))]
    // Navigation and movement must run before punch detection. Emission rechecks launch state after physics.
    [UpdateBefore(typeof(EnemyNavigationSystem))]
    public partial struct AlienSpikeVolleySystem : ISystem
    {
        private const double CoverSearchInterval = .2;
        private const float CoverArrivalDistance = .1f;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<AlienSpikeTuning>();
            state.RequireForUpdate<PlayerSnapshot>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var player = SystemAPI.GetSingleton<PlayerSnapshot>();
            SystemAPI.TryGetSingleton<NavigationGrid>(out var grid);
            if (SystemAPI.TryGetSingleton<NavigationRuntimeSettings>(out var navigationSettings) && navigationSettings.Enabled == 0)
                grid = default;
            bool hasPhysics = SystemAPI.TryGetSingleton<PhysicsWorldSingleton>(out var physics);
            bool hasGround = SystemAPI.TryGetSingleton<ArenaGroundPlane>(out var ground);
            SystemAPI.TryGetSingletonBuffer<EnemyAttackAudioEvent>(out var audio);
            var enemies = SystemAPI.GetComponentLookup<Enemy>(true);
            var owners = SystemAPI.GetComponentLookup<EnemyWaveOwnership>(true);
            var rooms = SystemAPI.GetComponentLookup<BarricadeRoom>(true);
            float dt = SystemAPI.Time.DeltaTime;
            double now = SystemAPI.Time.ElapsedTime;
            foreach (var (tuning, launch, pose, agent, volley, movement, navigation, entity) in
                SystemAPI.Query<RefRO<AlienSpikeTuning>, RefRO<EnemyLaunchState>, RefRO<LocalTransform>,
                    RefRO<NavigationAgent>, RefRW<AlienSpikeVolley>, RefRW<DesiredMovement>, RefRW<NavigationIntent>>()
                    .WithAll<EnemyMovementSettings>().WithNone<RespawnRequest>().WithEntityAccess())
            {
                var t = tuning.ValueRO;
                ref var attack = ref volley.ValueRW;
                float3 position = pose.ValueRO.Position;
                float distance = math.distance(position.xz, player.Position.xz);
                float minimum = math.max(0, t.PreferredDistance - .65f);
                float maximum = math.min(t.EngagementRadius, t.PreferredDistance + .65f);
                bool positioning = t.VolleyMode == AlienSpikeVolleyMode.MidRangePositioning;
                bool inBand = distance >= minimum && distance <= maximum;
                bool hasRoom = BarricadeRoomMovement.TryGetRoom(entity, owners, rooms, out var room);
                bool playerInRoom = !hasRoom || BarricadeRoomMovement.Contains(room, player.Position);
                bool canEngage = player.IsAvailable && playerInRoom && distance <= t.EngagementRadius;
                float floor = hasGround ? ground.SurfaceHeight : position.y - .9f;
                float radius = math.max(.01f, t.Shot.Radius);
                float shotY = AlienSpikeEmissionSystem.ShotHeight(player.Position.y, floor, radius);
                bool blocked = canEngage && hasPhysics && launch.ValueRO.Phase == EnemyLaunchPhase.Active
                    && attack.Phase == AlienSpikeVolleyPhase.Moving
                    && Blocked(physics.CollisionWorld, enemies, position.xz, player.Position.xz, shotY, radius, floor);
                if (!blocked)
                {
                    attack.HasCoverDestination = false;
                    attack.NextCoverSearchAt = 0;
                }
                bool ready = canEngage && !blocked && (!positioning || inBand);
                var previousPhase = attack.Phase;
                if (Advance(ref attack, t, launch.ValueRO.Phase, ready, dt))
                {
                    if (previousPhase != AlienSpikeVolleyPhase.Telegraph && attack.Phase == AlienSpikeVolleyPhase.Telegraph)
                        audio.Record(EnemyAttackAudioKind.AlienSpikeWindup, position);
                    movement.ValueRW = default;
                    navigation.ValueRW = default;
                    continue;
                }

                if (!player.IsAvailable || !playerInRoom || launch.ValueRO.Phase != EnemyLaunchPhase.Active) continue;
                float speed = SystemAPI.GetComponent<EnemyMovementSettings>(entity).MoveSpeed;
                if (blocked)
                {
                    if (now >= attack.NextCoverSearchAt)
                    {
                        attack.NextCoverSearchAt = now + CoverSearchInterval;
                        float2 target = player.Position.xz;
                        float goalDistance = math.distance(attack.CoverDestination, target);
                        // Allow for navigation braking just short of the destination.
                        float goalShotRadius = radius + CoverArrivalDistance;
                        if (!attack.HasCoverDestination || goalDistance > (positioning ? maximum : t.EngagementRadius)
                            || positioning && goalDistance < minimum
                            || Blocked(physics.CollisionWorld, enemies, attack.CoverDestination, target, shotY, goalShotRadius, floor))
                        {
                            float spacing = positioning ? (minimum + maximum) * .5f : distance;
                            float closer = positioning ? math.lerp(minimum, spacing, .5f) : spacing * .5f;
                            var bounds = hasRoom ? BarricadeRoomMovement.Bounds(room) : default;
                            attack.HasCoverDestination = TryFindFiringAngle(physics.CollisionWorld, enemies, grid, position,
                                target, spacing, closer, shotY, goalShotRadius, agent.ValueRO.Radius, floor, hasRoom, bounds,
                                out attack.CoverDestination);
                        }
                    }
                    if (attack.HasCoverDestination)
                    {
                        float2 point = attack.CoverDestination;
                        float3 goal = new float3(point.x, position.y, point.y);
                        movement.ValueRW = new DesiredMovement { Direction = math.normalizesafe(goal - position), Speed = speed };
                        navigation.ValueRW = NavigationIntent.Travel(goal, speed, CoverArrivalDistance, float3.zero,
                            NavigationGoalKind.ExactSetup);
                    }
                    else
                    {
                        movement.ValueRW = default;
                        navigation.ValueRW = default;
                    }
                    continue;
                }

                if (positioning && inBand)
                {
                    movement.ValueRW = default;
                    navigation.ValueRW = default;
                    continue;
                }
                float3 destination = positioning
                    ? NavigationGeometry.DistanceBandDestination(grid, position, player.Position, minimum, maximum, agent.ValueRO.Radius)
                    : new float3(player.Position.x, position.y, player.Position.z);
                var separation = navigation.ValueRO.Separation;
                movement.ValueRW = new DesiredMovement
                {
                    Direction = math.normalizesafe(destination - position + separation), Speed = speed
                };
                navigation.ValueRW = NavigationIntent.Travel(destination, speed, .2f, separation,
                    positioning ? NavigationGoalKind.DistanceBand : NavigationGoalKind.Position);
            }
        }

        private static bool TryFindFiringAngle(in CollisionWorld world, ComponentLookup<Enemy> enemies, NavigationGrid nav,
            float3 position, float2 target, float spacing, float closer, float shotY, float shotRadius, float agentRadius,
            float floor, bool hasRoom, in ArenaBounds bounds, out float2 destination)
        {
            destination = default;
            float2 away = position.xz - target;
            float heading = math.atan2(away.y, away.x);
            int clearance = -1, region = 0;
            if (nav.Data.IsCreated)
            {
                ref var grid = ref nav.Data.Value;
                clearance = NavigationGeometry.ClearanceClass(ref grid, agentRadius);
                region = NavigationGeometry.Region(ref grid, NavigationGeometry.Anchor(ref grid, position.xz, clearance), clearance);
            }

            // Try the smallest angle change on either side before looking closer to the player.
            for (int ring = 0; ring < 2; ring++)
            for (int probe = ring == 0 ? 1 : 0; probe < 16; probe++)
            {
                float offset = (probe % 2 == 1 ? 1 : -1) * ((probe + 1) / 2) * (math.PI / 8f);
                math.sincos(heading + offset, out float sine, out float cosine);
                float2 candidate = target + new float2(cosine, sine) * (ring == 0 ? spacing : closer);
                if (hasRoom && math.any(math.abs(candidate - bounds.Center.xz) > bounds.Extents.xz)) continue;
                if (region > 0)
                {
                    ref var grid = ref nav.Data.Value;
                    if (NavigationGeometry.Region(ref grid, NavigationGeometry.Anchor(ref grid, candidate, clearance), clearance) != region)
                        continue;
                }
                else if (Blocked(world, enemies, position.xz, candidate, math.max(position.y, floor + agentRadius + .02f),
                    math.max(.01f, agentRadius), floor)) continue;
                if (Blocked(world, enemies, candidate, target, shotY, shotRadius, floor)) continue;
                destination = candidate;
                return true;
            }
            return false;
        }

        private static bool Blocked(in CollisionWorld world, ComponentLookup<Enemy> enemies, float2 start, float2 end,
            float height, float radius, float floor)
        {
            float2 delta = end - start;
            float distance = math.length(delta);
            if (distance <= .00001f) return false;
            float2 direction = delta / distance;
            return MushroomShotGeometry.FindCover(world, enemies, new float3(start.x, height, start.y),
                new float3(direction.x, 0, direction.y), distance, radius, floor, out _);
        }

        // Holding the full cooldown during flight and recovery starts the timer on the next active step.
        internal static bool Advance(ref AlienSpikeVolley attack, in AlienSpikeTuning tuning,
            EnemyLaunchPhase phase, bool ready, float dt)
        {
            if (phase != EnemyLaunchPhase.Active)
            {
                attack = new AlienSpikeVolley { Remaining = tuning.Cooldown };
                return false;
            }

            attack.Remaining = math.max(0, attack.Remaining - dt);
            switch (attack.Phase)
            {
                case AlienSpikeVolleyPhase.Telegraph:
                    attack.AnimationPhase = AlienSpikeAuthoring.ReleasePhase
                        * (1 - attack.Remaining / tuning.TelegraphSeconds);
                    if (attack.Remaining <= 0) attack.Phase = AlienSpikeVolleyPhase.Release;
                    return true;
                case AlienSpikeVolleyPhase.Release:
                    return true;
                case AlienSpikeVolleyPhase.FollowThrough:
                    attack.AnimationPhase = math.lerp(AlienSpikeAuthoring.ReleasePhase, 1,
                        1 - attack.Remaining / math.max(.01f, tuning.FollowThroughSeconds));
                    if (attack.Remaining > 0) return true;
                    attack = new AlienSpikeVolley { Remaining = math.max(0, tuning.Cooldown - tuning.FollowThroughSeconds) };
                    return false;
                default:
                    if (attack.Remaining > 0 || !ready) return false;
                    attack.Phase = AlienSpikeVolleyPhase.Telegraph;
                    attack.Remaining = tuning.TelegraphSeconds;
                    return true;
            }
        }
    }
}

