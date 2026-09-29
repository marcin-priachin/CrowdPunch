using System;
using System.Collections.Generic;
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
        [MenuItem("Crowd Punch/Levels/Build Track Gauntlet 16")]
        public static void BuildTrack()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode before authoring.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save open scenes before authoring.");
            var previous = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                var profiles = new EnemySpawnSettings[ProfileNames.Length];
                for (int i = 0; i < profiles.Length; i++)
                    profiles[i] = AssetDatabase.LoadAssetAtPath<EnemySpawnSettings>(Root + "Data/Settings/Enemies/" + ProfileNames[i] + ".asset");
                var tuning = AssetDatabase.LoadAssetAtPath<TrackObjectSettings>(Root + "Data/Settings/TrackObjectSettings.asset");
                if (tuning == null)
                {
                    tuning = ScriptableObject.CreateInstance<TrackObjectSettings>();
                    AssetDatabase.CreateAsset(tuning, Root + "Data/Settings/TrackObjectSettings.asset");
                }
                var solidSettings = AssetDatabase.LoadAssetAtPath<BarricadeSettings>(Root + "Data/Settings/TrackSolidSettings.asset");
                if (solidSettings == null)
                {
                    solidSettings = ScriptableObject.CreateInstance<BarricadeSettings>();
                    solidSettings.requiredHits = 1;
                    AssetDatabase.CreateAsset(solidSettings, Root + "Data/Settings/TrackSolidSettings.asset");
                }
                var ranges = new[] { Range(-9, 0, 5, 22), Range(9, 0, 5, 22), Range(0, 10, 12, 3), Range(0, -10, 12, 3) };
                var design = new Level { Name = "Knock Into Place", Outline = Rectangle(32, 34),
                    Spacing = new Vector2(28, 30), Entry = new Vector2(0, -12), Lanes = Array.Empty<Vector4>(),
                    Waves = new[] { W("Track Baselines", 12, ranges, delay: 2) } };
                BuildLevel(15, design, profiles, AssetDatabase.LoadAssetAtPath<Material>(Root + "Materials/Ground.mat"),
                    MaterialAsset("GauntletWalls", new Color(.13f,.19f,.24f)),
                    MaterialAsset("GauntletLanes", new Color(.43f,.48f,.4f)),
                    MaterialAsset("GauntletBackdrop", new Color(.08f,.105f,.13f)));
                var sub = EditorSceneManager.OpenScene(Scenes + "Gauntlet_16/Gauntlet_16 Sub Scene.unity", OpenSceneMode.Single);
                var arena = UnityEngine.Object.FindFirstObjectByType<ArenaAuthoring>();
                var navigation = arena.gameObject.AddComponent<NavigationArenaAuthoring>();
                navigation.settings = AssetDatabase.LoadAssetAtPath<NavigationSettings>(Root + "Data/Settings/NavigationSettings.asset");
                navigation.overrideParticipationAnchor = true; navigation.participationAnchor = new Vector2(-10, -10);
                var baseline = UnityEngine.Object.FindFirstObjectByType<EnemyWaveSequenceAuthoring>();
                var solid = new GameObject("Knock Into Place - sliding objective").AddComponent<BarricadeAuthoring>();
                solid.settings = solidSettings; solid.size = new Vector3(3.6f, 3.4f, 3.2f);
                solid.transform.position = new Vector3(0, .7f, -5); solid.completeOnDestruction = true;
                var track = solid.gameObject.AddComponent<TrackObjectAuthoring>(); track.settings = tuning;
                var socket = new GameObject("End Socket").transform; socket.position = new Vector3(0, .7f, 5);
                track.destination = socket;
                baseline.barricade = solid;
                var baselineData = new SerializedObject(baseline);
                baselineData.FindProperty("minimumPlayerDistance").floatValue = 4;
                baselineData.ApplyModifiedPropertiesWithoutUndo();
                var explosive = new GameObject("Replenishing Explosives - delayed introduction").AddComponent<EnemyWaveSequenceAuthoring>();
                explosive.barricade = solid;
                var explosiveData = new SerializedObject(explosive);
                var list = explosiveData.FindProperty("waves"); list.arraySize = 1;
                // Opening the SubScene can unload profiles unused by its initial Baseline wave.
                profiles[2] = AssetDatabase.LoadAssetAtPath<EnemySpawnSettings>(Root + "Data/Settings/Enemies/ExplosiveEnemySpawnSettings.asset");
                list.GetArrayElementAtIndex(0).objectReferenceValue = WaveAsset("CP16_02_Track_Explosives", W("Track Explosives", 0, ranges, x: 2, delay: 10), profiles);
                explosiveData.FindProperty("randomSeed").longValue = 16002;
                explosiveData.FindProperty("minimumPlayerDistance").floatValue = 4;
                explosiveData.FindProperty("placementAttemptsPerEnemy").intValue = 32;
                explosiveData.ApplyModifiedPropertiesWithoutUndo();

                var block = MaterialAsset("TrackBlock", new Color(.95f, .52f, .12f));
                var metal = MaterialAsset("TrackMetal", new Color(.13f, .2f, .28f));
                var arrow = MaterialAsset("TrackArrow", new Color(1, .88f, .4f));
                var destination = MaterialAsset("TrackSocket", new Color(.15f, .65f, .9f));
                Box("Chunky launch target", solid.transform, solid.transform.position + new Vector3(0, .15f, 0), new Vector3(3.3f, 3.1f, 2.9f), block, false);
                Box("Sliding base", solid.transform, solid.transform.position + new Vector3(0, -1.4f, 0), new Vector3(3.6f, .6f, 3.2f), metal, false);
                for (int side = -1; side <= 1; side += 2)
                {
                    Box("Track rail", arena.transform, new Vector3(side * 1.55f, -.95f, 0), new Vector3(.18f, .08f, 13.6f), metal, false);
                    for (int z = -6; z <= 6; z += 3)
                    {
                        for (int arm = -1; arm <= 1; arm += 2)
                        {
                            var chevron = Box("Direction arrow", arena.transform, new Vector3(side * 2.6f + arm * .28f, -.94f, z), new Vector3(.16f, .06f, 1.1f), arrow, false);
                            chevron.transform.rotation = Quaternion.Euler(0, arm * -35, 0);
                        }
                    }
                    var rim = Box("Socket rim", socket, new Vector3(side * 2, -.82f, 5), new Vector3(.22f, .32f, 4), destination, false);
                    rim.AddComponent<TrackSocketVisualAuthoring>().trackObject = track;
                }
                var stop = Box("Socket end stop", socket, new Vector3(0, -.82f, 7), new Vector3(4.2f, .32f, .22f), destination, false);
                stop.AddComponent<TrackSocketVisualAuthoring>().trackObject = track;
                EditorSceneManager.MarkSceneDirty(sub); EditorSceneManager.SaveScene(sub);
                var main = EditorSceneManager.OpenScene(Scenes + "Gauntlet_16.unity", OpenSceneMode.Single);
                var marker = new SerializedObject(UnityEngine.Object.FindFirstObjectByType<GauntletLevel>());
                marker.FindProperty("openingHint").stringValue = "Launch bodies or blast the block along the arrows into the blue socket. Hits from the far side push it back.";
                marker.ApplyModifiedPropertiesWithoutUndo(); EditorSceneManager.MarkSceneDirty(main); EditorSceneManager.SaveScene(main);
                var bootstrap = EditorSceneManager.OpenScene(Root + "Scenes/Bootstrap.unity", OpenSceneMode.Single);
                var sequence = new SerializedObject(UnityEngine.Object.FindFirstObjectByType<GauntletSequence>());
                var names = sequence.FindProperty("levelSceneNames"); var titles = sequence.FindProperty("levelDisplayNames");
                names.arraySize = titles.arraySize = Mathf.Max(16, names.arraySize);
                names.GetArrayElementAtIndex(15).stringValue = "Gauntlet_16";
                titles.GetArrayElementAtIndex(15).stringValue = "16 Knock Into Place";
                sequence.ApplyModifiedPropertiesWithoutUndo(); EditorSceneManager.MarkSceneDirty(bootstrap); EditorSceneManager.SaveScene(bootstrap);
                var build = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
                if (!build.Exists(s => s.path == Scenes + "Gauntlet_16.unity"))
                    build.Insert(build.FindIndex(s => s.path == Scenes + "Gauntlet_15.unity") + 1,
                        new EditorBuildSettingsScene(Scenes + "Gauntlet_16.unity", true));
                EditorBuildSettings.scenes = build.ToArray(); AssetDatabase.SaveAssets();
                GauntletNatureEnvironment.ApplyLevel(16);
            }
            finally { EditorSceneManager.RestoreSceneManagerSetup(previous); }
        }
    }
}
