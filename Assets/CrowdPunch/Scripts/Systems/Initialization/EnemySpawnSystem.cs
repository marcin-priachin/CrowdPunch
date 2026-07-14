using CrowdPunch.Components;
using CrowdPunch.Systems.Groups;
using Unity.Burst;
using Unity.Entities;

namespace CrowdPunch.Systems.Initialization
{
    /// <summary>
    /// Creates the initial ECS enemy crowd from baked spawn settings.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(GameInitializationGroup))]
    [UpdateAfter(typeof(BootstrapSystem))]
    public partial struct EnemySpawnSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SpawnSettings>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // TODO: Instantiate enemies through an EntityCommandBuffer, then disable this system once the initial crowd exists.
        }
    }
}
