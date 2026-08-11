using CrowdPunch.Components;
using CrowdPunch.Mono.UI;
using CrowdPunch.Systems.Groups;
using Unity.Entities;
using Unity.Transforms;

namespace CrowdPunch.Systems.Presentation
{
    /// <summary>
    /// Publishes temporary ECS enemy-health snapshots to the registered screen-space canvas.
    /// </summary>
    [UpdateInGroup(typeof(GamePresentationGroup))]
    [UpdateAfter(typeof(HealthBarPresentationSystem))]
    public partial struct EnemyHealthBarBridgeSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            EnemyHealthBarCanvasRegistry.BeginFrame();

            foreach ((RefRO<LocalTransform> transform,
                         RefRO<HealthBar> healthBar,
                         RefRO<EnemyLaunchState> launchState,
                         EnabledRefRO<EnemyHealthBarVisibility> healthBarVisibility,
                         EnabledRefRO<RespawnRequest> respawnRequest,
                         Entity enemy) in
                     SystemAPI.Query<RefRO<LocalTransform>, RefRO<HealthBar>, RefRO<EnemyLaunchState>, EnabledRefRO<EnemyHealthBarVisibility>, EnabledRefRO<RespawnRequest>>()
                         .WithAll<Enemy>()
                         .WithOptions(EntityQueryOptions.IgnoreComponentEnabledState)
                         .WithEntityAccess())
            {
                EnemyLaunchPhase phase = launchState.ValueRO.Phase;
                if (respawnRequest.ValueRO || (!healthBarVisibility.ValueRO && phase == EnemyLaunchPhase.Active))
                {
                    continue;
                }

                EnemyHealthBarCanvasRegistry.Publish(
                    enemy.Index,
                    transform.ValueRO.Position,
                    healthBar.ValueRO.Normalized,
                    healthBarVisibility.ValueRO,
                    GetStateLabel(phase));
            }

            EnemyHealthBarCanvasRegistry.EndFrame();
        }

        private static string GetStateLabel(EnemyLaunchPhase phase)
        {
            return phase switch
            {
                EnemyLaunchPhase.Launched => "Launched",
                EnemyLaunchPhase.Recovering => "Recovering",
                EnemyLaunchPhase.Defeated => "Defeated",
                _ => string.Empty
            };
        }
    }
}
