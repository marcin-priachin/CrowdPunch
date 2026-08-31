using Unity.Entities;
using Unity.Mathematics;

namespace CrowdPunch.Components
{
    /// <summary>
    /// World-space area used for enemy spacing, distribution, containment, and edge respawn.
    /// </summary>
    public struct ArenaBounds : IComponentData
    {
        public float3 Center;
        public float3 Extents;
    }

    /// <summary>World-space volume outside which enemies are defeated or pooled.</summary>
    public struct EnemyDefeatBounds : IComponentData
    {
        public float3 Center;
        public float3 Extents;
    }
}
