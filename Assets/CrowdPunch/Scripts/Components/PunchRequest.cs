using Unity.Entities;
using Unity.Mathematics;

namespace CrowdPunch.Components
{
    /// <summary>
    /// One-frame request written by MonoBehaviour player code and consumed by ECS combat systems.
    /// </summary>
    public struct PunchRequest : IComponentData, IEnableableComponent
    {
        public float3 Origin;
        public float3 Direction;
        public float Radius;
        public float Range;
        public float Strength;
        public float PushDirectionPositionWeight;
        public uint Sequence;
    }
}
