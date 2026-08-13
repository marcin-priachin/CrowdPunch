using CrowdPunch.Components;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;

namespace CrowdPunch.Systems.Initialization
{
    /// <summary>Creates and configures one enemy from a baked profile and resolved position.</summary>
    internal static class EnemySpawnInitialization
    {
        public static Entity Create(
            EntityCommandBuffer commandBuffer,
            EntityManager entityManager,
            in EnemySpawnProfile profile,
            float3 position,
            ref Random random)
        {
            if (profile.EnemyPrefab == Entity.Null
                || !entityManager.HasComponent<EnemyMovementSettings>(profile.EnemyPrefab))
            {
                return Entity.Null;
            }

            EnemyMovementSettings movementSettings = profile.MovementSettings;
            float separationMin = math.max(0f, math.min(
                movementSettings.SeparationDistanceMin,
                movementSettings.SeparationDistanceMax));
            float separationMax = math.max(separationMin, math.max(
                movementSettings.SeparationDistanceMin,
                movementSettings.SeparationDistanceMax));

            Entity enemy = commandBuffer.Instantiate(profile.EnemyPrefab);
            commandBuffer.SetComponent(enemy, LocalTransform.FromPosition(position));
            commandBuffer.SetComponent(enemy, movementSettings);
            commandBuffer.SetComponent(enemy, profile.Health);
            commandBuffer.SetComponent(enemy, profile.ContactDamageSettings);
            commandBuffer.SetComponent(enemy, new EnemySeparationDistance
            {
                Value = random.NextFloat(separationMin, separationMax)
            });
            commandBuffer.AddComponent(enemy, new EnemyArchetype
            {
                Value = profile.Archetype
            });
            commandBuffer.AddComponent(enemy, new EnemyRespawnSettings
            {
                Enabled = profile.RespawnEnabled
            });
            commandBuffer.AddComponent(enemy, new EnemySpawnClearance
            {
                Value = profile.SpawnClearance
            });

            AddArchetypeComponents(commandBuffer, enemy, profile, ref random);
            return enemy;
        }

        private static void AddArchetypeComponents(
            EntityCommandBuffer commandBuffer,
            Entity enemy,
            in EnemySpawnProfile profile,
            ref Random random)
        {
            if (profile.Archetype == EnemyArchetypeKind.Ranged)
            {
                commandBuffer.AddComponent(enemy, profile.RangedSettings);
                commandBuffer.AddComponent(enemy, new RangedPositioningState
                {
                    Mode = RangedPositioningMode.Hold
                });
                commandBuffer.AddComponent(enemy, new URPMaterialPropertyBaseColor
                {
                    Value = new float4(0.15f, 0.65f, 1f, 1f)
                });
                float delayVariation = math.max(0f, profile.RangedSettings.InitialDelayVariation);
                commandBuffer.AddComponent(enemy, new RangedAttackState
                {
                    Phase = RangedAttackPhase.InitialDelay,
                    SecondsRemaining = math.max(0f, profile.RangedSettings.InitialAttackDelay)
                        + random.NextFloat(0f, delayVariation)
                });
            }
            else if (profile.Archetype == EnemyArchetypeKind.Explosive)
            {
                commandBuffer.AddComponent(enemy, profile.ExplosiveSettings);
                commandBuffer.AddComponent<ExplosiveEnemyState>(enemy);
                commandBuffer.AddComponent<ExplosiveDetonationRequest>(enemy);
                commandBuffer.SetComponentEnabled<ExplosiveDetonationRequest>(enemy, false);
                commandBuffer.AddComponent(enemy, new URPMaterialPropertyBaseColor
                {
                    Value = new float4(1f, 0.25f, 0.05f, 1f)
                });
            }
            else if (profile.Archetype == EnemyArchetypeKind.Dasher)
            {
                commandBuffer.AddComponent(enemy, profile.DasherSettings);
                commandBuffer.AddComponent(enemy, new DasherState { Phase = DasherPhase.Positioning });
                commandBuffer.AddComponent<DasherColliderState>(enemy);
                commandBuffer.AddBuffer<DasherHitHistory>(enemy);
                commandBuffer.AddComponent(enemy, new PostTransformMatrix
                {
                    Value = float4x4.Scale(new float3(0.8f, 1.15f, 1.45f))
                });
                commandBuffer.AddComponent(enemy, new URPMaterialPropertyBaseColor
                {
                    Value = new float4(0.38f, 0.38f, 0.42f, 1f)
                });
            }
        }
    }
}
