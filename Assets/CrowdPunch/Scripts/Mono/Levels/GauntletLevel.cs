using UnityEngine;

namespace CrowdPunch.Mono.Levels
{
    /// <summary>Scene-facing identity and player entry point for one closed gauntlet.</summary>
    public sealed class GauntletLevel : MonoBehaviour
    {
        [SerializeField] private Transform playerEntryPoint;

        public Transform PlayerEntryPoint => playerEntryPoint != null ? playerEntryPoint : transform;
    }
}
