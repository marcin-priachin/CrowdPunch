using CrowdPunch.Components;
using CrowdPunch.Systems.Groups;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using CrowdPunch.Mono.Player;

namespace CrowdPunch.Systems.Presentation
{
    /// <summary>
    /// Bridges ECS simulation results to presentation-only GameObject or rendering state.
    /// </summary>
    [UpdateInGroup(typeof(GamePresentationGroup))]
    public partial struct PresentationBridgeSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Enemy>();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!PlayerBridgeRegistry.TryGetBridge(out PlayerEcsBridge bridge))
            {
                return;
            }

            bridge.BeginTrajectoryPreview();

            if (!bridge.IsPunchPreviewAvailable || bridge.PunchPreviewLength <= 0f)
            {
                return;
            }

            float3 origin = bridge.PunchPreviewOrigin;
            float3 punchDirection = math.normalizesafe(bridge.PunchPreviewDirection);
            float radiusSquared = bridge.PunchPreviewRadius * bridge.PunchPreviewRadius;
            float positionWeight = math.saturate(bridge.PunchPreviewPositionWeight);

            foreach ((RefRO<LocalTransform> transform, RefRO<EnemyLaunchState> launchState) in
                     SystemAPI.Query<RefRO<LocalTransform>, RefRO<EnemyLaunchState>>()
                         .WithAll<Enemy>()
                         .WithNone<RespawnRequest>())
            {
                if (launchState.ValueRO.Phase != EnemyLaunchPhase.Active)
                {
                    continue;
                }

                float3 enemyPosition = transform.ValueRO.Position;
                float3 toEnemy = enemyPosition - origin;
                float forwardDistance = math.dot(toEnemy, punchDirection);

                if (forwardDistance < 0f || forwardDistance > bridge.PunchPreviewRange)
                {
                    continue;
                }

                float3 closestPoint = origin + punchDirection * forwardDistance;
                if (math.lengthsq(enemyPosition - closestPoint) > radiusSquared)
                {
                    continue;
                }

                float3 positionDirection = math.normalizesafe(toEnemy, punchDirection);
                float3 launchDirection = math.normalizesafe(
                    math.lerp(punchDirection, positionDirection, positionWeight),
                    positionDirection);
                bridge.AddTrajectoryPreview(
                    enemyPosition,
                    enemyPosition + launchDirection * bridge.PunchPreviewLength);
            }
        }
    }
}
