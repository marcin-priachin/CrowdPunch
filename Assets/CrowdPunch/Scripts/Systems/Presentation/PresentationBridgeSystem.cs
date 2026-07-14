using CrowdPunch.Components;
using CrowdPunch.Systems.Groups;
using Unity.Burst;
using Unity.Entities;

namespace CrowdPunch.Systems.Presentation
{
    /// <summary>
    /// Bridges ECS simulation results to presentation-only GameObject or rendering state.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(GamePresentationGroup))]
    public partial struct PresentationBridgeSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Enemy>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // TODO: Publish read-only ECS presentation data without allowing MonoBehaviours to directly mutate enemy entities.
        }
    }
}
