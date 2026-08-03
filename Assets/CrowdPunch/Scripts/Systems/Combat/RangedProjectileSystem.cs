using CrowdPunch.Components;
using CrowdPunch.Mono.Player;
using CrowdPunch.Systems.Groups;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace CrowdPunch.Systems.Combat
{
    /// <summary>Evaluates fixed projectile arcs, reports one player hit, and removes expired projectiles.</summary>
    [UpdateInGroup(typeof(GamePostPhysicsGroup))]
    [UpdateBefore(typeof(PlayerContactDamageSystem))]
    public partial class RangedProjectileSystem : SystemBase
    {
        protected override void OnCreate()
        {
            RequireForUpdate<PlayerSnapshot>();
        }

        protected override void OnUpdate()
        {
            PlayerSnapshot player = SystemAPI.GetSingleton<PlayerSnapshot>();
            float deltaTime = SystemAPI.Time.DeltaTime;
            EntityCommandBuffer commandBuffer = new EntityCommandBuffer(Allocator.Temp);
            bool bridgeAvailable = PlayerBridgeRegistry.TryGetBridge(out PlayerEcsBridge bridge);

            foreach ((RefRW<RangedProjectile> projectile, RefRW<LocalTransform> transform, Entity entity) in
                     SystemAPI.Query<RefRW<RangedProjectile>, RefRW<LocalTransform>>().WithEntityAccess())
            {
                float3 previousPosition = transform.ValueRO.Position;
                projectile.ValueRW.ElapsedSeconds += deltaTime;
                float normalizedTime = math.saturate(
                    projectile.ValueRO.ElapsedSeconds / math.max(0.01f, projectile.ValueRO.TravelDuration));
                float3 position = math.lerp(projectile.ValueRO.Start, projectile.ValueRO.Target, normalizedTime);
                position.y += 4f * projectile.ValueRO.ArcHeight * normalizedTime * (1f - normalizedTime);
                transform.ValueRW.Position = position;

                bool reachedTarget = normalizedTime >= 1f;
                bool playerHit = false;
                bool acceptsPlayerLayer = (projectile.ValueRO.PlayerCollisionLayers & player.CollisionLayer) != 0;
                if (projectile.ValueRO.HasAppliedDamage == 0 && player.IsAvailable && acceptsPlayerLayer)
                {
                    float hitRadius = math.max(0f, projectile.ValueRO.Radius) + math.max(0f, player.Radius);
                    float3 segment = position - previousPosition;
                    float segmentLengthSq = math.lengthsq(segment);
                    float segmentTime = segmentLengthSq <= 0.0001f
                        ? 0f
                        : math.saturate(math.dot(player.Position - previousPosition, segment) / segmentLengthSq);
                    float3 closestPoint = previousPosition + segment * segmentTime;
                    playerHit = math.distancesq(closestPoint, player.Position) <= hitRadius * hitRadius;
                    if (playerHit)
                    {
                        projectile.ValueRW.HasAppliedDamage = 1;
                        if (bridgeAvailable)
                        {
                            bridge.ReceiveEnemyHit(
                                projectile.ValueRO.Damage,
                                projectile.ValueRO.PlayerInvincibilitySeconds,
                                float3.zero);
                        }
                    }
                }

                bool expired = projectile.ValueRO.ElapsedSeconds >= projectile.ValueRO.Lifetime;
                if (playerHit || reachedTarget || expired)
                {
                    commandBuffer.DestroyEntity(entity);
                }
            }

            commandBuffer.Playback(EntityManager);
            commandBuffer.Dispose();
        }
    }
}
