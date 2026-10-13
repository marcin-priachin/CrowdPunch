using CrowdPunch.Components;
using UnityEngine;

namespace CrowdPunch.Configuration
{
    [CreateAssetMenu(fileName = "NinjaSettings", menuName = "Crowd Punch/Ninja Settings")]
    public sealed class NinjaSettings : ScriptableObject
    {
        [Header("Scheduling")]
        public NinjaTriggerMode triggerMode = NinjaTriggerMode.Both;
        [Min(0)] public float initialDelay = 3, minimumCooldown = 6, periodicInterval = 10, periodicVariation = 2;
        [Min(0)] public float proximityDistance = 6, engagementDistance = 20;
        [Min(1)] public int maximumConcurrentAmbushes = 2;
        public Vector2 slotRetry = new Vector2(.15f, .35f), destinationRetry = new Vector2(.35f, .65f);
        [Header("Positioning")]
        public PlayerMovementSettings playerMovement;
        [Min(0)] public float preferredMinimumDistance = 12, preferredMaximumDistance = 18;
        [Min(0)] public float retreatSpeed = 6, approachSpeed = 4;
        public bool matchPlayerRetreatSpeed = true;
        [Header("Teleport")]
        [Min(0)] public float preparationDuration = .75f, teleportDistance = 2, arrivalWarningDuration = .2f;
        [Range(0, 90)] public float rearArcDegrees = 60;
        [Range(1, 16)] public int rearSearchSteps = 6;
        [Header("Committed dash")]
        [Min(0)] public float dashSpeed = 48, maximumDashDistance = 5, recoveryDuration = 1.6f;
        [Min(0)] public float playerDamage = 20, playerKnockback = 18, playerInvulnerability = .5f;

        public NinjaTuning Tuning => new NinjaTuning
        {
            TriggerMode = triggerMode, InitialDelay = initialDelay, MinimumCooldown = minimumCooldown,
            PeriodicInterval = periodicInterval, PeriodicVariation = periodicVariation,
            ProximityDistance = proximityDistance, EngagementDistance = engagementDistance,
            MaximumConcurrentAmbushes = maximumConcurrentAmbushes,
            SlotRetryMinimum = slotRetry.x, SlotRetryMaximum = slotRetry.y,
            DestinationRetryMinimum = destinationRetry.x, DestinationRetryMaximum = destinationRetry.y,
            PreferredMinimumDistance = preferredMinimumDistance, PreferredMaximumDistance = preferredMaximumDistance,
            RetreatSpeed = matchPlayerRetreatSpeed && playerMovement != null ? playerMovement.MoveSpeed : retreatSpeed,
            ApproachSpeed = approachSpeed, PreparationDuration = preparationDuration, TeleportDistance = teleportDistance,
            ArrivalWarningDuration = arrivalWarningDuration, RearArcDegrees = rearArcDegrees, RearSearchSteps = rearSearchSteps,
            DashSpeed = dashSpeed, MaximumDashDistance = maximumDashDistance, RecoveryDuration = recoveryDuration,
            PlayerDamage = playerDamage, PlayerKnockback = playerKnockback, PlayerInvulnerability = playerInvulnerability
        };
    }
}
