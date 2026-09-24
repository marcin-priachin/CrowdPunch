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
            if (authoring.bossEncounter != null)
            {
                if (authoring.Waves.Count != 1) throw new System.InvalidOperationException("Boss crowd requires exactly one bounded wave.");
                AddComponent(entity, new BossCrowdSequence { Encounter=GetEntity(authoring.bossEncounter,TransformUsageFlags.Dynamic) });
            }
            AddComponent(entity, new EnemyWaveSequence
            {
                InitialSeed = authoring.RandomSeed,
                RandomState = authoring.RandomSeed,
                RunGeneration = 1,
                MinimumPlayerDistance = math.max(0f, authoring.MinimumPlayerDistance),
                PlacementAttemptsPerEnemy = math.max(1, authoring.PlacementAttemptsPerEnemy),
                Phase = EnemyWaveRuntimePhase.PreWaveDelay
            });
            AddComponent<EnemyWaveEncounterComplete>(entity);
            SetComponentEnabled<EnemyWaveEncounterComplete>(entity, false);

            DynamicBuffer<EnemyWaveDefinition> definitions = AddBuffer<EnemyWaveDefinition>(entity);
            DynamicBuffer<EnemyWaveProfile> profiles = AddBuffer<EnemyWaveProfile>(entity);
            DynamicBuffer<EnemyWaveEliteProfile> eliteProfiles = AddBuffer<EnemyWaveEliteProfile>(entity);
            DynamicBuffer<EnemyWaveSpawnRange> ranges = AddBuffer<EnemyWaveSpawnRange>(entity);

            for (int waveIndex = 0; waveIndex < authoring.Waves.Count; waveIndex++)
            {
                EnemyWaveSettings wave = authoring.Waves[waveIndex];
                int profileStart = profiles.Length;
                int eliteProfileStart = eliteProfiles.Length;
                int rangeStart = ranges.Length;
                float totalWeight = 0f;
                float totalArea = 0f;
                int requestedMinimumNormalCount = 0;
                int validMinimumNormalCount = 0;

                if (wave == null)
                {
                    definitions.Add(new EnemyWaveDefinition { BatchSize = 1, IsValid = 1 });
                    continue;
                }

                DependsOn(wave);
                foreach (EnemyWaveSettings.WeightedEnemy entry in wave.Enemies)
                {
                    int minimumCount = math.max(0, entry.MinimumCount);
                    requestedMinimumNormalCount += minimumCount;
                    if (entry.Settings != null && (entry.Weight > 0f || minimumCount > 0)
                        && entry.Settings.Archetype == Configuration.EnemyArchetype.Elite)
                    {
                        Debug.LogError($"Wave '{wave.name}' contains an Elite profile in its weighted normal list; the entry was excluded.", authoring);
                    }
                    if (entry.Settings == null || entry.Settings.EnemyPrefab == null
                        || entry.Weight <= 0f && minimumCount <= 0
                        || entry.Settings.Archetype == Configuration.EnemyArchetype.Elite)
                        continue;
                    DependsOn(entry.Settings);
                    DependsOn(entry.Settings.EnemyPrefab);
                    if (!EnemySpawnProfileBaking.TryCreate(this, entry.Settings, out EnemySpawnProfile profile)
                        || profile.SpawnClearance <= 0f)
                        continue;
                    profiles.Add(new EnemyWaveProfile { Profile = profile, MinimumCount = minimumCount, Weight = entry.Weight });
                    validMinimumNormalCount += minimumCount;
                    totalWeight += entry.Weight;
                }

                int totalEliteCount = 0;
                int requestedEliteCount = 0;
                foreach (EnemyWaveSettings.FixedEliteEnemy entry in wave.EliteEnemies)
                {
                    requestedEliteCount += math.max(0, entry.Count);
                    if (entry.Count > 0 && (entry.Settings == null || entry.Settings.EnemyPrefab == null
                        || entry.Settings.Archetype != Configuration.EnemyArchetype.Elite))
                    {
                        Debug.LogError($"Wave '{wave.name}' has a positive fixed elite count with a null, prefab-less, or non-Elite profile; the entry was excluded.", authoring);
                    }
                    if (entry.Count <= 0 || entry.Settings == null || entry.Settings.EnemyPrefab == null
                        || entry.Settings.Archetype != Configuration.EnemyArchetype.Elite)
                        continue;
                    DependsOn(entry.Settings);
                    DependsOn(entry.Settings.EnemyPrefab);
                    if (!EnemySpawnProfileBaking.TryCreate(this, entry.Settings, out EnemySpawnProfile profile)
                        || profile.SpawnClearance <= 0f)
                        continue;
                    eliteProfiles.Add(new EnemyWaveEliteProfile { Profile = profile, Count = entry.Count });
                    totalEliteCount += entry.Count;
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

                EnemySpawnProfile ammunition = default;
                bool hasAmmunition = wave.ArmoredAmmunitionProfile != null;
                bool ammunitionValid = !hasAmmunition || wave.ArmoredAmmunitionProfile.Archetype == Configuration.EnemyArchetype.Baseline
                    && EnemySpawnProfileBaking.TryCreate(this, wave.ArmoredAmmunitionProfile, out ammunition)
                    && ammunition.SpawnClearance > 0f;
                if (!ammunitionValid) Debug.LogError($"Wave '{wave.name}' needs a valid Baseline ammunition profile.", authoring);
                int totalCount = math.max(0, wave.TotalEnemyCount);
                int weightedNormalCount = totalCount - requestedMinimumNormalCount;
                bool normalValid = (weightedNormalCount <= 0 || totalWeight > 0f)
                    && requestedMinimumNormalCount == validMinimumNormalCount
                    && requestedMinimumNormalCount <= totalCount;
                bool eliteValid = totalEliteCount == requestedEliteCount;
                bool valid = ammunitionValid && normalValid && eliteValid
                    && (totalCount + requestedEliteCount == 0 || totalArea > 0f);
                if (!valid)
                    Debug.LogError($"Wave '{wave.name}' cannot spawn: ensure it has a positive-area range, valid positive-weight profiles with prefab colliders, and profile minimums no greater than its normal-enemy total.", authoring);

                definitions.Add(new EnemyWaveDefinition
                {
                    AmmunitionProfile = ammunition,
                    AmmunitionSafeguard = hasAmmunition && ammunitionValid ? (byte)1 : (byte)0,
                    BossReplenishment=(byte)(wave.ReplenishWhileBossLives?1:0),
                    BossReplenishDelay=math.max(0,wave.BossReplenishDelay),
                    TotalEnemyCount = totalCount,
                    TotalMinimumNormalCount = requestedMinimumNormalCount,
                    ProfileStart = profileStart,
                    ProfileCount = profiles.Length - profileStart,
                    EliteProfileStart = eliteProfileStart,
                    EliteProfileCount = eliteProfiles.Length - eliteProfileStart,
                    TotalEliteCount = requestedEliteCount,
                    RangeStart = rangeStart,
                    RangeCount = ranges.Length - rangeStart,
                    TotalProfileWeight = totalWeight,
                    TotalRangeArea = totalArea,
                    DelayBeforeWave = math.max(0f, wave.DelayBeforeWave),
                    Duration = math.max(0f, wave.Duration),
                    BatchInterval = math.max(0f, wave.BatchInterval),
                    BatchSize = math.max(1, wave.BatchSize),
                    ActivationMode = (byte)wave.ActivationMode,
                    SpawnMode = (byte)wave.SpawnMode,
                    IsValid = valid ? (byte)1 : (byte)0
                });
            }
        }
    }
}
