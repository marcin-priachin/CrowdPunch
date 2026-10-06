using Unity.Entities;
using Unity.Mathematics;

namespace CrowdPunch.Components
{
    public struct TrailEmitter : IComponentData
    {
        public Entity Record;
        public float3 PreviousPosition, Anchor;
        public byte Initialized, Emitting, Launched;
        public float ReverseRemaining, OrbitSign;
    }
}
