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
                         EnabledRefRW<DeathRequest> deathRequestEnabled, Entity enemy) in
                     SystemAPI.Query<RefRW<RespawnRequest>, EnabledRefRW<RespawnRequest>, EnabledRefRW<DeathRequest>>()
                         .WithAll<Enemy>()
                         .WithOptions(EntityQueryOptions.IgnoreComponentEnabledState).WithEntityAccess())
            {
                if (!deathRequestEnabled.ValueRO)
                {
                    continue;
                }

                double landingDuration = SystemAPI.HasComponent<EnemyLandingAnimation>(enemy)
                    && SystemAPI.HasComponent<EnemyLaunchState>(enemy)
                    && SystemAPI.GetComponent<EnemyLaunchState>(enemy).LaunchSequence != 0
                    ? SystemAPI.GetComponent<EnemyLandingAnimation>(enemy).Duration : 0d;
                // INFO-004 / ENEMY-013: retain the Bomb only for its authored visual after
                // damage and detonation have resolved, including direct player contact.
                if (SystemAPI.HasComponent<EnemyLandingAnimation>(enemy)
                    && SystemAPI.GetComponent<EnemyLandingAnimation>(enemy).OrdinaryDefeatDuration > 0)
                {
                    var animation = SystemAPI.GetComponent<EnemyLandingAnimation>(enemy);
                    landingDuration = SystemAPI.HasComponent<ExplosiveEnemyState>(enemy)
                        && SystemAPI.GetComponent<ExplosiveEnemyState>(enemy).HasExploded != 0
                        ? animation.Duration : animation.OrdinaryDefeatDuration;
                }
                respawnRequest.ValueRW = new RespawnRequest
                {
                    ForcePoolAt = elapsedTime + MaximumDefeatTravelSeconds,
                    PoolNotBefore = elapsedTime + landingDuration
                };
                respawnRequestEnabled.ValueRW = true;
                deathRequestEnabled.ValueRW = false;
            }
        }
    }
}
