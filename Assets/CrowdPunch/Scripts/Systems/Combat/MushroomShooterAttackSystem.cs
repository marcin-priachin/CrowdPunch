using CrowdPunch.Components;
using CrowdPunch.Systems.AI;
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
            var owners = SystemAPI.GetComponentLookup<EnemyWaveOwnership>(true);
            var rooms = SystemAPI.GetComponentLookup<BarricadeRoom>(true);
            SystemAPI.TryGetSingletonBuffer<EnemyAttackAudioEvent>(out var audio);
            float ground = SystemAPI.GetSingleton<ArenaGroundPlane>().SurfaceHeight;
            Entity arena = SystemAPI.GetSingletonEntity<ArenaGroundPlane>();
            float dt = SystemAPI.Time.DeltaTime;
            double now = SystemAPI.Time.ElapsedTime;
            using var commands = new EntityCommandBuffer(Allocator.Temp);
            foreach (var (attackRef, laneRef, tuningRef, rangedRef, launch, pose, impacts, entity) in
                SystemAPI.Query<RefRW<RangedAttackState>, RefRW<MushroomWarningLane>, RefRO<MushroomShooterSettings>,
                    RefRO<RangedEnemySettings>, RefRO<EnemyLaunchState>, RefRO<LocalTransform>, DynamicBuffer<MushroomBeamImpact>>()
                    .WithNone<RespawnRequest>().WithEntityAccess())
            {
                ref var attack = ref attackRef.ValueRW;
                ref var lane = ref laneRef.ValueRW;
                var tuning = tuningRef.ValueRO;
                var ranged = rangedRef.ValueRO;
                float radius = math.max(.01f, ranged.ProjectileRadius);
                float range = math.max(.01f, tuning.TravelRange);
                float height = ground + math.max(radius + .02f, tuning.Height);
                bool hasSource = sources.TryGetComponent(entity, out var source);
                float duration = tuning.BeamDuration > 0f ? tuning.BeamDuration : MushroomShooterSettings.Default.BeamDuration;
                var beam = new MushroomBeam
                {
                    Arena = arena, Emitter = entity, Radius = radius, Range = range, ExpiresAt = now + duration
                };
                foreach (var impact in impacts)
                {
                    float3 impactDirection = MushroomShotGeometry.Horizontal(impact.Direction,
                        MushroomShotGeometry.Horizontal(math.forward(pose.ValueRO.Rotation), math.forward()));
                    float3 impactOrigin = impact.Position;
                    impactOrigin.y = height;
                    if (hasSource)
                        impactOrigin = impact.Position + math.mul(quaternion.LookRotationSafe(impactDirection, math.up()),
                            source.LocalPosition * pose.ValueRO.Scale);
                    beam.Origin = impactOrigin;
                    beam.Direction = impactDirection;
                    beam.Owner = EnemyLaunchOwner.Player;
                    beam.Damage = tuning.EnemyDamage;
                    beam.PunchSequence = impact.PunchSequence;
                    beam.ChainDepth = impact.ChainDepth;
                    Emit(commands, beam, audio);
                }
                impacts.Clear();

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

                if (attack.Phase == RangedAttackPhase.Ready)
                {
                    if (BarricadeRoomMovement.TryGetRoom(entity, owners, rooms, out var room)
                        && !BarricadeRoomMovement.Contains(room, player.Position)) continue;
                    float3 direction = MushroomShotGeometry.Aim(pose.ValueRO, player.Position, height,
                        hasSource, source, out float3 origin);
                    float distance = math.distance(origin.xz, player.Position.xz);
                    if (distance > math.min(ranged.EngagementRange, range)
                        || MushroomShotGeometry.FindCover(world, enemies, origin, direction,
                            math.max(0f, math.dot(player.Position - origin, direction)), radius, ground, out _))
                        continue;
                    attack.Phase = RangedAttackPhase.WindUp;
                    attack.SecondsRemaining = math.max(0f, ranged.WindUpDuration);
                    lane = new MushroomWarningLane { Origin = origin, Direction = direction, GroundHeight = ground };
                    if (attack.SecondsRemaining > .00001f)
                        audio.Record(EnemyAttackAudioKind.MushroomWindup, lane.Origin);
                }
                else
                {
                    attack.SecondsRemaining -= dt;
                }

                if (attack.SecondsRemaining > .00001f)
                {
                    lane.Length = MushroomShotGeometry.ClearDistance(world, enemies, lane.Origin, lane.Direction,
                        range, radius, ground);
                    continue;
                }
                beam.Origin = lane.Origin;
                beam.Direction = lane.Direction;
                beam.Owner = EnemyLaunchOwner.Enemy;
                beam.Damage = ranged.ProjectileDamage;
                beam.PlayerInvincibility = ranged.PlayerInvincibilitySeconds;
                beam.PlayerLayers = ranged.ProjectilePlayerLayers;
                Emit(commands, beam, audio);
                attack.ProjectilesSpawned++;
                SetCooldown(entity, ranged, ref attack);
                lane.Length = 0f; // Facing keeps the last committed direction for the release frame.
            }
            commands.Playback(state.EntityManager);
        }

        private static void Emit(EntityCommandBuffer commands, in MushroomBeam beam, DynamicBuffer<EnemyAttackAudioEvent> audio)
        {
            var entity = commands.CreateEntity();
            commands.AddComponent(entity, beam);
            if (beam.Owner == EnemyLaunchOwner.Player) commands.AddBuffer<BodyProjectileHit>(entity);
            audio.Record(EnemyAttackAudioKind.MushroomFire, beam.Origin);
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
