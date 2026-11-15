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

                double landingDuration = 0d;
                if (SystemAPI.HasComponent<EnemyLandingAnimation>(enemy))
                {
                    var animation = SystemAPI.GetComponent<EnemyLandingAnimation>(enemy);
                    if (animation.OrdinaryDefeatDuration > 0f)
                    {
                        // Exploders use the detonation clip after an explosion and Die for ordinary defeats.
                        landingDuration = SystemAPI.HasComponent<ExplosiveEnemyState>(enemy)
                            && SystemAPI.GetComponent<ExplosiveEnemyState>(enemy).HasExploded != 0
                            ? animation.Duration : animation.OrdinaryDefeatDuration;
                    }
                    else if (SystemAPI.HasComponent<EnemyLaunchState>(enemy)
                        && SystemAPI.GetComponent<EnemyLaunchState>(enemy).LaunchSequence != 0)
                    {
                        landingDuration = animation.Duration;
                    }
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
