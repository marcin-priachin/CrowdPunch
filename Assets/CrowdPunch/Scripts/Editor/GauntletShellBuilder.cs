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
        [MenuItem("Crowd Punch/Levels/Build Shell Gauntlet 15")]
        public static void BuildShell()
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
                var tuning = AssetDatabase.LoadAssetAtPath<ShellTargetSettings>(Root + "Data/Settings/ShellTargetSettings.asset");
                if (tuning == null)
                {
                    tuning = ScriptableObject.CreateInstance<ShellTargetSettings>();
                    tuning.coreMaxHealth = profiles[0].Health.Max;
                    AssetDatabase.CreateAsset(tuning, Root + "Data/Settings/ShellTargetSettings.asset");
                }
                var solidSettings = AssetDatabase.LoadAssetAtPath<BarricadeSettings>(Root + "Data/Settings/ShellSolidSettings.asset");
                if (solidSettings == null)
                {
                    solidSettings = ScriptableObject.CreateInstance<BarricadeSettings>();
                    solidSettings.requiredHits = 1;
                    AssetDatabase.CreateAsset(solidSettings, Root + "Data/Settings/ShellSolidSettings.asset");
                }
                var design = new Level { Name = "Crack the Shell", Outline = Rectangle(30, 30),
                    Spacing = new Vector2(26, 26), Entry = new Vector2(0, -10), Lanes = Array.Empty<Vector4>(),
                    Waves = new[] { W("Shell Crowd", 12, new[] {
                        Range(-9, 0, 5, 20), Range(9, 0, 5, 20), Range(0, 9, 12, 4), Range(0, -9, 12, 4) }, x: 2, delay: 2) } };
                BuildLevel(14, design, profiles, AssetDatabase.LoadAssetAtPath<Material>(Root + "Materials/Ground.mat"),
                    MaterialAsset("GauntletWalls", new Color(.13f,.19f,.24f)),
                    MaterialAsset("GauntletLanes", new Color(.43f,.48f,.4f)),
                    MaterialAsset("GauntletBackdrop", new Color(.08f,.105f,.13f)));
                var sub = EditorSceneManager.OpenScene(Scenes + "Gauntlet_15/Gauntlet_15 Sub Scene.unity", OpenSceneMode.Single);
                var arena = UnityEngine.Object.FindFirstObjectByType<ArenaAuthoring>();
                var navigation = arena.gameObject.AddComponent<NavigationArenaAuthoring>();
                navigation.settings = AssetDatabase.LoadAssetAtPath<NavigationSettings>(Root + "Data/Settings/NavigationSettings.asset");
                navigation.overrideParticipationAnchor = true; navigation.participationAnchor = new Vector2(0, -8);
                var encounter = UnityEngine.Object.FindFirstObjectByType<EnemyWaveSequenceAuthoring>();
                var encounterData = new SerializedObject(encounter);
                encounterData.FindProperty("minimumPlayerDistance").floatValue = 4;
                encounterData.ApplyModifiedPropertiesWithoutUndo();
                var solid = new GameObject("Crack the Shell - explosion shell and damageable core").AddComponent<BarricadeAuthoring>();
                solid.transform.SetParent(arena.transform);
                solid.settings = solidSettings; solid.size = new Vector3(3.2f, 4, 3.2f);
                solid.transform.position = new Vector3(0, 1, 0); solid.completeOnDestruction = true;
                solid.gameObject.AddComponent<ShellTargetAuthoring>().settings = tuning;
                encounter.barricade = solid;
                var shell = MaterialAsset("ShellArmor", new Color(.75f, .4f, .16f));
                var core = MaterialAsset("ShellCore", new Color(.18f, .9f, .8f));
                var crack = MaterialAsset("ShellCrack", new Color(.045f, .025f, .02f));
                ShellPiece(solid, "Turquoise core", Vector3.zero, new Vector3(3.05f, 3.85f, 3.05f), core, true, 0, Vector3.down);
                for (int face = 0; face < 4; face++)
                {
                    var rotation = Quaternion.Euler(0, face * 90, 0);
                    for (int band = 0; band < 3; band++)
                    {
                        var position = rotation * new Vector3(0, band * 1.3f - 1.3f, -1.59f);
                        var panel = ShellPiece(solid, "Shell plate " + face + "-" + band, position,
                            new Vector3(3.1f, 1.18f, .22f), shell, false, 0, rotation * new Vector3(0, -1, -3));
                        panel.transform.localRotation = rotation;
                    }
                    for (int stage = 1; stage <= 2; stage++)
                    {
                        var panel = ShellPiece(solid, "Shell crack " + face + "-" + stage,
                            rotation * new Vector3(stage * .8f - 1.2f, 0, -1.72f), new Vector3(.16f, 3.7f, .03f),
                            crack, false, stage, rotation * new Vector3(0, -1, -3));
                        panel.transform.localRotation = rotation * Quaternion.Euler(0, 0, stage == 1 ? 12 : -17);
                        var scar = ShellPiece(solid, "Core damage " + face + "-" + stage,
                            rotation * new Vector3(0, stage - 1.5f, -1.54f), new Vector3(2.9f, .17f, .03f),
                            crack, true, stage, Vector3.down);
                        scar.transform.localRotation = rotation;
                    }
                }
                EditorSceneManager.MarkSceneDirty(sub); EditorSceneManager.SaveScene(sub);
                var main = EditorSceneManager.OpenScene(Scenes + "Gauntlet_15.unity", OpenSceneMode.Single);
                var marker = new SerializedObject(UnityEngine.Object.FindFirstObjectByType<GauntletLevel>());
                marker.FindProperty("openingHint").stringValue = "Break the shell with explosions. Then destroy the exposed core.";
                marker.ApplyModifiedPropertiesWithoutUndo(); EditorSceneManager.MarkSceneDirty(main); EditorSceneManager.SaveScene(main);
                var bootstrap = EditorSceneManager.OpenScene(Root + "Scenes/Bootstrap.unity", OpenSceneMode.Single);
                var sequence = new SerializedObject(UnityEngine.Object.FindFirstObjectByType<GauntletSequence>());
                var names = sequence.FindProperty("levelSceneNames"); var titles = sequence.FindProperty("levelDisplayNames");
                names.arraySize = titles.arraySize = Mathf.Max(15, names.arraySize);
                names.GetArrayElementAtIndex(14).stringValue = "Gauntlet_15";
                titles.GetArrayElementAtIndex(14).stringValue = "15 Crack the Shell";
                sequence.ApplyModifiedPropertiesWithoutUndo(); EditorSceneManager.MarkSceneDirty(bootstrap); EditorSceneManager.SaveScene(bootstrap);
                var build = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
                if (!build.Exists(s => s.path == Scenes + "Gauntlet_15.unity"))
                    build.Insert(build.FindIndex(s => s.path == Scenes + "Gauntlet_14.unity") + 1,
                        new EditorBuildSettingsScene(Scenes + "Gauntlet_15.unity", true));
                EditorBuildSettings.scenes = build.ToArray();
                AssetDatabase.SaveAssets(); GauntletNatureEnvironment.ApplyLevel(15);
            }
            finally { EditorSceneManager.RestoreSceneManagerSetup(previous); }
        }

        private static GameObject ShellPiece(BarricadeAuthoring target, string name, Vector3 position,
            Vector3 size, Material material, bool core, int stage, Vector3 debris)
        {
            var piece = BarricadePiece(target, name, position, size, material, stage, debris);
            var visual = piece.GetComponent<BarricadeVisualAuthoring>();
            visual.shellVisual = true; visual.shellCore = core;
            return piece;
        }
    }
}
