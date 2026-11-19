using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace CrowdPunch.Components
{
    public struct EnemyShadowFeet : IComponentData
    {
        public Entity Root;
        public FixedList128Bytes<ShadowFoot> Feet;
    }

    public struct ShadowFoot
    {
        public int Bone;
        public float3 BindPosition;
    }
}
