using Unity.Entities;
using Unity.Mathematics;

namespace CrowdPunch.Components
{
    public struct EnemyShadowAnchor : IComponentData
    {
        public Entity Root;
        public int Bone;
        public float3 BindPosition;
    }
}
