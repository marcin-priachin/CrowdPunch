using Unity.Entities;
using Unity.Mathematics;

namespace CrowdPunch.Components
{
    /// <summary>One baked authored enemy request at an exact world-space position.</summary>
    public struct AuthoredEnemySpawnPoint : IComponentData
    {
        public EnemySpawnProfile Profile;
        public float3 Position;
        public uint RandomSeed;
    }
}
