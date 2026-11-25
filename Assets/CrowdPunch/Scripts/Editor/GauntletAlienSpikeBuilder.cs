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
        private const string AlienSpikeGauntletTitle = "Spike Crossfire";

        [MenuItem("Crowd Punch/Levels/Build Alien Spike Gauntlet 32")]
        public static void BuildAlienSpike()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play mode before authoring.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save open scenes before authoring.");
            var setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                var spike = EnemyAlienSpikePrefabBuilder.Build();
                var profiles = new EnemySpawnSettings[ProfileNames.Length];
                for (int i = 0; i < profiles.Length; i++)
                    profiles[i] = AssetDatabase.LoadAssetAtPath<EnemySpawnSettings>(Root + "Data/Settings/Enemies/" + ProfileNames[i] + ".asset");

                var design = new Level
                {
                    Name = AlienSpikeGauntletTitle, Outline = Rectangle(32, 32), Spacing = new Vector2(32, 32),
                    Entry = new Vector2(0, -10), Lanes = Array.Empty<Vector4>(),
                    Waves = new[]
                    {
                        W("Read the Gaps", 5, new[] { Range(0, 4, 15, 8) }, delay: 1.5f),
                        new Wave { Name = "Crossfire", B = 11, A = 1, Delay = 2,
                            Ranges = new[] { Range(-6, 4, 10, 15), Range(6, 4, 10, 15) } }
                    }
                };
                var wall = MaterialAsset("GauntletWalls", new Color(.13f, .19f, .24f));
                BuildLevel(31, design, profiles, AssetDatabase.LoadAssetAtPath<Material>(Root + "Materials/Ground.mat"),
                    wall, MaterialAsset("GauntletLanes", new Color(.43f, .48f, .4f)),
                    MaterialAsset("GauntletBackdrop", new Color(.08f, .105f, .13f)));
                AddAlienSpikes("CP32_01_Read_the_Gaps", spike, 1);
                AddAlienSpikes("CP32_02_Crossfire", spike, 3);

                var sub = EditorSceneManager.OpenScene(Scenes + "Gauntlet_32/Gauntlet_32 Sub Scene.unity", OpenSceneMode.Single);
                var arena = UnityEngine.Object.FindFirstObjectByType<ArenaAuthoring>();
                arena.gameObject.AddComponent<NavigationArenaAuthoring>().settings =
                    AssetDatabase.LoadAssetAtPath<NavigationSettings>(Root + "Data/Settings/NavigationSettings.asset");
                for (int side = -1; side <= 1; side += 2)
                {
                    var cover = new GameObject(side < 0 ? "West solid cover" : "East solid cover");
                    cover.transform.SetParent(arena.transform);
                    cover.transform.position = new Vector3(side * 10, -1, side * 2);
                    var solid = cover.AddComponent<SolidObstacleAuthoring>();
                    solid.footprint = new Vector2Int(2, 3);
                    solid.height = 1.75f;
                    Box("Cover", cover.transform, cover.transform.position + .875f * Vector3.up,
                        new Vector3(2, 1.75f, 3), wall, false);
                }
                EditorSceneManager.MarkSceneDirty(sub);
                EditorSceneManager.SaveScene(sub);


                var bootstrap = EditorSceneManager.OpenScene(Root + "Scenes/Bootstrap.unity", OpenSceneMode.Single);
                var sequence = new SerializedObject(UnityEngine.Object.FindFirstObjectByType<GauntletSequence>());
                var names = sequence.FindProperty("levelSceneNames");
                var titles = sequence.FindProperty("levelDisplayNames");
                names.arraySize = titles.arraySize = Math.Max(32, names.arraySize);
                names.GetArrayElementAtIndex(31).stringValue = "Gauntlet_32";
                titles.GetArrayElementAtIndex(31).stringValue = "32 " + AlienSpikeGauntletTitle;
                sequence.ApplyModifiedPropertiesWithoutUndo();
                EditorSceneManager.SaveScene(bootstrap);
                AssetDatabase.SaveAssets();
            }
            finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }
        }

        private static void AddAlienSpikes(string name, EnemySpawnSettings spike, int count)
        {
            var wave = new SerializedObject(AssetDatabase.LoadAssetAtPath<EnemyWaveSettings>(Waves + name + ".asset"));
            wave.FindProperty("totalEnemyCount").intValue += count;
            var entries = wave.FindProperty("enemies");
            var entry = entries.GetArrayElementAtIndex(entries.arraySize++);
            entry.FindPropertyRelative("Settings").objectReferenceValue = spike;
            entry.FindPropertyRelative("MinimumCount").intValue = count;
            entry.FindPropertyRelative("Weight").floatValue = 0;
            wave.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}

