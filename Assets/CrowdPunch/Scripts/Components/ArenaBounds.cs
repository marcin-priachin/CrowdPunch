using Unity.Entities;
using Unity.Mathematics;

namespace CrowdPunch.Components
{
    /// <summary>
    /// World-space play area used by lifetime and spawn systems.
    /// </summary>
    public struct ArenaBounds : IComponentData
    {
        public float3 Center;
        public float3 Extents;
    }
}
