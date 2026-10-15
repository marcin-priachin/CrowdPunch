using Unity.Entities;
using Unity.Mathematics;

namespace CrowdPunch.Components
{
    public struct BossHandVisualPose : IComponentData
    {
        public float4x4 AuthoredWorld, LastOutput;
        public byte Initialized;
    }
}
