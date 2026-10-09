using CrowdPunch.Components;
using UnityEngine;

namespace CrowdPunch.Authoring
{
    [DisallowMultipleComponent]
    public sealed class GroundHazardAuthoring : MonoBehaviour
    {
        public EnemyWaveSequenceAuthoring sequence;
        public GroundHazardShape shape;
        public GroundHazardOperation operation;
        [Min(0)] public int firstWave;
        [Min(.1f)] public float width = 5, depth = 8, radius = 3;
        [Min(0)] public float damage = 12;
        [Min(.01f)] public float damageInterval = .75f;
        [Min(0)] public float inactiveDuration = 3, warningDuration = 1.5f;
        [Min(.01f)] public float activeDuration = 2.5f;
        public float cycleOffset;

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1, .3f, .1f, .7f);
            Gizmos.matrix = Matrix4x4.TRS(transform.position, Quaternion.Euler(0, transform.eulerAngles.y, 0), Vector3.one);
            if (shape == GroundHazardShape.Rectangle) Gizmos.DrawWireCube(Vector3.zero, new Vector3(width, .04f, depth));
            else Gizmos.DrawWireSphere(Vector3.zero, radius);
        }
    }
}
