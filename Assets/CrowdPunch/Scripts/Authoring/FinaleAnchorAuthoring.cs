using UnityEngine;

namespace CrowdPunch.Authoring
{
    [RequireComponent(typeof(BarricadeAuthoring))]
    public sealed class FinaleAnchorAuthoring : MonoBehaviour
    {
        public FinaleBossAuthoring boss;
        public Vector3 landing;
    }
}
