using Unity.Entities;

namespace CrowdPunch.Components
{
    public enum RangedPositioningMode : byte
    {
        Hold,
        Approach,
        Retreat
    }

    /// <summary>Development-facing ranged positioning decision.</summary>
    public struct RangedPositioningState : IComponentData
    {
        public RangedPositioningMode Mode;
    }
}
