using CrowdPunch.Authoring;
using CrowdPunch.Components;
using Unity.Entities;
using Unity.Mathematics;

namespace CrowdPunch.Bakers
{
    public sealed class ProtectedPointBaker : Baker<ProtectedPointAuthoring>
    {
        public override void Bake(ProtectedPointAuthoring authoring)
        {
            if (authoring.settings == null) throw new System.InvalidOperationException("Protected point requires settings.");
            DependsOn(authoring.settings);
            var entity = GetEntity(TransformUsageFlags.None);
            AddComponent(entity, new ProtectedPoint {
                Center = authoring.zoneCenter, HalfSize = math.max((float2)authoring.zoneSize, .1f) * .5f,
                BreachThreshold = math.max(1, authoring.settings.breachThreshold),
                MaximumPlayerAttackers = math.max(0, authoring.settings.maximumPlayerAttackers)
            });
            AddBuffer<ProtectedPointAttacker>(entity);
        }
    }
}
