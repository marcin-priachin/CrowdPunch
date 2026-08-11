using CrowdPunch.Components;
using CrowdPunch.Systems.Combat;
using CrowdPunch.Systems.Groups;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;

namespace CrowdPunch.Systems.Physics
{
    [BurstCompile, UpdateInGroup(typeof(GamePostPhysicsGroup)), UpdateAfter(typeof(DasherEnemyImpactSystem))]
    public partial struct DasherObstacleStopSystem : ISystem
    {
        [BurstCompile] public void OnUpdate(ref SystemState state)
        {
            foreach ((RefRW<DasherState> dash, RefRO<DasherSettings> settings, RefRO<PhysicsVelocity> velocity) in
                     SystemAPI.Query<RefRW<DasherState>, RefRO<DasherSettings>, RefRO<PhysicsVelocity>>().WithNone<RespawnRequest>())
            {
                if (dash.ValueRO.Phase != DasherPhase.Dashing) continue;
                if (math.length(velocity.ValueRO.Linear.xz) < math.max(0.5f, settings.ValueRO.DashSpeed * 0.25f))
                {
                    dash.ValueRW.Phase = DasherPhase.Recovering;
                    dash.ValueRW.SecondsRemaining = math.max(0f, settings.ValueRO.RecoveryDuration);
                }
            }
        }
    }
}
