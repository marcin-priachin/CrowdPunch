using UnityEngine;

namespace CrowdPunch.Authoring
{
    [DisallowMultipleComponent, RequireComponent(typeof(EnemyAnimationAuthoring))]
    public sealed class GatekeeperRigAuthoring : MonoBehaviour
    {
        [Min(0), Tooltip("Minimum visual hand clearance above the encounter floor; collision and attack heights are unchanged.")]
        public float groundClearance = .02f;
    }
}
