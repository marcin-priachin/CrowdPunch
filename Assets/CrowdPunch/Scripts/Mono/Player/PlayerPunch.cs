using UnityEngine;

namespace CrowdPunch.Mono.Player
{
    /// <summary>
    /// Traditional GameObject punch input and timing component.
    /// </summary>
    [RequireComponent(typeof(PlayerEcsBridge))]
    public sealed class PlayerPunch : MonoBehaviour
    {
        [SerializeField] private PlayerEcsBridge ecsBridge;
        [SerializeField] private float punchRadius = 2f;
        [SerializeField] private float punchRange = 3f;
        [SerializeField] private float punchStrength = 12f;

        private void Reset()
        {
            ecsBridge = GetComponent<PlayerEcsBridge>();
        }

        private void Update()
        {
            // TODO: Detect punch input and publish punch intent to PlayerEcsBridge without querying ECS enemies.
        }
    }
}
