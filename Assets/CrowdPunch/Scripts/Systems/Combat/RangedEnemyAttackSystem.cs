using CrowdPunch.Components;
using CrowdPunch.Systems.Groups;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace CrowdPunch.Systems.Combat
{
    /// <summary>Owns ranged eligibility, wind-up, projectile emission, cooldown, and cancellation.</summary>
    [BurstCompile]
    [UpdateInGroup(typeof(GamePrePhysicsGroup))]
    [UpdateAfter(typeof(DamageApplicationSystem))]
    [UpdateBefore(typeof(CrowdPunch.Systems.Physics.ApplyImpulseSystem))]
    public partial struct RangedEnemyAttackSystem : ISystem
    {
        private const float ProjectileSpawnHeight = 1f;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerSnapshot>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            PlayerSnapshot player = SystemAPI.GetSingleton<PlayerSnapshot>();
            float deltaTime = SystemAPI.Time.DeltaTime;
            EntityCommandBuffer commandBuffer = new EntityCommandBuffer(Allocator.Temp);

            foreach ((RefRW<RangedAttackState> attack,
                         RefRO<RangedEnemySettings> settings,
                         RefRO<EnemyLaunchState> launchState,
                         RefRO<LocalTransform> transform,
                         EnabledRefRO<RespawnRequest> respawnEnabled) in
                     SystemAPI.Query<RefRW<RangedAttackState>, RefRO<RangedEnemySettings>, RefRO<EnemyLaunchState>,
                             RefRO<LocalTransform>, EnabledRefRO<RespawnRequest>>()
                         .WithAll<Enemy>()
                         .WithOptions(EntityQueryOptions.IgnoreComponentEnabledState))
            {
                bool active = launchState.ValueRO.Phase == EnemyLaunchPhase.Active && !respawnEnabled.ValueRO;
                if (!active || !player.IsAvailable)
                {
                    CancelWindUp(ref attack.ValueRW);
                    attack.ValueRW.IsAttackEligible = 0;
                    continue;
                }

                float distance = math.distance(transform.ValueRO.Position.xz, player.Position.xz);
                bool inRange = distance <= math.max(0f, settings.ValueRO.EngagementRange);

                if (attack.ValueRO.Phase == RangedAttackPhase.InitialDelay
                    || attack.ValueRO.Phase == RangedAttackPhase.Cooldown)
                {
                    attack.ValueRW.SecondsRemaining = math.max(0f, attack.ValueRO.SecondsRemaining - deltaTime);
                    if (attack.ValueRO.SecondsRemaining <= 0f)
                    {
                        attack.ValueRW.Phase = RangedAttackPhase.Ready;
                    }
                }

                attack.ValueRW.IsAttackEligible = (byte)(inRange && attack.ValueRO.Phase == RangedAttackPhase.Ready ? 1 : 0);
                if (attack.ValueRO.Phase == RangedAttackPhase.Ready)
                {
                    if (inRange && settings.ValueRO.ProjectilePrefab != Entity.Null)
                    {
                        attack.ValueRW.Phase = RangedAttackPhase.WindUp;
                        attack.ValueRW.SecondsRemaining = math.max(0f, settings.ValueRO.WindUpDuration);
                    }

                    continue;
                }

                if (attack.ValueRO.Phase != RangedAttackPhase.WindUp)
                {
                    continue;
                }

                attack.ValueRW.SecondsRemaining -= deltaTime;
                if (attack.ValueRO.SecondsRemaining > 0f)
                {
                    continue;
                }

                SpawnProjectile(ref commandBuffer, transform.ValueRO.Position, player.Position, settings.ValueRO);
                attack.ValueRW.Phase = RangedAttackPhase.Cooldown;
                attack.ValueRW.SecondsRemaining = math.max(0f, settings.ValueRO.Cooldown);
                attack.ValueRW.IsAttackEligible = 0;
                attack.ValueRW.ProjectilesSpawned++;
            }

            commandBuffer.Playback(state.EntityManager);
            commandBuffer.Dispose();
        }

        private static void CancelWindUp(ref RangedAttackState attack)
        {
            if (attack.Phase != RangedAttackPhase.WindUp)
            {
                return;
            }

            attack.Phase = RangedAttackPhase.Cooldown;
            attack.SecondsRemaining = 0f;
            attack.CancelledWindUps++;
        }

        private static void SpawnProjectile(
            ref EntityCommandBuffer commandBuffer,
            float3 shooterPosition,
            float3 playerPosition,
            RangedEnemySettings settings)
        {
            float3 start = shooterPosition + new float3(0f, ProjectileSpawnHeight, 0f);
            float3 target = playerPosition;
            Entity projectile = commandBuffer.Instantiate(settings.ProjectilePrefab);
            commandBuffer.SetComponent(projectile, LocalTransform.FromPosition(start));
            commandBuffer.SetComponent(projectile, new RangedProjectile
            {
                Start = start,
                Target = target,
                TravelDuration = math.max(0.01f, settings.ProjectileTravelDuration),
                ArcHeight = math.max(0f, settings.ProjectileArcHeight),
                Lifetime = math.max(0.01f, settings.ProjectileLifetime),
                Radius = math.max(0.01f, settings.ProjectileRadius),
                Damage = math.max(0f, settings.ProjectileDamage),
                PlayerInvincibilitySeconds = math.max(0f, settings.PlayerInvincibilitySeconds),
                PlayerCollisionLayers = settings.ProjectilePlayerLayers
            });
        }
    }
}
