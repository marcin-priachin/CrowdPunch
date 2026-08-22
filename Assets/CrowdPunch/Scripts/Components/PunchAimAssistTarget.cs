using Unity.Entities;

namespace CrowdPunch.Components
{
    /// <summary>Persistent aim-assist target for one enemy currently inside the player punch volume.</summary>
    public struct PunchAimAssistTarget : IComponentData
    {
        public Entity Target;
        public byte IsAiming;
    }
}
