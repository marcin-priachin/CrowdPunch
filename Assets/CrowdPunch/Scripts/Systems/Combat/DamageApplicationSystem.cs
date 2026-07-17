using CrowdPunch.Components;
using CrowdPunch.Systems.Groups;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace CrowdPunch.Systems.Combat
{
    /// <summary>
    /// Applies pending damage requests to health values.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(GamePrePhysicsGroup))]
    [UpdateAfter(typeof(PunchDetectionSystem))]
    public partial struct DamageApplicationSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Health>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            foreach ((RefRW<Health> health, RefRO<DamageRequest> damageRequest, Entity entity) in
                     SystemAPI.Query<RefRW<Health>, RefRO<DamageRequest>>()
                         .WithEntityAccess())
            {
                health.ValueRW.Current = math.clamp(
                    health.ValueRO.Current - math.max(0f, damageRequest.ValueRO.Amount),
                    0f,
                    math.max(0f, health.ValueRO.Max));

                SystemAPI.SetComponentEnabled<DamageRequest>(entity, false);

                if (health.ValueRO.Current <= 0f)
                {
                    SystemAPI.SetComponentEnabled<DeathRequest>(entity, true);
                }
            }
        }
    }
}
