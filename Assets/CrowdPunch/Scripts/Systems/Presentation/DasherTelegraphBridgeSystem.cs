using CrowdPunch.Components;
using CrowdPunch.Mono.Player;
using CrowdPunch.Mono.UI;
using CrowdPunch.Systems.Groups;
using CrowdPunch.Systems.Movement;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace CrowdPunch.Systems.Presentation
{
    [UpdateInGroup(typeof(GamePresentationGroup))]
    [UpdateAfter(typeof(EnemyReadabilitySystem))]
    public partial struct DasherTelegraphBridgeSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            if (!PlayerBridgeRegistry.TryGetBridge(out PlayerEcsBridge bridge)
                || bridge.FeedbackSettings == null)
                return;

            bridge.BeginDasherTelegraphs();
            if (bridge.gameObject.activeInHierarchy && !FeedbackTimeController.IsSuspended)
            {
                foreach ((RefRO<DasherState> dash, RefRO<DasherSettings> settings,
                             RefRO<EnemyLaunchState> launch, RefRO<LocalTransform> transform, Entity entity) in
                         SystemAPI.Query<RefRO<DasherState>, RefRO<DasherSettings>, RefRO<EnemyLaunchState>, RefRO<LocalTransform>>()
                             .WithAll<Enemy>().WithNone<RespawnRequest>().WithEntityAccess())
                {
                    ulong id = ((ulong)(uint)entity.Version << 32) | (uint)entity.Index;
                    float3 position = transform.ValueRO.Position;
                    bool playerDash = launch.ValueRO.Phase == EnemyLaunchPhase.Launched
                        && launch.ValueRO.Owner == EnemyLaunchOwner.Player;
                    if (launch.ValueRO.Phase == EnemyLaunchPhase.Launched && !playerDash)
                    {
                        // The launched roll animation plays at its base speed.
                        bridge.ReceiveDasherRoll(id, position, 1f, 1f);
                        continue;
                    }
                    if (playerDash
                        ? !DasherMovementSystem.IsPlayerDash(dash.ValueRO, launch.ValueRO)
                        : launch.ValueRO.Phase != EnemyLaunchPhase.Active
                            || (dash.ValueRO.Phase != DasherPhase.Preparing && dash.ValueRO.Phase != DasherPhase.Dashing))
                        continue;
                    float duration = math.max(0.0001f, settings.ValueRO.TelegraphDuration);
                    float progress = dash.ValueRO.Phase == DasherPhase.Dashing ? 1f
                        : math.saturate(1f - dash.ValueRO.SecondsRemaining / duration);
                    bridge.ReceiveDasherTelegraph(id, position,
                        math.normalizesafe(dash.ValueRO.LockedDirection, math.forward()), progress, playerDash);
                    bridge.ReceiveDasherRoll(id, position, math.lerp(1f, 3f, progress), progress);
                }
            }
            bridge.EndDasherTelegraphs();
        }
    }
}
