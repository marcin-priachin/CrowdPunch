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
        [MenuItem("Crowd Punch/Levels/Build Trail Gauntlet 19")]
        public static void BuildTrail()
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
                if (profiles[7] == null) throw new InvalidOperationException("Rebuild the Trail prefab first.");
                var range = new[] { Range(0, 3, 24, 20) };
                var design = new Level { Name = "Slippery Circuit", Outline = Clipped(32, 34, 3),
                    Spacing = new Vector2(27.5f, 29.5f), Entry = new Vector2(0, -12),
                    Lanes = Array.Empty<Vector4>(),
                    Waves = new[] {
                        new Wave { Name = "First Trail", B = 6, Trail = 1, Delay = 2, Ranges = range },
                        new Wave { Name = "Two Trails", B = 12, Trail = 2, Delay = 3, Ranges = range }
                    } };
                BuildLevel(18, design, profiles, AssetDatabase.LoadAssetAtPath<Material>(Root + "Materials/Ground.mat"),
                    MaterialAsset("GauntletWalls", new Color(.13f,.19f,.24f)),
                    MaterialAsset("GauntletLanes", new Color(.43f,.48f,.4f)),
                    MaterialAsset("GauntletBackdrop", new Color(.08f,.105f,.13f)));
                var sub = EditorSceneManager.OpenScene(Scenes + "Gauntlet_19/Gauntlet_19 Sub Scene.unity", OpenSceneMode.Single);
                var arena = UnityEngine.Object.FindFirstObjectByType<ArenaAuthoring>();
                var navigation = arena.gameObject.AddComponent<NavigationArenaAuthoring>();
                navigation.settings = AssetDatabase.LoadAssetAtPath<NavigationSettings>(Root + "Data/Settings/NavigationSettings.asset");
                var encounter = UnityEngine.Object.FindFirstObjectByType<EnemyWaveSequenceAuthoring>();
                var wd = new SerializedObject(encounter); wd.FindProperty("minimumPlayerDistance").floatValue = 4;
                wd.ApplyModifiedPropertiesWithoutUndo();
                EditorSceneManager.MarkSceneDirty(sub); EditorSceneManager.SaveScene(sub);
                var main = EditorSceneManager.OpenScene(Scenes + "Gauntlet_19.unity", OpenSceneMode.Single);
                var marker = new SerializedObject(UnityEngine.Object.FindFirstObjectByType<GauntletLevel>());
                marker.FindProperty("openingHint").stringValue = "Trail enemies leave damaging paths. Launch one to draw a wider path through the crowd. Trails hurt you even while dashing.";
                marker.ApplyModifiedPropertiesWithoutUndo(); EditorSceneManager.MarkSceneDirty(main); EditorSceneManager.SaveScene(main);
                var bootstrap = EditorSceneManager.OpenScene(Root + "Scenes/Bootstrap.unity", OpenSceneMode.Single);
                var sequence = new SerializedObject(UnityEngine.Object.FindFirstObjectByType<GauntletSequence>());
                var names = sequence.FindProperty("levelSceneNames"); var titles = sequence.FindProperty("levelDisplayNames");
                names.arraySize = titles.arraySize = Mathf.Max(19, names.arraySize);
                names.GetArrayElementAtIndex(18).stringValue = "Gauntlet_19";
                titles.GetArrayElementAtIndex(18).stringValue = "19 Slippery Circuit";
                sequence.ApplyModifiedPropertiesWithoutUndo(); EditorSceneManager.MarkSceneDirty(bootstrap); EditorSceneManager.SaveScene(bootstrap);
                var build = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
                build.RemoveAll(s => s.path == Scenes + "Gauntlet_19.unity");
                int boss = build.FindIndex(s => s.path == Scenes + "Gauntlet_18.unity");
                build.Insert(boss + 1, new EditorBuildSettingsScene(Scenes + "Gauntlet_19.unity", true));
                EditorBuildSettings.scenes = build.ToArray();
                GauntletNatureEnvironment.ApplyLevel(19);
                AssetDatabase.SaveAssets();
            }
            finally { EditorSceneManager.RestoreSceneManagerSetup(previous); }
        }

    }
}

