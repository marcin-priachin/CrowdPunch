using CrowdPunch.Components;
using CrowdPunch.Mono.Player;
using CrowdPunch.Systems.Combat;
using CrowdPunch.Systems.Groups;
using CrowdPunch.Systems.Physics;
using Unity.Entities;

namespace CrowdPunch.Systems.InputBridge
{
    [UpdateInGroup(typeof(GamePostPhysicsGroup))]
    [UpdateAfter(typeof(WizardZoneSystem))]
    [UpdateBefore(typeof(EnemyRecoverySystem))]
    public partial struct WizardPlayerHitSystem : ISystem
    {
        public void OnCreate(ref SystemState state) => state.RequireForUpdate<WizardPlayerHit>();
        public void OnUpdate(ref SystemState state)
        {
            var hits = SystemAPI.GetSingletonBuffer<WizardPlayerHit>();
            if (PlayerBridgeRegistry.TryGetBridge(out var bridge))
                foreach (var hit in hits) bridge.ReceiveWizardHit(hit.Damage, hit.Impulse);
            hits.Clear();
        }
    }
}
