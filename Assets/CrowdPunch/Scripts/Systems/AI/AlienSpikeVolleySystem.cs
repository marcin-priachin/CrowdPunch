using CrowdPunch.Authoring;
using CrowdPunch.Components;
using CrowdPunch.Systems.Groups;
using CrowdPunch.Utilities;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
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
            float dt = SystemAPI.Time.DeltaTime;
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
                bool ready = player.IsAvailable && distance <= t.EngagementRadius && (!positioning || inBand);
                if (Advance(ref attack, t, launch.ValueRO.Phase, ready, dt))
                {
                    movement.ValueRW = default;
                    navigation.ValueRW = default;
                    continue;
                }

                if (!player.IsAvailable || launch.ValueRO.Phase != EnemyLaunchPhase.Active) continue;
                if (positioning && inBand)
                {
                    movement.ValueRW = default;
                    navigation.ValueRW = default;
                    continue;
                }
                float3 destination = positioning
                    ? NavigationGeometry.DistanceBandDestination(grid, position, player.Position, minimum, maximum, agent.ValueRO.Radius)
                    : new float3(player.Position.x, position.y, player.Position.z);
                float speed = SystemAPI.GetComponent<EnemyMovementSettings>(entity).MoveSpeed;
                var separation = navigation.ValueRO.Separation;
                movement.ValueRW = new DesiredMovement
                {
                    Direction = math.normalizesafe(destination - position + separation), Speed = speed
                };
                navigation.ValueRW = NavigationIntent.Travel(destination, speed, .2f, separation,
                    positioning ? NavigationGoalKind.DistanceBand : NavigationGoalKind.Position);
            }
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

