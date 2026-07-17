using Unity.Entities;
using Unity.Mathematics;

namespace CrowdPunch.Components
{
    /// <summary>
    /// ECS-readable snapshot of the MonoBehaviour-owned player health.
    /// </summary>
    public struct PlayerHealthSnapshot : IComponentData
    {
        public float Current;
        public float Max;
        public bool IsAvailable;

        public readonly float Normalized => Max <= 0f ? 0f : math.saturate(Current / Max);
    }
}
