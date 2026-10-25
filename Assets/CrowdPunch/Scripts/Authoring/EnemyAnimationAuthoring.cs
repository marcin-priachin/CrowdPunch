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
        [Tooltip("Trail model's animated mesh clearance above the arena floor, in metres. Grounded poses (including Spawn) sit at this gap; launched bodies retain flight height with this minimum. Translation only; respects the current prefab scale without rebuilding samples.")]
        [Min(0f)] public float GroundClearance = .1f;
        [Tooltip("Minimum remaining armor stages required to render this equipment. Zero keeps it visible.")]
        [Range(0, 2)] public byte ArmorStagesRequired;
        [Tooltip("Sampled motion index for ground spawning; -1 when this model has no spawn clip.")]
        [Range(-1, EnemyAnimationSamples.MotionCount - 1)] public int SpawnMotion = -1;
        public bool RotateLaunchAroundOwner;
        [Min(0f)] public float LaunchPivotHeight;
        [Min(0f)] public float BlendResponse = 12f;
    }
}
