using CrowdPunch.Components;
using CrowdPunch.Mono.Player;
using CrowdPunch.Systems.Groups;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace CrowdPunch.Systems.Combat
{
    /// <summary>Resolves queued explosions to a fixed point so overlap-driven chain reactions complete this frame.</summary>
    [UpdateInGroup(typeof(GamePostPhysicsGroup))]
    [UpdateAfter(typeof(EnemyLaunchCollisionSystem))]
    [UpdateBefore(typeof(CrowdPunch.Systems.Physics.EnemyRecoverySystem))]
    public partial class ExplosionResolutionSystem : SystemBase
    {
        protected override void OnCreate()
        {
            RequireForUpdate<ExplosiveEnemySettings>();
            RequireForUpdate<PlayerSnapshot>();
        }

        protected override void OnUpdate()
        {
            PlayerSnapshot player = SystemAPI.GetSingleton<PlayerSnapshot>();
            RequestPlayerContactDetonations(player);

            bool resolvedExplosion;
            do
            {
                resolvedExplosion = false;
                foreach ((RefRO<LocalTransform> transform,
                             RefRO<ExplosiveEnemySettings> settings,
                             RefRW<ExplosiveEnemyState> explosiveState,
                             EnabledRefRW<ExplosiveDetonationRequest> requestEnabled,
                             Entity explosive) in
                         SystemAPI.Query<RefRO<LocalTransform>, RefRO<ExplosiveEnemySettings>,
                                 RefRW<ExplosiveEnemyState>, EnabledRefRW<ExplosiveDetonationRequest>>()
                             .WithAll<Enemy>()
                             .WithOptions(EntityQueryOptions.IgnoreComponentEnabledState)
                             .WithEntityAccess())
                {
                    if (!requestEnabled.ValueRO)
                    {
                        continue;
                    }

                    requestEnabled.ValueRW = false;
                    if (explosiveState.ValueRO.HasExploded != 0)
                    {
                        continue;
                    }

                    explosiveState.ValueRW.HasExploded = 1;
                    ResolveExplosion(explosive, transform.ValueRO.Position, settings.ValueRO, player);
                    resolvedExplosion = true;
                }
            } while (resolvedExplosion);
        }

        private void RequestPlayerContactDetonations(PlayerSnapshot player)
        {
            if (!player.IsAvailable)
            {
                return;
            }

            foreach ((RefRO<LocalTransform> transform,
                         RefRO<EnemyContactDamageSettings> contact,
                         RefRO<EnemyLaunchState> launchState,
                         RefRO<ExplosiveEnemyState> explosiveState,
                         EnabledRefRW<ExplosiveDetonationRequest> requestEnabled) in
                     SystemAPI.Query<RefRO<LocalTransform>, RefRO<EnemyContactDamageSettings>,
                             RefRO<EnemyLaunchState>, RefRO<ExplosiveEnemyState>,
                             EnabledRefRW<ExplosiveDetonationRequest>>()
                         .WithAll<Enemy>()
                         .WithNone<RespawnRequest>()
                         .WithOptions(EntityQueryOptions.IgnoreComponentEnabledState))
            {
                if (explosiveState.ValueRO.HasExploded != 0
                    || launchState.ValueRO.Phase != EnemyLaunchPhase.Active)
                {
                    continue;
                }

                float contactRadius = player.Radius + math.max(0f, contact.ValueRO.ContactRadius);
                if (math.distancesq(transform.ValueRO.Position.xz, player.Position.xz) <= contactRadius * contactRadius)
                {
                    requestEnabled.ValueRW = true;
                }
            }
        }

        private void ResolveExplosion(
            Entity explosive,
            float3 center,
            ExplosiveEnemySettings settings,
            PlayerSnapshot player)
        {
            float radius = math.max(0f, settings.Radius);
            float radiusSquared = radius * radius;

            foreach ((RefRO<LocalTransform> transform,
                         RefRW<EnemyLaunchState> launchState,
                         Entity target) in
                     SystemAPI.Query<RefRO<LocalTransform>, RefRW<EnemyLaunchState>>()
                         .WithAll<Enemy>()
                         .WithNone<RespawnRequest>()
                         .WithEntityAccess())
            {
                if (target == explosive || launchState.ValueRO.Phase == EnemyLaunchPhase.Defeated
                    || math.distancesq(transform.ValueRO.Position, center) > radiusSquared)
                {
                    continue;
                }

                QueueDamage(target, settings.Damage);
                if (SystemAPI.HasComponent<ExplosiveEnemyState>(target)
                    && SystemAPI.GetComponent<ExplosiveEnemyState>(target).HasExploded == 0)
                {
                    SystemAPI.SetComponentEnabled<ExplosiveDetonationRequest>(target, true);
                }

                float3 direction = transform.ValueRO.Position - center;
                direction.y = 0f;
                direction = math.normalizesafe(direction, new float3(0f, 0f, 1f));
                QueueImpulse(target, direction * math.max(0f, settings.NormalEnemyKnockbackForce));

                EnemyLaunchState nextLaunchState = launchState.ValueRO;
                EnemyLaunchTransition.Begin(ref nextLaunchState, EnemyLaunchCause.Explosion, settings.Damage);
                launchState.ValueRW = nextLaunchState;
            }

            DefeatExplosive(explosive);
            AffectPlayer(center, radiusSquared, settings, player);
            if (PlayerBridgeRegistry.TryGetBridge(out PlayerEcsBridge bridge))
            {
                bridge.ReceiveExplosion(center, radius, settings.VisualDuration, settings.VisualSizeMultiplier);
            }
        }

        private void QueueDamage(Entity target, float amount)
        {
            DamageRequest request = SystemAPI.IsComponentEnabled<DamageRequest>(target)
                ? SystemAPI.GetComponent<DamageRequest>(target)
                : default;
            request.Amount += math.max(0f, amount);
            SystemAPI.SetComponent(target, request);
            SystemAPI.SetComponentEnabled<DamageRequest>(target, true);
        }

        private void QueueImpulse(Entity target, float3 value)
        {
            ExternalImpulse impulse = SystemAPI.IsComponentEnabled<ExternalImpulse>(target)
                ? SystemAPI.GetComponent<ExternalImpulse>(target)
                : default;
            impulse.Value += value;
            SystemAPI.SetComponent(target, impulse);
            SystemAPI.SetComponentEnabled<ExternalImpulse>(target, true);
        }

        private void DefeatExplosive(Entity explosive)
        {
            EnemyLaunchState state = SystemAPI.GetComponent<EnemyLaunchState>(explosive);
            state.Phase = EnemyLaunchPhase.Defeated;
            state.BelowUsefulMomentumSeconds = 0f;
            state.RecoverySecondsRemaining = 0f;
            SystemAPI.SetComponent(explosive, state);
            SystemAPI.SetComponentEnabled<DeathRequest>(explosive, true);
            SystemAPI.SetComponentEnabled<DamageRequest>(explosive, false);
            SystemAPI.SetComponentEnabled<ExternalImpulse>(explosive, false);
        }

        private static void AffectPlayer(
            float3 center,
            float radiusSquared,
            ExplosiveEnemySettings settings,
            PlayerSnapshot player)
        {
            if (!player.IsAvailable || math.distancesq(player.Position, center) > radiusSquared
                || !PlayerBridgeRegistry.TryGetBridge(out PlayerEcsBridge bridge))
            {
                return;
            }

            float3 direction = player.Position - center;
            direction.y = 0f;
            direction = math.normalizesafe(direction, -math.normalizesafe(player.Forward, new float3(0f, 0f, 1f)));
            bridge.ReceiveEnemyHit(
                math.max(0f, settings.Damage),
                math.max(0f, settings.PlayerInvincibilitySeconds),
                direction * math.max(0f, settings.PlayerEliteKnockbackForce));
        }
    }
}
