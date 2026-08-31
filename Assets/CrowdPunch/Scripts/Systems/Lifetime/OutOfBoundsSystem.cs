using CrowdPunch.Components;
using CrowdPunch.Systems.Groups;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace CrowdPunch.Systems.Lifetime
{
    /// <summary>
    /// Detects enemies that have left the arena.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(GamePostPhysicsGroup))]
    public partial struct OutOfBoundsSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<EnemyDefeatBounds>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            EnemyDefeatBounds defeatBounds = SystemAPI.GetSingleton<EnemyDefeatBounds>();
            float3 minimum = defeatBounds.Center - math.max(defeatBounds.Extents, float3.zero);
            float3 maximum = defeatBounds.Center + math.max(defeatBounds.Extents, float3.zero);
            double elapsedTime = SystemAPI.Time.ElapsedTime;

            foreach ((RefRO<LocalTransform> transform,
                         RefRW<Health> health,
                         RefRW<EnemyLaunchState> launchState,
                         RefRO<EnemyRespawnSettings> respawnSettings,
                         RefRW<RespawnRequest> respawnRequest,
                         EnabledRefRW<RespawnRequest> respawnRequestEnabled,
                         EnabledRefRW<DeathRequest> deathRequestEnabled) in
                     SystemAPI.Query<RefRO<LocalTransform>, RefRW<Health>, RefRW<EnemyLaunchState>,
                             RefRO<EnemyRespawnSettings>, RefRW<RespawnRequest>,
                             EnabledRefRW<RespawnRequest>, EnabledRefRW<DeathRequest>>()
                         .WithAll<Enemy>()
                         .WithOptions(EntityQueryOptions.IgnoreComponentEnabledState))
            {
                if (respawnRequestEnabled.ValueRO || IsInside(transform.ValueRO.Position, minimum, maximum))
                {
                    continue;
                }

                if (respawnSettings.ValueRO.Enabled != 0)
                {
                    respawnRequest.ValueRW = new RespawnRequest { ForcePoolAt = elapsedTime };
                    respawnRequestEnabled.ValueRW = true;
                    continue;
                }

                // Fixed wave enemies cannot return to play. Treat leaving the authored defeat bounds as
                // terminal defeat so they cannot invisibly block cumulative wave completion.
                health.ValueRW.Current = 0f;
                launchState.ValueRW.Phase = EnemyLaunchPhase.Defeated;
                deathRequestEnabled.ValueRW = true;
            }
        }

        internal static bool IsInside(float3 position, float3 minimum, float3 maximum)
        {
            return math.all(position >= minimum) && math.all(position <= maximum);
        }
    }
}
