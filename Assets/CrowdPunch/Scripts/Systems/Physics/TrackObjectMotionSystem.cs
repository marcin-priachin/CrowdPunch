using CrowdPunch.Components;
using CrowdPunch.Systems.Combat;
using CrowdPunch.Systems.Groups;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace CrowdPunch.Systems.Physics
{
    [UpdateInGroup(typeof(GamePrePhysicsGroup), OrderLast = true)]
    [UpdateAfter(typeof(BarricadeImpactSystem))]
    public partial struct TrackObjectMotionSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            float dt = SystemAPI.Time.DeltaTime;
            foreach (var (track, motion, transform, velocity) in
                SystemAPI.Query<RefRO<TrackObject>, RefRW<TrackObjectState>, RefRO<LocalTransform>, RefRW<PhysicsVelocity>>())
            {
                motion.ValueRW.NextDistance = TrackObjectMotion.Next(track.ValueRO, ref motion.ValueRW, dt);
                float3 next = track.ValueRO.Start + track.ValueRO.Direction * motion.ValueRO.NextDistance;
                velocity.ValueRW = new PhysicsVelocity { Linear = (next - transform.ValueRO.Position) / math.max(.0001f, dt) };
            }
        }
    }
}
