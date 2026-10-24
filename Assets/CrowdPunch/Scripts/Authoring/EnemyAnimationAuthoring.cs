using CrowdPunch.Components;
using UnityEngine;

namespace CrowdPunch.Authoring
{
    [DisallowMultipleComponent, RequireComponent(typeof(SkinnedMeshRenderer))]
    public sealed class EnemyAnimationAuthoring : MonoBehaviour
    {
        public TextAsset Samples;
        [Tooltip("Optional directional jog samples applied only to leg bones during active movement.")]
        public TextAsset JogSamples;
        [Tooltip("Maximum leg-animation playback multiplier: 1 = original jog speed, 2 = double speed. Zero removes the cap. Gameplay movement and upper-body playback are unaffected.")]
        [Min(0f)] public float MaxLegAnimationSpeed = 2f;
        [HideInInspector] public float[] JogCycleDistances;
        public EnemyAnimationProfile Profile;
        [Tooltip("Sampled motion index for ground spawning; -1 when this model has no spawn clip.")]
        [Range(-1, EnemyAnimationSamples.MotionCount - 1)] public int SpawnMotion = -1;
        public bool RotateLaunchAroundOwner;
        [Min(0f)] public float LaunchPivotHeight;
        [Min(0f)] public float BlendResponse = 12f;
    }
}
