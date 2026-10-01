using Unity.Entities;
using Unity.Mathematics;

namespace CrowdPunch.Components
{
    public enum WizardZoneKind : byte { Cast, Impact }

    public struct WizardZone : IComponentData
    {
        public Entity Source;
        public Entity SceneOwner;
        public Entity Sequence;
        public uint RunGeneration;
        public int WaveIndex;
        public float3 Position;
        public WizardSettings Settings;
        public WizardZoneKind Kind;
        public double ExpiresAt;
        public byte Follow, Active;
    }

    [InternalBufferCapacity(0)]
    public struct WizardZoneTarget : IBufferElementData
    {
        // Entity.Null denotes the hybrid player. No entity crosses the Mono bridge.
        public Entity Target;
        public double NextHitAt;
        public double PlayerProtectedUntil;
        public byte Seen;
    }
}
