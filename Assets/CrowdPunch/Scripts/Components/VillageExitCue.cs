using Unity.Entities;
using Unity.Mathematics;

namespace CrowdPunch.Components
{
    public struct VillageExitCue : IComponentData
    {
        public Entity Gate;
        public float4 LockedColor, OpenColor;
        public float3 Position;
        public float Scale, Phase;
        public byte Arrow, Initialized;
    }
}
