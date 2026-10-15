using Unity.Entities;
using Unity.Mathematics;

namespace CrowdPunch.Components
{
    // Model-space floor anchor for presentation-only squash. Never changes the physical body.
    public struct BossAnimationAnchor : IComponentData
    {
        public float3 Floor;
    }
}
