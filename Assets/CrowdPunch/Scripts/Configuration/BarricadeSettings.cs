using CrowdPunch.Components;
using UnityEngine;

namespace CrowdPunch.Configuration
{
    [CreateAssetMenu(menuName = "Crowd Punch/Barricade Settings")]
    public sealed class BarricadeSettings : ScriptableObject
    {
        [Min(1)] public int requiredHits = 3;
        [Tooltip("Launch ownership, inherited by propagated bodies. Explosions also count without a launch.")]
        public BarricadeLaunchSources eligibleLaunchSources = BarricadeLaunchSources.All;
        [Range(0.1f, 1.5f)] public float reboundMultiplier = .85f;
        [Min(0)] public float replenishDelay = 2f;
        [Min(.01f)] public float flashDuration = .18f;
        [Min(.01f)] public float debrisDuration = .65f;
    }
}
