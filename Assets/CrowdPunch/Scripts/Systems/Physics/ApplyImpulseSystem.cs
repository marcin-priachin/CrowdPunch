using CrowdPunch.Components;
using CrowdPunch.Systems.Combat;
using CrowdPunch.Systems.Groups;
using Unity.Burst;
using Unity.Entities;

namespace CrowdPunch.Systems.Physics
{
    /// <summary>
    /// Transfers gameplay impulses into Unity Physics velocity data.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(GamePrePhysicsGroup))]
    [UpdateAfter(typeof(PunchDetectionSystem))]
    public partial struct ApplyImpulseSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ExternalImpulse>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // TODO: Apply enabled ExternalImpulse values to PhysicsVelocity, then disable ExternalImpulse after consumption.
        }
    }
}
