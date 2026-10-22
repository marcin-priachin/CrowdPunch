using CrowdPunch.Components;
using UnityEngine;

namespace CrowdPunch.Authoring
{
    [DisallowMultipleComponent, RequireComponent(typeof(SkinnedMeshRenderer))]
    public sealed class EnemyAnimationAuthoring : MonoBehaviour
    {
        public TextAsset Samples;
        public EnemyAnimationProfile Profile;
        [Tooltip("Sampled motion index for ground spawning; -1 when this model has no spawn clip.")]
        [Range(-1, EnemyAnimationSamples.MotionCount - 1)] public int SpawnMotion = -1;
        public bool RotateLaunchAroundOwner;
        [Min(0f)] public float LaunchPivotHeight;
        [Min(0f)] public float BlendResponse = 12f;
    }
}
