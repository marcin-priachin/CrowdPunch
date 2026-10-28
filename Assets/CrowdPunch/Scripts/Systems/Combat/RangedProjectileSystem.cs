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
            bool hasGround = SystemAPI.TryGetSingleton<ArenaGroundPlane>(out var ground);

            foreach ((RefRW<RangedProjectile> projectile, RefRW<LocalTransform> transform, Entity entity) in
                     SystemAPI.Query<RefRW<RangedProjectile>, RefRW<LocalTransform>>().WithEntityAccess())
            {
                float3 previousPosition = transform.ValueRO.Position;
                projectile.ValueRW.ElapsedSeconds += deltaTime;
                float normalizedTime = projectile.ValueRO.ElapsedSeconds
                    / math.max(0.01f, projectile.ValueRO.TravelDuration);
                float3 position = math.lerp(projectile.ValueRO.Start, projectile.ValueRO.Target, normalizedTime);
                position.y += 4f * projectile.ValueRO.ArcHeight * normalizedTime * (1f - normalizedTime);
                transform.ValueRW.Position = position;

                bool playerHit = false;
                float playerAt = 1f;
                float groundAt = 1f;
                float radius = math.max(0f, projectile.ValueRO.Radius);
                bool groundHit = hasGround && position.y <= ground.SurfaceHeight + radius
                    && position.y < previousPosition.y;
                if (groundHit)
                    groundAt = math.saturate((previousPosition.y - ground.SurfaceHeight - radius)
                        / math.max(.00001f, previousPosition.y - position.y));
                bool acceptsPlayerLayer = (projectile.ValueRO.PlayerCollisionLayers & player.CollisionLayer) != 0;
                if (projectile.ValueRO.HasAppliedDamage == 0 && player.IsAvailable && acceptsPlayerLayer)
                {
                    float hitRadius = radius + math.max(0f, player.Radius);
                    playerHit = SweepPlayer(previousPosition, position, player.Position, hitRadius, out playerAt)
                        && (!groundHit || playerAt < groundAt);
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
                bool belowMinimumAltitude = position.y <= projectile.ValueRO.MinimumAltitude;
                if (playerHit || groundHit || belowMinimumAltitude || expired)
                {
                    // Use the swept contact, not the end of a step that may already be underground.
                    if (bridgeAvailable && (playerHit || groundHit))
                    {
                        float3 contact = math.lerp(previousPosition, position, playerHit ? playerAt : groundAt);
                        float3 normal = playerHit ? math.normalizesafe(contact - player.Position,
                            math.normalizesafe(previousPosition - position, math.up())) : math.up();
                        contact -= normal * radius;
                        if (!playerHit) contact.y = ground.SurfaceHeight + .025f;
                        bridge.ReceiveProjectileBurst(ProjectileVisualKind.Ranged, contact, normal, playerHit ? 1f : .85f);
                    }
                    commandBuffer.DestroyEntity(entity);
                }
            }

            commandBuffer.Playback(EntityManager);
            commandBuffer.Dispose();
        }

        private static bool SweepPlayer(float3 start, float3 end, float3 center, float radius, out float fraction)
        {
            float3 offset = start - center, delta = end - start;
            float c = math.lengthsq(offset) - radius * radius;
            fraction = 0f;
            if (c <= 0f) return true;
            float a = math.lengthsq(delta), b = math.dot(offset, delta);
            float discriminant = b * b - a * c;
            if (a < .000001f || discriminant < 0f) return false;
            fraction = (-b - math.sqrt(discriminant)) / a;
            return fraction >= 0f && fraction <= 1f;
        }
    }
}
