using CrowdPunch.Components;
using CrowdPunch.Systems.Combat;
using CrowdPunch.Utilities;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;

namespace CrowdPunch.Systems.Initialization
{
    // ENEMY-015. Runs only for opted-in waves after their initial spawn queue has drained.
    internal static class ArmoredAmmunitionSupply
    {
        internal static bool NeedsAmmunition(EntityManager em, NativeArray<Entity> enemies,
            Entity sequenceEntity, uint generation)
        {
            bool protectedEnemy = false;
            for (int i = 0; i < enemies.Length; i++)
            {
                Entity enemy = enemies[i];
                var ownership = em.GetComponentData<EnemyWaveOwnership>(enemy);
                if (ownership.Sequence != sequenceEntity || ownership.RunGeneration != generation
                    || ownership.DefeatCounted != 0
                    || em.HasComponent<RespawnRequest>(enemy) && em.IsComponentEnabled<RespawnRequest>(enemy)) continue;
                var launch = em.GetComponentData<EnemyLaunchState>(enemy);
                if (launch.Phase == EnemyLaunchPhase.Defeated) continue;
                if (ArmorHitResolution.IsProtected(em, enemy)) { protectedEnemy = true; continue; }
                if (em.GetComponentData<EnemyTier>(enemy).Value == EnemyCombatTier.Normal
                    && EnemyLaunchTransition.CanReceivePlayerPunch(launch, em.GetComponentData<Health>(enemy))) return false;
            }
            return protectedEnemy;
        }

        internal static void TrySpawn(EntityManager em, EntityCommandBuffer commands, Entity sequenceEntity,
            ref EnemyWaveSequence sequence, EnemyWaveDefinition wave, DynamicBuffer<EnemyWaveSpawnRange> ranges,
            PhysicsWorldSingleton physics, NavigationGrid navigation, PlayerSnapshot player,
            NativeList<float4> occupied)
        {
            using var query = em.CreateEntityQuery(ComponentType.ReadOnly<EnemyWaveOwnership>(),
                ComponentType.ReadOnly<EnemyLaunchState>(), ComponentType.ReadOnly<Health>(), ComponentType.ReadOnly<EnemyTier>());
            using var enemies = query.ToEntityArray(Allocator.Temp);
            if (!NeedsAmmunition(em, enemies, sequenceEntity, sequence.RunGeneration)) return;
            var profile = wave.AmmunitionProfile;
            var random = new Random(sequence.RandomState == 0 ? 1u : sequence.RandomState);
            using var accepted = new NativeList<float4>(Allocator.Temp);
            for (int attempt = 0; attempt < math.max(1, sequence.PlacementAttemptsPerEnemy); attempt++)
            {
                float3 position = EnemyWaveSpawnSystem.SelectPosition(ref random, wave, ranges);
                if (!NavigationGeometry.SpawnAllowed(navigation, position.xz, profile.NavigationRadius)
                    || !EnemyWaveSpawnSystem.IsSafe(physics, player, position, profile.SpawnClearance,
                        sequence.MinimumPlayerDistance, occupied, accepted)) continue;
                Entity enemy = EnemySpawnInitialization.Create(commands, em, profile, position, ref random);
                if (enemy == Entity.Null) break;
                // Retain only one spare root per wave; old counted spares have no remaining gameplay role.
                foreach (Entity old in enemies)
                {
                    var owner = em.GetComponentData<EnemyWaveOwnership>(old);
                    if (owner.Sequence == sequenceEntity && owner.RunGeneration == sequence.RunGeneration
                        && owner.WaveIndex == sequence.CurrentWaveIndex && owner.DefeatCounted != 0
                        && em.HasComponent<ArmoredAmmunitionReplacement>(old)) commands.DestroyEntity(old);
                }
                commands.SetComponent(enemy, new EnemyRespawnSettings { Enabled = 0 });
                commands.AddComponent<ArmoredAmmunitionReplacement>(enemy);
                commands.AddComponent(enemy, new EnemyWaveOwnership { Sequence = sequenceEntity,
                    RunGeneration = sequence.RunGeneration, WaveIndex = sequence.CurrentWaveIndex });
                sequence.AmmunitionSpawnedCount++;
                sequence.UndefeatedCount++;
                occupied.Add(new float4(position, profile.SpawnClearance));
                break;
            }
            sequence.RandomState = random.state;
        }
    }
}
