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
        private const string SporellaGauntlet = "Gauntlet_34";
        private const string SporellaTitle = "Spore Weave";

        [MenuItem("Crowd Punch/Levels/Build Sporella Gauntlet 34")]
        public static void BuildSporella()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play mode before authoring.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save open scenes before authoring.");
            var previous = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                var sporella = EnemySporellaPrefabBuilder.Rebuild();
                var profiles = ProfileNames.Select(name =>
                    AssetDatabase.LoadAssetAtPath<EnemySpawnSettings>(Root + "Data/Settings/Enemies/" + name + ".asset")).ToArray();
                var design = new Level
                {
                    Name = SporellaTitle, Outline = Rectangle(42, 38), Spacing = new Vector2(42, 38),
                    Entry = new Vector2(0, -12), Lanes = Array.Empty<Vector4>(),
                    Waves = new[]
                    {
                        new Wave { Name = "First Weave", B = 6, Delay = 1.5f, Ranges = new[] { Range(0, 5, 14, 7) } },
                        new Wave { Name = "Crossed Spores", B = 12, Delay = 1.5f,
                            Ranges = new[] { Range(-10, 6, 7, 10), Range(10, 6, 7, 10), Range(0, 10, 8, 6) } }
                    }
                };
                BuildLevel(33, design, profiles, AssetDatabase.LoadAssetAtPath<Material>(Root + "Materials/Ground.mat"),
                    MaterialAsset("GauntletWalls", new Color(.13f, .19f, .24f)),
                    MaterialAsset("GauntletLanes", new Color(.43f, .48f, .4f)),
                    MaterialAsset("GauntletBackdrop", new Color(.08f, .105f, .13f)));
                AddSporellas("CP34_01_First_Weave", sporella, 6, 1);
                AddSporellas("CP34_02_Crossed_Spores", sporella, 12, 2);

                var sub = EditorSceneManager.OpenScene(Scenes + SporellaGauntlet + "/" + SporellaGauntlet + " Sub Scene.unity", OpenSceneMode.Single);
                var arena = UnityEngine.Object.FindFirstObjectByType<ArenaAuthoring>();
                arena.gameObject.AddComponent<NavigationArenaAuthoring>().settings =
                    AssetDatabase.LoadAssetAtPath<NavigationSettings>(Root + "Data/Settings/NavigationSettings.asset");
                var material = MaterialAsset("SporellaGauntletCover", new Color(.23f, .40f, .37f));
                AddProjectileCover(arena.transform, material, "West Cover", -6f, -2f, 3, 5);
                AddProjectileCover(arena.transform, material, "East Cover", 6f, 0f, 4, 3);
                AddProjectileCover(arena.transform, material, "North Cover", -3f, 10f, 3, 3);
                EditorSceneManager.MarkSceneDirty(sub);
                EditorSceneManager.SaveScene(sub);

                var bootstrap = EditorSceneManager.OpenScene(CampaignBuildRegistration.BootstrapPath, OpenSceneMode.Single);
                using (var data = new SerializedObject(UnityEngine.Object.FindFirstObjectByType<GauntletSequence>()))
                {
                    var names = data.FindProperty("levelSceneNames");
                    var titles = data.FindProperty("levelDisplayNames");
                    int slot = 0;
                    while (slot < names.arraySize && names.GetArrayElementAtIndex(slot).stringValue != SporellaGauntlet) slot++;
                    if (slot == names.arraySize) names.arraySize++;
                    if (slot >= titles.arraySize) titles.arraySize = slot + 1;
                    names.GetArrayElementAtIndex(slot).stringValue = SporellaGauntlet;
                    titles.GetArrayElementAtIndex(slot).stringValue = "34 " + SporellaTitle;
                    data.ApplyModifiedPropertiesWithoutUndo();
                }
                EditorSceneManager.MarkSceneDirty(bootstrap);
                EditorSceneManager.SaveScene(bootstrap);
                AssetDatabase.SaveAssets();
            }
            finally { EditorSceneManager.RestoreSceneManagerSetup(previous); }
        }

        private static void AddSporellas(string waveName, EnemySpawnSettings settings, int baselines, int shooters)
        {
            using var wave = new SerializedObject(AssetDatabase.LoadAssetAtPath<EnemyWaveSettings>(Waves + waveName + ".asset"));
            wave.FindProperty("totalEnemyCount").intValue = baselines + shooters;
            var entries = wave.FindProperty("enemies");
            entries.arraySize = 2;
            var sporella = entries.GetArrayElementAtIndex(1);
            sporella.FindPropertyRelative("Settings").objectReferenceValue = settings;
            sporella.FindPropertyRelative("MinimumCount").intValue = shooters;
            sporella.FindPropertyRelative("Weight").floatValue = 0f;
            wave.FindProperty("replenishWhileBossLives").boolValue = false;
            wave.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
