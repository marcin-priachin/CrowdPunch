using CrowdPunch.Configuration;
using UnityEngine;

namespace CrowdPunch.Authoring
{
    /// <summary>
    /// Scene-level ECS settings and singleton bootstrap data.
    /// </summary>
    public sealed class GameSettingsAuthoring : MonoBehaviour
    {
        [SerializeField] private GameRuntimeSettings settings;

        /// <summary>Whether ECS simulation should start immediately.</summary>
        public GameRuntimeSettings Settings => settings;
    }
}
