using Unity.Entities;
using Unity.Mathematics;

namespace CrowdPunch.Components
{
    /// <summary>Exact authored layout position restored only by a full game restart.</summary>
    public struct AuthoredEnemyInitialPosition : IComponentData
    {
        public float3 Value;
    }
}
