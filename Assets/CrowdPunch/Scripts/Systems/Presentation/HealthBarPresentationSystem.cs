using CrowdPunch.Components;
using CrowdPunch.Systems.Groups;
using Unity.Burst;
using Unity.Entities;

namespace CrowdPunch.Systems.Presentation
{
    /// <summary>
    /// Publishes health as normalized bar values for presentation systems.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(GamePresentationGroup))]
    public partial struct HealthBarPresentationSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<HealthBar>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            foreach ((RefRO<Health> health, RefRW<HealthBar> healthBar) in
                     SystemAPI.Query<RefRO<Health>, RefRW<HealthBar>>())
            {
                healthBar.ValueRW.Normalized = health.ValueRO.Normalized;
            }
        }
    }
}
