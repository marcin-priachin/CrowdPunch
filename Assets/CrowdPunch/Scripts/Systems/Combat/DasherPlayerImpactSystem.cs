using CrowdPunch.Components;
using CrowdPunch.Mono.Player;
using CrowdPunch.Systems.Groups;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace CrowdPunch.Systems.Combat
{
    [UpdateInGroup(typeof(GamePostPhysicsGroup))]
    public partial class DasherPlayerImpactSystem : SystemBase
    {
        protected override void OnCreate() => RequireForUpdate<PlayerSnapshot>();
        protected override void OnUpdate()
        {
            if (!PlayerBridgeRegistry.TryGetBridge(out PlayerEcsBridge bridge)) return;
            PlayerSnapshot player = SystemAPI.GetSingleton<PlayerSnapshot>();
            if (!player.IsAvailable) return;
            foreach ((RefRW<DasherState> dash, RefRO<DasherSettings> settings, RefRO<EnemyContactDamageSettings> contact,
                         RefRO<LocalTransform> transform) in
                     SystemAPI.Query<RefRW<DasherState>, RefRO<DasherSettings>, RefRO<EnemyContactDamageSettings>, RefRO<LocalTransform>>()
                         .WithAll<Enemy>().WithNone<RespawnRequest>())
            {
                if (dash.ValueRO.Phase != DasherPhase.Dashing || dash.ValueRO.HasHitPlayer != 0) continue;
                float3 delta = player.Position - transform.ValueRO.Position; delta.y = 0f;
                float radius = player.Radius + math.max(0f, contact.ValueRO.ContactRadius);
                if (math.lengthsq(delta) > radius * radius) continue;
                dash.ValueRW.HasHitPlayer = 1;
                bridge.ReceiveEnemyHit(settings.ValueRO.PlayerDamage, settings.ValueRO.PlayerInvincibilitySeconds,
                    math.normalizesafe(delta, dash.ValueRO.LockedDirection) * math.max(0f, settings.ValueRO.PlayerKnockback));
            }
        }
    }
}
