using Unity.Mathematics;
using UnityEngine;

namespace CrowdPunch.Authoring
{
    /// <summary>
    /// GameObject-side arena bounds used by ECS lifetime systems.
    /// </summary>
    public sealed class ArenaAuthoring : MonoBehaviour
    {
        [SerializeField] private Vector3 size = new Vector3(50f, 10f, 50f);

        /// <summary>World-space dimensions of the playable arena.</summary>
        public float3 Size => new float3(size.x, size.y, size.z);
    }
}
