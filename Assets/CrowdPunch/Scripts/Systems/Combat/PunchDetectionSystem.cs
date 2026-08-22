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
    [UpdateAfter(typeof(PunchAimAssistSystem))]
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
            PunchSpecification punch = new PunchSpecification
            {
                Origin = punchRequest.Origin, Direction = punchRequest.Direction, Range = punchRequest.Range,
                Radius = punchRequest.Radius, Strength = punchRequest.Strength,
                Damage = punchRequest.Damage, PositionWeight = punchRequest.PushDirectionPositionWeight,
                Cause = EnemyLaunchCause.PlayerPunch, AffectActive = 1, AffectRecovering = 1,
                AffectLaunched = 1, ApplyDamage = 1
            };
            foreach ((RefRO<LocalTransform> transform, RefRO<EnemyLaunchState> launchState,
                         RefRO<Health> health, RefRO<EnemyTier> tier, Entity enemy) in
                     SystemAPI.Query<RefRO<LocalTransform>, RefRO<EnemyLaunchState>, RefRO<Health>, RefRO<EnemyTier>>()
                         .WithAll<Enemy>()
                         .WithNone<RespawnRequest>()
                         .WithEntityAccess())
            {
                PunchSpecification targetPunch = punch;
                if (PunchResolution.IsEligible(launchState.ValueRO, health.ValueRO, punch)
                    && PunchResolution.Contains(transform.ValueRO.Position, punch)
                    && PunchAimAssist.TryGetLockedDirection(
                        state.EntityManager, enemy, transform.ValueRO.Position, out float3 assistedDirection))
                {
                    targetPunch.AssistedLaunchDirection = assistedDirection;
                    targetPunch.HasAssistedLaunchDirection = 1;
                }
                if (tier.ValueRO.Value == EnemyCombatTier.Elite)
                    targetPunch.Strength *= math.max(0f, punchRequest.EliteKnockbackMultiplier);
                PunchResolution.TryApply(state.EntityManager, enemy, targetPunch);
            }

            SystemAPI.SetComponentEnabled<PunchRequest>(punchEntity, false);
        }
    }
}
