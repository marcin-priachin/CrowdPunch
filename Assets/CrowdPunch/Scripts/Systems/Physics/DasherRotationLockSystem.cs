using CrowdPunch.Components;
using CrowdPunch.Systems.Combat;
using CrowdPunch.Systems.Groups;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace CrowdPunch.Systems.Physics
{
    /// <summary>Reapplies committed yaw after physics so solver rotation is never presented.</summary>
    [BurstCompile]
    [UpdateInGroup(typeof(GamePostPhysicsGroup))]
    [UpdateAfter(typeof(DasherEnemyImpactSystem))]
    public partial struct DasherRotationLockSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            foreach ((RefRO<DasherState> dash, RefRO<EnemyLaunchState> launch,
                         RefRW<LocalTransform> transform, RefRW<PhysicsVelocity> velocity) in
                     SystemAPI.Query<RefRO<DasherState>, RefRO<EnemyLaunchState>, RefRW<LocalTransform>, RefRW<PhysicsVelocity>>()
                         .WithNone<RespawnRequest>())
            {
                bool locked = dash.ValueRO.HasLockedRotation != 0
                    && (dash.ValueRO.Phase == DasherPhase.Dashing
                        || launch.ValueRO.Phase == EnemyLaunchPhase.Launched);
                if (!locked) continue;
                transform.ValueRW.Rotation = dash.ValueRO.LockedRotation;
                velocity.ValueRW.Angular.y = 0f;
            }
        }
    }
}
