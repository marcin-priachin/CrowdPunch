using CrowdPunch.Components;
using CrowdPunch.Mono.Player;
using CrowdPunch.Systems.Combat;
using CrowdPunch.Systems.Groups;
using Unity.Entities;

namespace CrowdPunch.Systems.InputBridge
{
    [UpdateInGroup(typeof(GamePostPhysicsGroup)), UpdateAfter(typeof(LinkedPairDamageSystem))]
    public partial struct LinkedPairPlayerHitSystem : ISystem
    {
        public void OnCreate(ref SystemState state) => state.RequireForUpdate<LinkedPairPlayerHit>();
        public void OnUpdate(ref SystemState state) {
            var hits = SystemAPI.GetSingletonBuffer<LinkedPairPlayerHit>();
            if (PlayerBridgeRegistry.TryGetBridge(out var bridge)) foreach (var hit in hits) bridge.ReceiveLinkedPairDamage(hit.Damage);
            hits.Clear();
        }
    }
}
