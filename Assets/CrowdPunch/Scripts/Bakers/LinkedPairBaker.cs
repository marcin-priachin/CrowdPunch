using CrowdPunch.Authoring;
using CrowdPunch.Components;
using Unity.Entities;

namespace CrowdPunch.Bakers
{
    public sealed class LinkedPairBaker : Baker<LinkedPairAuthoring>
    {
        public override void Bake(LinkedPairAuthoring authoring)
        {
            if (authoring.settings == null) return;
            DependsOn(authoring.settings);
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, authoring.settings.Tuning);
            AddComponent<LinkedPairEndpoint>(entity);
        }
    }
}
