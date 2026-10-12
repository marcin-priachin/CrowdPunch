using CrowdPunch.Components;
using CrowdPunch.Systems.Groups;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace CrowdPunch.Systems.Physics
{
    [BurstCompile, UpdateInGroup(typeof(GamePrePhysicsGroup), OrderLast = true)]
    public partial struct SnakeCollisionClearanceSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (segment, pose) in SystemAPI.Query<RefRW<SnakeSegment>, RefRO<LocalTransform>>())
            {
                var s = segment.ValueRO;
                if (s.Detached == 0 || s.Cleared != 0) continue;
                bool overlapping = false;
                if (SystemAPI.HasComponent<LocalTransform>(s.Head) && SystemAPI.HasComponent<SnakeHead>(s.Head)
                    && SystemAPI.GetComponent<SnakeHead>(s.Head).Generation == s.Generation)
                {
                    var headPose = SystemAPI.GetComponent<LocalTransform>(s.Head);
                    float headRadius = SystemAPI.GetComponent<EnemyContactDamageSettings>(s.Head).ContactRadius;
                    overlapping = math.distancesq(pose.ValueRO.Position.xz, headPose.Position.xz) < math.square(s.Radius + headRadius + .15f);
                    foreach (var link in SystemAPI.GetBuffer<SnakeTailLink>(s.Head))
                    {
                        if (!SystemAPI.HasComponent<SnakeSegment>(link.Orb)) continue;
                        var other = SystemAPI.GetComponent<SnakeSegment>(link.Orb);
                        if (other.Detached == 0 && math.distancesq(pose.ValueRO.Position.xz,
                            SystemAPI.GetComponent<LocalTransform>(link.Orb).Position.xz) < math.square(s.Radius + other.Radius + .15f))
                            overlapping = true;
                    }
                }
                if (!overlapping) segment.ValueRW.Cleared = 1;
            }
        }
    }
}
