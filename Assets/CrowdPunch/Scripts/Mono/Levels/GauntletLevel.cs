using UnityEngine;

namespace CrowdPunch.Mono.Levels
{
    /// <summary>Scene-facing identity and player entry point for one closed gauntlet.</summary>
    public sealed class GauntletLevel : MonoBehaviour
    {
        [SerializeField] private Transform playerEntryPoint;
        [SerializeField, TextArea] private string openingHint;

        public Transform PlayerEntryPoint => playerEntryPoint != null ? playerEntryPoint : transform;
        public string OpeningHint => openingHint;
    }
}
