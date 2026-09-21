using CrowdPunch.Components;
using CrowdPunch.Mono.Player;
using CrowdPunch.Mono.UI;
using CrowdPunch.Systems.Groups;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace CrowdPunch.Systems.Presentation
{
    [UpdateInGroup(typeof(GamePresentationGroup))]
    [UpdateAfter(typeof(DasherPresentationSystem))]
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
                    if (dash.ValueRO.Phase != DasherPhase.Preparing
                        || launch.ValueRO.Phase != EnemyLaunchPhase.Active)
                        continue;
                    ulong id = ((ulong)(uint)entity.Version << 32) | (uint)entity.Index;
                    float duration = math.max(0.0001f, settings.ValueRO.TelegraphDuration);
                    float progress = math.saturate(1f - dash.ValueRO.SecondsRemaining / duration);
                    bridge.ReceiveDasherTelegraph(id, transform.ValueRO.Position,
                        math.normalizesafe(dash.ValueRO.LockedDirection, math.forward()), progress);
                }
            }
            bridge.EndDasherTelegraphs();
        }
    }
}
