using UnityEngine;
using Hash128 = Unity.Entities.Hash128;

namespace CrowdPunch.Mono.Levels
{
    /// <summary>Exchanges scene identities and readiness with ECS during additive level entry.</summary>
    internal static class LevelEntryRegistry
    {
        public static Hash128[] RequiredSubScenes { get; set; }
        public static bool SceneReady { get; private set; }
        public static Vector3? ArenaCenter { get; private set; }
        public static bool WaitingForPhysics { get; set; }

        public static void ReportSceneReady(Vector3? arenaCenter)
        {
            ArenaCenter = arenaCenter;
            SceneReady = true;
            RequiredSubScenes = null;
        }

        public static void Clear()
        {
            RequiredSubScenes = null;
            SceneReady = false;
            ArenaCenter = null;
            WaitingForPhysics = false;
        }
    }
}
