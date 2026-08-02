using CrowdPunch.Components;
using CrowdPunch.Systems.Groups;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace CrowdPunch.Systems.Combat
{
    /// <summary>
    /// Applies pending damage, then resolves immediate or launch-deferred defeat.
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
            foreach ((RefRW<Health> health,
                         RefRO<DamageRequest> damageRequest,
                         RefRW<EnemyLaunchState> launchState,
                         RefRW<EnemyDamageState> damageState,
                         Entity entity) in
                     SystemAPI.Query<RefRW<Health>, RefRO<DamageRequest>, RefRW<EnemyLaunchState>, RefRW<EnemyDamageState>>()
                         .WithAll<Enemy>()
                         .WithEntityAccess())
            {
                if (launchState.ValueRO.Phase == EnemyLaunchPhase.Defeated)
                {
                    damageState.ValueRW.LastDamageReceived = 0f;
                    damageState.ValueRW.IsDefeatDeferred = 0;
                    SystemAPI.SetComponentEnabled<DamageRequest>(entity, false);
                    continue;
                }

                float appliedDamage = health.ValueRO.Current <= 0f
                    ? 0f
                    : math.max(0f, damageRequest.ValueRO.Amount);
                health.ValueRW.Current = math.clamp(
                    health.ValueRO.Current - appliedDamage,
                    0f,
                    math.max(0f, health.ValueRO.Max));
                damageState.ValueRW.LastDamageReceived = appliedDamage;

                SystemAPI.SetComponentEnabled<DamageRequest>(entity, false);

                if (health.ValueRO.Current <= 0f)
                {
                    if (launchState.ValueRO.Phase == EnemyLaunchPhase.Launched)
                    {
                        damageState.ValueRW.IsDefeatDeferred = 1;
                        continue;
                    }

                    launchState.ValueRW.Phase = EnemyLaunchPhase.Defeated;
                    damageState.ValueRW.IsDefeatDeferred = 0;
                    SystemAPI.SetComponentEnabled<DeathRequest>(entity, true);
                }
            }
        }
    }
}
