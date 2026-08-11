using CrowdPunch.Components;
using CrowdPunch.Systems.Groups;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Rendering;

namespace CrowdPunch.Systems.Initialization
{
    /// <summary>
    /// Creates the initial ECS enemy crowd from baked spawn settings.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(GameInitializationGroup))]
    [UpdateAfter(typeof(BootstrapSystem))]
    public partial struct EnemySpawnSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SpawnSettings>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            EntityCommandBuffer commandBuffer = new EntityCommandBuffer(Allocator.Temp);
            Random random = Random.CreateFromIndex(1);

            foreach (RefRO<SpawnSettings> spawnSettingsReference in SystemAPI.Query<RefRO<SpawnSettings>>())
            {
                SpawnSettings spawnSettings = spawnSettingsReference.ValueRO;

                if (spawnSettings.EnemyPrefab == Entity.Null || spawnSettings.InitialCount <= 0)
                {
                    continue;
                }

                EnemyMovementSettings movementSettings = SystemAPI.GetComponent<EnemyMovementSettings>(
                    spawnSettings.EnemyPrefab);
                float separationMin = math.max(0f, math.min(
                    movementSettings.SeparationDistanceMin,
                    movementSettings.SeparationDistanceMax));
                float separationMax = math.max(separationMin, math.max(
                    movementSettings.SeparationDistanceMin,
                    movementSettings.SeparationDistanceMax));

                for (int index = 0; index < spawnSettings.InitialCount; index++)
                {
                    Entity enemy = commandBuffer.Instantiate(spawnSettings.EnemyPrefab);
                    float3 position = GetRandomSpawnPosition(ref random, spawnSettings.Center, spawnSettings.SpawnRadius);
                    float separationDistance = random.NextFloat(separationMin, separationMax);

                    commandBuffer.SetComponent(enemy, LocalTransform.FromPosition(position));
                    commandBuffer.SetComponent(enemy, new EnemySeparationDistance
                    {
                        Value = separationDistance
                    });
                    commandBuffer.AddComponent(enemy, new EnemyArchetype
                    {
                        Value = spawnSettings.Archetype
                    });
                    commandBuffer.AddComponent(enemy, new EnemyRespawnSettings
                    {
                        Enabled = spawnSettings.RespawnEnabled
                    });

                    if (spawnSettings.Archetype == EnemyArchetypeKind.Ranged)
                    {
                        commandBuffer.AddComponent(enemy, spawnSettings.RangedSettings);
                        commandBuffer.AddComponent(enemy, new RangedPositioningState
                        {
                            Mode = RangedPositioningMode.Hold
                        });
                        commandBuffer.AddComponent(enemy, new URPMaterialPropertyBaseColor
                        {
                            Value = new float4(0.15f, 0.65f, 1f, 1f)
                        });
                        float delayVariation = math.max(0f, spawnSettings.RangedSettings.InitialDelayVariation);
                        commandBuffer.AddComponent(enemy, new RangedAttackState
                        {
                            Phase = RangedAttackPhase.InitialDelay,
                            SecondsRemaining = math.max(0f, spawnSettings.RangedSettings.InitialAttackDelay)
                                + random.NextFloat(0f, delayVariation)
                        });
                    }
                    else if (spawnSettings.Archetype == EnemyArchetypeKind.Explosive)
                    {
                        commandBuffer.AddComponent(enemy, spawnSettings.ExplosiveSettings);
                        commandBuffer.AddComponent<ExplosiveEnemyState>(enemy);
                        commandBuffer.AddComponent<ExplosiveDetonationRequest>(enemy);
                        commandBuffer.SetComponentEnabled<ExplosiveDetonationRequest>(enemy, false);
                        commandBuffer.AddComponent(enemy, new URPMaterialPropertyBaseColor
                        {
                            Value = new float4(1f, 0.25f, 0.05f, 1f)
                        });
                    }
                    else if (spawnSettings.Archetype == EnemyArchetypeKind.Dasher)
                    {
                        commandBuffer.AddComponent(enemy, spawnSettings.DasherSettings);
                        commandBuffer.AddComponent(enemy, new DasherState { Phase = DasherPhase.Positioning });
                        commandBuffer.AddComponent<DasherColliderState>(enemy);
                        commandBuffer.AddBuffer<DasherHitHistory>(enemy);
                        commandBuffer.AddComponent(enemy, new PostTransformMatrix { Value = float4x4.Scale(new float3(0.8f, 1.15f, 1.45f)) });
                        commandBuffer.AddComponent(enemy, new URPMaterialPropertyBaseColor
                        {
                            Value = new float4(0.38f, 0.38f, 0.42f, 1f)
                        });
                    }
                }
            }

            commandBuffer.Playback(state.EntityManager);
            commandBuffer.Dispose();

            state.Enabled = false;
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
