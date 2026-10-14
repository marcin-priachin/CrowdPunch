using CrowdPunch.Components;
using CrowdPunch.Systems.Combat;
using CrowdPunch.Systems.Groups;
using Unity.Entities;
using Unity.Burst;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace CrowdPunch.Systems.Physics
{
    [BurstCompile]
    [UpdateInGroup(typeof(GamePrePhysicsGroup), OrderLast = true)]
    [UpdateAfter(typeof(BarricadeImpactSystem))]
    public partial struct TrackObjectMotionSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float dt = SystemAPI.Time.DeltaTime;
            var paths = SystemAPI.GetBufferLookup<TrackPathPoint>(true);
            foreach (var (track, motion, transform, velocity, entity) in
                SystemAPI.Query<RefRO<TrackObject>, RefRW<TrackObjectState>, RefRO<LocalTransform>, RefRW<PhysicsVelocity>>().WithEntityAccess())
            {
                if (SystemAPI.HasComponent<FinaleAnchor>(entity)) {
                    var anchor = SystemAPI.GetComponent<FinaleAnchor>(entity);
                    if (anchor.Spent != 0 || SystemAPI.GetComponent<FinaleBoss>(anchor.Boss).Phase != FinalePhase.Flying) {
                        velocity.ValueRW = default; motion.ValueRW.NextDistance = motion.ValueRO.Distance; continue;
                    }
                }
                paths.TryGetBuffer(entity, out var path);
                motion.ValueRW.NextDistance = TrackObjectMotion.Next(track.ValueRO, path, ref motion.ValueRW, dt);
                float3 next = TrackPathGeometry.Position(track.ValueRO, path, motion.ValueRO.NextDistance);
                velocity.ValueRW = new PhysicsVelocity { Linear = (next - transform.ValueRO.Position) / math.max(.0001f, dt) };
            }
        }
    }
}
