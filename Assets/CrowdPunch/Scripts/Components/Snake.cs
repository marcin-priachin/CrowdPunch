using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;

namespace CrowdPunch.Components
{
    public enum SnakeExplosionResponse : byte { LaunchAndSever, IgnoreAttachedOrbs }

    public struct SnakeTuning : IComponentData
    {
        public Entity OrbPrefab;
        public int SegmentCount;
        public float OrbRadius, Spacing, WaveAmplitude, WaveFrequencyHz, PursuitSpeed, ContactDamagePercent;
        public float4 ProtectedColor, VulnerableColor;
        public SnakeExplosionResponse ExplosionResponse;
    }
    public struct SnakeHead : IComponentData
    {
        public int AttachedCount;
        public uint Generation;
        public byte Initialized;
        public PhysicsMass DynamicMass;
    }
    public struct SnakeSegment : IComponentData
    {
        public Entity Head;
        public uint Generation;
        public int Index;
        public byte Detached, Cleared;
        public float Radius;
        public EnemyWaveOwnership Encounter;
    }
    public struct SnakeTailLink : IBufferElementData { public Entity Orb; }
    public struct SnakePathPoint : IBufferElementData { public float3 Position; }
}
