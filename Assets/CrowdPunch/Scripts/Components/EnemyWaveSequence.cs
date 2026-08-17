using Unity.Entities;
using Unity.Mathematics;

namespace CrowdPunch.Components
{
    public enum EnemyWaveRuntimePhase : byte { PreWaveDelay, Spawning, AwaitingActivation, Complete, Invalid }

    public struct EnemyWaveSequence : IComponentData
    {
        public uint InitialSeed;
        public uint RandomState;
        public uint RunGeneration;
        public int CurrentWaveIndex;
        public int SpawnedCount;
        public int DefeatedCount;
        public int NormalLivingCount;
        public int EliteSpawnedCount;
        public int EliteLivingCount;
        public int EliteProfileCursor;
        public int EliteProfileSpawnedInEntry;
        public double NextActionAt;
        public double NextPlacementWarningAt;
        public float MinimumPlayerDistance;
        public int PlacementAttemptsPerEnemy;
        public byte ActivationMode;
        public EnemyWaveRuntimePhase Phase;
        public byte Initialized;
    }

    public struct EnemyWaveEncounterComplete : IComponentData, IEnableableComponent { }

    public struct EnemyWaveDefinition : IBufferElementData
    {
        public int TotalEnemyCount;
        public int ProfileStart;
        public int ProfileCount;
        public int EliteProfileStart;
        public int EliteProfileCount;
        public int TotalEliteCount;
        public int RangeStart;
        public int RangeCount;
        public float TotalProfileWeight;
        public float TotalRangeArea;
        public float DelayBeforeWave;
        public float Duration;
        public float BatchInterval;
        public int BatchSize;
        public byte SpawnMode;
        public byte IsValid;
    }

    public struct EnemyWaveEliteProfile : IBufferElementData
    {
        public EnemySpawnProfile Profile;
        public int Count;
    }

    public struct EnemyWaveProfile : IBufferElementData
    {
        public EnemySpawnProfile Profile;
        public float Weight;
    }

    public struct EnemyWaveSpawnRange : IBufferElementData
    {
        public float3 Center;
        public float Width;
        public float Depth;
        public float Area;
    }

    public struct EnemyWaveOwnership : IComponentData
    {
        public Entity Sequence;
        public uint RunGeneration;
        public int WaveIndex;
        public byte DefeatCounted;
        public byte IsElite;
    }
}
