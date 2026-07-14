using CrowdPunch.Components;
using CrowdPunch.Systems.Groups;
using Unity.Burst;
using Unity.Entities;

namespace CrowdPunch.Systems.Combat
{
    /// <summary>
    /// Finds enemies affected by the current punch request.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(GamePrePhysicsGroup))]
    [UpdateAfter(typeof(InputBridge.PlayerBridgeSystem))]
    public partial struct PunchDetectionSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PunchRequest>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // TODO: When PunchRequest is enabled, detect affected enemies and enable ExternalImpulse and KnockbackRecovery on them.
        }
    }
}
