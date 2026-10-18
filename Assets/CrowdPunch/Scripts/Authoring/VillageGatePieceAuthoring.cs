using UnityEngine;

namespace CrowdPunch.Authoring
{
    [RequireComponent(typeof(BarricadeVisualAuthoring))]
    public sealed class VillageGatePieceAuthoring : MonoBehaviour
    {
        [Tooltip("0: timber/hardware, 1: fracture, 2: splinter, 3: dust puff.")]
        [Range(0,3)] public int kind;
        [Range(0,2)] public int damageStage;
        public Vector3 velocity;
        public Vector3 spin;
        [Min(.2f)] public float duration=1.5f;
        public float restingHeight=.15f;
        public float seed;
    }
}
