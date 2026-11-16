using System;
using System.Linq;
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
        private const string MushroomGauntlet = "Gauntlet_33";
        private const string MushroomTitle = "Return Fire";

        [MenuItem("Crowd Punch/Levels/Build Mushroom Shooter Gauntlet 33")]
        public static void BuildMushroomShooter()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play mode before authoring.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save open scenes before authoring.");
            var previous = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                var mushroom = EnemyMushroomShooterPrefabBuilder.Rebuild();
                var profiles = ProfileNames.Select(name =>
                    AssetDatabase.LoadAssetAtPath<EnemySpawnSettings>(Root + "Data/Settings/Enemies/" + name + ".asset")).ToArray();
                var design = new Level
                {
                    Name = MushroomTitle, Outline = Rectangle(44, 42), Spacing = new Vector2(44, 42),
                    Entry = new Vector2(0, -13), Lanes = Array.Empty<Vector4>(),
                    Waves = new[]
                    {
                        new Wave { Name = "First Shot", B = 6, Delay = 1.5f, Ranges = new[] { Range(0, 6, 15, 8) } },
                        new Wave { Name = "Crossing Fire", B = 12, Delay = 1.5f,
                            Ranges = new[] { Range(-11, 7, 9, 11), Range(11, 7, 9, 11), Range(0, 11, 9, 7) } }
                    }
                };
                var walls = MaterialAsset("GauntletWalls", new Color(.13f, .19f, .24f));
                BuildLevel(32, design, profiles, AssetDatabase.LoadAssetAtPath<Material>(Root + "Materials/Ground.mat"),
                    walls, MaterialAsset("GauntletLanes", new Color(.43f, .48f, .4f)),
                    MaterialAsset("GauntletBackdrop", new Color(.08f, .105f, .13f)));
                AddMushroomShooters("CP33_01_First_Shot", mushroom, 6, 1);
                AddMushroomShooters("CP33_02_Crossing_Fire", mushroom, 12, 2);

                var sub = EditorSceneManager.OpenScene(Scenes + MushroomGauntlet + "/" + MushroomGauntlet + " Sub Scene.unity", OpenSceneMode.Single);
                var arena = UnityEngine.Object.FindFirstObjectByType<ArenaAuthoring>();
                arena.gameObject.AddComponent<NavigationArenaAuthoring>().settings =
                    AssetDatabase.LoadAssetAtPath<NavigationSettings>(Root + "Data/Settings/NavigationSettings.asset");
                var coverMaterial = MaterialAsset("MushroomGauntletCover", new Color(.29f, .34f, .47f));
                AddProjectileCover(arena.transform, coverMaterial, "West Cover", -7f, 0f, 3, 5);
                AddProjectileCover(arena.transform, coverMaterial, "East Cover", 7f, -4f, 4, 3);
                AddProjectileCover(arena.transform, coverMaterial, "Back Cover", 4f, 11f, 3, 3);
                // The warning sits just above the floor. The decorative entry apron
                // shares that height and otherwise hides the lane under the player.
                var apron = sub.GetRootGameObjects().Select(root => root.transform.Find("Entry Apron")).FirstOrDefault(t => t != null);
                if (apron != null) UnityEngine.Object.DestroyImmediate(apron.gameObject);
                EditorSceneManager.MarkSceneDirty(sub);
                EditorSceneManager.SaveScene(sub);

                var level = EditorSceneManager.OpenScene(Scenes + MushroomGauntlet + ".unity", OpenSceneMode.Single);
                using (var data = new SerializedObject(UnityEngine.Object.FindFirstObjectByType<GauntletLevel>()))
                {
                    data.FindProperty("openingHint").stringValue = "Sidestep the violet warning lane. Punch the Shooter to fire its beam through the crowd.";
                    data.ApplyModifiedPropertiesWithoutUndo();
                }
                EditorSceneManager.MarkSceneDirty(level);
                EditorSceneManager.SaveScene(level);

                var bootstrap = EditorSceneManager.OpenScene(CampaignBuildRegistration.BootstrapPath, OpenSceneMode.Single);
                using (var data = new SerializedObject(UnityEngine.Object.FindFirstObjectByType<GauntletSequence>()))
                {
                    var names = data.FindProperty("levelSceneNames");
                    var titles = data.FindProperty("levelDisplayNames");
                    int slot = 0;
                    while (slot < names.arraySize && names.GetArrayElementAtIndex(slot).stringValue != MushroomGauntlet) slot++;
                    if (slot == names.arraySize) names.arraySize++;
                    if (slot >= titles.arraySize) titles.arraySize = slot + 1;
                    names.GetArrayElementAtIndex(slot).stringValue = MushroomGauntlet;
                    titles.GetArrayElementAtIndex(slot).stringValue = "33 " + MushroomTitle;
                    data.ApplyModifiedPropertiesWithoutUndo();
                }
                EditorSceneManager.MarkSceneDirty(bootstrap);
                EditorSceneManager.SaveScene(bootstrap);
                // Legacy Editor encounter only: Bootstrap registration, no campaign catalog or player build entry.
                AssetDatabase.SaveAssets();
            }
            finally { EditorSceneManager.RestoreSceneManagerSetup(previous); }
        }

        private static void AddMushroomShooters(string waveName, EnemySpawnSettings settings, int baselines, int shooters)
        {
            using var wave = new SerializedObject(AssetDatabase.LoadAssetAtPath<EnemyWaveSettings>(Waves + waveName + ".asset"));
            wave.FindProperty("totalEnemyCount").intValue = baselines + shooters;
            var entries = wave.FindProperty("enemies");
            entries.arraySize = 2;
            var mushroom = entries.GetArrayElementAtIndex(1);
            mushroom.FindPropertyRelative("Settings").objectReferenceValue = settings;
            mushroom.FindPropertyRelative("MinimumCount").intValue = shooters;
            mushroom.FindPropertyRelative("Weight").floatValue = 0;
            wave.FindProperty("replenishWhileBossLives").boolValue = false;
            wave.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AddProjectileCover(Transform parent, Material material, string name, float x, float z, int width, int depth)
        {
            const float height = 2.5f;
            var obstacle = new GameObject(name);
            obstacle.transform.SetParent(parent, false);
            obstacle.transform.position = new Vector3(x, -1f, z);
            var solid = obstacle.AddComponent<SolidObstacleAuthoring>();
            solid.footprint = new Vector2Int(width, depth);
            solid.height = height;
            var mesh = GameObject.CreatePrimitive(PrimitiveType.Cube);
            UnityEngine.Object.DestroyImmediate(mesh.GetComponent<BoxCollider>());
            mesh.transform.SetParent(obstacle.transform, false);
            mesh.transform.localPosition = Vector3.up * (height * .5f);
            mesh.transform.localScale = new Vector3(width, height, depth);
            mesh.GetComponent<Renderer>().sharedMaterial = material;
        }
    }
}
