using CrowdPunch.Components;
using CrowdPunch.Mono.UI;
using CrowdPunch.Systems.Groups;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace CrowdPunch.Systems.Initialization
{
    /// <summary>
    /// Resets ECS-owned gameplay state without reloading scenes or SubScenes.
    /// </summary>
    [UpdateInGroup(typeof(GameInitializationGroup))]
    [UpdateAfter(typeof(EnemySpawnSystem))]
    public partial class GameRestartSystem : SystemBase
    {
        private uint lastRestartSequence;

        protected override void OnCreate()
        {
            RequireForUpdate<SpawnSettings>();
            RequireForUpdate<Enemy>();
            lastRestartSequence = GameRestartRegistry.Sequence;
        }

        protected override void OnUpdate()
        {
            uint restartSequence = GameRestartRegistry.Sequence;
            if (restartSequence == lastRestartSequence)
            {
                return;
            }

            lastRestartSequence = restartSequence;

            SpawnSettings spawnSettings = SystemAPI.GetSingleton<SpawnSettings>();
            Random random = Random.CreateFromIndex(1);

            foreach ((RefRW<LocalTransform> transform, RefRW<Health> health, RefRW<HealthBar> healthBar, Entity enemy) in
                     SystemAPI.Query<RefRW<LocalTransform>, RefRW<Health>, RefRW<HealthBar>>()
                         .WithAll<Enemy>()
                         .WithEntityAccess())
            {
                transform.ValueRW = LocalTransform.FromPosition(GetRandomSpawnPosition(
                    ref random,
                    spawnSettings.Center,
                    spawnSettings.SpawnRadius));

                health.ValueRW.Current = health.ValueRO.Max;
                healthBar.ValueRW.Normalized = health.ValueRO.Normalized;

                if (SystemAPI.HasComponent<PhysicsVelocity>(enemy))
                {
                    SystemAPI.SetComponent(enemy, new PhysicsVelocity());
                }

                if (SystemAPI.HasComponent<DamageRequest>(enemy))
                {
                    SystemAPI.SetComponentEnabled<DamageRequest>(enemy, false);
                }

                if (SystemAPI.HasComponent<DeathRequest>(enemy))
                {
                    SystemAPI.SetComponentEnabled<DeathRequest>(enemy, false);
                }

                if (SystemAPI.HasComponent<ExternalImpulse>(enemy))
                {
                    SystemAPI.SetComponentEnabled<ExternalImpulse>(enemy, false);
                }

                if (SystemAPI.HasComponent<KnockbackRecovery>(enemy))
                {
                    SystemAPI.SetComponentEnabled<KnockbackRecovery>(enemy, false);
                }

                if (SystemAPI.HasComponent<RespawnRequest>(enemy))
                {
                    SystemAPI.SetComponentEnabled<RespawnRequest>(enemy, false);
                }
            }
        }

        private static float3 GetRandomSpawnPosition(ref Random random, float3 center, float radius)
        {
            float angle = random.NextFloat(0f, math.PI * 2f);
            float distance = math.sqrt(random.NextFloat()) * math.max(0f, radius);
            float x = math.cos(angle) * distance;
            float z = math.sin(angle) * distance;

            return center + new float3(x, 0f, z);
        }
    }
}
