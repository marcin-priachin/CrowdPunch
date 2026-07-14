using CrowdPunch.Components;
using CrowdPunch.Systems.Groups;
using Unity.Burst;
using Unity.Entities;

namespace CrowdPunch.Systems.InputBridge
{
    /// <summary>
    /// Copies MonoBehaviour player bridge data into ECS singleton components.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(GamePrePhysicsGroup), OrderFirst = true)]
    public partial struct PlayerBridgeSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerSnapshot>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // TODO: Read dedicated player bridge data and write PlayerSnapshot and enableable PunchRequest with SystemAPI.
        }
    }
}
