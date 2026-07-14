using CrowdPunch.Components;
using CrowdPunch.Systems.Groups;
using Unity.Burst;
using Unity.Entities;

namespace CrowdPunch.Systems.Lifetime
{
    /// <summary>
    /// Returns invalid enemies to spawn positions instead of destroying them.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(GamePostPhysicsGroup))]
    [UpdateAfter(typeof(OutOfBoundsSystem))]
    public partial struct EnemyRespawnSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<RespawnRequest>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // TODO: Move enemies with enabled RespawnRequest back into valid spawn space and reset transient movement state.
        }
    }
}
