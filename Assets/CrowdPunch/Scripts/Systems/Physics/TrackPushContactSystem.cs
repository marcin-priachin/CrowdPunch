using CrowdPunch.Components;
using CrowdPunch.Systems.Groups;
using Unity.Entities;
using Unity.Burst;
using Unity.Mathematics;
using Unity.Transforms;

namespace CrowdPunch.Systems.Physics
{
    [BurstCompile]
    [UpdateInGroup(typeof(GamePrePhysicsGroup), OrderLast = true)]
    [UpdateAfter(typeof(TrackObjectMotionSystem))]
    public partial struct TrackPushContactSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (track, motion, wall, pose, contacts) in SystemAPI.Query<RefRO<TrackObject>, RefRO<TrackObjectState>,
                RefRO<Barricade>, RefRO<LocalTransform>, DynamicBuffer<TrackPushContact>>())
            {
                contacts.Clear();
                if (track.ValueRO.DamagingPush == 0 || motion.ValueRO.Moving == 0 || motion.ValueRO.Locked != 0) continue;
                float3 end = track.ValueRO.Start + track.ValueRO.Direction * motion.ValueRO.NextDistance;
                if (math.distancesq(pose.ValueRO.Position, end) < 1e-10f) continue;
                // Keep contact eligibility even when the solver successfully separates the enemy.
                foreach (var (character, agent, launch, entity) in SystemAPI.Query<RefRO<LocalTransform>, RefRO<NavigationAgent>, RefRO<EnemyLaunchState>>()
                    .WithAll<Enemy>().WithNone<RespawnRequest>().WithEntityAccess())
                    if (launch.ValueRO.Phase != EnemyLaunchPhase.Launched && launch.ValueRO.Phase != EnemyLaunchPhase.Defeated
                        && TrackPushGeometry.Intersects(character.ValueRO.Position, agent.ValueRO.Radius,
                            pose.ValueRO.Position, end, pose.ValueRO.Rotation, wall.ValueRO.Size))
                        contacts.Add(new TrackPushContact { Character = entity });
            }
        }
    }
}
