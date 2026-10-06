using CrowdPunch.Components;
using UnityEngine;

namespace CrowdPunch.Configuration
{
    [CreateAssetMenu(fileName = "TrailEnemySettings", menuName = "Crowd Punch/Trail Enemy Settings")]
    public sealed class TrailEnemySettings : ScriptableObject
    {
        [SerializeField] private TrailSettings settings = TrailSettings.Default;
        public TrailSettings Settings => settings;
    }
}
