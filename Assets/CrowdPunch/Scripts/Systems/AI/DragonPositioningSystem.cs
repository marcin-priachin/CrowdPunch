using CrowdPunch.Components;
using CrowdPunch.Systems.Groups;
using CrowdPunch.Utilities;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace CrowdPunch.Systems.AI
{
    [BurstCompile, UpdateInGroup(typeof(GamePrePhysicsGroup))]
    [UpdateAfter(typeof(EnemyChaseSystem)), UpdateAfter(typeof(EliteCrowdSupportSystem))]
    [UpdateBefore(typeof(EnemyNavigationSystem))]
    public partial struct DragonPositioningSystem : ISystem
    {
        public void OnCreate(ref SystemState state) { state.RequireForUpdate<DragonTuning>(); state.RequireForUpdate<PlayerSnapshot>(); }
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var player = SystemAPI.GetSingleton<PlayerSnapshot>();
            var grid = SystemAPI.HasSingleton<NavigationGrid>() ? SystemAPI.GetSingleton<NavigationGrid>() : default;
            foreach (var (tuning, launch, pose, agent, movement, navigation) in
                SystemAPI.Query<RefRO<DragonTuning>, RefRO<EnemyLaunchState>, RefRO<LocalTransform>, RefRO<NavigationAgent>,
                    RefRW<DesiredMovement>, RefRW<NavigationIntent>>().WithNone<RespawnRequest>())
            {
                if (launch.ValueRO.Phase != EnemyLaunchPhase.Active || !player.IsAvailable)
                { movement.ValueRW = default; navigation.ValueRW = default; continue; }
                var t = tuning.ValueRO;
                float distance = math.distance(player.Position.xz, pose.ValueRO.Position.xz);
                float speed = distance < t.PreferredMinimum ? t.RetreatSpeed : distance > t.PreferredMaximum ? t.ApproachSpeed : 0;
                float3 separation = navigation.ValueRO.Separation;
                float3 destination = NavigationGeometry.DistanceBandDestination(grid, pose.ValueRO.Position, player.Position,
                    t.PreferredMinimum, t.PreferredMaximum, agent.ValueRO.Radius);
                movement.ValueRW = new DesiredMovement { Direction = math.normalizesafe(destination - pose.ValueRO.Position + separation),
                    Speed = speed > 0 ? speed : math.lengthsq(separation) > .0001f ? 1.5f : 0 };
                navigation.ValueRW = NavigationIntent.Travel(destination, math.max(speed, 1.5f), .35f, separation, NavigationGoalKind.DistanceBand);
                if (speed <= 0) navigation.ValueRW.Mode = NavigationMode.Hold;
            }
        }
    }
}
