using CrowdPunch.Authoring;
using CrowdPunch.Components;
using Unity.Entities;

namespace CrowdPunch.Bakers
{
    public sealed class ShellFragmentBaker : Baker<ShellFragmentAuthoring>
    {
        public override void Bake(ShellFragmentAuthoring authoring)
        {
            if (GetComponent<BarricadeVisualAuthoring>().barricade == null) return;
            AddComponent(GetEntity(TransformUsageFlags.Dynamic), new ShellFragmentMotion
            {
                Kind = (byte)authoring.kind, Velocity = authoring.velocity, Spin = authoring.spin,
                Duration = authoring.duration, RestingY = authoring.restingY, Seed = authoring.seed
            });
        }
    }
}
