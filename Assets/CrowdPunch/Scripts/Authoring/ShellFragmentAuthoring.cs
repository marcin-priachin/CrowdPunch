using UnityEngine;

namespace CrowdPunch.Authoring
{
    [RequireComponent(typeof(BarricadeVisualAuthoring))]
    public sealed class ShellFragmentAuthoring : MonoBehaviour
    {
        [Tooltip("0: solid panel/hardware; 1: damage seam; 2: successful-hit spark; 3: break dust.")]
        [Range(0, 3)] public int kind;
        public Vector3 velocity;
        public Vector3 spin;
        [Min(.1f)] public float duration = 1.25f;
        public float restingY = -1;
        [Range(0, 1)] public float seed;
    }
}
