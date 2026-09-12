using CrowdPunch.Components;
using CrowdPunch.Systems.Groups;
using Unity.Burst;
using Unity.Entities;
using Unity.Physics;

namespace CrowdPunch.Systems.Physics
{
    [BurstCompile, UpdateInGroup(typeof(GamePrePhysicsGroup), OrderLast = true)]
    public partial struct ImpactVelocityCaptureSystem : ISystem
    {
        [BurstCompile] public void OnUpdate(ref SystemState state)
        {
            foreach (var (feedback, velocity) in SystemAPI.Query<RefRW<EnemyImpactFeedback>, RefRO<PhysicsVelocity>>()
                         .WithNone<RespawnRequest>())
                feedback.ValueRW.IncomingVelocity = velocity.ValueRO.Linear;
        }
    }
}
