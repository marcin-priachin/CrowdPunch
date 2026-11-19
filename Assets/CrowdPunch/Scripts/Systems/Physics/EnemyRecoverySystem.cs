using CrowdPunch.Components;
using CrowdPunch.Systems.Groups;
using CrowdPunch.Systems.Combat;
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
    [UpdateAfter(typeof(EnemyLaunchCollisionSystem))]
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
            var paths = SystemAPI.GetComponentLookup<AuthoritativeLaunchPath>(true);
            var dashers = SystemAPI.GetComponentLookup<DasherState>();

            foreach ((RefRW<EnemyLaunchState> launchState,
                         RefRO<PhysicsVelocity> velocity,
                         RefRO<Health> health,
                         RefRW<EnemyDamageState> damageState,
                         Entity enemy) in
                     SystemAPI.Query<RefRW<EnemyLaunchState>, RefRO<PhysicsVelocity>, RefRO<Health>, RefRW<EnemyDamageState>>()
                         .WithAll<Enemy>()
                         .WithNone<RespawnRequest>()
                         .WithEntityAccess())
            {
                if (SystemAPI.HasComponent<EnemySpawnAnimation>(enemy)
                    && SystemAPI.IsComponentEnabled<EnemySpawnAnimation>(enemy)
                    && launchState.ValueRO.Phase != EnemyLaunchPhase.Recovering)
                    SystemAPI.SetComponentEnabled<EnemySpawnAnimation>(enemy, false);
                if (SystemAPI.HasComponent<LinkedPairEndpoint>(enemy) && Combat.LinkedPairLaunchResolution.TryGet(state.EntityManager, enemy, out _, out _)) continue;
                if (launchState.ValueRO.Phase == EnemyLaunchPhase.Active
                    || launchState.ValueRO.Phase == EnemyLaunchPhase.Defeated)
                {
                    continue;
                }

                bool playerDasher = launchState.ValueRO.Owner == EnemyLaunchOwner.Player
                    && dashers.HasComponent(enemy);
                if (launchState.ValueRO.Phase == EnemyLaunchPhase.Launched)
                {
                    if (paths.HasComponent(enemy) && paths.IsComponentEnabled(enemy)
                        && paths[enemy].LaunchSequence == launchState.ValueRO.LaunchSequence) continue;
                    float recoveryDuration = math.max(0f, settings.RecoveryDuration);
                    if (playerDasher)
                    {
                        var dash = dashers[enemy];
                        // A launch propagated after physics has yet to enter the dash motor.
                        if (dash.NormalizedLaunchSequence != launchState.ValueRO.LaunchSequence
                            || dash.Phase != DasherPhase.Recovering) continue;
                        recoveryDuration = dash.SecondsRemaining;
                    }
                    else
                    {
                        float horizontalSpeed = math.length(velocity.ValueRO.Linear.xz);
                        if (horizontalSpeed >= math.max(0f, settings.UsefulMomentumSpeed))
                        {
                            launchState.ValueRW.BelowUsefulMomentumSeconds = 0f;
                            continue;
                        }

                        launchState.ValueRW.BelowUsefulMomentumSeconds += deltaTime;
                        if (launchState.ValueRO.BelowUsefulMomentumSeconds < math.max(0f, settings.LowMomentumPeriod))
                            continue;
                    }

                    if (health.ValueRO.Current <= 0f || SystemAPI.HasComponent<SnakeSegment>(enemy))
                    {
                        MarkDefeated(ref launchState.ValueRW, ref damageState.ValueRW);
                        SystemAPI.SetComponentEnabled<DeathRequest>(enemy, true);
                        continue;
                    }

                    launchState.ValueRW.Phase = EnemyLaunchPhase.Recovering;
                    launchState.ValueRW.RecoverySecondsRemaining = recoveryDuration;
                    continue;
                }

                launchState.ValueRW.RecoverySecondsRemaining -= deltaTime;
                if (launchState.ValueRO.RecoverySecondsRemaining <= 0f)
                {
                    launchState.ValueRW.Phase = EnemyLaunchPhase.Active;
                    launchState.ValueRW.LastCause = EnemyLaunchCause.None;
                    launchState.ValueRW.BelowUsefulMomentumSeconds = 0f;
                    launchState.ValueRW.RecoverySecondsRemaining = 0f;
                    if (playerDasher)
                    {
                        var dash = dashers[enemy];
                        dash.Phase = DasherPhase.Positioning;
                        dash.SecondsRemaining = 0f;
                        dash.HasLockedRotation = 0;
                        dashers[enemy] = dash;
                    }
                    if (SystemAPI.HasComponent<EnemySpawnAnimation>(enemy))
                        SystemAPI.SetComponentEnabled<EnemySpawnAnimation>(enemy, false);
                }
            }
        }

        internal static void MarkDefeated(ref EnemyLaunchState launch, ref EnemyDamageState damage)
        {
            launch.Phase = EnemyLaunchPhase.Defeated;
            launch.RecoverySecondsRemaining = 0f;
            damage.IsDefeatDeferred = 0;
        }
    }
}
