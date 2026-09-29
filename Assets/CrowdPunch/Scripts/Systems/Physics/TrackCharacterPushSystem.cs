using CrowdPunch.Components;
using CrowdPunch.Systems.Combat;
using CrowdPunch.Mono.Player;
using CrowdPunch.Systems.Groups;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace CrowdPunch.Systems.Physics
{
    [UpdateInGroup(typeof(GamePostPhysicsGroup), OrderFirst = true)]
    [UpdateBefore(typeof(TrackObjectArrivalSystem))]
    public partial struct TrackCharacterPushSystem : ISystem
    {
        public void OnCreate(ref SystemState state) { state.RequireForUpdate<TrackObject>(); state.RequireForUpdate<PhysicsWorldSingleton>(); }

        public void OnUpdate(ref SystemState state)
        {
            var world = SystemAPI.GetSingleton<PhysicsWorldSingleton>().CollisionWorld;
            foreach (var (track, motion, wall, pose, target) in SystemAPI.Query<RefRO<TrackObject>, RefRO<TrackObjectState>,
                RefRO<Barricade>, RefRO<LocalTransform>>().WithEntityAccess())
            {
                if (motion.ValueRO.Moving == 0 || motion.ValueRO.Locked != 0) continue;
                float3 start = track.ValueRO.Start + track.ValueRO.Direction * motion.ValueRO.Distance;
                float3 end = track.ValueRO.Start + track.ValueRO.Direction * motion.ValueRO.NextDistance;
                if (math.distancesq(start, end) < 1e-10f) continue;
                foreach (var (character, agent, launch, entity) in SystemAPI.Query<RefRW<LocalTransform>, RefRO<NavigationAgent>, RefRO<EnemyLaunchState>>()
                    .WithAll<Enemy>().WithNone<RespawnRequest>().WithEntityAccess())
                {
                    // Launched bodies keep the existing swept impact/rebound response.
                    if (launch.ValueRO.Phase == EnemyLaunchPhase.Launched || launch.ValueRO.Phase == EnemyLaunchPhase.Defeated) continue;
                    float radius = agent.ValueRO.Radius;
                    if (!TrackPushGeometry.Intersects(character.ValueRO.Position, radius, start, end, pose.ValueRO.Rotation, wall.ValueRO.Size)) continue;
                    if (track.ValueRO.DamagingPush != 0 && RecordPush(state.EntityManager, target, entity))
                    {
                        var damage = state.EntityManager.IsComponentEnabled<DamageRequest>(entity)
                            ? state.EntityManager.GetComponentData<DamageRequest>(entity) : default;
                        damage.Amount += track.ValueRO.PushDamage;
                        state.EntityManager.SetComponentData(entity, damage);
                        state.EntityManager.SetComponentEnabled<DamageRequest>(entity, true);
                    }
                    if (!TryDisplace(state.EntityManager, world, target, character.ValueRO.Position, radius, end,
                        pose.ValueRO.Rotation, wall.ValueRO.Size, track.ValueRO.Direction, out float3 safe)) continue;
                    // This is moving-obstacle penetration correction; ordinary movement remains velocity-owned.
                    character.ValueRW.Position = safe;
                }
                if (!PlayerBridgeRegistry.TryGetBridge(out PlayerEcsBridge bridge)) continue;
                if (!TrackPushGeometry.Intersects(bridge.Position, bridge.Radius, start, end, pose.ValueRO.Rotation, wall.ValueRO.Size)) continue;
                if (track.ValueRO.DamagingPush != 0 && track.ValueRO.PushDamagesPlayer != 0 && RecordPush(state.EntityManager, target, Entity.Null))
                    bridge.ReceiveEnemyHit(track.ValueRO.PushDamage, 0, float3.zero);
                if (!TryDisplace(state.EntityManager, world, target, bridge.Position, bridge.Radius, end,
                    pose.ValueRO.Rotation, wall.ValueRO.Size, track.ValueRO.Direction, out float3 playerSafe)) continue;
                bridge.ReceiveObstacleDisplacement(playerSafe);
                if (SystemAPI.HasSingleton<PlayerSnapshot>())
                {
                    var player = SystemAPI.GetSingleton<PlayerSnapshot>(); player.Position = playerSafe;
                    SystemAPI.SetSingleton(player);
                }
            }
        }

        private static bool RecordPush(EntityManager em, Entity target, Entity character)
        {
            var history = em.GetBuffer<TrackPushHistory>(target);
            foreach (var hit in history) if (hit.Character == character) return false;
            history.Add(new TrackPushHistory { Character = character });
            return true;
        }

        private static bool TryDisplace(EntityManager em, CollisionWorld world, Entity obstacle, float3 position, float radius,
            float3 center, quaternion rotation, float3 size, float3 direction, out float3 result)
        {
            // Test both sides, then walk around either end if a static solid blocks direct displacement.
            // Crowd overlap is left to Unity Physics, so a packed crowd cannot stall the objective.
            var hits = new NativeList<DistanceHit>(Allocator.Temp);
            var input = new PointDistanceInput { MaxDistance = radius + .02f,
                Filter = new CollisionFilter { BelongsTo = uint.MaxValue, CollidesWith = ~(1u << 7) } };
            float3 forward = math.rotate(rotation, new float3(0, 0, 1));
            float3 right = math.rotate(rotation, new float3(1, 0, 0));
            float extent = math.abs(math.dot(direction, right)) * size.x * .5f + math.abs(math.dot(direction, forward)) * size.z * .5f;
            for (int i = 0; i < 6; i++)
            {
                float3 candidate = TrackPushGeometry.SideCandidate(position, radius, center, rotation, size, direction, i % 2 == 1);
                if (i >= 2)
                    candidate += direction * ((i < 4 ? -1 : 1) * (extent + radius + .05f) - math.dot(candidate - center, direction));
                input.Position = candidate;
                hits.Clear(); world.CalculateDistance(input, ref hits);
                bool blocked = false;
                foreach (var hit in hits)
                    if (hit.Entity != obstacle && !em.HasComponent<Enemy>(hit.Entity) && math.abs(hit.SurfaceNormal.y) < .7f)
                    { blocked = true; break; }
                if (!blocked) { result = candidate; hits.Dispose(); return true; }
            }
            result = position;
            hits.Dispose();
            return false;
        }
    }
}
