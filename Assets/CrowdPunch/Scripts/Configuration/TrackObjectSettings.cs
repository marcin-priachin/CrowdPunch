using UnityEngine;

namespace CrowdPunch.Configuration
{
    [CreateAssetMenu(menuName = "Crowd Punch/Track Object Settings")]
    public sealed class TrackObjectSettings : ScriptableObject
    {
        [Min(1)] public int requiredNetHits = 5;
        [Min(.05f)] public float slideDuration = .35f;
        public bool playerLaunchedBodiesOnly;
        [Tooltip("Provisional edge-case option: also reject blasts from currently non-player-owned launches.")]
        public bool filterLaunchedExplosions;
        [Tooltip("Provisional edge-case option: perpendicular and endpoint-clamped contacts spend launch eligibility.")]
        public bool consumeNonMovingHits;
        public bool damagingPush;
        public bool pushDamagesPlayer = true;
        [Min(0)] public float pushDamage = 1;
    }
}
