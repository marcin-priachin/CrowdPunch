using Unity.Entities;

namespace CrowdPunch.Components
{
    public enum RangedAttackPhase : byte
    {
        InitialDelay,
        Ready,
        WindUp,
        Cooldown
    }

    /// <summary>ECS-owned ranged attack lifecycle, retained for development inspection.</summary>
    public struct RangedAttackState : IComponentData
    {
        public RangedAttackPhase Phase;
        public float SecondsRemaining;
        public byte IsAttackEligible;
        public uint ProjectilesSpawned;
        public uint CancelledWindUps;
    }
}
