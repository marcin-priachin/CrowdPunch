using Unity.Entities;
using Unity.Mathematics;

namespace CrowdPunch.Components
{
    /// <summary>
    /// Current and maximum health for ECS-owned simulation entities.
    /// </summary>
    public struct Health : IComponentData
    {
        public float Current;
        public float Max;

        public readonly float Normalized => Max <= 0f ? 0f : math.saturate(Current / Max);
    }
}
