using CrowdPunch.Components;
using CrowdPunch.Systems.Groups;
using Unity.Entities;
using Unity.Burst;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace CrowdPunch.Systems.Physics
{
    [BurstCompile]
    [UpdateInGroup(typeof(GamePostPhysicsGroup), OrderFirst = true)]
    public partial struct TrackObjectArrivalSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (track, motion, pose, velocity, wall) in
                SystemAPI.Query<RefRO<TrackObject>, RefRW<TrackObjectState>, RefRW<LocalTransform>, RefRW<PhysicsVelocity>, RefRW<Barricade>>())
            {
                // Kinematic integration owns the slide. Remove only floating-point drift from its rail.
                motion.ValueRW.Distance = motion.ValueRO.NextDistance;
                pose.ValueRW.Position = track.ValueRO.Start + track.ValueRO.Direction * motion.ValueRO.Distance;
                velocity.ValueRW = default;
                if (motion.ValueRO.Moving == 0 || motion.ValueRO.Elapsed < track.ValueRO.SlideDuration) continue;
                motion.ValueRW.Moving = 0;
                if (motion.ValueRO.TargetStep != track.ValueRO.RequiredNetHits) continue;
                motion.ValueRW.Locked = 1;
                // Reuse the objective completion/replenishment flag, but keep the solid collider intact.
                wall.ValueRW.HitsRemaining = 0;
            }
        }
    }
}
