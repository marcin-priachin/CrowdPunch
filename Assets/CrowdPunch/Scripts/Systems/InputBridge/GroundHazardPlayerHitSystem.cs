using CrowdPunch.Components;
using CrowdPunch.Mono.Player;
using CrowdPunch.Systems.Combat;
using CrowdPunch.Systems.Groups;
using Unity.Entities;

namespace CrowdPunch.Systems.InputBridge
{
    [UpdateInGroup(typeof(GamePostPhysicsGroup)), UpdateAfter(typeof(GroundHazardDamageSystem))]
    public partial struct GroundHazardPlayerHitSystem : ISystem
    {
        public void OnCreate(ref SystemState state) => state.RequireForUpdate<GroundHazardPlayerHit>();
        public void OnUpdate(ref SystemState state)
        {
            var hits = SystemAPI.GetSingletonBuffer<GroundHazardPlayerHit>();
            if (PlayerBridgeRegistry.TryGetBridge(out var bridge))
                foreach (var hit in hits) bridge.ReceiveGroundHazardDamage(hit.Damage);
            hits.Clear();
        }
    }
}
