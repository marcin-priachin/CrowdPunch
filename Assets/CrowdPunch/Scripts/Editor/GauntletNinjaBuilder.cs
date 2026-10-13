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
        [MenuItem("Crowd Punch/Levels/Build Ninja Gauntlet 25")]
        public static void BuildNinja()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode before authoring.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save open scenes before authoring.");
            var previous = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                EnemyNinjaPrefabBuilder.Rebuild();
                var ninja = AssetDatabase.LoadAssetAtPath<EnemySpawnSettings>(Root + "Data/Settings/Enemies/NinjaEnemySpawnSettings.asset");
                var profiles = new EnemySpawnSettings[ProfileNames.Length];
                for (int i = 0; i < profiles.Length; i++) profiles[i] = AssetDatabase.LoadAssetAtPath<EnemySpawnSettings>(Root + "Data/Settings/Enemies/" + ProfileNames[i] + ".asset");
                var design = new Level { Name = "Behind You", Outline = Rectangle(42, 42), Spacing = new Vector2(42, 42),
                    Entry = new Vector2(0, -10), Lanes = Array.Empty<Vector4>(), Waves = new[] {
                        new Wave { Name = "First Ambush", B = 6, Delay = 2, Ranges = new[] { Range(-10, 4, 10, 22), Range(10, 4, 10, 22) } },
                        new Wave { Name = "Two At A Time", B = 12, Delay = 2, Ranges = new[] { Range(-10, 1, 10, 28), Range(10, 1, 10, 28) } }
                    } };
                BuildLevel(24, design, profiles, AssetDatabase.LoadAssetAtPath<Material>(Root + "Materials/Ground.mat"),
                    MaterialAsset("GauntletWalls", new Color(.13f,.19f,.24f)), MaterialAsset("GauntletLanes", new Color(.43f,.48f,.4f)),
                    MaterialAsset("GauntletBackdrop", new Color(.08f,.105f,.13f)));
                ConfigureNinjaWave("CP25_01_First_Ambush", ninja, 6, 1);
                ConfigureNinjaWave("CP25_02_Two_At_A_Time", ninja, 12, 3);
                var sub = EditorSceneManager.OpenScene(Scenes + "Gauntlet_25/Gauntlet_25 Sub Scene.unity", OpenSceneMode.Single);
                var arena = UnityEngine.Object.FindFirstObjectByType<ArenaAuthoring>();
                arena.gameObject.AddComponent<NavigationArenaAuthoring>().settings = AssetDatabase.LoadAssetAtPath<NavigationSettings>(Root + "Data/Settings/NavigationSettings.asset");
                for (int i = 0; i < 2; i++)
                {
                    var obstacle = new GameObject("Ambush Cover " + i);
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
                var main = EditorSceneManager.OpenScene(Scenes + "Gauntlet_25.unity", OpenSceneMode.Single);
                var marker = new SerializedObject(UnityEngine.Object.FindFirstObjectByType<GauntletLevel>());
                marker.FindProperty("openingHint").stringValue = "Watch the preparation. Dodge the ambush, then punch during recovery.";
                marker.ApplyModifiedPropertiesWithoutUndo(); EditorSceneManager.MarkSceneDirty(main); EditorSceneManager.SaveScene(main);
                var bootstrap = EditorSceneManager.OpenScene(Root + "Scenes/Bootstrap.unity", OpenSceneMode.Single);
                var sequence = new SerializedObject(UnityEngine.Object.FindFirstObjectByType<GauntletSequence>());
                var names = sequence.FindProperty("levelSceneNames"); var titles = sequence.FindProperty("levelDisplayNames");
                names.arraySize = titles.arraySize = Math.Max(25, names.arraySize);
                names.GetArrayElementAtIndex(24).stringValue = "Gauntlet_25";
                titles.GetArrayElementAtIndex(24).stringValue = "25 Behind You";
                sequence.ApplyModifiedPropertiesWithoutUndo();
                var player = UnityEngine.Object.FindFirstObjectByType<PlayerEcsBridge>();
                var smoke = player.GetComponent<NinjaSmokePresentation>();
                if (smoke == null) smoke = player.gameObject.AddComponent<NinjaSmokePresentation>();
                smoke.smokeMaterial = NinjaSmokeMaterial();
                EditorSceneManager.MarkSceneDirty(bootstrap); EditorSceneManager.SaveScene(bootstrap);
                // NINJA-006: legacy-only registration; no campaign or build-list edits.
                AssetDatabase.SaveAssets();
            }
            finally { EditorSceneManager.RestoreSceneManagerSetup(previous); }
        }

        private static void ConfigureNinjaWave(string name, EnemySpawnSettings profile, int baselines, int ninjas)
        {
            var wave = new SerializedObject(AssetDatabase.LoadAssetAtPath<EnemyWaveSettings>(Waves + name + ".asset"));
            wave.FindProperty("totalEnemyCount").intValue = baselines + ninjas;
            var entries = wave.FindProperty("enemies"); entries.arraySize = 2;
            entries.GetArrayElementAtIndex(1).FindPropertyRelative("Settings").objectReferenceValue = profile;
            entries.GetArrayElementAtIndex(1).FindPropertyRelative("MinimumCount").intValue = ninjas;
            entries.GetArrayElementAtIndex(1).FindPropertyRelative("Weight").floatValue = 0;
            wave.FindProperty("replenishWhileBossLives").boolValue = false;
            wave.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Material NinjaSmokeMaterial()
        {
            const string path = Root + "Materials/Enemies/NinjaSmoke.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            var disc = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "Materials/Feedback/ParticleSoftDisc.asset");
            if (material != null)
            {
                material.SetTexture("_BaseMap", disc); EditorUtility.SetDirty(material);
                return material;
            }
            material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")) { name = "Ninja Smoke", renderQueue = 3000 };
            material.SetFloat("_Surface", 1); material.SetFloat("_Blend", 0);
            material.SetFloat("_SrcBlend", 5); material.SetFloat("_DstBlend", 10); material.SetFloat("_ZWrite", 0);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetTexture("_BaseMap", disc);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }
    }
}
