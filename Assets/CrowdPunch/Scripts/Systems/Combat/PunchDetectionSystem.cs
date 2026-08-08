using CrowdPunch.Components;
using CrowdPunch.Systems.Groups;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

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
            foreach ((RefRO<LocalTransform> transform, RefRW<EnemyLaunchState> launchState, RefRO<Health> health, Entity enemy) in
                     SystemAPI.Query<RefRO<LocalTransform>, RefRW<EnemyLaunchState>, RefRO<Health>>()
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
                SystemAPI.SetComponent(enemy, new ExternalImpulse
                {
                    Value = impulseDirection * punchRequest.Strength
                });
                SystemAPI.SetComponentEnabled<ExternalImpulse>(enemy, true);
                EnemyLaunchState nextLaunchState = launchState.ValueRO;
                EnemyLaunchTransition.Begin(
                    ref nextLaunchState,
                    EnemyLaunchCause.PlayerPunch,
                    punchRequest.Damage);
                launchState.ValueRW = nextLaunchState;

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
