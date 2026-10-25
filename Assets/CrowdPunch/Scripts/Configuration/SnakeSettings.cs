using CrowdPunch.Components;
using UnityEngine;

namespace CrowdPunch.Configuration
{
    [CreateAssetMenu(menuName = "Crowd Punch/Snake Settings")]
    public sealed class SnakeSettings : ScriptableObject
    {
        public SnakeExplosionResponse explosionResponse = SnakeExplosionResponse.LaunchAndSever;
        [Range(0, 32)] public int initialTailSegmentCount = 6;
        [Min(.1f), InspectorName("Segment Radius"), Tooltip("Segment size: spherical collider radius in world metres. Uniformly scales the complete Slime segment model with its physics body.")] public float orbRadius = .6f;
        [Min(.2f), Tooltip("Centre-to-centre distance between attached segments in world metres.")] public float segmentSpacing = 1.5f;
        [Tooltip("Attached segment centre Y relative to the head physics origin, in world metres. Negative values lower the tail. Detached segments retain their release height.")] public float segmentHeightOffset = -.4f;
        [Min(0)] public float waveAmplitude = .5f;
        [Min(0), Tooltip("Oscillation cycles per second.")] public float waveFrequencyHz = .7f;
        [Min(0)] public float pursuitSpeed = 3.1f;
        [Range(0, 1), Tooltip("Fraction of maximum player health, using shared contact protection.")] public float contactDamagePercent = .08f;
        public Color protectedColor = new Color(.2f, .6f, 0f, 1);
        public Color vulnerableColor = new Color(1, .65f, .08f, 1).linear;
    }
}
