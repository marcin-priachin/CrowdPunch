using System.Collections.Generic;
using CrowdPunch.Components;
using CrowdPunch.Mono.Player;
using CrowdPunch.Systems.Groups;
using CrowdPunch.Systems.Physics;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;

namespace CrowdPunch.Systems.Combat
{
    [UpdateInGroup(typeof(GamePostPhysicsGroup))]
    [UpdateAfter(typeof(MushroomShooterAttackSystem))]
    [UpdateBefore(typeof(EnemyRecoverySystem))]
    [UpdateBefore(typeof(PlayerContactDamageSystem))]
    public partial struct MushroomBeamSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<MushroomBeam>();
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
            double now = SystemAPI.Time.ElapsedTime;
            using var commands = new EntityCommandBuffer(Allocator.Temp);
            var hits = new NativeList<ColliderCastHit>(Allocator.Temp);
            foreach (var (beamRef, entity) in SystemAPI.Query<RefRW<MushroomBeam>>().WithEntityAccess())
            {
                ref var beam = ref beamRef.ValueRW;
                bool hostile = beam.Owner == EnemyLaunchOwner.Enemy;
                if (now >= beam.ExpiresAt || !em.HasComponent<ArenaGroundPlane>(beam.Arena)
                    || hostile && !CanContinueFiring(em, beam.Emitter))
                {
                    commands.DestroyEntity(entity);
                    continue;
                }

                float ground = em.GetComponentData<ArenaGroundPlane>(beam.Arena).SurfaceHeight;
                bool blocked = MushroomShotGeometry.FindCover(world, enemies, beam.Origin, beam.Direction,
                    beam.Range, beam.Radius, ground, out var cover);
                float end = blocked ? cover.Fraction : 1f;
                beam.Length = beam.Range * end;
                if (blocked && cover.Entity != beam.CoverContact)
                    bridge?.ReceiveProjectileBurst(ProjectileVisualKind.MushroomBeam, cover.Position, cover.SurfaceNormal,
                        owner: beam.Owner, playSound: false);
                beam.CoverContact = blocked ? cover.Entity : Entity.Null;

                if (hostile)
                {
                    if (!beam.HitPlayer && player.IsAvailable && (beam.PlayerLayers & player.CollisionLayer) != 0
                        && MushroomShotGeometry.SweepPlayer(beam.Origin, beam.Direction * beam.Range, player.Position,
                            beam.Radius + math.max(0f, player.Radius), out float fraction)
                        && (!blocked || fraction < end))
                    {
                        beam.HitPlayer = true;
                        bridge?.ReceiveEnemyHit(beam.Damage, beam.PlayerInvincibility, float3.zero);
                        float3 center = beam.Origin + beam.Direction * (beam.Range * fraction);
                        float3 normal = math.normalizesafe(center - player.Position, -beam.Direction);
                        bridge?.ReceiveProjectileBurst(ProjectileVisualKind.MushroomBeam,
                            player.Position + normal * math.max(0f, player.Radius), normal, 1.2f,
                            beam.Owner, playSound: false);
                    }
                    continue;
                }

                hits.Clear();
                world.SphereCastAll(beam.Origin, beam.Radius, beam.Direction, beam.Range, ref hits,
                    MushroomShotGeometry.Filter, QueryInteraction.IgnoreTriggers);
                hits.Sort(new ContactOrder());
                var history = em.GetBuffer<BodyProjectileHit>(entity);
                foreach (var hit in hits)
                {
                    if (blocked && hit.Fraction >= end) break;
                    if (hit.Entity == beam.Emitter) continue;
                    if (BodyProjectileDamage.TryDamage(em, hit.Entity, entity, history,
                        beam.Damage, beam.PunchSequence, now, beam.ChainDepth))
                        bridge?.ReceiveProjectileBurst(ProjectileVisualKind.MushroomBeam, hit.Position, hit.SurfaceNormal,
                            .9f, beam.Owner);
                }
            }
            commands.Playback(em);
            hits.Dispose();
        }

        private static bool CanContinueFiring(EntityManager em, Entity emitter)
            => em.HasComponent<EnemyLaunchState>(emitter)
                && em.GetComponentData<EnemyLaunchState>(emitter).Phase == EnemyLaunchPhase.Active
                && (!em.HasComponent<RespawnRequest>(emitter) || !em.IsComponentEnabled<RespawnRequest>(emitter));

        private struct ContactOrder : IComparer<ColliderCastHit>
        {
            public int Compare(ColliderCastHit a, ColliderCastHit b)
            {
                int fraction = a.Fraction.CompareTo(b.Fraction);
                return fraction != 0 ? fraction : a.Entity.Index.CompareTo(b.Entity.Index);
            }
        }
    }
}
