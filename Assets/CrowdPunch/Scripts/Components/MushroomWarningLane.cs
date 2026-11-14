using Unity.Entities;
using Unity.Mathematics;

namespace CrowdPunch.Components
{
    public struct MushroomWarningLane : IComponentData
    {
        public float3 Origin, Direction;
        public float GroundHeight, Length;
        public bool Locked;
    }
}
