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
            if (playerBridge == null)
            {
                playerBridge = FindFirstObjectByType<PlayerEcsBridge>();
            }

            PlayerBridgeRegistry.Register(playerBridge);
        }

        private void OnDestroy()
        {
            PlayerBridgeRegistry.Unregister(playerBridge);
        }
    }
}
