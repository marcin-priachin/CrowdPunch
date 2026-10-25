using System;
using CrowdPunch.Authoring;
using CrowdPunch.Configuration;
using CrowdPunch.Mono.Levels;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CrowdPunch.Editor
{
    public static partial class GauntletProgressionBuilder
    {
        [MenuItem("Crowd Punch/Levels/Build Snake Gauntlet 24")]
        public static void BuildSnake()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode before authoring.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save open scenes before authoring.");
            var previous = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                string settingsPath = Root + "Data/Settings/SnakeSettings.asset";
                var tuning = AssetDatabase.LoadAssetAtPath<SnakeSettings>(settingsPath);
                if (tuning == null) { tuning = ScriptableObject.CreateInstance<SnakeSettings>(); AssetDatabase.CreateAsset(tuning, settingsPath); }
                BuildSnakeOrb();
                string profilePath = Root + "Data/Settings/Enemies/SnakeEnemySpawnSettings.asset";
                var profile = AssetDatabase.LoadAssetAtPath<EnemySpawnSettings>(profilePath);
                bool createdProfile = profile == null;
                if (profile == null)
                {
                    AssetDatabase.CopyAsset(Root + "Data/Settings/Enemies/EnemySpawnSettings.asset", profilePath);
                    profile = AssetDatabase.LoadAssetAtPath<EnemySpawnSettings>(profilePath);
                }
                var profileData = new SerializedObject(profile);
                profileData.FindProperty("respawnEnabled").boolValue = false;
                profileData.FindProperty("contactAttemptWindUpDuration").floatValue = 0;
                if (createdProfile) profileData.FindProperty("maxHealth").floatValue = 90;
                profileData.ApplyModifiedPropertiesWithoutUndo();
                EnemySnakePrefabBuilder.Rebuild();
                var profiles = new EnemySpawnSettings[ProfileNames.Length];
                for (int i = 0; i < profiles.Length; i++) profiles[i] = AssetDatabase.LoadAssetAtPath<EnemySpawnSettings>(Root + "Data/Settings/Enemies/" + ProfileNames[i] + ".asset");
                var design = new Level { Name = "Cut The Tail", Outline = Clipped(36, 38, 3), Spacing = new Vector2(31, 33),
                    Entry = new Vector2(0, -13), Lanes = Array.Empty<Vector4>(), Waves = new[] {
                        new Wave { Name = "Snakes And Ammunition", B = 12, Delay = 2, Ranges = new[] { Range(-9, 3, 6, 22), Range(9, 3, 6, 22) } } } };
                BuildLevel(23, design, profiles, AssetDatabase.LoadAssetAtPath<Material>(Root + "Materials/Ground.mat"),
                    MaterialAsset("GauntletWalls", new Color(.13f,.19f,.24f)), MaterialAsset("GauntletLanes", new Color(.43f,.48f,.4f)),
                    MaterialAsset("GauntletBackdrop", new Color(.08f,.105f,.13f)));
                var wave = new SerializedObject(AssetDatabase.LoadAssetAtPath<EnemyWaveSettings>(Waves + "CP24_01_Snakes_And_Ammunition.asset"));
                wave.FindProperty("totalEnemyCount").intValue = 14;
                var entries = wave.FindProperty("enemies"); entries.arraySize = 2;
                entries.GetArrayElementAtIndex(1).FindPropertyRelative("Settings").objectReferenceValue = profile;
                entries.GetArrayElementAtIndex(1).FindPropertyRelative("MinimumCount").intValue = 2;
                entries.GetArrayElementAtIndex(1).FindPropertyRelative("Weight").floatValue = 0;
                wave.FindProperty("replenishWhileBossLives").boolValue = false;
                wave.ApplyModifiedPropertiesWithoutUndo();
                var sub = EditorSceneManager.OpenScene(Scenes + "Gauntlet_24/Gauntlet_24 Sub Scene.unity", OpenSceneMode.Single);
                var arena = UnityEngine.Object.FindFirstObjectByType<ArenaAuthoring>();
                arena.gameObject.AddComponent<NavigationArenaAuthoring>().settings = AssetDatabase.LoadAssetAtPath<NavigationSettings>(Root + "Data/Settings/NavigationSettings.asset");
                for (int i = 0; i < 2; i++)
                {
                    var obstacle = new GameObject();
                    obstacle.name = "Tail Routing Obstacle " + i;
                    obstacle.transform.SetParent(arena.transform, false);
                    obstacle.transform.position = new Vector3(i == 0 ? -3 : 3, -1, i == 0 ? 3 : -3);
                    var solid = obstacle.AddComponent<SolidObstacleAuthoring>(); solid.footprint = new Vector2Int(2,5); solid.height = 2;
                    var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    UnityEngine.Object.DestroyImmediate(visual.GetComponent<BoxCollider>());
                    visual.transform.SetParent(obstacle.transform, false); visual.transform.localPosition = Vector3.up;
                    visual.transform.localScale = new Vector3(2,2,5);
                    visual.GetComponent<Renderer>().sharedMaterial = MaterialAsset("GauntletWalls", new Color(.13f,.19f,.24f));
                }
                EditorSceneManager.MarkSceneDirty(sub); EditorSceneManager.SaveScene(sub);
                var bootstrap = EditorSceneManager.OpenScene(Root + "Scenes/Bootstrap.unity", OpenSceneMode.Single);
                var sequence = new SerializedObject(UnityEngine.Object.FindFirstObjectByType<GauntletSequence>());
                var names = sequence.FindProperty("levelSceneNames"); var titles = sequence.FindProperty("levelDisplayNames");
                names.arraySize = titles.arraySize = Math.Max(24, names.arraySize);
                names.GetArrayElementAtIndex(23).stringValue = "Gauntlet_24";
                titles.GetArrayElementAtIndex(23).stringValue = "24 Cut The Tail";
                sequence.ApplyModifiedPropertiesWithoutUndo(); EditorSceneManager.MarkSceneDirty(bootstrap); EditorSceneManager.SaveScene(bootstrap);
                // Legacy scenes load by Editor path and never enter player build settings.
                AssetDatabase.SaveAssets();
            }
            finally { EditorSceneManager.RestoreSceneManagerSetup(previous); }
        }

        private static void BuildSnakeOrb() => EnemySnakePrefabBuilder.RebuildSegments();
    }
}
