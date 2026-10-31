using Unity.Entities;
using Unity.Mathematics;

namespace CrowdPunch.Components
{
    /// <summary>Persistent aim-assist target for one enemy currently inside the player punch volume.</summary>
    public struct PunchAimAssistTarget : IComponentData
    {
        public Entity Target;
        public Entity Challenger;
        public double ChallengerSince;
        public float LaunchSpeed, LeadSeconds, LeadMaximumDistance;
        public float3 Facing;
        public float DirectionLimitDegrees;
        public float3 PreviewDirection;
        public Entity PreviewTarget;
        public uint PreviewRevision;
        public byte IsAiming;
    }
}
