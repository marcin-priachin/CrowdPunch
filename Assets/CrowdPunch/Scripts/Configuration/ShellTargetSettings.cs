using UnityEngine;

namespace CrowdPunch.Configuration
{
    [CreateAssetMenu(menuName = "Crowd Punch/Shell Target Settings")]
    public sealed class ShellTargetSettings : ScriptableObject
    {
        [Min(1)] public int requiredExplosions = 3;
        [Min(.01f)] public float coreMaxHealth = 5;
        [Min(0)] public float exploderReplacementDelay = 2;
    }
}
