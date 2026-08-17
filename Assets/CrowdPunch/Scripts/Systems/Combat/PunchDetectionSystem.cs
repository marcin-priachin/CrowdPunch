using CrowdPunch.Components;
using CrowdPunch.Systems.Groups;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Physics;

namespace CrowdPunch.Systems.Combat
{
    /// <summary>
    /// Finds enemies affected by the current punch request.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(GamePrePhysicsGroup))]
    [UpdateAfter(typeof(InputBridge.PlayerBridgeSystem))]
    public partial struct PunchDetectionSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerSnapshot>();
            state.RequireForUpdate<PunchRequest>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            Entity punchEntity = SystemAPI.GetSingletonEntity<PlayerSnapshot>();

            if (!SystemAPI.IsComponentEnabled<PunchRequest>(punchEntity))
            {
                return;
            }

            PunchRequest punchRequest = SystemAPI.GetComponent<PunchRequest>(punchEntity);
            float3 punchDirection = math.normalizesafe(punchRequest.Direction);
            float radiusSquared = punchRequest.Radius * punchRequest.Radius;
            float pushDirectionPositionWeight = math.saturate(punchRequest.PushDirectionPositionWeight);
            foreach ((RefRO<LocalTransform> transform, RefRW<EnemyLaunchState> launchState, RefRO<Health> health,
                         RefRO<EnemyTier> tier, Entity enemy) in
                     SystemAPI.Query<RefRO<LocalTransform>, RefRW<EnemyLaunchState>, RefRO<Health>, RefRO<EnemyTier>>()
                         .WithAll<Enemy>()
                         .WithNone<RespawnRequest>()
                         .WithEntityAccess())
            {
                if (!EnemyLaunchTransition.CanReceivePlayerPunch(launchState.ValueRO, health.ValueRO))
                {
                    continue;
                }

                float3 toEnemy = transform.ValueRO.Position - punchRequest.Origin;
                float forwardDistance = math.dot(toEnemy, punchDirection);

                if (forwardDistance < 0f || forwardDistance > punchRequest.Range)
                {
                    continue;
                }

                float3 closestPointOnPunchLine = punchRequest.Origin + punchDirection * forwardDistance;
                float distanceFromPunchLineSquared = math.lengthsq(transform.ValueRO.Position - closestPointOnPunchLine);

                if (distanceFromPunchLineSquared > radiusSquared)
                {
                    continue;
                }

                float3 positionDirection = math.normalizesafe(toEnemy, punchDirection);
                float3 impulseDirection = math.normalizesafe(
                    math.lerp(punchDirection, positionDirection, pushDirectionPositionWeight),
                    positionDirection);
                if (SystemAPI.HasComponent<DasherState>(enemy))
                {
                    DasherState interruptedDash = SystemAPI.GetComponent<DasherState>(enemy);
                    interruptedDash.Phase = DasherPhase.Positioning;
                    interruptedDash.SecondsRemaining = 0f;
                    interruptedDash.LockedDirection = impulseDirection;
                    interruptedDash.LockedRotation = quaternion.LookRotationSafe(impulseDirection, math.up());
                    interruptedDash.HasLockedRotation = 1;
                    SystemAPI.SetComponent(enemy, interruptedDash);
                    PhysicsVelocity interruptedVelocity = SystemAPI.GetComponent<PhysicsVelocity>(enemy);
                    interruptedVelocity.Linear.xz = float2.zero;
                    SystemAPI.SetComponent(enemy, interruptedVelocity);
                }
                float targetStrength = tier.ValueRO.Value == EnemyCombatTier.Elite
                    ? punchRequest.Strength * math.max(0f, punchRequest.EliteKnockbackMultiplier)
                    : punchRequest.Strength;
                SystemAPI.SetComponent(enemy, new ExternalImpulse
                {
                    Value = impulseDirection * targetStrength
                });
                SystemAPI.SetComponentEnabled<ExternalImpulse>(enemy, true);
                if (EnemyLaunchTransition.IsLaunchable(tier.ValueRO))
                {
                    EnemyLaunchState nextLaunchState = launchState.ValueRO;
                    EnemyLaunchTransition.Begin(
                        ref nextLaunchState,
                        EnemyLaunchCause.PlayerPunch,
                        punchRequest.Damage);
                    launchState.ValueRW = nextLaunchState;
                }

                DamageRequest pendingDamage = SystemAPI.IsComponentEnabled<DamageRequest>(enemy)
                    ? SystemAPI.GetComponent<DamageRequest>(enemy)
                    : default;
                pendingDamage.Amount += punchRequest.Damage;
                SystemAPI.SetComponent(enemy, pendingDamage);
                SystemAPI.SetComponentEnabled<DamageRequest>(enemy, true);

            }

            SystemAPI.SetComponentEnabled<PunchRequest>(punchEntity, false);
        }
    }
}
