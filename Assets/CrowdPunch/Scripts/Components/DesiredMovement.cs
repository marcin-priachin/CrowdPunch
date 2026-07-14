using Unity.Entities;
using Unity.Mathematics;

namespace CrowdPunch.Components
{
    /// <summary>
    /// Movement intent produced by AI and consumed by movement or physics systems.
    /// </summary>
    public struct DesiredMovement : IComponentData
    {
        public float3 Direction;
        public float Speed;
    }
}
