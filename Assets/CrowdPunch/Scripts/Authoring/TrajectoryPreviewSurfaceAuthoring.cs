using UnityEngine;

namespace CrowdPunch.Authoring
{
    public sealed class TrajectoryPreviewSurfaceAuthoring : MonoBehaviour
    {
        [Min(0f)] public float radius = 1f;
        public float heightOffset;
    }
}
