using CrowdPunch.Authoring;
using CrowdPunch.Components;
using Unity.Entities;

namespace CrowdPunch.Bakers
{
    public sealed class GroundHazardPolicyBaker : Baker<GroundHazardPolicyAuthoring>
    {
        public override void Bake(GroundHazardPolicyAuthoring a)
        {
            if (a.settings != null) DependsOn(a.settings);
            AddComponent(GetEntity(TransformUsageFlags.None), new GroundHazardPolicy
            {
                Avoidance = a.settings == null ? GroundHazardAvoidance.ActiveOnly : a.settings.avoidance,
                Fallback = a.settings == null ? GroundHazardFallback.WaitSafely : a.settings.noSafeRoute
            });
        }
    }
}
