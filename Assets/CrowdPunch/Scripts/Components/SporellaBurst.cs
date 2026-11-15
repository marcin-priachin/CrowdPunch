using Unity.Entities;
using Unity.Mathematics;

namespace CrowdPunch.Components
{
    public struct SporellaSweep
    {
        public float3 Origin;
        public float3 Forward;
        public float AimDistance;
        public float HalfWidth;
        public int ShotsPerPass;
        public int Passes;

        public int ShotCount => ShotsPerPass * Passes;
    }

    public struct SporellaNormalBurst : IComponentData
    {
        public SporellaSweep Sweep;
        public float Wait;
        public int ShotsRemaining;
    }

    // Independent of the originating enemy once the first shot has fired.
    public struct SporellaImpactBurst : IComponentData
    {
        public SporellaProjectile Shot;
        public SporellaSweep Sweep;
        public int ShotsRemaining;
        public float Interval;
        public float Wait;
    }

    [InternalBufferCapacity(1)]
    public struct SporellaImpactContact : IBufferElementData
    {
        public float3 Position;
        public float3 Direction;
        public uint PunchSequence;
        public int ChainDepth;
    }

    [InternalBufferCapacity(4)]
    public struct SporellaImpactHistory : IBufferElementData
    {
        public uint PunchSequence;
        public Entity Source;
        public uint SourceLifetime;
        public uint LaunchSequence;
    }
}
