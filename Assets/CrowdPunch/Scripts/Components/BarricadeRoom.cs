using Unity.Entities;
using Unity.Mathematics;

namespace CrowdPunch.Components
{
    public struct BarricadeRoom : IComponentData
    {
        public Entity PreviousGate;
        public float2 Minimum, Maximum;
        public byte Activated;
    }
}
