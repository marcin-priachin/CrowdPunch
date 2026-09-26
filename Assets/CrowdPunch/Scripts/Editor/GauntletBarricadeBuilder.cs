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
        [MenuItem("Crowd Punch/Levels/Build Barricade Gauntlet 13")]
        public static void BuildBarricade()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode before authoring.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save open scenes before authoring.");
            var previous = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                const string tuningPath = Root + "Data/Settings/BarricadeSettings.asset";
                var tuning = AssetDatabase.LoadAssetAtPath<BarricadeSettings>(tuningPath);
                if (tuning == null)
                {
                    tuning = ScriptableObject.CreateInstance<BarricadeSettings>();
                    AssetDatabase.CreateAsset(tuning, tuningPath);
                }
                var profiles = new EnemySpawnSettings[ProfileNames.Length];
                for (int i = 0; i < profiles.Length; i++)
                    profiles[i] = AssetDatabase.LoadAssetAtPath<EnemySpawnSettings>(Root + "Data/Settings/Enemies/" + ProfileNames[i] + ".asset");
                var design = new Level { Name = "Break Through", Outline = Rectangle(28, 36),
                    Spacing = new Vector2(24, 22), Entry = new Vector2(0, -12),
                    Lanes = new[] { new Vector4(0, 0, 3, 26) },
                    Waves = new[] { W("Barricade Crowd", 14, new[] { Range(0, -1, 23, 20) }, x: 2, delay: 2) } };
                BuildLevel(12, design, profiles, AssetDatabase.LoadAssetAtPath<Material>(Root + "Materials/Ground.mat"),
                    MaterialAsset("GauntletWalls", new Color(.13f,.19f,.24f)),
                    MaterialAsset("GauntletLanes", new Color(.43f,.48f,.4f)),
                    MaterialAsset("GauntletBackdrop", new Color(.08f,.105f,.13f)));
                var sub = EditorSceneManager.OpenScene(Scenes + "Gauntlet_13/Gauntlet_13 Sub Scene.unity", OpenSceneMode.Single);
                var arena = UnityEngine.Object.FindFirstObjectByType<ArenaAuthoring>();
                arena.transform.position = new Vector3(0, 1, -2);
                var encounter = UnityEngine.Object.FindFirstObjectByType<EnemyWaveSequenceAuthoring>();
                var wd = new SerializedObject(encounter);
                wd.FindProperty("minimumPlayerDistance").floatValue = 4;
                wd.ApplyModifiedPropertiesWithoutUndo();

                var wall = new GameObject("Barricade - shared three hit objective").AddComponent<BarricadeAuthoring>();
                wall.settings = tuning;
                wall.size = new Vector3(28, 4, 1.2f);
                wall.transform.position = new Vector3(0, 1, 11);
                var exit = new GameObject("Exposed Exit").transform;
                exit.position = new Vector3(0, .5f, 15);
                wall.exit = exit;
                encounter.barricade = wall;
                var metal = MaterialAsset("BarricadeMetal", new Color(.22f, .34f, .39f));
                var frame = MaterialAsset("BarricadeFrame", new Color(.95f, .59f, .15f));
                var crack = MaterialAsset("BarricadeCracks", new Color(.025f, .035f, .04f));
                BarricadePiece(wall, "Solid plate", Vector3.zero, new Vector3(28, 4, 1.2f), metal, 0, new Vector3(0, -3, 2));
                for (int i = 0; i < 7; i++)
                    BarricadePiece(wall, "Reinforcing rib", new Vector3(-12 + i * 4, 0, -.68f),
                        new Vector3(.22f, 4, .2f), frame, 0, new Vector3((i - 3) * .8f, 1, -2));
                for (int stage = 1; stage <= 2; stage++)
                    for (int i = 0; i < 9; i++)
                    {
                        var piece = BarricadePiece(wall, "Damage crack " + stage, new Vector3(-10 + i * 2.5f, stage == 1 ? -.45f : .6f, -.72f),
                            new Vector3(2.8f, .13f, .07f), crack, stage, new Vector3(0, -2, -1));
                        piece.transform.localRotation = Quaternion.Euler(0, 0, i % 2 == 0 ? 18 : -18);
                    }
                var exitMaterial = MaterialAsset("BarricadeExit", new Color(.2f, .95f, .55f));
                Box("Exit landing", exit, new Vector3(0, -.96f, 15), new Vector3(4, .06f, 3), exitMaterial, false);
                Box("Exit left post", exit, new Vector3(-2, .5f, 16), new Vector3(.25f, 3, .25f), exitMaterial, false);
                Box("Exit right post", exit, new Vector3(2, .5f, 16), new Vector3(.25f, 3, .25f), exitMaterial, false);
                EditorSceneManager.MarkSceneDirty(sub); EditorSceneManager.SaveScene(sub);
                var main = EditorSceneManager.OpenScene(Scenes + "Gauntlet_13.unity", OpenSceneMode.Single);
                var marker = new SerializedObject(UnityEngine.Object.FindFirstObjectByType<GauntletLevel>());
                marker.FindProperty("openingHint").stringValue = "Launch enemies into the barricade, then reach the green exit. Punches alone cannot break it.";
                marker.ApplyModifiedPropertiesWithoutUndo(); EditorSceneManager.MarkSceneDirty(main); EditorSceneManager.SaveScene(main);
                var bootstrap = EditorSceneManager.OpenScene(Root + "Scenes/Bootstrap.unity", OpenSceneMode.Single);
                var sequence = new SerializedObject(UnityEngine.Object.FindFirstObjectByType<GauntletSequence>());
                var names = sequence.FindProperty("levelSceneNames"); var titles = sequence.FindProperty("levelDisplayNames");
                names.arraySize = titles.arraySize = Mathf.Max(13, names.arraySize);
                names.GetArrayElementAtIndex(12).stringValue = "Gauntlet_13";
                titles.GetArrayElementAtIndex(12).stringValue = "13 Break Through";
                sequence.ApplyModifiedPropertiesWithoutUndo(); EditorSceneManager.MarkSceneDirty(bootstrap); EditorSceneManager.SaveScene(bootstrap);
                var build = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
                if (!build.Exists(s => s.path == Scenes + "Gauntlet_13.unity"))
                {
                    int previousLevel = build.FindIndex(s => s.path == Scenes + "Gauntlet_12.unity");
                    build.Insert(previousLevel + 1, new EditorBuildSettingsScene(Scenes + "Gauntlet_13.unity", true));
                }
                EditorBuildSettings.scenes = build.ToArray();
                AssetDatabase.SaveAssets();
                GauntletNatureEnvironment.ApplyLevel(13);
            }
            finally { EditorSceneManager.RestoreSceneManagerSetup(previous); }
        }

        private static GameObject BarricadePiece(BarricadeAuthoring wall, string name, Vector3 position,
            Vector3 size, Material material, int stage, Vector3 debris)
        {
            var piece = Box(name, wall.transform, wall.transform.position + position, size, material, false);
            var visual = piece.AddComponent<BarricadeVisualAuthoring>();
            visual.barricade = wall; visual.crackStage = stage; visual.debrisDirection = debris;
            return piece;
        }
    }
}
