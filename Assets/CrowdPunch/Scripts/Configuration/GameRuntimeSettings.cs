using UnityEngine;
using UnityEngine.Serialization;
using CrowdPunch.Components;

namespace CrowdPunch.Configuration
{
    /// <summary>Reusable match bootstrap and enemy-launch tuning.</summary>
    [CreateAssetMenu(fileName = "GameRuntimeSettings", menuName = "Crowd Punch/Game Runtime Settings")]
    public sealed class GameRuntimeSettings : ScriptableObject
    {
        [SerializeField] private bool startRunning = true;
        [Header("Campaign")]
        [Tooltip("Allow selection of all available campaign levels from startup without marking them complete or changing saved progress.")]
        [SerializeField] private bool startWithAllLevelsUnlocked;
        [Header("Baseline Crowd Tactics (Provisional)")]
        [Tooltip("Regional engages nearby groups; Whole Arena involves every eligible Baseline. Settings are baked: restart/rebake after editing.")]
        [SerializeField] private CrowdParticipation crowdParticipation = CrowdParticipation.Regional;
        [SerializeField, Range(0f, 1f)] private float pursuitShare = 0.6f;
        [Tooltip("Regional participation radius in metres. The closest participating Baselines form the pursuit group.")]
        [SerializeField, Min(0f)] private float crowdEngagementRadius = 45f;
        [Tooltip("Extra distance engaged enemies may travel before returning to reserve at the next plan.")]
        [SerializeField, Min(0f)] private float crowdDisengagementMargin = 10f;
        [SerializeField, Min(0f)] private float interceptionPredictionSeconds = 1.25f;
        [Tooltip("Caps prediction from fast movement, including dashes, in metres.")]
        [SerializeField, Min(0f)] private float maximumInterceptionPredictionDistance = 16f;
        [SerializeField, Min(0f)] private float interceptionFlankDistance = 10f;
        [Tooltip("Seconds to commit to fixed flank/interception destinations before closing in. No continuous retargeting.")]
        [SerializeField, Min(0.1f)] private float interceptionCommitmentSeconds = 4f;
        [Tooltip("Minimum closing-in period before the next crowd plan; opened gaps are not immediately refilled.")]
        [SerializeField, Min(0.1f)] private float interceptionCloseInSeconds = 2f;
        [SerializeField, Min(0f)] private float crowdGroupRadius = 4f;
        [Tooltip("Number of concentrated reserve areas across each arena/room.")]
        [SerializeField, Min(1)] private int reserveGroupCount = 6;
        [Tooltip("Smaller populations pursue together instead of producing ineffective interception groups.")]
        [SerializeField, Min(1)] private int minimumInterceptionGroupSize = 4;
        [Header("Explosive Pressure (Provisional)")]
        [FormerlySerializedAs("maximumApproachingEnemies")]
        [Tooltip("Existing Explosive pressure limit outside contact range. Baselines do not use this cap.")]
        [SerializeField, Min(0)] private int maximumApproachingExplosives = 8;
        [Header("Enemy Launch (Provisional)")]
        [Tooltip("Minimum solver-estimated contact impulse required for a launched enemy to launch another enemy.")]
        [SerializeField, Min(0f)] private float minimumPropagationImpulse = 1.5f;
        [Tooltip("Radius around a newly propagated enemy searched for the smallest-angle follow-up target. Set to 0 to preserve its solver-produced direction.")]
        [SerializeField, Min(0f)] private float propagationAimCorrectionRadius = 8f;
        [Tooltip("Maximum horizontal homing turn rate, in degrees per second, while a launched enemy has an aim-assist or propagation target. Set to 0 to disable homing.")]
        [SerializeField, Min(0f)] private float launchHomingDegreesPerSecond = 30f;
        [Tooltip("Minimum solver-estimated contact impulse required for a launched enemy to damage another enemy.")]
        [SerializeField, Min(0f)] private float minimumDamageImpulse = 2f;
        [Tooltip("Fraction of the originating punch damage dealt at the minimum damaging impulse.")]
        [SerializeField, Min(0f)] private float baseCollisionDamageMultiplier = 0.25f;
        [SerializeField, Min(0f)] private float damageMultiplierPerExcessImpulse = 0.05f;
        [SerializeField, Min(0f)] private float maximumCollisionDamageMultiplier = 0.75f;
        [Tooltip("Fraction of launched-body damage prevented when the current launch is player-owned, including propagated chains. 0 = full damage, 1 = no damage. Elite and boss launches are unaffected.")]
        [SerializeField, Range(0f, 1f)] private float playerOwnedLaunchDamageReduction = 0.5f;
        [Tooltip("Maximum health points dealt to the player by one launched-body impact, after the player-owned damage reduction. Applies to every launch owner; does not limit enemy damage or explosion splash. 0 prevents launched-body damage to the player.")]
        [SerializeField, Min(0f)] private float maximumPlayerImpactDamage = 25f;
        [SerializeField, Min(0f)] private float usefulMomentumSpeed = 2f;
        [SerializeField, Min(0f)] private float lowMomentumPeriod = 0.25f;
        [SerializeField, Min(0f)] private float recoveryDuration = 0.6f;

        public bool StartRunning => startRunning;
        public bool StartWithAllLevelsUnlocked => startWithAllLevelsUnlocked;
        public EnemyCrowdPressureSettings CrowdPressure => new EnemyCrowdPressureSettings
        {
            Participation = crowdParticipation,
            PursuitShare = Mathf.Clamp01(pursuitShare),
            EngagementRadius = Mathf.Max(0f, crowdEngagementRadius),
            DisengagementMargin = Mathf.Max(0f, crowdDisengagementMargin),
            PredictionSeconds = Mathf.Max(0f, interceptionPredictionSeconds),
            MaximumPredictionDistance = Mathf.Max(0f, maximumInterceptionPredictionDistance),
            FlankDistance = Mathf.Max(0f, interceptionFlankDistance),
            CommitmentSeconds = Mathf.Max(0.1f, interceptionCommitmentSeconds),
            CloseInSeconds = Mathf.Max(0.1f, interceptionCloseInSeconds),
            GroupRadius = Mathf.Max(0f, crowdGroupRadius),
            ReserveGroupCount = Mathf.Clamp(reserveGroupCount, 1, 32),
            MinimumInterceptionGroupSize = Mathf.Max(1, minimumInterceptionGroupSize),
            MaximumApproachingExplosives = Mathf.Max(0, maximumApproachingExplosives)
        };
        public float MinimumPropagationImpulse => minimumPropagationImpulse;
        public float PropagationAimCorrectionRadius => propagationAimCorrectionRadius;
        public float LaunchHomingDegreesPerSecond => launchHomingDegreesPerSecond;
        public float MinimumDamageImpulse => minimumDamageImpulse;
        public float BaseCollisionDamageMultiplier => baseCollisionDamageMultiplier;
        public float DamageMultiplierPerExcessImpulse => damageMultiplierPerExcessImpulse;
        public float MaximumCollisionDamageMultiplier => maximumCollisionDamageMultiplier;
        public float PlayerOwnedLaunchDamageReduction => Mathf.Clamp01(playerOwnedLaunchDamageReduction);
        public float MaximumPlayerImpactDamage => Mathf.Max(0f, maximumPlayerImpactDamage);
        public float UsefulMomentumSpeed => usefulMomentumSpeed;
        public float LowMomentumPeriod => lowMomentumPeriod;
        public float RecoveryDuration => recoveryDuration;
    }
}
