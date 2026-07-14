using UnityEngine;

namespace CrowdPunch.Authoring
{
    /// <summary>
    /// Scene-level ECS settings and singleton bootstrap data.
    /// </summary>
    public sealed class GameSettingsAuthoring : MonoBehaviour
    {
        [SerializeField] private bool startRunning = true;

        /// <summary>Whether ECS simulation should start immediately.</summary>
        public bool StartRunning => startRunning;
    }
}
