using CrowdPunch.Components;
using CrowdPunch.Mono.UI;
using CrowdPunch.Systems.Groups;
using Unity.Entities;
using Unity.Transforms;

namespace CrowdPunch.Systems.Presentation
{
    /// <summary>
    /// Publishes ECS health snapshots to the registered screen-space canvas.
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
                if (SystemAPI.HasComponent<EnemyArmor>(enemy) || SystemAPI.HasComponent<TrailEmitter>(enemy) || SystemAPI.HasComponent<DragonTuning>(enemy)) continue;
                EnemyLaunchPhase phase = launchState.ValueRO.Phase;
                bool alwaysVisible = policy.ValueRO.Value == EnemyHealthBarPolicyKind.AlwaysWhileAlive;
                if (respawnRequest.ValueRO || phase == EnemyLaunchPhase.Defeated
                    || (!alwaysVisible && (!healthBarVisibility.ValueRO || healthBar.ValueRO.Normalized <= 0f)))
                {
                    continue;
                }

                // The canvas adds its shared 1.5m offset; the Giant needs 3.5m
                // above its physics root to keep the bar clear of its head.
                var barPosition = transform.ValueRO.Position;
                if (SystemAPI.HasComponent<ElitePunchSettings>(enemy)) barPosition.y += 2f;
                EnemyHealthBarCanvasRegistry.Publish(
                    enemy.Index,
                    barPosition,
                    healthBar.ValueRO.Normalized,
                    alwaysVisible || healthBarVisibility.ValueRO,
                    alwaysVisible,
                    string.Empty);
            }

            foreach (var (boss, health) in SystemAPI.Query<RefRO<BossEncounter>, RefRO<Health>>())
                if (boss.ValueRO.Cycle != BossCycle.Defeated)
                    EnemyHealthBarCanvasRegistry.PublishBoss(health.ValueRO.Normalized);
            foreach (var (boss, health) in SystemAPI.Query<RefRO<ChickenBoss>, RefRO<Health>>())
                if (boss.ValueRO.Phase != ChickenPhase.Defeated)
                    EnemyHealthBarCanvasRegistry.PublishBoss(health.ValueRO.Normalized);
            foreach (var (boss, health) in SystemAPI.Query<RefRO<RollingBoss>, RefRO<Health>>())
                if (boss.ValueRO.Phase != RollingPhase.Defeated)
                    EnemyHealthBarCanvasRegistry.PublishBoss(health.ValueRO.Normalized);
            foreach (var (boss, health) in SystemAPI.Query<RefRO<DinoBoss>, RefRO<Health>>())
                if (boss.ValueRO.Phase != DinoPhase.Defeated)
                    EnemyHealthBarCanvasRegistry.PublishBoss(health.ValueRO.Normalized);
            EnemyHealthBarCanvasRegistry.EndFrame();
        }

    }
}
