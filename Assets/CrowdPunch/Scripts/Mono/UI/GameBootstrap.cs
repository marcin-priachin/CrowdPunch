using CrowdPunch.Mono.Player;
using UnityEngine;

namespace CrowdPunch.Mono.UI
{
    /// <summary>
    /// Scene-level MonoBehaviour bootstrap for hybrid player-to-ECS bridge wiring.
    /// </summary>
    public sealed class GameBootstrap : MonoBehaviour
    {
        [SerializeField] private PlayerEcsBridge playerBridge;

        private void Awake()
        {
            // TODO: Register the player bridge with the ECS bridge path without exposing enemy entities to MonoBehaviours.
        }
    }
}
