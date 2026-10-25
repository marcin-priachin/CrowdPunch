using CrowdPunch.Components;
using UnityEngine;

namespace CrowdPunch.Configuration
{
    [CreateAssetMenu(fileName = "DragonSettings", menuName = "Crowd Punch/Dragon Settings")]
    public sealed class DragonSettings : ScriptableObject
    {
        public DragonEnemyFlameMode enemyMode = DragonEnemyFlameMode.PlayerLaunches;
        public DragonImmunityMode immunity = DragonImmunityMode.OwnFlame;
        public DragonAvoidanceMode avoidance = DragonAvoidanceMode.DamagingFlame;
        [Min(0)] public float warningDuration = .9f, activeDuration = 1.5f, cooldownDuration = 2.5f;
        [Min(0)] public float initialDelay = 2, initialVariation = .8f;
        [Min(0)] public float engagementRange = 16, preferredMinimum = 6, preferredMaximum = 9;
        [Min(0)] public float approachSpeed = 3, retreatSpeed = 4;
        [Min(0), Tooltip("Maximum yaw change in degrees per second.")] public float turnDegreesPerSecond = 65;
        [Min(.01f)] public float hitInterval = .4f;
        [Header("Push is added horizontal velocity (metres/second) per tick")]
        public DragonFlameProfile normal = new DragonFlameProfile { Length = 8, AngleDegrees = 50,
            PlayerDamage = 6, EnemyDamage = 8, PlayerPush = 1.5f, EnemyPush = 1.2f };
        public DragonFlameProfile launched = new DragonFlameProfile { Length = 10, AngleDegrees = 65,
            PlayerDamage = 6, EnemyDamage = 16, PlayerPush = 1.5f, EnemyPush = 1.8f };
        [Tooltip("World-space floor height for XZ flame projection and cover tests.")] public float groundY = -1;
        [HideInInspector] public float idleDuration = 1.5f, moveDuration = .8333334f, castDuration = 1f;
        [Tooltip("Root-local mouth attachment; calibrated by prefab builder from supplied jaw bone.")]
        public Vector3 mouthOffset = new Vector3(0, .6f, .65f);

        public DragonTuning Tuning => new DragonTuning
        {
            EnemyMode = enemyMode, Immunity = immunity, Avoidance = avoidance,
            WarningDuration = Mathf.Max(.01f, warningDuration), ActiveDuration = Mathf.Max(.01f, activeDuration),
            CooldownDuration = Mathf.Max(.01f, cooldownDuration), InitialDelay = Mathf.Max(0, initialDelay), InitialVariation = Mathf.Max(0, initialVariation),
            EngagementRange = Mathf.Max(0, engagementRange), PreferredMinimum = Mathf.Max(0, preferredMinimum),
            PreferredMaximum = Mathf.Max(preferredMinimum, preferredMaximum), ApproachSpeed = Mathf.Max(0, approachSpeed), RetreatSpeed = Mathf.Max(0, retreatSpeed),
            TurnDegreesPerSecond = Mathf.Max(0, turnDegreesPerSecond), HitInterval = Mathf.Max(.01f, hitInterval),
            Normal = Sanitize(normal), Launched = Sanitize(launched), MouthOffset = mouthOffset, GroundY = groundY,
            IdleDuration = idleDuration, MoveDuration = moveDuration, CastDuration = castDuration
        };

        private static DragonFlameProfile Sanitize(DragonFlameProfile p) => new DragonFlameProfile
        {
            Length = Mathf.Max(0, p.Length), AngleDegrees = Mathf.Clamp(p.AngleDegrees, 1, 170),
            PlayerDamage = Mathf.Max(0, p.PlayerDamage), EnemyDamage = Mathf.Max(0, p.EnemyDamage),
            PlayerPush = Mathf.Max(0, p.PlayerPush), EnemyPush = Mathf.Max(0, p.EnemyPush)
        };
    }
}
