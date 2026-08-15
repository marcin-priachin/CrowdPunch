using CrowdPunch.Authoring;
using CrowdPunch.Components;
using CrowdPunch.Configuration;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace CrowdPunch.Bakers
{
    public sealed class EnemyWaveSequenceBaker : Baker<EnemyWaveSequenceAuthoring>
    {
        public override void Bake(EnemyWaveSequenceAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.None);
            AddComponent(entity, new EnemyWaveSequence
            {
                InitialSeed = authoring.RandomSeed,
                RandomState = authoring.RandomSeed,
                RunGeneration = 1,
                MinimumPlayerDistance = math.max(0f, authoring.MinimumPlayerDistance),
                PlacementAttemptsPerEnemy = math.max(1, authoring.PlacementAttemptsPerEnemy),
                ActivationMode = (byte)authoring.ActivationMode,
                Phase = EnemyWaveRuntimePhase.PreWaveDelay
            });
            AddComponent<EnemyWaveEncounterComplete>(entity);
            SetComponentEnabled<EnemyWaveEncounterComplete>(entity, false);

            DynamicBuffer<EnemyWaveDefinition> definitions = AddBuffer<EnemyWaveDefinition>(entity);
            DynamicBuffer<EnemyWaveProfile> profiles = AddBuffer<EnemyWaveProfile>(entity);
            DynamicBuffer<EnemyWaveSpawnRange> ranges = AddBuffer<EnemyWaveSpawnRange>(entity);

            for (int waveIndex = 0; waveIndex < authoring.Waves.Count; waveIndex++)
            {
                EnemyWaveSettings wave = authoring.Waves[waveIndex];
                int profileStart = profiles.Length;
                int rangeStart = ranges.Length;
                float totalWeight = 0f;
                float totalArea = 0f;

                if (wave == null)
                {
                    definitions.Add(new EnemyWaveDefinition { BatchSize = 1, IsValid = 1 });
                    continue;
                }

                DependsOn(wave);
                foreach (EnemyWaveSettings.WeightedEnemy entry in wave.Enemies)
                {
                    if (entry.Settings == null || entry.Settings.EnemyPrefab == null || entry.Weight <= 0f)
                        continue;
                    DependsOn(entry.Settings);
                    DependsOn(entry.Settings.EnemyPrefab);
                    if (!EnemySpawnProfileBaking.TryCreate(this, entry.Settings, out EnemySpawnProfile profile)
                        || profile.SpawnClearance <= 0f)
                        continue;
                    profiles.Add(new EnemyWaveProfile { Profile = profile, Weight = entry.Weight });
                    totalWeight += entry.Weight;
                }

                foreach (EnemyWaveSettings.SpawnRectangle rectangle in wave.SpawnRectangles)
                {
                    float width = math.max(0f, rectangle.Width);
                    float depth = math.max(0f, rectangle.Depth);
                    float area = width * depth;
                    if (area <= 0f) continue;
                    ranges.Add(new EnemyWaveSpawnRange
                    {
                        Center = rectangle.Center,
                        Width = width,
                        Depth = depth,
                        Area = area
                    });
                    totalArea += area;
                }

                int totalCount = math.max(0, wave.TotalEnemyCount);
                bool valid = totalCount == 0 || (totalWeight > 0f && totalArea > 0f);
                if (!valid)
                    Debug.LogError($"Wave '{wave.name}' cannot spawn: add a positive-area range and a positive-weight profile with a prefab collider.", authoring);

                definitions.Add(new EnemyWaveDefinition
                {
                    TotalEnemyCount = totalCount,
                    ProfileStart = profileStart,
                    ProfileCount = profiles.Length - profileStart,
                    RangeStart = rangeStart,
                    RangeCount = ranges.Length - rangeStart,
                    TotalProfileWeight = totalWeight,
                    TotalRangeArea = totalArea,
                    DelayBeforeWave = math.max(0f, wave.DelayBeforeWave),
                    Duration = math.max(0f, wave.Duration),
                    BatchInterval = math.max(0f, wave.BatchInterval),
                    BatchSize = math.max(1, wave.BatchSize),
                    SpawnMode = (byte)wave.SpawnMode,
                    IsValid = valid ? (byte)1 : (byte)0
                });
            }
        }
    }
}
