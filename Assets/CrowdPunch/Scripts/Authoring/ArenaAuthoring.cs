using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Serialization;

namespace CrowdPunch.Authoring
{
    /// <summary>
    /// GameObject-side arena bounds used by ECS lifetime systems.
    /// </summary>
    public sealed class ArenaAuthoring : MonoBehaviour
    {
        [Header("Enemy Spacing Bounds")]
        [SerializeField] private Vector3 spacingCenterOffset;
        [FormerlySerializedAs("size")]
        [SerializeField] private Vector3 spacingSize = new Vector3(50f, 10f, 50f);

        [Header("Enemy Defeat Bounds")]
        [SerializeField] private Vector3 defeatCenterOffset;
        [Tooltip("Enemies outside this volume are defeated or pooled. A zero size uses the spacing bounds for backward compatibility.")]
        [SerializeField] private Vector3 defeatSize;

        public float3 SpacingCenterOffset => ToFloat3(spacingCenterOffset);
        public float3 SpacingSize => ToFloat3(Vector3.Max(Vector3.zero, spacingSize));
        public float3 DefeatCenterOffset => ToFloat3(defeatCenterOffset);
        public float3 DefeatSize => ToFloat3(defeatSize.sqrMagnitude <= 0f
            ? Vector3.Max(Vector3.zero, spacingSize)
            : Vector3.Max(Vector3.zero, defeatSize));

        private static float3 ToFloat3(Vector3 value) => new float3(value.x, value.y, value.z);

        private void OnDrawGizmosSelected()
        {
            Vector3 origin = transform.position;
            Gizmos.color = new Color(0.15f, 0.8f, 1f, 0.9f);
            Gizmos.DrawWireCube(origin + spacingCenterOffset, Vector3.Max(Vector3.zero, spacingSize));

            Vector3 effectiveDefeatSize = defeatSize.sqrMagnitude <= 0f ? spacingSize : defeatSize;
            Gizmos.color = new Color(1f, 0.25f, 0.15f, 0.9f);
            Gizmos.DrawWireCube(origin + defeatCenterOffset, Vector3.Max(Vector3.zero, effectiveDefeatSize));
        }
    }
}
