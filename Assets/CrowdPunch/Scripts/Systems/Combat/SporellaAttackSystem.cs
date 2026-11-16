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
    [UpdateAfter(typeof(SporellaProjectileSystem))]
    [UpdateBefore(typeof(EnemyRecoverySystem))]
    [UpdateBefore(typeof(EnemyFacingSystem))]
    public partial struct SporellaAttackSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SporellaSettings>();
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
            var lifetimes = SystemAPI.GetComponentLookup<EnemyLifetime>(true);
            var velocities = SystemAPI.GetComponentLookup<PhysicsVelocity>();
            SystemAPI.TryGetSingletonBuffer<EnemyAttackAudioEvent>(out var audio);
            float ground = SystemAPI.GetSingleton<ArenaGroundPlane>().SurfaceHeight;
            Entity arena = SystemAPI.GetSingletonEntity<ArenaGroundPlane>();
            float dt = SystemAPI.Time.DeltaTime;
            using var commands = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (attackRef, burstRef, tuning, ranged, launch, pose, entity) in
                SystemAPI.Query<RefRW<RangedAttackState>, RefRW<SporellaNormalBurst>, RefRO<SporellaSettings>,
                    RefRO<RangedEnemySettings>, RefRO<EnemyLaunchState>, RefRO<LocalTransform>>()
                    .WithNone<RespawnRequest>().WithEntityAccess())
            {
                ref var attack = ref attackRef.ValueRW;
                ref var burst = ref burstRef.ValueRW;
                var settings = tuning.ValueRO;
                var shared = ranged.ValueRO;
                if (launch.ValueRO.Phase != EnemyLaunchPhase.Active || !player.IsAvailable)
                {
                    if (attack.Phase == RangedAttackPhase.WindUp || burst.ShotsRemaining > 0)
                    {
                        attack.CancelledWindUps++;
                        EnterCooldown(entity, shared, ref attack);
                    }
                    burst = default;
                    attack.IsAttackEligible = 0;
                    continue;
                }

                if (burst.ShotsRemaining == 0)
                {
                    burst.Sweep = default;
                    if (attack.Phase is RangedAttackPhase.InitialDelay or RangedAttackPhase.Cooldown)
                    {
                        attack.SecondsRemaining -= dt;
                        if (attack.SecondsRemaining > .00001f) continue;
                        attack.Phase = RangedAttackPhase.Ready;
                    }
                    float radius = math.max(.01f, shared.ProjectileRadius);
                    float3 position = pose.ValueRO.Position;
                    float height = SporellaShotGeometry.Height(ground, radius, settings);
                    if (attack.Phase == RangedAttackPhase.Ready)
                    {
                        if (math.distancesq(position.xz, player.Position.xz) > shared.EngagementRange * shared.EngagementRange)
                            continue;
                        var candidate = SporellaShotGeometry.Aim(position, player.Position,
                            math.forward(pose.ValueRO.Rotation), height, settings);
                        if (!SporellaShotGeometry.HasClearShot(world, enemies, candidate, settings.Range, radius, ground))
                            continue;
                        attack.Phase = RangedAttackPhase.WindUp;
                        attack.IsAttackEligible = 1;
                        attack.SecondsRemaining = math.max(.01f, settings.Windup);
                        audio.Record(EnemyAttackAudioKind.SporellaWindup, candidate.Origin);
                        continue;
                    }

                    attack.SecondsRemaining -= dt;
                    if (attack.SecondsRemaining > .00001f) continue;
                    // Committed release: sample the live position once. Cover affects flight after release.
                    burst.Sweep = SporellaShotGeometry.Aim(position, player.Position,
                        math.forward(pose.ValueRO.Rotation), height, settings);
                    burst.ShotsRemaining = burst.Sweep.ShotCount;
                    burst.Wait = attack.SecondsRemaining;
                    EnterCooldown(entity, shared, ref attack);
                    if (velocities.TryGetComponent(entity, out var velocity))
                    {
                        velocity.Linear.xz = float2.zero;
                        velocities[entity] = velocity;
                    }
                }
                else burst.Wait -= dt;

                float interval = SporellaShotEmission.Interval(settings);
                uint lifetime = lifetimes.TryGetComponent(entity, out var life) ? life.Generation : 0;
                while (burst.ShotsRemaining > 0 && burst.Wait <= .00001f)
                {
                    var shot = SporellaShotEmission.Create(arena, entity, lifetime, SporellaShotKind.EnemyShot, settings, shared);
                    SporellaShotEmission.FireSweepShot(commands, shot, burst.Sweep, burst.ShotsRemaining, audio);
                    burst.ShotsRemaining--;
                    burst.Wait += interval;
                    attack.ProjectilesSpawned++;
                }
            }
            commands.Playback(state.EntityManager);
        }

        private static void EnterCooldown(Entity shooter, in RangedEnemySettings settings, ref RangedAttackState attack)
        {
            var random = Random.CreateFromIndex(math.hash(new uint3((uint)shooter.Index,
                attack.ProjectilesSpawned, attack.CancelledWindUps)) % uint.MaxValue);
            attack.Phase = RangedAttackPhase.Cooldown;
            attack.IsAttackEligible = 0;
            attack.SecondsRemaining = math.max(0f, settings.Cooldown)
                + random.NextFloat() * math.max(0f, settings.CooldownVariation);
        }
    }
}
