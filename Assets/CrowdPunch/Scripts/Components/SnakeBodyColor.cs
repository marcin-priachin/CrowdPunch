using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;

namespace CrowdPunch.Components
{
    [MaterialProperty("_SkinColor")]
    public struct SnakeBodyColor : IComponentData
    {
        public float4 Value;
    }
}
