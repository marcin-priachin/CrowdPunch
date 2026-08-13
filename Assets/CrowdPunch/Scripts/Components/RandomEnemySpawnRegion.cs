using Unity.Entities;
using Unity.Mathematics;

namespace CrowdPunch.Components
{
    /// <summary>Legacy random spawn region retained by each enemy for full restart.</summary>
    public struct RandomEnemySpawnRegion : IComponentData
    {
        public float3 Center;
        public float Radius;
    }
}
