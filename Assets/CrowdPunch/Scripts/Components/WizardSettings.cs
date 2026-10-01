using Unity.Entities;

namespace CrowdPunch.Components
{
    public enum WizardMovementMode : byte { StopThroughout, StopTelegraph, KeepMoving }
    public enum WizardForceMode : byte { DamageOnly, SmallPush, StrongKnockback }

    [System.Serializable]
    public struct WizardSettings : IComponentData
    {
        public float Cooldown, CheckInterval, EngagementRange, TelegraphDuration, ActiveDuration, Radius;
        public float BaseChance, ProximityBonus, ApproachBonus, FullApproachSpeed;
        public float PreferredMinimum, PreferredMaximum, ApproachSpeed, RetreatSpeed, Acceleration, Braking, TurnSpeed;
        public float PlayerDamage, EnemyDamage, TickInterval, PlayerInvulnerability;
        public float EnemyPush, EnemyKnockback, PlayerPush, PlayerKnockback;
        public WizardMovementMode MovementMode;
        public WizardForceMode ForceMode;
        public bool AlwaysReserveRadius;

        public static WizardSettings Default => new WizardSettings
        {
            Cooldown = 2, CheckInterval = .5f, EngagementRange = 12, TelegraphDuration = 1,
            ActiveDuration = 3, Radius = 4, BaseChance = .15f, ProximityBonus = .35f,
            ApproachBonus = .35f, FullApproachSpeed = 4, PreferredMinimum = 6, PreferredMaximum = 9,
            ApproachSpeed = 3, RetreatSpeed = 4, Acceleration = 10, Braking = 10, TurnSpeed = 10,
            PlayerDamage = 10, EnemyDamage = 8, TickInterval = .5f, PlayerInvulnerability = .5f,
            EnemyPush = 3, EnemyKnockback = 12, PlayerPush = 2, PlayerKnockback = 8,
            ForceMode = WizardForceMode.SmallPush
        };
    }
}
