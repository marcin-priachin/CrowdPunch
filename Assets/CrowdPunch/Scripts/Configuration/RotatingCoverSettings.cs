using CrowdPunch.Components;
using UnityEngine;

namespace CrowdPunch.Configuration
{
    [CreateAssetMenu(menuName = "Crowd Punch/Rotating Cover Settings")]
    public sealed class RotatingCoverSettings : ScriptableObject
    {
        [Tooltip("Asset changes take effect after rebaking/reloading the level.")]
        [Range(30, 160)] public float openingDegrees = 75;
        [Min(3)] public float radius = 5;
        [Min(2)] public float height = 4;
        [Range(.1f, 1)] public float thickness = .45f;
        public float initialOpeningDegrees = 180;
        public CoverRotationMode rotationMode;
        [Min(0)] public float degreesPerSecond = 35;
        [Min(.1f)] public float rotateSeconds = 3;
        [Min(0)] public float pauseSeconds = 1;
        [Min(.1f)] public float reverseSeconds = 5;
        public CoverHitResponse hitResponse;
        [Min(0)] public float hitPauseSeconds = 1;
        [Min(0)] public float speedIncreasePerHit = .2f;
        [Min(0)] public float reflectionSpeedMultiplier = 1;
    }
}
