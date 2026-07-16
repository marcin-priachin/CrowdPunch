using Unity.Entities;
using Unity.Mathematics;

namespace CrowdPunch.Components
{
    /// <summary>
    /// Current evenly distributed destination used while an enemy is wandering.
    /// </summary>
    public struct WanderDestination : IComponentData
    {
        public float3 Position;
        public int SequenceIndex;
        public byte IsAssigned;
    }
}
