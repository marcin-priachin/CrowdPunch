using CrowdPunch.Authoring;
using CrowdPunch.Components;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;

namespace CrowdPunch.Bakers
{
    public sealed class RangedProjectileBaker : Baker<RangedProjectileAuthoring>
    {
        public override void Bake(RangedProjectileAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent<RangedProjectile>(entity);
            AddComponent(entity, new URPMaterialPropertyBaseColor
            {
                Value = new float4(1f, 0.8f, 0.1f, 1f)
            });
        }
    }
}
