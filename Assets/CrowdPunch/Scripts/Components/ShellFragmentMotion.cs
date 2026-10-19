using Unity.Entities;
using Unity.Mathematics;

namespace CrowdPunch.Components
{
    // SHELL-006: optional presentation for authored metal panels and brief break accents.
    public struct ShellFragmentMotion : IComponentData
    {
        public float3 Position, Velocity, Spin;
        public quaternion Rotation;
        public float Scale, Duration, RestingY, Seed;
        public byte Kind, Initialized;
    }
}
