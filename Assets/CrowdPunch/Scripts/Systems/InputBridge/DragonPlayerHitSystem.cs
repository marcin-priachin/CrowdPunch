using CrowdPunch.Components;
using CrowdPunch.Mono.Player;
using CrowdPunch.Systems.Combat;
using CrowdPunch.Systems.Groups;
using Unity.Entities;

namespace CrowdPunch.Systems.InputBridge
{
    [UpdateInGroup(typeof(GamePostPhysicsGroup)), UpdateAfter(typeof(DragonFlameDamageSystem))]
    public partial struct DragonPlayerHitSystem : ISystem
    {
        public void OnCreate(ref SystemState state) => state.RequireForUpdate<DragonPlayerHit>();
        public void OnUpdate(ref SystemState state)
        {
            var hits = SystemAPI.GetSingletonBuffer<DragonPlayerHit>();
            if (PlayerBridgeRegistry.TryGetBridge(out var bridge))
                foreach (var hit in hits) bridge.ReceiveDragonFlame(hit.Damage, hit.Push);
            hits.Clear();
        }
    }
}
