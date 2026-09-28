using CrowdPunch.Configuration;
using UnityEngine;

namespace CrowdPunch.Authoring
{
    public sealed class BarricadeAuthoring : MonoBehaviour
    {
        public BarricadeSettings settings;
        public Vector3 size = new Vector3(28, 4, 1);
        public Transform exit;
        public bool completeOnDestruction;
        public RotatingCoverAuthoring cover;
        [Min(.1f)] public float exitRadius = 2;
    }
}
