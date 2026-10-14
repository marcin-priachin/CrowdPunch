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
        [MenuItem("Crowd Punch/Levels/Build Linked Pair Gauntlet 27")]
        public static void BuildLinkedPair()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode before authoring.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save open scenes before authoring.");
            var previous = EditorSceneManager.GetSceneManagerSetup();
            try {
                EnemyLinkedPairPrefabBuilder.Rebuild();
                const string materialPath = Root + "Resources/LinkedPairTether.mat";
                if (AssetDatabase.LoadAssetAtPath<Material>(materialPath) == null)
                    AssetDatabase.CreateAsset(new Material(AssetDatabase.LoadAssetAtPath<Material>(Root + "Resources/EnemyTrail.mat")) { name = "LinkedPairTether" }, materialPath);
                var pair = AssetDatabase.LoadAssetAtPath<EnemySpawnSettings>(Root + "Data/Settings/Enemies/LinkedPairEnemySpawnSettings.asset");
                var profiles = new EnemySpawnSettings[ProfileNames.Length];
                for (int i = 0; i < profiles.Length; i++) profiles[i] = AssetDatabase.LoadAssetAtPath<EnemySpawnSettings>(Root + "Data/Settings/Enemies/" + ProfileNames[i] + ".asset");
                var design = new Level { Name = "Swing Together", Outline = Rectangle(32, 34), Spacing = new Vector2(29, 31),
                    Entry = new Vector2(0, -11), Lanes = Array.Empty<Vector4>(), Waves = new[] {
                        new Wave { Name = "First Tether", B = 6, Delay = 2, Ranges = new[] { Range(-9, 6, 8, 14), Range(9, 6, 8, 14) } },
                        new Wave { Name = "Crossing Links", B = 12, Delay = 2, Ranges = new[] { Range(-9, 3, 8, 20), Range(9, 3, 8, 20) } }
                    } };
                BuildLevel(26, design, profiles, AssetDatabase.LoadAssetAtPath<Material>(Root + "Materials/Ground.mat"),
                    MaterialAsset("GauntletWalls", new Color(.13f,.19f,.24f)), MaterialAsset("GauntletLanes", new Color(.43f,.48f,.4f)),
                    MaterialAsset("GauntletBackdrop", new Color(.08f,.105f,.13f)));
                ConfigurePairWave("CP27_01_First_Tether", pair, 1);
                ConfigurePairWave("CP27_02_Crossing_Links", pair, 2);
                var sub = EditorSceneManager.OpenScene(Scenes + "Gauntlet_27/Gauntlet_27 Sub Scene.unity", OpenSceneMode.Single);
                var arena = UnityEngine.Object.FindFirstObjectByType<ArenaAuthoring>();
                arena.gameObject.AddComponent<NavigationArenaAuthoring>().settings = AssetDatabase.LoadAssetAtPath<NavigationSettings>(Root + "Data/Settings/NavigationSettings.asset");
                for (int i = 0; i < 2; i++) {
                    var obstacle = new GameObject("Tether Cover " + i); obstacle.transform.SetParent(arena.transform, false);
                    obstacle.transform.position = new Vector3(i == 0 ? -4 : 4, -1, i == 0 ? 4 : -4);
                    var solid = obstacle.AddComponent<SolidObstacleAuthoring>(); solid.footprint = new Vector2Int(2, 4); solid.height = 2;
                    var visual = GameObject.CreatePrimitive(PrimitiveType.Cube); UnityEngine.Object.DestroyImmediate(visual.GetComponent<BoxCollider>());
                    visual.transform.SetParent(obstacle.transform, false); visual.transform.localPosition = Vector3.up; visual.transform.localScale = new Vector3(2, 2, 4);
                    visual.GetComponent<Renderer>().sharedMaterial = MaterialAsset("GauntletWalls", new Color(.13f,.19f,.24f));
                }
                EditorSceneManager.MarkSceneDirty(sub); EditorSceneManager.SaveScene(sub);
                var main = EditorSceneManager.OpenScene(Scenes + "Gauntlet_27.unity", OpenSceneMode.Single);
                var marker = new SerializedObject(UnityEngine.Object.FindFirstObjectByType<GauntletLevel>());
                marker.FindProperty("openingHint").stringValue = "Punch either Mushnub to swing its partner through the crowd. The tether can hurt you, even after your punch. Use cover and flank.";
                marker.ApplyModifiedPropertiesWithoutUndo(); EditorSceneManager.MarkSceneDirty(main); EditorSceneManager.SaveScene(main);
                var bootstrap = EditorSceneManager.OpenScene(Root + "Scenes/Bootstrap.unity", OpenSceneMode.Single);
                var sequence = new SerializedObject(UnityEngine.Object.FindFirstObjectByType<GauntletSequence>());
                var names = sequence.FindProperty("levelSceneNames"); var titles = sequence.FindProperty("levelDisplayNames");
                names.arraySize = titles.arraySize = Math.Max(27, names.arraySize);
                names.GetArrayElementAtIndex(26).stringValue = "Gauntlet_27"; titles.GetArrayElementAtIndex(26).stringValue = "27 Swing Together";
                sequence.ApplyModifiedPropertiesWithoutUndo(); EditorSceneManager.MarkSceneDirty(bootstrap); EditorSceneManager.SaveScene(bootstrap);
                AssetDatabase.SaveAssets();
            } finally { EditorSceneManager.RestoreSceneManagerSetup(previous); }
        }
        private static void ConfigurePairWave(string name, EnemySpawnSettings profile, int count) {
            var wave = new SerializedObject(AssetDatabase.LoadAssetAtPath<EnemyWaveSettings>(Waves + name + ".asset"));
            wave.FindProperty("linkedPairProfile").objectReferenceValue = profile; wave.FindProperty("linkedPairCount").intValue = count;
            wave.FindProperty("replenishWhileBossLives").boolValue = false; wave.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
