using CrowdPunch.Authoring;
using CrowdPunch.Components;
using Unity.Entities;

namespace CrowdPunch.Bakers
{
    public sealed class CoverPanelBaker : Baker<CoverPanelAuthoring>
    {
        public override void Bake(CoverPanelAuthoring a)
        {
            var e = GetEntity(TransformUsageFlags.Dynamic | TransformUsageFlags.NonUniformScale);
            AddComponent(e, new CoverPanel { Cover = GetEntity(a.cover, TransformUsageFlags.Dynamic), Index = a.index });
        }
    }
}
