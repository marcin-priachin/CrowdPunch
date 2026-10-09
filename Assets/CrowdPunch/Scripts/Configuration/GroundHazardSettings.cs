using CrowdPunch.Components;
using UnityEngine;

namespace CrowdPunch.Configuration
{
    [CreateAssetMenu(menuName = "Crowd Punch/Ground Hazard Settings")]
    public sealed class GroundHazardSettings : ScriptableObject
    {
        public GroundHazardAvoidance avoidance = GroundHazardAvoidance.ActiveOnly;
        public GroundHazardFallback noSafeRoute = GroundHazardFallback.WaitSafely;
    }
}
