using Unity.Entities;

namespace CrowdPunch.Components
{
    /// <summary>
    /// Enemy touch damage tuning used when an ECS enemy reaches the GameObject player.
    /// </summary>
    public struct EnemyContactDamageSettings : IComponentData
    {
        public float DamagePercent;
        public float PushStrength;
        public float PlayerInvincibilitySeconds;
        public float ContactRadius;
        public float AttemptDistance;
        public float AttemptIntervalMin;
        public float AttemptIntervalMax;
        public float AttemptDuration;
        public float AttemptSpeedMultiplier;
        public float AttemptSeparationWeight;
    }

    /// <summary>Per-enemy cadence for brief attempts to leave the surround ring and contact the player.</summary>
    public struct EnemyContactAttemptState : IComponentData
    {
        public float SecondsRemaining;
        public uint Sequence;
        public byte IsAttempting;
    }
}
