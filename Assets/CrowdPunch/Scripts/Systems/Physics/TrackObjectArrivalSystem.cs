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
            var paths = SystemAPI.GetBufferLookup<TrackPathPoint>(true);
            foreach (var (track, motion, pose, velocity, wall, entity) in
                SystemAPI.Query<RefRO<TrackObject>, RefRW<TrackObjectState>, RefRW<LocalTransform>, RefRW<PhysicsVelocity>, RefRW<Barricade>>().WithEntityAccess())
            {
                // Kinematic integration owns the slide. Remove only floating-point drift from its rail.
                motion.ValueRW.Distance = motion.ValueRO.NextDistance;
                paths.TryGetBuffer(entity, out var path);
                pose.ValueRW.Position = TrackPathGeometry.Position(track.ValueRO, path, motion.ValueRO.Distance);
                velocity.ValueRW = default;
                if (motion.ValueRO.Moving == 0 || motion.ValueRO.Elapsed < track.ValueRO.SlideDuration
                    || math.abs(motion.ValueRO.Distance - track.ValueRO.Length * motion.ValueRO.TargetStep / track.ValueRO.RequiredNetHits) > .00001f) continue;
                motion.ValueRW.Moving = 0;
                if (motion.ValueRO.TargetStep != track.ValueRO.RequiredNetHits) continue;
                motion.ValueRW.Locked = 1;
                // Reuse the objective completion/replenishment flag, but keep the solid collider intact.
                wall.ValueRW.HitsRemaining = 0;
            }
        }
    }
}
