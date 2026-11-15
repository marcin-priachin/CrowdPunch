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
    [UpdateAfter(typeof(EnemyLaunchCollisionSystem))]
    [UpdateAfter(typeof(ExplosionResolutionSystem))]
    [UpdateBefore(typeof(EnemyRecoverySystem))]
    [UpdateBefore(typeof(PlayerContactDamageSystem))]
    public partial struct SporellaProjectileSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SporellaProjectile>();
            state.RequireForUpdate<PlayerSnapshot>();
            state.RequireForUpdate<PhysicsWorldSingleton>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var em = state.EntityManager;
            var world = SystemAPI.GetSingleton<PhysicsWorldSingleton>().CollisionWorld;
            bool hasArena = SystemAPI.TryGetSingleton<ArenaGroundPlane>(out var floor);
            Entity arena = hasArena ? SystemAPI.GetSingletonEntity<ArenaGroundPlane>() : Entity.Null;
            var player = SystemAPI.GetSingleton<PlayerSnapshot>();
            var enemies = SystemAPI.GetComponentLookup<Enemy>(true);
            var lifetimes = SystemAPI.GetComponentLookup<EnemyLifetime>(true);
            PlayerBridgeRegistry.TryGetBridge(out var bridge);
            float dt = SystemAPI.Time.DeltaTime;
            double now = SystemAPI.Time.ElapsedTime;
            using var commands = new EntityCommandBuffer(Allocator.Temp);
            var hits = new NativeList<ColliderCastHit>(Allocator.Temp);

            foreach (var (shotRef, pose, entity) in SystemAPI.Query<RefRW<SporellaProjectile>, RefRW<LocalTransform>>().WithEntityAccess())
            {
                ref var shot = ref shotRef.ValueRW;
                if (!hasArena || shot.Arena != arena)
                {
                    commands.DestroyEntity(entity);
                    continue;
                }

                bool playerOwned = shot.Kind != SporellaShotKind.EnemyShot;
                var visual = playerOwned ? ProjectileVisualKind.SporellaPlayer : ProjectileVisualKind.SporellaEnemy;
                var history = playerOwned ? em.GetBuffer<BodyProjectileHit>(entity) : default;
                float playerRadius = shot.Radius + math.max(0f, player.Radius);
                float length = math.min(shot.Range - shot.Distance, shot.Speed * dt);
                if (length <= 0f)
                {
                    if (shot.Distance >= shot.Range) commands.DestroyEntity(entity);
                    continue;
                }

                float3 start = pose.ValueRO.Position;
                float3 displacement = shot.Direction * length;
                bool skipEmitter = !lifetimes.TryGetComponent(shot.Emitter, out var emitterLife)
                    || emitterLife.Generation == shot.EmitterLifetime;
                if (shot.ExitingRedirectContact && math.distancesq(start, player.Position) > playerRadius * playerRadius)
                    shot.ExitingRedirectContact = false;

                float stopAt = 1f;
                bool playerHit = !shot.ExitingRedirectContact && player.IsAvailable
                    && (shot.PlayerLayers & player.CollisionLayer) != 0
                    && MushroomShotGeometry.SweepPlayer(start, displacement, player.Position, playerRadius, out stopAt);
                if (!playerHit) stopAt = 1f;
                bool coverHit = MushroomShotGeometry.FindCover(world, enemies, start, shot.Direction, length,
                    shot.Radius, floor.SurfaceHeight, out var cover) && cover.Fraction <= stopAt;
                if (coverHit)
                {
                    stopAt = cover.Fraction;
                    playerHit = false;
                }

                bool enemyStop = false;
                if (playerOwned && length > .00001f)
                {
                    hits.Clear();
                    world.SphereCastAll(start, shot.Radius, shot.Direction, length, ref hits,
                        MushroomShotGeometry.Filter, QueryInteraction.IgnoreTriggers);
                    hits.Sort(new TravelOrder());
                    foreach (var hit in hits)
                    {
                        if (hit.Fraction > stopAt || ((coverHit || playerHit) && hit.Fraction >= stopAt)) break;
                        if (shot.Kind == SporellaShotKind.ImpactShot && skipEmitter && hit.Entity == shot.Emitter)
                            continue;
                        if (!BodyProjectileDamage.TryDamage(em, hit.Entity, entity, history, shot.EnemyDamage,
                            shot.PunchSequence, now, shot.ChainDepth)) continue;
                        bridge?.ReceiveProjectileBurst(visual, hit.Position, hit.SurfaceNormal, .65f);
                        if (shot.Kind != SporellaShotKind.ImpactShot) continue;
                        enemyStop = true;
                        stopAt = hit.Fraction;
                        break;
                    }
                }
                pose.ValueRW.Position = start + displacement * stopAt;
                shot.Distance += length * stopAt;
                if (shot.ExitingRedirectContact && math.distancesq(pose.ValueRO.Position, player.Position) > playerRadius * playerRadius)
                    shot.ExitingRedirectContact = false;

                if (playerHit && !enemyStop)
                    bridge?.ReceiveEnemyHit(shot.PlayerDamage, shot.PlayerInvincibility, float3.zero);
                if ((playerHit || coverHit) && !enemyStop)
                    bridge?.ReceiveProjectileBurst(visual, pose.ValueRO.Position,
                        coverHit ? cover.SurfaceNormal : -shot.Direction);
                if (playerHit || coverHit || enemyStop || shot.Distance >= shot.Range) commands.DestroyEntity(entity);
            }
            commands.Playback(em);
            hits.Dispose();
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
