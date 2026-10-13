using System;
using CrowdPunch.Authoring;
using CrowdPunch.Configuration;
using CrowdPunch.Mono.Levels;
using CrowdPunch.Mono.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CrowdPunch.Editor
{
    public static partial class GauntletProgressionBuilder
    {
        [MenuItem("Crowd Punch/Levels/Build Dragon Gauntlet 26")]
        public static void BuildDragon()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode before authoring.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save open scenes before authoring.");
            var previous = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                EnemyDragonPrefabBuilder.Rebuild();
                EnsureDragonMaterial();
                var dragon = AssetDatabase.LoadAssetAtPath<EnemySpawnSettings>(Root + "Data/Settings/Enemies/DragonEnemySpawnSettings.asset");
                var profiles = new EnemySpawnSettings[ProfileNames.Length];
                for (int i = 0; i < profiles.Length; i++) profiles[i] = AssetDatabase.LoadAssetAtPath<EnemySpawnSettings>(Root + "Data/Settings/Enemies/" + ProfileNames[i] + ".asset");
                var design = new Level { Name = "Flame Front", Outline = Rectangle(34, 36), Spacing = new Vector2(31, 33),
                    Entry = new Vector2(0, -12), Lanes = Array.Empty<Vector4>(), Waves = new[] {
                        new Wave { Name = "First Flame", B = 6, Delay = 2, Ranges = new[] { Range(-10, 7, 6, 12), Range(10, 7, 6, 12) } },
                        new Wave { Name = "Crossfire", B = 12, Delay = 2, Ranges = new[] { Range(-10, 4, 6, 18), Range(10, 4, 6, 18) } }
                    } };
                BuildLevel(25, design, profiles, AssetDatabase.LoadAssetAtPath<Material>(Root + "Materials/Ground.mat"),
                    MaterialAsset("GauntletWalls", new Color(.13f,.19f,.24f)), MaterialAsset("GauntletLanes", new Color(.43f,.48f,.4f)),
                    MaterialAsset("GauntletBackdrop", new Color(.08f,.105f,.13f)));
                ConfigureDragonWave("CP26_01_First_Flame", dragon, 6, 1);
                ConfigureDragonWave("CP26_02_Crossfire", dragon, 12, 2);
                var sub = EditorSceneManager.OpenScene(Scenes + "Gauntlet_26/Gauntlet_26 Sub Scene.unity", OpenSceneMode.Single);
                var arena = UnityEngine.Object.FindFirstObjectByType<ArenaAuthoring>();
                arena.gameObject.AddComponent<NavigationArenaAuthoring>().settings = AssetDatabase.LoadAssetAtPath<NavigationSettings>(Root + "Data/Settings/NavigationSettings.asset");
                for (int i = 0; i < 2; i++)
                {
                    var obstacle = new GameObject("Flame Cover " + i);
                    obstacle.transform.SetParent(arena.transform, false);
                    obstacle.transform.position = new Vector3(i == 0 ? -5 : 5, -1, i == 0 ? 5 : -5);
                    var solid = obstacle.AddComponent<SolidObstacleAuthoring>(); solid.footprint = new Vector2Int(2, 4); solid.height = 2;
                    var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    UnityEngine.Object.DestroyImmediate(visual.GetComponent<BoxCollider>());
                    visual.transform.SetParent(obstacle.transform, false); visual.transform.localPosition = Vector3.up;
                    visual.transform.localScale = new Vector3(2, 2, 4);
                    visual.GetComponent<Renderer>().sharedMaterial = MaterialAsset("GauntletWalls", new Color(.13f,.19f,.24f));
                }
                EditorSceneManager.MarkSceneDirty(sub); EditorSceneManager.SaveScene(sub);
                var main = EditorSceneManager.OpenScene(Scenes + "Gauntlet_26.unity", OpenSceneMode.Single);
                var marker = new SerializedObject(UnityEngine.Object.FindFirstObjectByType<GauntletLevel>());
                marker.FindProperty("openingHint").stringValue = "Flank the slow-turning flame. Use cover, then launch a dragon through the crowd. Flame hurts even during dash.";
                marker.ApplyModifiedPropertiesWithoutUndo(); EditorSceneManager.MarkSceneDirty(main); EditorSceneManager.SaveScene(main);
                var bootstrap = EditorSceneManager.OpenScene(Root + "Scenes/Bootstrap.unity", OpenSceneMode.Single);
                var sequence = new SerializedObject(UnityEngine.Object.FindFirstObjectByType<GauntletSequence>());
                var names = sequence.FindProperty("levelSceneNames"); var titles = sequence.FindProperty("levelDisplayNames");
                names.arraySize = titles.arraySize = Math.Max(26, names.arraySize);
                names.GetArrayElementAtIndex(25).stringValue = "Gauntlet_26";
                titles.GetArrayElementAtIndex(25).stringValue = "26 Flame Front";
                sequence.ApplyModifiedPropertiesWithoutUndo();
                EditorSceneManager.MarkSceneDirty(bootstrap); EditorSceneManager.SaveScene(bootstrap);
                // DRAGON-007: legacy-only registration; no campaign or build-list edits.
                AssetDatabase.SaveAssets();
            }
            finally { EditorSceneManager.RestoreSceneManagerSetup(previous); }
        }

        private static void ConfigureDragonWave(string name, EnemySpawnSettings profile, int baselines, int dragons)
        {
            var wave = new SerializedObject(AssetDatabase.LoadAssetAtPath<EnemyWaveSettings>(Waves + name + ".asset"));
            wave.FindProperty("totalEnemyCount").intValue = baselines + dragons;
            var entries = wave.FindProperty("enemies"); entries.arraySize = 2;
            entries.GetArrayElementAtIndex(1).FindPropertyRelative("Settings").objectReferenceValue = profile;
            entries.GetArrayElementAtIndex(1).FindPropertyRelative("MinimumCount").intValue = dragons;
            entries.GetArrayElementAtIndex(1).FindPropertyRelative("Weight").floatValue = 0;
            wave.FindProperty("replenishWhileBossLives").boolValue = false;
            wave.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsureDragonMaterial()
        {
            const string path = Root + "Resources/DragonFlame.mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(path) != null) return;
            var template = AssetDatabase.LoadAssetAtPath<Material>(Root + "Resources/EnemyTrail.mat");
            if (template == null) throw new InvalidOperationException("Missing vertex-color area material.");
            AssetDatabase.CreateAsset(new Material(template) { name = "DragonFlame" }, path);
        }
    }
}
