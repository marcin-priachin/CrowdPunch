using CrowdPunch.Configuration;
using UnityEngine;
namespace CrowdPunch.Authoring
{
    [RequireComponent(typeof(ArenaAuthoring))]
    public sealed class NavigationArenaAuthoring : MonoBehaviour
    {
        [Tooltip("Static grid and runtime defaults. All asset edits require rebaking and reloading the arena.")]
        public NavigationSettings settings;
        [Tooltip("Static perimeter mesh colliders that intersect the enemy spacing bounds. Their horizontal footprints block navigation and spawn placement.")]
        public MeshCollider[] perimeterColliders;
        [Tooltip("Override automatic selection near the enemy spacing bounds centre. Enable when the centre is in the wrong connected region. Requires rebaking.")]
        public bool overrideParticipationAnchor;
        [Tooltip("Used only when Override Participation Anchor is enabled. World XZ point in the playable region, clear for all configured radius classes. Invalid overrides are reported, never relocated.")]
        public Vector2 participationAnchor;
    }
}
