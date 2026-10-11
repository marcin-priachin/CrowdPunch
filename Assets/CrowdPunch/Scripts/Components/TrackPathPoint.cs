using Unity.Entities;
using Unity.Mathematics;

namespace CrowdPunch.Components
{
    [InternalBufferCapacity(4)]
    public struct TrackPathPoint : IBufferElementData
    {
        public float3 Position;
        public float Distance;
    }
}
