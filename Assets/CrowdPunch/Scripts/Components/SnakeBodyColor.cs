using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;

namespace CrowdPunch.Components
{
    [MaterialProperty("_SkinColorAmount")]
    public struct SnakeBodyTintAmount : IComponentData
    {
        public float Value;
    }

    [MaterialProperty("_SkinColor")]
    public struct SnakeBodyColor : IComponentData
    {
        public float4 Value;
    }
}
