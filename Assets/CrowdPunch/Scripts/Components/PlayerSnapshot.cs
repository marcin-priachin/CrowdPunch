using Unity.Entities;
using Unity.Mathematics;

namespace CrowdPunch.Components
{
    /// <summary>
    /// ECS-readable snapshot of the MonoBehaviour player state.
    /// </summary>
    public struct PlayerSnapshot : IComponentData
    {
        public float3 Position;
        public float3 Forward;
        public float Radius;
        public uint CollisionLayer;
        public bool IsAvailable;
    }
}
