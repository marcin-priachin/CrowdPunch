using CrowdPunch.Components;
using CrowdPunch.Mono.UI;
using CrowdPunch.Systems.Groups;
using Unity.Entities;
using Unity.Transforms;

namespace CrowdPunch.Systems.Presentation
{
    /// <summary>
    /// Publishes ECS health and shield-count snapshots to the registered screen-space canvas.
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
                         RefRO<EnemyHealthBarPolicy> policy,
                         EnabledRefRO<EnemyHealthBarVisibility> healthBarVisibility,
                         EnabledRefRO<RespawnRequest> respawnRequest,
                         Entity enemy) in
                     SystemAPI.Query<RefRO<LocalTransform>, RefRO<HealthBar>, RefRO<EnemyLaunchState>, RefRO<EnemyHealthBarPolicy>, EnabledRefRO<EnemyHealthBarVisibility>, EnabledRefRO<RespawnRequest>>()
                         .WithAll<Enemy>()
                         .WithOptions(EntityQueryOptions.IgnoreComponentEnabledState)
                         .WithEntityAccess())
            {
                if (SystemAPI.HasComponent<EnemyArmor>(enemy)) continue;
                EnemyLaunchPhase phase = launchState.ValueRO.Phase;
                bool alwaysVisible = policy.ValueRO.Value == EnemyHealthBarPolicyKind.AlwaysWhileAlive;
                if (respawnRequest.ValueRO || phase == EnemyLaunchPhase.Defeated
                    || (!alwaysVisible && (!healthBarVisibility.ValueRO || healthBar.ValueRO.Normalized <= 0f)))
                {
                    continue;
                }

                EnemyHealthBarCanvasRegistry.Publish(
                    enemy.Index,
                    transform.ValueRO.Position,
                    healthBar.ValueRO.Normalized,
                    alwaysVisible || healthBarVisibility.ValueRO,
                    alwaysVisible,
                    string.Empty);
            }

            foreach (var (boss,health,transform,entity) in SystemAPI.Query<RefRO<BossEncounter>,RefRO<Health>,RefRO<LocalTransform>>().WithEntityAccess())
                if(boss.ValueRO.Cycle!=BossCycle.Defeated)
                    EnemyHealthBarCanvasRegistry.Publish(entity.Index,transform.ValueRO.Position+new Unity.Mathematics.float3(0,2.8f,0),
                        health.ValueRO.Normalized,true,true,string.Empty);
            foreach ((RefRO<EnemyArmor> armor, RefRO<LocalTransform> transform,
                         RefRO<EnemyLaunchState> launch, EnabledRefRO<RespawnRequest> respawn,
                         Entity enemy) in
                     SystemAPI.Query<RefRO<EnemyArmor>, RefRO<LocalTransform>,
                             RefRO<EnemyLaunchState>, EnabledRefRO<RespawnRequest>>()
                         .WithAll<Enemy>()
                         .WithOptions(EntityQueryOptions.IgnoreComponentEnabledState)
                         .WithEntityAccess())
            {
                if (respawn.ValueRO || launch.ValueRO.Phase == EnemyLaunchPhase.Defeated
                    || armor.ValueRO.Stages == 0) continue;
                EnemyHealthBarCanvasRegistry.PublishShields(enemy.Index,
                    transform.ValueRO.Position,
                    armor.ValueRO.Stages);
            }
            EnemyHealthBarCanvasRegistry.EndFrame();
        }

    }
}
