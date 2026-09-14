using CrowdPunch.Configuration;
using UnityEngine;
namespace CrowdPunch.Authoring
{
    [RequireComponent(typeof(ArenaAuthoring))]
    public sealed class NavigationArenaAuthoring : MonoBehaviour
    {
        [Tooltip("Static grid and runtime defaults. All asset edits require rebaking and reloading the arena.")]
        public NavigationSettings settings;
        [Tooltip("World XZ point in the playable connected region. Spawns must connect here for their radius class.")]
        public Vector2 participationAnchor = new Vector2(0, -12);
    }
}
