using Unity.Entities;
using Unity.Mathematics;

namespace CrowdPunch.Components
{
    // INFO-006: a reserved enemy stays Disabled (including prefab children) until activation.
    public struct EnemySpawnWarning : IComponentData
    {
        public const float Duration = 1f;
        public float SecondsRemaining;
        public float Radius;
        public float3 Position;
    }
}
