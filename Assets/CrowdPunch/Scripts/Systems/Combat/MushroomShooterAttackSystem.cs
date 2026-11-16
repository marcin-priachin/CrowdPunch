using CrowdPunch.Components;
using CrowdPunch.Systems.Groups;
using CrowdPunch.Systems.Physics;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace CrowdPunch.Systems.Combat
{
    [BurstCompile]
    [UpdateInGroup(typeof(GamePostPhysicsGroup))]
    [UpdateAfter(typeof(EnemyLaunchCollisionSystem))]
    [UpdateAfter(typeof(ExplosionResolutionSystem))]
    [UpdateAfter(typeof(MushroomProjectileSystem))]
    [UpdateBefore(typeof(EnemyRecoverySystem))]
    [UpdateBefore(typeof(EnemyFacingSystem))]
    public partial struct MushroomShooterAttackSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<MushroomWarningLane>();
            state.RequireForUpdate<PlayerSnapshot>();
            state.RequireForUpdate<ArenaGroundPlane>();
            state.RequireForUpdate<PhysicsWorldSingleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var player = SystemAPI.GetSingleton<PlayerSnapshot>();
            var world = SystemAPI.GetSingleton<PhysicsWorldSingleton>().CollisionWorld;
            var enemies = SystemAPI.GetComponentLookup<Enemy>(true);
            var sources = SystemAPI.GetComponentLookup<MushroomShotSource>(true);
            SystemAPI.TryGetSingletonBuffer<EnemyAttackAudioEvent>(out var audio);
            float ground = SystemAPI.GetSingleton<ArenaGroundPlane>().SurfaceHeight;
            Entity arena = SystemAPI.GetSingletonEntity<ArenaGroundPlane>();
            float dt = SystemAPI.Time.DeltaTime;
            using var commands = new EntityCommandBuffer(Allocator.Temp);
            foreach (var (attackRef, laneRef, tuningRef, rangedRef, launch, pose, entity) in
                SystemAPI.Query<RefRW<RangedAttackState>, RefRW<MushroomWarningLane>, RefRO<MushroomShooterSettings>,
                    RefRO<RangedEnemySettings>, RefRO<EnemyLaunchState>, RefRO<LocalTransform>>()
                    .WithNone<RespawnRequest>().WithEntityAccess())
            {
                ref var attack = ref attackRef.ValueRW;
                ref var lane = ref laneRef.ValueRW;
                var tuning = tuningRef.ValueRO;
                var ranged = rangedRef.ValueRO;
                if (launch.ValueRO.Phase != EnemyLaunchPhase.Active || !player.IsAvailable)
                {
                    if (attack.Phase == RangedAttackPhase.WindUp)
                    {
                        attack.CancelledWindUps++;
                        SetCooldown(entity, ranged, ref attack);
                    }
                    attack.IsAttackEligible = 0;
                    lane = default;
                    continue;
                }

                if (attack.Phase == RangedAttackPhase.InitialDelay || attack.Phase == RangedAttackPhase.Cooldown)
                {
                    lane = default;
                    attack.SecondsRemaining = math.max(0f, attack.SecondsRemaining - dt);
                    if (attack.SecondsRemaining > .00001f) continue;
                    attack.Phase = RangedAttackPhase.Ready;
                }

                float radius = math.max(.01f, ranged.ProjectileRadius);
                float range = math.max(.01f, tuning.TravelRange);
                float height = ground + math.max(radius + .02f, tuning.Height);
                float lockedSeconds = math.max(0f, tuning.LockedSeconds);
                float trackingSeconds = math.max(0f, tuning.TrackingSeconds);
                float3 direction = MushroomShotGeometry.Aim(pose.ValueRO, player.Position, height,
                    sources.TryGetComponent(entity, out var source), source, out float3 origin);
                bool starting = attack.Phase == RangedAttackPhase.Ready;
                bool wasLocked = !starting && lane.Locked;
                if (starting)
                {
                    float distance = math.distance(origin.xz, player.Position.xz);
                    if (distance > math.min(ranged.EngagementRange, range)
                        || MushroomShotGeometry.FindCover(world, enemies, origin, direction,
                            math.max(0f, math.dot(player.Position - origin, direction)), radius, ground, out _))
                        continue;
                    attack.Phase = RangedAttackPhase.WindUp;
                    attack.SecondsRemaining = trackingSeconds + lockedSeconds;
                    lane = new MushroomWarningLane { Origin = origin, Direction = direction,
                        GroundHeight = ground, Locked = trackingSeconds <= .00001f };
                }
                else
                {
                    if (!lane.Locked)
                    {
                        lane.Origin = origin;
                        lane.Direction = direction;
                    }
                    attack.SecondsRemaining -= dt;
                    if (attack.SecondsRemaining <= lockedSeconds + .00001f) lane.Locked = true;
                }

                // Keep checking cover, including moving cover, against the same frozen line after lock.
                lane.Length = MushroomShotGeometry.ClearDistance(world, enemies, lane.Origin, lane.Direction,
                    range, radius, ground);
                if (attack.SecondsRemaining > .00001f)
                {
                    if (lane.Locked && !wasLocked)
                        audio.Record(EnemyAttackAudioKind.MushroomLock, lane.Origin);
                    else if (starting)
                        audio.Record(EnemyAttackAudioKind.MushroomWindup, lane.Origin);
                    continue;
                }
                var projectile = commands.CreateEntity();
                commands.AddComponent(projectile, LocalTransform.FromPosition(lane.Origin));
                commands.AddComponent(projectile, new MushroomProjectile
                {
                    Arena = arena, Direction = lane.Direction, Owner = EnemyLaunchOwner.Enemy,
                    Radius = radius, Range = range, RangeRemaining = range,
                    Speed = math.max(.01f, ranged.ProjectileSpeed), PlayerDamage = ranged.ProjectileDamage,
                    PlayerInvincibility = ranged.PlayerInvincibilitySeconds, EnemyDamage = tuning.EnemyDamage,
                    PlayerLayers = ranged.ProjectilePlayerLayers
                });
                commands.AddBuffer<BodyProjectileHit>(projectile);
                audio.Record(EnemyAttackAudioKind.MushroomFire, lane.Origin);
                attack.ProjectilesSpawned++;
                SetCooldown(entity, ranged, ref attack);
                lane.Length = 0f; // Facing keeps the last committed direction for the release frame.
            }
            commands.Playback(state.EntityManager);
        }

        private static void SetCooldown(Entity shooter, in RangedEnemySettings settings, ref RangedAttackState attack)
        {
            var random = Random.CreateFromIndex(math.hash(new uint3((uint)shooter.Index,
                attack.ProjectilesSpawned, attack.CancelledWindUps)) % uint.MaxValue);
            attack.Phase = RangedAttackPhase.Cooldown;
            attack.SecondsRemaining = math.max(0f, settings.Cooldown)
                + random.NextFloat() * math.max(0f, settings.CooldownVariation);
        }
    }
}
