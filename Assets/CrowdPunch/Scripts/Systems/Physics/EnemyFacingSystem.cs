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
            new FacePlayerJob { PlayerPosition = player.Position, PlayerAvailable = player.IsAvailable }.ScheduleParallel();
        }

        [BurstCompile]
        [WithAll(typeof(Enemy))]
        [WithNone(typeof(RespawnRequest))]
        private partial struct FacePlayerJob : IJobEntity
        {
            public float3 PlayerPosition;
            public bool PlayerAvailable;

            private void Execute(ref LocalTransform transform, ref PhysicsVelocity velocity,
                in EnemyLaunchState launch)
            {
                bool launched = launch.Phase == EnemyLaunchPhase.Launched;
                if (!launched && (!PlayerAvailable ||
                    (launch.Phase != EnemyLaunchPhase.Active && launch.Phase != EnemyLaunchPhase.Recovering)))
                    return;
                float3 toward = launched ? velocity.Linear : PlayerPosition - transform.Position;
                toward.y = 0f;
                if (math.lengthsq(toward) <= 0.0001f) return;
                // Keep the physics capsule upright; animation supplies launched visual pitch.
                transform.Rotation = quaternion.LookRotationSafe(toward, math.up());
                velocity.Angular.y = 0f;
            }
        }
    }
}
