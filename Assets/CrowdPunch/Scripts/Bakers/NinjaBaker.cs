using CrowdPunch.Authoring;
using CrowdPunch.Components;
using Unity.Entities;

namespace CrowdPunch.Bakers
{
    public sealed class NinjaBaker : Baker<NinjaAuthoring>
    {
        public override void Bake(NinjaAuthoring authoring)
        {
            if (authoring.settings == null) return;
            DependsOn(authoring.settings);
            if (authoring.settings.playerMovement != null) DependsOn(authoring.settings.playerMovement);
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, authoring.settings.Tuning);
            AddComponent<NinjaState>(entity);
            AddComponent<NinjaPresentation>(entity);
        }
    }
}
