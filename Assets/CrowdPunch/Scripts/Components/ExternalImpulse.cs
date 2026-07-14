using Unity.Entities;
using Unity.Mathematics;

namespace CrowdPunch.Components
{
    /// <summary>
    /// Pending impulse to apply to an enemy physics body.
    /// </summary>
    public struct ExternalImpulse : IComponentData, IEnableableComponent
    {
        public float3 Value;
    }
}
