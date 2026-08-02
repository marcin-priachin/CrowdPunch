using CrowdPunch.Components;
using CrowdPunch.Systems.Groups;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;

namespace CrowdPunch.Systems.Physics
{
    /// <summary>
    /// Advances launched enemies through low-momentum dwell, recovery, and back to active.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(GamePostPhysicsGroup))]
    public partial struct EnemyRecoverySystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<EnemyLaunchSettings>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;
            EnemyLaunchSettings settings = SystemAPI.GetSingleton<EnemyLaunchSettings>();

            foreach ((RefRW<EnemyLaunchState> launchState, RefRO<PhysicsVelocity> velocity) in
                     SystemAPI.Query<RefRW<EnemyLaunchState>, RefRO<PhysicsVelocity>>()
                         .WithAll<Enemy>())
            {
                if (launchState.ValueRO.Phase == EnemyLaunchPhase.Active)
                {
                    continue;
                }

                if (launchState.ValueRO.Phase == EnemyLaunchPhase.Launched)
                {
                    float horizontalSpeed = math.length(velocity.ValueRO.Linear.xz);
                    if (horizontalSpeed >= math.max(0f, settings.UsefulMomentumSpeed))
                    {
                        launchState.ValueRW.BelowUsefulMomentumSeconds = 0f;
                        continue;
                    }

                    launchState.ValueRW.BelowUsefulMomentumSeconds += deltaTime;
                    if (launchState.ValueRO.BelowUsefulMomentumSeconds < math.max(0f, settings.LowMomentumPeriod))
                    {
                        continue;
                    }

                    launchState.ValueRW.Phase = EnemyLaunchPhase.Recovering;
                    launchState.ValueRW.RecoverySecondsRemaining = math.max(0f, settings.RecoveryDuration);
                    continue;
                }

                launchState.ValueRW.RecoverySecondsRemaining -= deltaTime;
                if (launchState.ValueRO.RecoverySecondsRemaining <= 0f)
                {
                    launchState.ValueRW.Phase = EnemyLaunchPhase.Active;
                    launchState.ValueRW.LastCause = EnemyLaunchCause.None;
                    launchState.ValueRW.BelowUsefulMomentumSeconds = 0f;
                    launchState.ValueRW.RecoverySecondsRemaining = 0f;
                }
            }
        }
    }
}
