using Unity.Entities;
using Unity.Mathematics;

namespace CrowdPunch.Components
{
    // Presentation-only circular floor; never changes body grounding or collision.
    public struct TrajectoryPreviewSurface : IComponentData
    {
        public float2 Center;
        public float Radius;
        public float Height;
    }
}
