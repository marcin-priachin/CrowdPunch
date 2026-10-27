using Unity.Entities;
using Unity.Mathematics;

namespace CrowdPunch.Components
{
    // The marker stays in bind pose; sampled skinning moves the actual wand.
    public struct RangedProjectileOrigin : IComponentData
    {
        public Entity Animation;
        public Entity Root;
        public float3 BindPosition;
        public float4x4 ModelToOwner;
        public float ChargeRadius;
    }
}
