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
                         EnabledRefRO<RespawnRequest> respawnEnabled,
                         Entity shooter) in
                     SystemAPI.Query<RefRW<RangedAttackState>, RefRO<RangedEnemySettings>, RefRO<EnemyLaunchState>,
                             RefRO<LocalTransform>, EnabledRefRO<RespawnRequest>>()
                         .WithAll<Enemy>()
                         .WithOptions(EntityQueryOptions.IgnoreComponentEnabledState)
                         .WithEntityAccess())
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

                uint shotSequence = attack.ValueRO.ProjectilesSpawned + 1;
                Random random = CreateShotRandom(shooter, shotSequence);
                SpawnProjectile(
                    ref commandBuffer,
                    transform.ValueRO.Position,
                    player.Position,
                    settings.ValueRO,
                    ref random);
                attack.ValueRW.Phase = RangedAttackPhase.Cooldown;
                attack.ValueRW.SecondsRemaining = math.max(0f, settings.ValueRO.Cooldown)
                    + random.NextFloat(0f, math.max(0f, settings.ValueRO.CooldownVariation));
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
            RangedEnemySettings settings,
            ref Random random)
        {
            float3 start = shooterPosition + new float3(0f, ProjectileSpawnHeight, 0f);
            float angle = random.NextFloat(0f, math.PI * 2f);
            float spreadDistance = math.sqrt(random.NextFloat()) * math.max(0f, settings.ProjectileAimSpreadRadius);
            float3 spread = new float3(math.cos(angle), 0f, math.sin(angle)) * spreadDistance;
            float3 target = playerPosition
                + spread
                + new float3(0f, settings.ProjectileAimTargetYOffset, 0f);
            float horizontalDistance = math.distance(start.xz, target.xz);
            float travelDuration = horizontalDistance / math.max(0.01f, settings.ProjectileSpeed);
            Entity projectile = commandBuffer.Instantiate(settings.ProjectilePrefab);
            commandBuffer.SetComponent(projectile, LocalTransform.FromPosition(start));
            commandBuffer.SetComponent(projectile, new RangedProjectile
            {
                Start = start,
                Target = target,
                TravelDuration = math.max(0.01f, travelDuration),
                ArcHeight = math.max(0f, settings.ProjectileArcHeight),
                MinimumAltitude = settings.ProjectileMinimumAltitude,
                Lifetime = math.max(0.01f, settings.ProjectileLifetime),
                Radius = math.max(0.01f, settings.ProjectileRadius),
                Damage = math.max(0f, settings.ProjectileDamage),
                PlayerInvincibilitySeconds = math.max(0f, settings.PlayerInvincibilitySeconds),
                PlayerCollisionLayers = settings.ProjectilePlayerLayers
            });
        }

        private static Random CreateShotRandom(Entity shooter, uint shotSequence)
        {
            uint seed = (uint)math.max(1, shooter.Index + 1) * 747796405u;
            seed ^= (uint)math.max(1, shooter.Version + 1) * 2891336453u;
            seed ^= math.max(1u, shotSequence) * 277803737u;
            return Random.CreateFromIndex(seed);
        }
    }
}
