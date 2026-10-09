using Unity.Entities;
using Unity.Mathematics;

namespace CrowdPunch.Components
{
    public struct GroundHazardRoute : IComponentData
    {
        public float2 Goal, LastPosition;
        public double RetryAt, ProgressAt;
        public uint Version, Revision;
        public int Waypoint;
        public byte Pending, Failed;
    }
    [InternalBufferCapacity(0)]
    public struct GroundHazardWaypoint : IBufferElementData { public float2 Position; }
}
