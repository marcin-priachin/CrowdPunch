using CrowdPunch.Components;
using CrowdPunch.Mono.Player;
using CrowdPunch.Systems.Groups;
using CrowdPunch.Systems.Physics;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace CrowdPunch.Systems.Combat
{
    [UpdateInGroup(typeof(GamePostPhysicsGroup))]
    [UpdateAfter(typeof(AlienSpikeEmissionSystem)), UpdateBefore(typeof(ExplosionResolutionSystem))]
    [UpdateBefore(typeof(EnemyRecoverySystem))]
    public partial struct AlienSpikeProjectileSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<AlienSpikeProjectile>();
            state.RequireForUpdate<PhysicsWorldSingleton>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var em = state.EntityManager;
            var world = SystemAPI.GetSingleton<PhysicsWorldSingleton>().CollisionWorld;
            SystemAPI.TryGetSingleton<PlayerSnapshot>(out var player);
            SystemAPI.TryGetSingleton<EnemyLaunchSettings>(out var guidance);
            float dt = SystemAPI.Time.DeltaTime;
            double now = SystemAPI.Time.ElapsedTime;
            PlayerBridgeRegistry.TryGetBridge(out var bridge);
            using var commands = new EntityCommandBuffer(Allocator.Temp);

            var collector = Contacts(ref state);
            foreach (var (projectile, pose, entity) in
                SystemAPI.Query<RefRW<AlienSpikeProjectile>, RefRW<LocalTransform>>().WithEntityAccess())
            {
                ref var shot = ref projectile.ValueRW;
                if (!WaveIsCurrent(em, shot.Wave))
                {
                    commands.DestroyEntity(entity);
                    continue;
                }

                float time = math.min(dt, shot.Remaining);
                shot.Remaining -= time;
                float3 start = pose.ValueRO.Position;
                AlienSpikeProjectilePunch.Guide(em, entity, ref shot, start, guidance, time);
                float3 displacement = shot.Velocity * time;
                var contact = AlienSpikeShotContact.Sweep(world, collector, shot, player, start, displacement);
                pose.ValueRW.Position += displacement * contact.MaxFraction;
                if (contact.Kind == AlienSpikeContactKind.Player)
                    bridge?.ReceiveEnemyHit(shot.PlayerOwned != 0 ? shot.Tuning.BurstDamage : shot.Tuning.NormalDamage,
                        shot.Tuning.PlayerProtection, float3.zero);
                else if (contact.Kind == AlienSpikeContactKind.Enemy)
                    ApplyHit(em, contact.Target, shot, now);

                if (contact.Kind != AlienSpikeContactKind.None)
                    bridge?.ReceiveProjectileBurst(ProjectileVisualKind.AlienSpike, contact.Position, contact.Normal,
                        owner: shot.PlayerOwned != 0 ? EnemyLaunchOwner.Player : EnemyLaunchOwner.Enemy);

                if (contact.Kind != AlienSpikeContactKind.None || shot.Remaining <= 0)
                    commands.DestroyEntity(entity);
            }
            commands.Playback(em);
        }

        internal static AlienSpikeShotContact Contacts(ref SystemState state)
        {
            var em = state.EntityManager;
            em.CompleteDependencyBeforeRO<Enemy>();
            em.CompleteDependencyBeforeRO<EnemyTier>();
            em.CompleteDependencyBeforeRO<EnemyLaunchState>();
            em.CompleteDependencyBeforeRO<Health>();
            em.CompleteDependencyBeforeRO<RespawnRequest>();
            em.CompleteDependencyBeforeRO<SnakeHead>();
            em.CompleteDependencyBeforeRO<SnakeSegment>();
            em.CompleteDependencyBeforeRO<ExplosiveEnemyState>();
            return new AlienSpikeShotContact
            {
                Enemies = state.GetComponentLookup<Enemy>(true),
                Tiers = state.GetComponentLookup<EnemyTier>(true),
                Launches = state.GetComponentLookup<EnemyLaunchState>(true),
                Health = state.GetComponentLookup<Health>(true),
                Respawns = state.GetComponentLookup<RespawnRequest>(true),
                SnakeHeads = state.GetComponentLookup<SnakeHead>(true),
                SnakeSegments = state.GetComponentLookup<SnakeSegment>(true),
                Exploders = state.GetComponentLookup<ExplosiveEnemyState>(true)
            };
        }

        internal static bool WaveIsCurrent(EntityManager em, in EnemyWaveOwnership wave)
        {
            if (wave.Sequence == Entity.Null) return true;
            if (!em.HasComponent<EnemyWaveSequence>(wave.Sequence)) return false;
            var sequence = em.GetComponentData<EnemyWaveSequence>(wave.Sequence);
            return sequence.RunGeneration == wave.RunGeneration && sequence.CurrentWaveIndex == wave.WaveIndex
                && sequence.Phase != EnemyWaveRuntimePhase.Complete;
        }

        internal static void ApplyHit(EntityManager em, Entity target, in AlienSpikeProjectile shot, double now)
        {
            if (ArmorHitResolution.IsProtected(em, target)
                || em.HasComponent<EnemyArmor>(target) && now < em.GetComponentData<EnemyArmor>(target).ProtectedUntil) return;
            if (shot.Tuning.ExploderMode == AlienSpikeExploderMode.DetonateOnHit
                && em.HasComponent<ExplosiveDetonationRequest>(target))
            {
                ExplosionResolutionSystem.RequestDetonation(em, target, shot.PunchSequence);
                return;
            }

            EnemyDamageResolution.ApplyPending(em, target, now);
            float remainingHealth = em.GetComponentData<Health>(target).Current;
            if (remainingHealth <= 0f || shot.Tuning.BurstDamage <= 0f) return;
            var damage = new DamageRequest();
            damage.Add(shot.Tuning.BurstDamage, remainingHealth, shot.PunchSequence);
            em.SetComponentData(target, damage);
            em.SetComponentEnabled<DamageRequest>(target, true);
            EnemyDamageResolution.ApplyPending(em, target, now, EnemyLaunchOwner.Player, shot.Source,
                shot.ChainDepth, shot.SourceLifetime);
        }
    }
}

