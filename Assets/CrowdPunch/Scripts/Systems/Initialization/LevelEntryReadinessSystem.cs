using CrowdPunch.Components;
using CrowdPunch.Mono.Levels;
using CrowdPunch.Systems.Groups;
using Unity.Entities;
using Unity.Scenes;
using UnityEngine;

namespace CrowdPunch.Systems.Initialization
{
    /// <summary>Reports when the incoming encounter's streamed ECS sections are present.</summary>
    [UpdateInGroup(typeof(GameInitializationGroup))]
    public partial struct LevelEntryReadinessSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            var subScenes = LevelEntryRegistry.RequiredSubScenes;
            if (subScenes == null || World.DefaultGameObjectInjectionWorld != state.World)
                return;

            foreach (var guid in subScenes)
            {
                var scene = SceneSystem.GetSceneEntity(state.WorldUnmanaged, guid);
                if (scene == Entity.Null || !SceneSystem.IsSceneLoaded(state.WorldUnmanaged, scene))
                    return;
            }

            // MatchState is required by the encounter reset. Authored arena bounds supply the heading.
            if (!SystemAPI.HasSingleton<MatchState>())
                return;

            Vector3? center = SystemAPI.TryGetSingleton<ArenaBounds>(out var bounds)
                ? new Vector3(bounds.Center.x, bounds.Center.y, bounds.Center.z) : null;
            LevelEntryRegistry.ReportSceneReady(center);
        }
    }
}
