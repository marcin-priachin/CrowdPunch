using CrowdPunch.Authoring;
using CrowdPunch.Components;
using Unity.Entities;

namespace CrowdPunch.Bakers
{
    public sealed class GroundHazardAppearanceBaker : Baker<GroundHazardAppearanceAuthoring>
    {
        public override void Bake(GroundHazardAppearanceAuthoring authoring)
            => AddComponent(GetEntity(TransformUsageFlags.None), new GroundHazardAppearance { Style = authoring.style });
    }
}
