using System;
using System.IO;
using System.Linq;
using CrowdPunch.Authoring;
using CrowdPunch.Configuration;
using CrowdPunch.Mono.Levels;
using Unity.Scenes;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace CrowdPunch.Editor
{
    public static class NavigationValidationArenaBuilder
    {
        public const string DirectoryPath = "Assets/CrowdPunch/Scenes/NavigationValidation";
        public const string BootstrapPath = DirectoryPath + "/NavigationValidationBootstrap.unity";
        public const string ArenaPath = DirectoryPath + "/NavigationValidationArena.unity";
        public const string SubScenePath = DirectoryPath + "/NavigationValidationGeometry.unity";
        public const string SettingsPath = "Assets/CrowdPunch/Data/Settings/NavigationSettings.asset";
        [MenuItem("Crowd Punch/Navigation/Open Validation Arena")]
        public static void Open() { if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(BootstrapPath); }
        public static void Build()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit play mode before building the arena.");
            var previous = EditorSceneManager.GetSceneManagerSetup();
            Directory.CreateDirectory(DirectoryPath); AssetDatabase.Refresh();
            var settings = AssetDatabase.LoadAssetAtPath<NavigationSettings>(SettingsPath);
            if (settings == null) { settings = ScriptableObject.CreateInstance<NavigationSettings>(); AssetDatabase.CreateAsset(settings, SettingsPath); }
            var material = AssetDatabase.LoadAssetAtPath<Material>(DirectoryPath + "/NavigationBlocks.mat");
            if (material == null) { material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = new Color(.3f, .4f, .45f) }; AssetDatabase.CreateAsset(material, DirectoryPath + "/NavigationBlocks.mat"); }
            try
            {
                var sub = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive); SceneManager.SetActiveScene(sub);
                var arena = new GameObject("Navigation Arena - XZ bounds").AddComponent<ArenaAuthoring>();
                var a = new SerializedObject(arena); a.FindProperty("spacingSize").vector3Value = new Vector3(48, 16, 40);
                a.FindProperty("defeatSize").vector3Value = new Vector3(58, 20, 50); a.ApplyModifiedPropertiesWithoutUndo();
                var nav = arena.gameObject.AddComponent<NavigationArenaAuthoring>(); nav.settings = settings;
                Block(arena.transform, "Short block west", new Vector2(-8, -4), new Vector2Int(6, 2), material);
                Block(arena.transform, "Short block east", new Vector2(8, 2), new Vector2Int(6, 2), material);
                Block(arena.transform, "L vertical", new Vector2(-7, 8), new Vector2Int(2, 8), material);
                Block(arena.transform, "L horizontal", new Vector2(-3, 11), new Vector2Int(6, 2), material);
                // 3 metre opening: small/medium class can use it, large class must go around.
                Block(arena.transform, "Passage west", new Vector2(8, 12), new Vector2Int(4, 6), material);
                Block(arena.transform, "Passage east", new Vector2(15, 12), new Vector2Int(4, 6), material);
                Block(arena.transform, "Pocket south", new Vector2(-19, 14), new Vector2Int(8, 2), material);
                Block(arena.transform, "Pocket east", new Vector2(-16, 17), new Vector2Int(2, 4), material);
                Block(arena.transform, "Pocket west", new Vector2(-23, 17), new Vector2Int(2, 4), material);
                Block(arena.transform, "Pocket north", new Vector2(-19, 20), new Vector2Int(8, 2), material);
                Block(arena.transform, "Boundary west", new Vector2(-25, 0), new Vector2Int(2, 42), material);
                Block(arena.transform, "Boundary east", new Vector2(25, 0), new Vector2Int(2, 42), material);
                Block(arena.transform, "Boundary south", new Vector2(0, -21), new Vector2Int(48, 2), material);
                Block(arena.transform, "Boundary north", new Vector2(0, 21), new Vector2Int(48, 2), material);
                var ground = GameObject.CreatePrimitive(PrimitiveType.Cube); ground.name = "Ground - deliberately excluded from navigation footprints";
                ground.transform.position = new Vector3(0, -1.5f, 0); ground.transform.localScale = new Vector3(54, 1, 46);
                ground.GetComponent<MeshRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/CrowdPunch/Materials/Ground.mat");
                var game = new GameObject("Shared Game Settings").AddComponent<GameSettingsAuthoring>(); var gs = new SerializedObject(game);
                gs.FindProperty("settings").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameRuntimeSettings>("Assets/CrowdPunch/Data/Settings/GameRuntimeSettings.asset"); gs.ApplyModifiedPropertiesWithoutUndo();
                var encounter = new GameObject("Representative waves").AddComponent<EnemyWaveSequenceAuthoring>();
                var es = new SerializedObject(encounter); var waves = es.FindProperty("waves"); waves.arraySize = 2;
                waves.GetArrayElementAtIndex(0).objectReferenceValue = Wave("NavigationMixedWave", false);
                waves.GetArrayElementAtIndex(1).objectReferenceValue = Wave("NavigationEliteWave", true);
                es.FindProperty("minimumPlayerDistance").floatValue = 5; es.FindProperty("placementAttemptsPerEnemy").intValue = 32; es.ApplyModifiedPropertiesWithoutUndo();
                EditorSceneManager.SaveScene(sub, SubScenePath); EditorSceneManager.CloseScene(sub, true);
                var main = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive); SceneManager.SetActiveScene(main);
                var level = new GameObject("Navigation validation").AddComponent<GauntletLevel>(); var entry = new GameObject("Player Entry").transform; entry.position = new Vector3(0, .5f, -14);
                var ls = new SerializedObject(level); ls.FindProperty("playerEntryPoint").objectReferenceValue = entry;
                ls.FindProperty("openingHint").stringValue = "Navigation test: blocks, L corner, narrow passage, sealed northwest pocket. Debug: Crowd Punch > Navigation > Inspect."; ls.ApplyModifiedPropertiesWithoutUndo();
                var scene = new GameObject("Navigation Geometry SubScene").AddComponent<SubScene>(); scene.SceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(SubScenePath); scene.AutoLoadScene = true;
                var light = new GameObject("Arena Light").AddComponent<Light>(); light.type = LightType.Directional; light.intensity = 1.2f; light.transform.rotation = Quaternion.Euler(50, -30, 0);
                EditorSceneManager.SaveScene(main, ArenaPath); EditorSceneManager.CloseScene(main, true);
                if (!File.Exists(BootstrapPath)) AssetDatabase.CopyAsset("Assets/CrowdPunch/Scenes/Bootstrap.unity", BootstrapPath);
                var bootstrap = EditorSceneManager.OpenScene(BootstrapPath, OpenSceneMode.Single);
                var sequence = UnityEngine.Object.FindFirstObjectByType<GauntletSequence>(); var ss = new SerializedObject(sequence);
                var names = ss.FindProperty("levelSceneNames"); names.arraySize = 1; names.GetArrayElementAtIndex(0).stringValue = "NavigationValidationArena";
                var titles = ss.FindProperty("levelDisplayNames"); titles.arraySize = 1; titles.GetArrayElementAtIndex(0).stringValue = "Navigation Validation";
                ss.ApplyModifiedPropertiesWithoutUndo(); EditorSceneManager.SaveScene(bootstrap);
                var build = EditorBuildSettings.scenes.ToList();
                foreach (string path in new[] { BootstrapPath, ArenaPath }) if (!build.Any(s => s.path == path)) build.Add(new EditorBuildSettingsScene(path, true));
                EditorBuildSettings.scenes = build.ToArray(); AssetDatabase.SaveAssets();
            }
            finally { EditorSceneManager.RestoreSceneManagerSetup(previous); }
        }
        private static void Block(Transform parent, string name, Vector2 position, Vector2Int size, Material material)
        {
            var obstacle = new GameObject(name).AddComponent<SolidObstacleAuthoring>(); obstacle.transform.SetParent(parent);
            obstacle.transform.position = new Vector3(position.x, -1, position.y); obstacle.footprint = size; obstacle.height = 3; obstacle.Snap();
            var mesh = GameObject.CreatePrimitive(PrimitiveType.Cube); mesh.name = "Blocking silhouette"; UnityEngine.Object.DestroyImmediate(mesh.GetComponent<BoxCollider>());
            mesh.transform.SetParent(obstacle.transform); mesh.transform.localPosition = new Vector3(0, 1.5f, 0); mesh.transform.localScale = new Vector3(size.x, 3, size.y);
            mesh.GetComponent<MeshRenderer>().sharedMaterial = material;
        }
        private static EnemyWaveSettings Wave(string name, bool elite)
        {
            string path = DirectoryPath + "/" + name + ".asset"; var wave = AssetDatabase.LoadAssetAtPath<EnemyWaveSettings>(path);
            if (wave == null) { wave = ScriptableObject.CreateInstance<EnemyWaveSettings>(); AssetDatabase.CreateAsset(wave, path); }
            var s = new SerializedObject(wave); var entries = s.FindProperty("enemies"); entries.arraySize = 4;
            string[] profiles = { "EnemySpawnSettings", "RangedEnemySpawnSettings", "ExplosiveEnemySpawnSettings", "EnemyDasherSpawnSettings" }; int[] counts = { 64, 6, 4, 4 };
            s.FindProperty("totalEnemyCount").intValue = 78;
            for (int i = 0; i < 4; i++) { var e = entries.GetArrayElementAtIndex(i); e.FindPropertyRelative("Settings").objectReferenceValue = AssetDatabase.LoadAssetAtPath<EnemySpawnSettings>("Assets/CrowdPunch/Data/Settings/Enemies/" + profiles[i] + ".asset"); e.FindPropertyRelative("MinimumCount").intValue = counts[i]; e.FindPropertyRelative("Weight").floatValue = 0; }
            var elites = s.FindProperty("eliteEnemies"); elites.arraySize = elite ? 1 : 0;
            if (elite) { var e = elites.GetArrayElementAtIndex(0); e.FindPropertyRelative("Settings").objectReferenceValue = AssetDatabase.LoadAssetAtPath<EnemySpawnSettings>("Assets/CrowdPunch/Data/Settings/Enemies/EliteEnemySpawnSettings.asset"); e.FindPropertyRelative("Count").intValue = 1; }
            var ranges = s.FindProperty("spawnRectangles"); ranges.arraySize = 1; var r = ranges.GetArrayElementAtIndex(0); r.FindPropertyRelative("Center").vector3Value = new Vector3(0, 1, 0); r.FindPropertyRelative("Width").floatValue = 44; r.FindPropertyRelative("Depth").floatValue = 36;
            s.FindProperty("delayBeforeWave").floatValue = 2; s.FindProperty("spawnMode").intValue = 1; s.FindProperty("batchSize").intValue = 20; s.FindProperty("batchInterval").floatValue = .5f;
            s.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(wave); return wave;
        }
    }
}
