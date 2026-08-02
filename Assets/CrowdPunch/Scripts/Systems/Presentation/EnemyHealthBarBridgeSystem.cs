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

            foreach ((RefRO<LocalTransform> transform, RefRO<HealthBar> healthBar, Entity enemy) in
                     SystemAPI.Query<RefRO<LocalTransform>, RefRO<HealthBar>>()
                         .WithAll<Enemy, EnemyHealthBarVisibility>()
                         .WithNone<RespawnRequest>()
                         .WithEntityAccess())
            {
                EnemyHealthBarCanvasRegistry.Publish(
                    enemy.Index,
                    transform.ValueRO.Position,
                    healthBar.ValueRO.Normalized);
            }

            EnemyHealthBarCanvasRegistry.EndFrame();
        }
    }
}
