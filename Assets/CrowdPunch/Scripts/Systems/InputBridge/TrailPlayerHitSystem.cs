using CrowdPunch.Components;
using CrowdPunch.Mono.Player;
using CrowdPunch.Systems.Combat;
using CrowdPunch.Systems.Groups;
using Unity.Entities;

namespace CrowdPunch.Systems.InputBridge
{
    [UpdateInGroup(typeof(GamePostPhysicsGroup)), UpdateAfter(typeof(TrailDamageSystem))]
    public partial struct TrailPlayerHitSystem : ISystem
    {
        public void OnCreate(ref SystemState state) => state.RequireForUpdate<TrailPlayerHit>();
        public void OnUpdate(ref SystemState state)
        {
            var hits = SystemAPI.GetSingletonBuffer<TrailPlayerHit>();
            if (PlayerBridgeRegistry.TryGetBridge(out var bridge))
                foreach (var hit in hits) bridge.ReceiveTrailDamage(hit.Damage);
            hits.Clear();
        }
    }
}
