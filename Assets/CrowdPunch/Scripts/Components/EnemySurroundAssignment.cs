using Unity.Entities;
using Unity.Mathematics;

namespace CrowdPunch.Components
{
    /// <summary>Exclusive player-relative surround position; contact commitment remains separately owned.</summary>
    public struct EnemySurroundAssignment : IComponentData
    {
        public float2 Offset;
        public int Slot;
        public byte Assigned;
    }
}
