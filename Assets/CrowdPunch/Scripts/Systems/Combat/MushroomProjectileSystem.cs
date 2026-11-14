using System.Collections.Generic;
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
    [UpdateAfter(typeof(ExplosionResolutionSystem))]
    [UpdateBefore(typeof(EnemyRecoverySystem))]
    [UpdateBefore(typeof(PlayerContactDamageSystem))]
    public partial struct MushroomProjectileSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<MushroomProjectile>();
            state.RequireForUpdate<PlayerSnapshot>();
            state.RequireForUpdate<PhysicsWorldSingleton>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var em = state.EntityManager;
            var world = SystemAPI.GetSingleton<PhysicsWorldSingleton>().CollisionWorld;
            var player = SystemAPI.GetSingleton<PlayerSnapshot>();
            var enemies = SystemAPI.GetComponentLookup<Enemy>(true);
            PlayerBridgeRegistry.TryGetBridge(out var bridge);
            float dt = SystemAPI.Time.DeltaTime;
            double now = SystemAPI.Time.ElapsedTime;
            using var commands = new EntityCommandBuffer(Allocator.Temp);
            var hits = new NativeList<ColliderCastHit>(Allocator.Temp);
            foreach (var (projectile, pose, history, entity) in SystemAPI
                .Query<RefRW<MushroomProjectile>, RefRW<LocalTransform>, DynamicBuffer<MushroomProjectileHit>>()
                .WithEntityAccess())
            {
                ref var shot = ref projectile.ValueRW;
                if (!em.Exists(shot.Arena) || !em.HasComponent<ArenaGroundPlane>(shot.Arena))
                {
                    commands.DestroyEntity(entity);
                    continue;
                }

                float distance = math.min(shot.RangeRemaining, shot.Speed * dt);
                float3 start = pose.ValueRO.Position;
                float3 displacement = shot.Direction * distance;
                float playerRadius = shot.Radius + math.max(0f, player.Radius);
                if (shot.ExitingRedirectContact && math.distancesq(start, player.Position) > playerRadius * playerRadius)
                    shot.ExitingRedirectContact = false;
                float stopAt = 1f;
                bool playerHit = !shot.ExitingRedirectContact && player.IsAvailable
                    && (shot.PlayerLayers & player.CollisionLayer) != 0
                    && MushroomShotGeometry.SweepPlayer(start, displacement, player.Position, playerRadius, out stopAt);
                if (!playerHit) stopAt = 1f;
                bool coverHit = MushroomShotGeometry.FindCover(world, enemies, start, shot.Direction,
                    distance, shot.Radius, out var cover) && (!playerHit || cover.Fraction <= stopAt);
                if (coverHit)
                {
                    stopAt = cover.Fraction;
                    playerHit = false;
                }
                if (shot.Owner == EnemyLaunchOwner.Player && distance > .00001f)
                {
                    hits.Clear();
                    world.SphereCastAll(start, shot.Radius, shot.Direction, distance, ref hits,
                        MushroomShotGeometry.Filter, QueryInteraction.IgnoreTriggers);
                    hits.Sort(new TravelOrder());
                    foreach (var hit in hits)
                    {
                        // Cover and player contact win ties. With no terminal hit, include the end of the step.
                        if (hit.Fraction > stopAt || (coverHit || playerHit) && hit.Fraction >= stopAt) break;
                        if (!TryDamage(em, hit.Entity, entity, history, shot, now)) continue;
                        bridge?.ReceiveProjectileBurst(ProjectileVisualKind.MushroomPlayer, hit.Position,
                            hit.SurfaceNormal, .65f);
                    }
                }
                pose.ValueRW.Position = start + displacement * stopAt;
                shot.RangeRemaining -= distance * stopAt;
                if (shot.ExitingRedirectContact && math.distancesq(pose.ValueRO.Position, player.Position) > playerRadius * playerRadius)
                    shot.ExitingRedirectContact = false;
                if (playerHit) bridge?.ReceiveEnemyHit(shot.PlayerDamage, shot.PlayerInvincibility, float3.zero);
                if (playerHit || coverHit)
                {
                    var kind = shot.Owner == EnemyLaunchOwner.Player ? ProjectileVisualKind.MushroomPlayer : ProjectileVisualKind.MushroomEnemy;
                    bridge?.ReceiveProjectileBurst(kind, pose.ValueRO.Position, coverHit ? cover.SurfaceNormal : -shot.Direction);
                }
                if (playerHit || coverHit || shot.RangeRemaining <= .00001f) commands.DestroyEntity(entity);
            }
            commands.Playback(em);
            hits.Dispose();
        }

        internal static bool TryDamage(EntityManager em, Entity target, Entity shotEntity,
            DynamicBuffer<MushroomProjectileHit> history, in MushroomProjectile shot, double now)
        {
            if (!em.HasComponent<Enemy>(target) || !em.HasComponent<EnemyTier>(target)
                || em.GetComponentData<EnemyTier>(target).Value == EnemyCombatTier.Boss
                || em.HasComponent<BossPart>(target) || em.HasComponent<Barricade>(target)
                || em.HasComponent<RespawnRequest>(target) && em.IsComponentEnabled<RespawnRequest>(target)
                || !em.HasComponent<DamageRequest>(target) || !em.HasComponent<Health>(target)
                || em.GetComponentData<Health>(target).Current <= 0f
                || SnakeHitRules.Protected(em, target) || SnakeHitRules.Attached(em, target)) return false;
            if (em.HasComponent<EnemyArmor>(target))
            {
                var armor = em.GetComponentData<EnemyArmor>(target);
                if (armor.Stages > 0 || now < armor.ProtectedUntil) return false;
            }
            uint generation = em.HasComponent<EnemyLifetime>(target) ? em.GetComponentData<EnemyLifetime>(target).Generation : 0;
            foreach (var hit in history)
                if (hit.Enemy == target && hit.Lifetime == generation) return false;
            history.Add(new MushroomProjectileHit { Enemy = target, Lifetime = generation });
            EnemyDamageResolution.ApplyPending(em, target, now);
            var health = em.GetComponentData<Health>(target);
            if (health.Current <= 0f) return false;
            var damage = new DamageRequest();
            damage.Add(shot.EnemyDamage, health.Current, shot.PunchSequence);
            em.SetComponentData(target, damage);
            em.SetComponentEnabled<DamageRequest>(target, true);
            EnemyDamageResolution.ApplyPending(em, target, now, EnemyLaunchOwner.Player, shotEntity, 1);
            return true;
        }

        private struct TravelOrder : IComparer<ColliderCastHit>
        {
            public int Compare(ColliderCastHit a, ColliderCastHit b)
            {
                int fraction = a.Fraction.CompareTo(b.Fraction);
                return fraction != 0 ? fraction : a.Entity.Index.CompareTo(b.Entity.Index);
            }
        }
    }
}
