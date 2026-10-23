using UnityEngine;

namespace CrowdPunch.Authoring
{
    [DisallowMultipleComponent, RequireComponent(typeof(EnemyAnimationAuthoring))]
    public sealed class GatekeeperRigAuthoring : MonoBehaviour
    {
        [Header("Visual fist size (1 = previous size)")]
        [Min(.1f), Tooltip("Constant visual size multiplier for the left fist. Does not change damage or collider size.")]
        public float leftFistScale = 1;
        [Min(.1f), Tooltip("Constant visual size multiplier for the right fist. Does not change damage or collider size.")]
        public float rightFistScale = 1;

        [Min(0), Tooltip("Minimum visual hand clearance above the encounter floor; collision and attack heights are unchanged.")]
        public float groundClearance = .02f;
    }
}
