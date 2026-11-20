using Unity.Entities;
using Unity.Rendering;

namespace CrowdPunch.Components
{
    [MaterialProperty("_ArenaDepthRole")]
    public struct ArenaDepthRole : IComponentData
    {
        public const float Rock = 1f;
        public const float Background = 2f;
        public float Value;
    }

    [BakingType]
    public struct ArenaDepthRoleBakingData : IComponentData
    {
        public ArenaDepthRole Role;
    }
}
