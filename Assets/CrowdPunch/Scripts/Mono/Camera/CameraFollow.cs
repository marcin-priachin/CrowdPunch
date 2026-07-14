using UnityEngine;

namespace CrowdPunch.Mono.Camera
{
    /// <summary>
    /// Traditional camera follower for the GameObject player.
    /// </summary>
    public sealed class CameraFollow : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 offset = new Vector3(0f, 12f, -10f);
        [SerializeField] private float followSharpness = 12f;

        private void LateUpdate()
        {
            // TODO: Follow the player GameObject after player movement has completed.
        }
    }
}
