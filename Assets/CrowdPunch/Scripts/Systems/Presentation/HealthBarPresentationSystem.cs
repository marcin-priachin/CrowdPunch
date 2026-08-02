using CrowdPunch.Components;
using CrowdPunch.Systems.Groups;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

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

            float deltaTime = SystemAPI.Time.DeltaTime;
            foreach ((RefRW<EnemyHealthBarVisibility> visibility, Entity enemy) in
                     SystemAPI.Query<RefRW<EnemyHealthBarVisibility>>()
                         .WithAll<Enemy>()
                         .WithEntityAccess())
            {
                visibility.ValueRW.SecondsRemaining = math.max(
                    0f,
                    visibility.ValueRO.SecondsRemaining - deltaTime);

                if (visibility.ValueRO.SecondsRemaining <= 0f)
                {
                    SystemAPI.SetComponentEnabled<EnemyHealthBarVisibility>(enemy, false);
                }
            }
        }
    }
}
