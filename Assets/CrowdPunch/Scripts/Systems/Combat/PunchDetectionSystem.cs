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
        private const double MaxPendingPoolSeconds = 2d;

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
            double forcePoolAt = SystemAPI.Time.ElapsedTime + MaxPendingPoolSeconds;

            foreach ((RefRO<LocalTransform> transform, Entity enemy) in
                     SystemAPI.Query<RefRO<LocalTransform>>()
                         .WithAll<Enemy>()
                         .WithNone<RespawnRequest, KnockbackRecovery>()
                         .WithEntityAccess())
            {
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

                SystemAPI.SetComponent(enemy, new KnockbackRecovery
                {
                    RemainingSeconds = 0.35f
                });
                SystemAPI.SetComponentEnabled<KnockbackRecovery>(enemy, true);

                SystemAPI.SetComponent(enemy, new DamageRequest
                {
                    Amount = punchRequest.Damage
                });
                SystemAPI.SetComponentEnabled<DamageRequest>(enemy, true);

                SystemAPI.SetComponent(enemy, new RespawnRequest
                {
                    RespawnAt = 0d,
                    IsPooled = 0,
                    ForcePoolAt = forcePoolAt,
                    FromPlayerPunch = 1
                });
                SystemAPI.SetComponentEnabled<RespawnRequest>(enemy, true);
            }

            SystemAPI.SetComponentEnabled<PunchRequest>(punchEntity, false);
        }
    }
}
