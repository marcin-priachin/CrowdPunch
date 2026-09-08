using CrowdPunch.Components;
using CrowdPunch.Systems.Groups;
using Unity.Burst;
using Unity.Entities;
using Unity.Physics;
using Unity.Transforms;

namespace CrowdPunch.Systems.Physics
{
    [BurstCompile]
    [UpdateInGroup(typeof(GamePostPhysicsGroup), OrderFirst = true)]
    public partial struct EnemyGroundReconciliationSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            new ReconcileJob().ScheduleParallel();
        }

        [BurstCompile]
        [WithAll(typeof(Enemy))]
        [WithNone(typeof(RespawnRequest))]
        private partial struct ReconcileJob : IJobEntity
        {
            private void Execute(ref EnemyGroundConstraint ground, ref LocalTransform transform,
                ref PhysicsVelocity velocity)
            {
                EnemyGroundProjection.Apply(ref ground, ref transform, ref velocity);
            }
        }
    }
}
