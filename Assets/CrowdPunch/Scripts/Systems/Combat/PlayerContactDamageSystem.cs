using CrowdPunch.Components;
using CrowdPunch.Mono.Player;
using CrowdPunch.Systems.Groups;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace CrowdPunch.Systems.Combat
{
    /// <summary>
    /// Detects ECS enemy contact against the GameObject player and reports accepted hit data through the player bridge.
    /// </summary>
    [UpdateInGroup(typeof(GamePostPhysicsGroup))]
    public partial class PlayerContactDamageSystem : SystemBase
    {
        protected override void OnCreate()
        {
            RequireForUpdate<PlayerSnapshot>();
            RequireForUpdate<EnemyContactDamageSettings>();
        }

        protected override void OnUpdate()
        {
            if (!PlayerBridgeRegistry.TryGetBridge(out PlayerEcsBridge bridge))
            {
                return;
            }

            PlayerSnapshot playerSnapshot = SystemAPI.GetSingleton<PlayerSnapshot>();
            if (!playerSnapshot.IsAvailable)
            {
                return;
            }

            bool hasHit = false;
            float closestDistanceSquared = float.MaxValue;
            float hitDamagePercent = 0f;
            float hitInvincibilitySeconds = 0f;
            float3 hitPushImpulse = float3.zero;

            foreach ((RefRO<LocalTransform> transform, RefRO<EnemyContactDamageSettings> contactSettings,
                         RefRO<EnemyLaunchState> launchState,
                         RefRO<EnemyArchetype> archetype) in
                     SystemAPI.Query<RefRO<LocalTransform>, RefRO<EnemyContactDamageSettings>, RefRO<EnemyLaunchState>, RefRO<EnemyArchetype>>()
                         .WithAll<Enemy>()
                         .WithNone<RespawnRequest>())
            {
                if (launchState.ValueRO.Phase != EnemyLaunchPhase.Active
                    || archetype.ValueRO.Value == EnemyArchetypeKind.Explosive)
                {
                    continue;
                }

                float3 toPlayer = playerSnapshot.Position - transform.ValueRO.Position;
                toPlayer.y = 0f;

                float contactDistance = playerSnapshot.Radius + math.max(0f, contactSettings.ValueRO.ContactRadius);
                float distanceSquared = math.lengthsq(toPlayer);

                if (distanceSquared > contactDistance * contactDistance || distanceSquared >= closestDistanceSquared)
                {
                    continue;
                }

                float3 fallbackDirection = -math.normalizesafe(playerSnapshot.Forward, new float3(0f, 0f, 1f));
                fallbackDirection.y = 0f;
                float3 pushDirection = math.normalizesafe(toPlayer, fallbackDirection);

                hasHit = true;
                closestDistanceSquared = distanceSquared;
                hitDamagePercent = contactSettings.ValueRO.DamagePercent;
                hitInvincibilitySeconds = contactSettings.ValueRO.PlayerInvincibilitySeconds;
                hitPushImpulse = pushDirection * math.max(0f, contactSettings.ValueRO.PushStrength);
            }

            if (hasHit)
            {
                bridge.ReceiveEnemyContactHit(hitDamagePercent, hitInvincibilitySeconds, hitPushImpulse);
            }
        }
    }
}
