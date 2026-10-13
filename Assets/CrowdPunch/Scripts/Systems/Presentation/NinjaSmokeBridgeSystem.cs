using CrowdPunch.Components;
using CrowdPunch.Mono.Player;
using CrowdPunch.Systems.Groups;
using Unity.Entities;
using Unity.Transforms;

namespace CrowdPunch.Systems.Presentation
{
    [UpdateInGroup(typeof(GamePresentationGroup))]
    public partial struct NinjaSmokeBridgeSystem : ISystem
    {
        public void OnCreate(ref SystemState state) => state.RequireForUpdate<NinjaState>();
        public void OnUpdate(ref SystemState state)
        {
            if (!PlayerBridgeRegistry.TryGetBridge(out PlayerEcsBridge bridge)) return;
            foreach (var (ninja, presentation, transform) in SystemAPI.Query<RefRO<NinjaState>, RefRW<NinjaPresentation>, RefRO<LocalTransform>>().WithNone<RespawnRequest>())
            {
                if (presentation.ValueRO.Generation != ninja.ValueRO.Generation) presentation.ValueRW = new NinjaPresentation { Generation = ninja.ValueRO.Generation };
                if (ninja.ValueRO.TeleportSequence == presentation.ValueRO.TeleportSequence) continue;
                presentation.ValueRW.TeleportSequence = ninja.ValueRO.TeleportSequence;
                bridge.ReceiveNinjaTeleport(ninja.ValueRO.DeparturePosition, transform.ValueRO.Position);
            }
        }
    }
}
