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
            var damage = SystemAPI.GetComponentLookup<EnemyDamageState>(true);

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
                bool elite = IsElite(state.EntityManager, enemy);
                if (SystemAPI.HasComponent<EnemyLandingAnimation>(enemy))
                {
                    var animation = SystemAPI.GetComponent<EnemyLandingAnimation>(enemy);
                    if (elite)
                    {
                        landingDuration = animation.Duration;
                    }
                    else if (animation.OrdinaryDefeatDuration > 0f)
                    {
                        // Exploders use the detonation clip after an explosion and Die for ordinary defeats.
                        landingDuration = SystemAPI.HasComponent<ExplosiveEnemyState>(enemy)
                            && SystemAPI.GetComponent<ExplosiveEnemyState>(enemy).HasExploded != 0
                            ? animation.Duration : animation.OrdinaryDefeatDuration;
                    }
                    else if (damage.TryGetComponent(enemy, out var hit) && hit.PlayDefeatAnimation != 0
                        || SystemAPI.HasComponent<EnemyLaunchState>(enemy)
                            && SystemAPI.GetComponent<EnemyLaunchState>(enemy).LaunchSequence != 0)
                    {
                        landingDuration = animation.Duration;
                    }
                }
                respawnRequest.ValueRW = new RespawnRequest
                {
                    ForcePoolAt = elapsedTime + (elite && landingDuration > 0d ? landingDuration : MaximumDefeatTravelSeconds),
                    PoolNotBefore = elapsedTime + landingDuration
                };
                respawnRequestEnabled.ValueRW = true;
                deathRequestEnabled.ValueRW = false;
            }
        }

        internal static bool IsEliteDefeatPlaying(EntityManager em, Entity enemy)
        {
            if (!IsElite(em, enemy) || !em.HasComponent<EnemyLandingAnimation>(enemy)
                || !em.HasComponent<EnemyLaunchState>(enemy)
                || em.GetComponentData<EnemyLaunchState>(enemy).Phase != EnemyLaunchPhase.Defeated) return false;

            Entity visual = em.GetComponentData<EnemyLandingAnimation>(enemy).Visual;
            return em.HasComponent<EnemyAnimation>(visual) && em.GetComponentData<EnemyAnimation>(visual).Owner == enemy
                && em.HasComponent<EnemyAnimationPlayback>(visual) && !em.HasComponent<Disabled>(visual)
                && em.GetComponentData<EnemyAnimationPlayback>(visual).DefeatFinished == 0;
        }

        internal static bool IsElite(EntityManager em, Entity enemy) => em.HasComponent<EnemyTier>(enemy)
            && em.GetComponentData<EnemyTier>(enemy).Value == EnemyCombatTier.Elite;
    }
}
