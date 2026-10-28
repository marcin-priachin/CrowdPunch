using Unity.Entities;
using Unity.Mathematics;

namespace CrowdPunch.Components
{
    public enum EnemyCrowdRole : byte { Reserve, Pursuit, Interception }

    /// <summary>Stable tactical orders. Launch/recovery and pooling invalidate these orders.</summary>
    public struct EnemyCrowdAssignment : IComponentData
    {
        public EnemyCrowdRole Role;
        public float3 Destination;
        public float3 InterceptionDestination;
        public double InterceptUntil;
        public byte HasFlankWaypoint;
        public byte Initialized;
    }
}
