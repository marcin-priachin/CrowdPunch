using CrowdPunch.Components;
using UnityEngine;

namespace CrowdPunch.Authoring
{
    public sealed class BossPartAuthoring : MonoBehaviour
    {
        public BossEncounterAuthoring encounter;
        public BossPartKind kind;
        [Min(.1f)] public float radius = 1.4f;
        [Min(.1f)] public float height = 2.5f;
    }
}
