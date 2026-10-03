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
        [MenuItem("Crowd Punch/Levels/Build Protected Point Gauntlet 18")]
        public static void BuildProtectedPoint()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode before authoring.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save open scenes before authoring.");
            var previous = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                var profiles = new EnemySpawnSettings[ProfileNames.Length];
                for (int i = 0; i < profiles.Length; i++)
                {
                    profiles[i] = AssetDatabase.LoadAssetAtPath<EnemySpawnSettings>(Root + "Data/Settings/Enemies/" + ProfileNames[i] + ".asset");
                    if (profiles[i] == null) throw new InvalidOperationException("Missing profile: " + ProfileNames[i]);
                }
                string settingsPath = Root + "Data/Settings/ProtectedPointSettings.asset";
                var settings = AssetDatabase.LoadAssetAtPath<ProtectedPointSettings>(settingsPath);
                if (settings == null)
                {
                    settings = ScriptableObject.CreateInstance<ProtectedPointSettings>();
                    AssetDatabase.CreateAsset(settings, settingsPath);
                }
                var ranges = new[] { Range(0, 41, 24, 8) };
                var design = new Level { Name = "Hold the Line", Outline = Rectangle(32, 96),
                    Spacing = new Vector2(30, 94), Entry = new Vector2(0, -36), Lanes = Array.Empty<Vector4>(),
                    Waves = new[] {
                        new Wave { Name = "First Advance", B = 14, X = 2, Delay = 3, Batch = 4, Interval = 3, Ranges = ranges },
                        new Wave { Name = "Mixed Advance", B = 16, X = 4, R = 2, D = 2, Delay = 5, Batch = 4, Interval = 3, Ranges = ranges },
                        new Wave { Name = "Final Advance", B = 20, X = 4, R = 3, D = 3, A = 1, Wizard = 1, Delay = 5, Batch = 4, Interval = 3, Ranges = ranges }
                    } };
                BuildLevel(17, design, profiles, AssetDatabase.LoadAssetAtPath<Material>(Root + "Materials/Ground.mat"),
                    MaterialAsset("GauntletWalls", new Color(.13f, .19f, .24f)),
                    MaterialAsset("GauntletLanes", new Color(.43f, .48f, .4f)),
                    MaterialAsset("GauntletBackdrop", new Color(.08f, .105f, .13f)));
                var sub = EditorSceneManager.OpenScene(Scenes + "Gauntlet_18/Gauntlet_18 Sub Scene.unity", OpenSceneMode.Single);
                var arena = UnityEngine.Object.FindFirstObjectByType<ArenaAuthoring>();
                arena.gameObject.AddComponent<NavigationArenaAuthoring>().settings =
                    AssetDatabase.LoadAssetAtPath<NavigationSettings>(Root + "Data/Settings/NavigationSettings.asset");
                var encounter = UnityEngine.Object.FindFirstObjectByType<EnemyWaveSequenceAuthoring>();
                var objective = encounter.gameObject.AddComponent<ProtectedPointAuthoring>();
                objective.settings = settings;
                var waveData = new SerializedObject(encounter);
                waveData.FindProperty("minimumPlayerDistance").floatValue = 4;
                waveData.ApplyModifiedPropertiesWithoutUndo();
                // PROTECT-004: this finite encounter must not inherit puzzle ammunition supply.
                for (int i = 0; i < design.Waves.Length; i++)
                {
                    var asset = AssetDatabase.LoadAssetAtPath<EnemyWaveSettings>(Waves + $"CP18_{i+1:00}_" + design.Waves[i].Name.Replace(' ', '_') + ".asset");
                    var data = new SerializedObject(asset);
                    data.FindProperty("armoredAmmunitionProfile").objectReferenceValue = null;
                    data.FindProperty("wizardAmmunitionProfile").objectReferenceValue = null;
                    data.FindProperty("waitForPersistentHazards").boolValue = false;
                    data.FindProperty("replenishWhileBossLives").boolValue = false;
                    data.ApplyModifiedPropertiesWithoutUndo();
                }
                var zoneMaterial = MaterialAsset("ProtectedPointZone", new Color(.15f, .75f, .68f));
                Box("Protected Zone 8 x 4", encounter.transform, new Vector3(0, -.97f, -46), new Vector3(8, .035f, 4), zoneMaterial, false);
                EditorSceneManager.MarkSceneDirty(sub); EditorSceneManager.SaveScene(sub);
                var main = EditorSceneManager.OpenScene(Scenes + "Gauntlet_18.unity", OpenSceneMode.Single);
                var marker = new SerializedObject(UnityEngine.Object.FindFirstObjectByType<GauntletLevel>());
                marker.FindProperty("openingHint").stringValue = "Protect the turquoise zone. Stop three waves before active enemies reach it.";
                marker.ApplyModifiedPropertiesWithoutUndo(); EditorSceneManager.MarkSceneDirty(main); EditorSceneManager.SaveScene(main);
                var bootstrap = EditorSceneManager.OpenScene(Root + "Scenes/Bootstrap.unity", OpenSceneMode.Single);
                var sequence = new SerializedObject(UnityEngine.Object.FindFirstObjectByType<GauntletSequence>());
                var names = sequence.FindProperty("levelSceneNames"); var titles = sequence.FindProperty("levelDisplayNames");
                names.arraySize = titles.arraySize = Mathf.Max(18, names.arraySize);
                names.GetArrayElementAtIndex(17).stringValue = "Gauntlet_18";
                titles.GetArrayElementAtIndex(17).stringValue = "18 Hold the Line";
                sequence.ApplyModifiedPropertiesWithoutUndo(); EditorSceneManager.MarkSceneDirty(bootstrap); EditorSceneManager.SaveScene(bootstrap);
                var build = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
                build.RemoveAll(s => s.path == Scenes + "Gauntlet_18.unity");
                int previousLevel = build.FindIndex(s => s.path == Scenes + "Gauntlet_17.unity");
                build.Insert(previousLevel + 1, new EditorBuildSettingsScene(Scenes + "Gauntlet_18.unity", true));
                EditorBuildSettings.scenes = build.ToArray();
                AssetDatabase.SaveAssets();
            }
            finally { EditorSceneManager.RestoreSceneManagerSetup(previous); }
        }
    }
}
