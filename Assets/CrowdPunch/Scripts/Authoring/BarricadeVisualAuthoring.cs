using UnityEngine;

namespace CrowdPunch.Authoring
{
    public sealed class BarricadeVisualAuthoring : MonoBehaviour
    {
        public BarricadeAuthoring barricade;
        [Range(0, 2)] public int crackStage;
        public Vector3 debrisDirection;
    }
}
