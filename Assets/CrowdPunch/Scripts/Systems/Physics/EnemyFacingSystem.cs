using CrowdPunch.Components;
using CrowdPunch.Systems.Groups;
using CrowdPunch.Systems.Lifetime;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace CrowdPunch.Systems.Physics
{
    [BurstCompile]
    [UpdateInGroup(typeof(GamePostPhysicsGroup))]
    [UpdateAfter(typeof(EnemyRespawnSystem))]
    [UpdateAfter(typeof(DasherRotationLockSystem))]
    public partial struct EnemyFacingSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state) => state.RequireForUpdate<PlayerSnapshot>();

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            PlayerSnapshot player = SystemAPI.GetSingleton<PlayerSnapshot>();
            if (!player.IsAvailable) return;
            new FacePlayerJob { PlayerPosition = player.Position }.ScheduleParallel();
        }

        [BurstCompile]
        [WithAll(typeof(Enemy))]
        [WithNone(typeof(RespawnRequest))]
        private partial struct FacePlayerJob : IJobEntity
        {
            public float3 PlayerPosition;

            private void Execute(ref LocalTransform transform, ref PhysicsVelocity velocity,
                in EnemyLaunchState launch)
            {
                if (launch.Phase != EnemyLaunchPhase.Active && launch.Phase != EnemyLaunchPhase.Recovering)
                    return;
                float3 toward = PlayerPosition - transform.Position;
                toward.y = 0f;
                if (math.lengthsq(toward) <= 0.0001f) return;
                // INFO-004: face the player independently of strafing or committed dash velocity.
                transform.Rotation = quaternion.LookRotationSafe(toward, math.up());
                velocity.Angular.y = 0f;
            }
        }
    }
}
