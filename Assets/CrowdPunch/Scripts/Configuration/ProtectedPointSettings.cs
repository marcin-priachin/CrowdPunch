using UnityEngine;

namespace CrowdPunch.Configuration
{
    [CreateAssetMenu(menuName = "Crowd Punch/Protected Point Settings")]
    public sealed class ProtectedPointSettings : ScriptableObject
    {
        [Min(1), Tooltip("Defeat occurs on this breach, including the first when set to one.")]
        public int breachThreshold = 1;
        [Min(0), Tooltip("Closest in-range enemies allowed to begin attacks. Committed attacks finish.")]
        public int maximumPlayerAttackers = 3;
    }
}
