using CrowdPunch.Components;
using UnityEngine;

namespace CrowdPunch.Configuration
{
    [CreateAssetMenu(menuName = "Crowd Punch/Snake Settings")]
    public sealed class SnakeSettings : ScriptableObject
    {
        public SnakeExplosionResponse explosionResponse = SnakeExplosionResponse.LaunchAndSever;
        [Range(0, 32)] public int initialTailSegmentCount = 6;
        [Min(.1f), Tooltip("Sphere radius in metres.")] public float orbRadius = .6f;
        [Min(.2f)] public float segmentSpacing = 1.5f;
        [Min(0)] public float waveAmplitude = .5f;
        [Min(0), Tooltip("Oscillation cycles per second.")] public float waveFrequencyHz = .7f;
        [Min(0)] public float pursuitSpeed = 3.1f;
        [Range(0, 1), Tooltip("Fraction of maximum player health, using shared contact protection.")] public float contactDamagePercent = .08f;
        public Color protectedColor = new Color(53f / 255f, 90f / 255f, 150f / 255f, 1).linear;
        public Color vulnerableColor = new Color(1, .65f, .08f, 1).linear;
    }
}
