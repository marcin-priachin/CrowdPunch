using UnityEngine;

namespace CrowdPunch.Mono.Player
{
    /// <summary>
    /// Traditional GameObject player movement controller.
    /// </summary>
    [RequireComponent(typeof(PlayerEcsBridge))]
    public sealed class PlayerController : MonoBehaviour
    {
        [SerializeField] private PlayerEcsBridge ecsBridge;
        [SerializeField] private float playerRadius = 1.5f;

        private void Reset()
        {
            ecsBridge = GetComponent<PlayerEcsBridge>();
        }

        private void Update()
        {
            // TODO: Read player input, move this GameObject, and publish only the resulting player snapshot to PlayerEcsBridge.
            ecsBridge.PublishPlayerSnapshot(transform.position, transform.forward, playerRadius);
        }
    }
}
