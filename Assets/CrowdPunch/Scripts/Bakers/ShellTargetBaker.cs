using CrowdPunch.Authoring;
using CrowdPunch.Components;
using Unity.Entities;
using Unity.Mathematics;

namespace CrowdPunch.Bakers
{
    public sealed class ShellTargetBaker : Baker<ShellTargetAuthoring>
    {
        public override void Bake(ShellTargetAuthoring a)
        {
            if (a.settings == null) throw new System.InvalidOperationException("Shell target requires settings.");
            DependsOn(a.settings);
            AddComponent(GetEntity(TransformUsageFlags.Dynamic), new ShellTarget {
                RequiredExplosions = math.max(1, a.settings.requiredExplosions),
                ExplosionsRemaining = math.max(1, a.settings.requiredExplosions),
                CoreHealth = math.max(.01f, a.settings.coreMaxHealth),
                CoreMaxHealth = math.max(.01f, a.settings.coreMaxHealth),
                ExploderReplacementDelay = math.max(0, a.settings.exploderReplacementDelay),
                ReplaceExplodersAt = -1
            });
        }
    }
}
