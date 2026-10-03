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
    [UpdateAfter(typeof(EliteCrowdSupportSystem))]
    [UpdateAfter(typeof(EnemyChaseSystem))]
    [UpdateBefore(typeof(WizardHazardAvoidanceSystem))]
    public partial struct WizardPositioningSystem : ISystem
    {
        public void OnCreate(ref SystemState state) => state.RequireForUpdate<PlayerSnapshot>();
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var player = SystemAPI.GetSingleton<PlayerSnapshot>();
            bool defending = SystemAPI.HasSingleton<ProtectedPoint>();
            var attackers = defending ? SystemAPI.GetSingletonBuffer<ProtectedPointAttacker>(true).AsNativeArray() : default;
            var grid = SystemAPI.HasSingleton<NavigationGrid>() ? SystemAPI.GetSingleton<NavigationGrid>() : default;
            foreach (var (s, cast, launch, pose, agent, movement, navigation, entity) in
                SystemAPI.Query<RefRO<WizardSettings>, RefRO<WizardCastState>, RefRO<EnemyLaunchState>, RefRO<LocalTransform>,
                    RefRO<NavigationAgent>, RefRW<DesiredMovement>, RefRW<NavigationIntent>>().WithNone<RespawnRequest>().WithEntityAccess())
            {
                if (defending && launch.ValueRO.Phase == EnemyLaunchPhase.Active && player.IsAvailable)
                {
                    if (cast.ValueRO.IsCasting || ProtectedPointAttacker.Contains(attackers, entity))
                    { movement.ValueRW = default; navigation.ValueRW = default; }
                    continue;
                }
                if (launch.ValueRO.Phase != EnemyLaunchPhase.Active || !player.IsAvailable || cast.ValueRO.StopsMovement(s.ValueRO))
                { movement.ValueRW = default; navigation.ValueRW = default; continue; }
                float3 delta = player.Position - pose.ValueRO.Position; delta.y = 0;
                float distance = math.length(delta);
                float min = math.max(0, s.ValueRO.PreferredMinimum), max = math.max(min, s.ValueRO.PreferredMaximum);
                float speed = distance < min ? s.ValueRO.RetreatSpeed : distance > max ? s.ValueRO.ApproachSpeed : 0;
                float3 separation = navigation.ValueRO.Separation;
                float3 destination = NavigationGeometry.DistanceBandDestination(grid, pose.ValueRO.Position, player.Position, min, max, agent.ValueRO.Radius);
                movement.ValueRW = new DesiredMovement { Direction = math.normalizesafe(destination - pose.ValueRO.Position + separation), Speed = speed > 0 ? speed : math.lengthsq(separation) > .0001f ? 1.5f : 0 };
                navigation.ValueRW = NavigationIntent.Travel(destination, math.max(speed, 1.5f), .35f, separation, NavigationGoalKind.DistanceBand);
                if (speed <= 0) navigation.ValueRW.Mode = NavigationMode.Hold;
            }
        }
    }
}
