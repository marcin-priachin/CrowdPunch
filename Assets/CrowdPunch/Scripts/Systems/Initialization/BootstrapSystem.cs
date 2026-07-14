using CrowdPunch.Components;
using CrowdPunch.Systems.Groups;
using Unity.Burst;
using Unity.Entities;

namespace CrowdPunch.Systems.Initialization
{
    /// <summary>
    /// Performs ECS world setup that must exist before gameplay systems run.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(GameInitializationGroup))]
    public partial struct BootstrapSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<MatchState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // TODO: Validate required singletons, initialize match-level runtime state, and keep this system free of gameplay rules.
        }
    }
}
