using CrowdPunch.Components;
using CrowdPunch.Systems.Groups;
using CrowdPunch.Systems.Physics;
using Unity.Burst;
using Unity.Entities;

namespace CrowdPunch.Systems.Lifetime
{
    /// <summary>
    /// Hands newly defeated enemies to the existing pool and respawn lifecycle once.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(GamePostPhysicsGroup))]
    [UpdateAfter(typeof(EnemyRecoverySystem))]
    [UpdateBefore(typeof(EnemyRespawnSystem))]
    public partial struct DefeatedEnemyLifecycleSystem : ISystem
    {
        private const double MaximumDefeatTravelSeconds = 3d;

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            double elapsedTime = SystemAPI.Time.ElapsedTime;

            foreach ((RefRW<RespawnRequest> respawnRequest,
                         EnabledRefRW<RespawnRequest> respawnRequestEnabled,
                         EnabledRefRW<DeathRequest> deathRequestEnabled) in
                     SystemAPI.Query<RefRW<RespawnRequest>, EnabledRefRW<RespawnRequest>, EnabledRefRW<DeathRequest>>()
                         .WithAll<Enemy>()
                         .WithOptions(EntityQueryOptions.IgnoreComponentEnabledState))
            {
                if (!deathRequestEnabled.ValueRO)
                {
                    continue;
                }

                respawnRequest.ValueRW = new RespawnRequest
                {
                    ForcePoolAt = elapsedTime + MaximumDefeatTravelSeconds
                };
                respawnRequestEnabled.ValueRW = true;
                deathRequestEnabled.ValueRW = false;
            }
        }
    }
}
