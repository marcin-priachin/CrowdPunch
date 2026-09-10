using UnityEngine;

namespace CrowdPunch.Authoring
{
    [DisallowMultipleComponent, RequireComponent(typeof(SkinnedMeshRenderer))]
    public sealed class EnemyAnimationAuthoring : MonoBehaviour
    {
        public TextAsset Samples;
        [Min(0f)] public float BlendResponse = 12f;
    }
}
