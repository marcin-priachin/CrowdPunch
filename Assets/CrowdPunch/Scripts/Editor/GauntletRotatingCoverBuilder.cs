using System;
using System.Collections.Generic;
using CrowdPunch.Authoring;
using CrowdPunch.Configuration;
using CrowdPunch.Mono.Levels;
using CrowdPunch.Utilities;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CrowdPunch.Editor
{
    public static partial class GauntletProgressionBuilder
    {
        [MenuItem("Crowd Punch/Levels/Build Rotating Cover Gauntlet 14")]
        public static void BuildRotatingCover()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode before authoring.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save open scenes before authoring.");
            var previous = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                var targetSettings = AssetDatabase.LoadAssetAtPath<BarricadeSettings>(Root + "Data/Settings/RotatingTargetSettings.asset");
                if (targetSettings == null)
                {
                    targetSettings = ScriptableObject.CreateInstance<BarricadeSettings>();
                    targetSettings.requiredHits = 4;
                    AssetDatabase.CreateAsset(targetSettings, Root + "Data/Settings/RotatingTargetSettings.asset");
                }
                var tuning = AssetDatabase.LoadAssetAtPath<RotatingCoverSettings>(Root + "Data/Settings/RotatingCoverSettings.asset");
                if (tuning == null)
                {
                    tuning = ScriptableObject.CreateInstance<RotatingCoverSettings>();
                    AssetDatabase.CreateAsset(tuning, Root + "Data/Settings/RotatingCoverSettings.asset");
                }
                var profiles = new EnemySpawnSettings[ProfileNames.Length];
                for (int i = 0; i < profiles.Length; i++)
                    profiles[i] = AssetDatabase.LoadAssetAtPath<EnemySpawnSettings>(Root + "Data/Settings/Enemies/" + ProfileNames[i] + ".asset");
                var design = new Level { Name = "Return to Sender", Outline = Rectangle(34, 34),
                    Spacing = new Vector2(30, 30), Entry = new Vector2(0, -12), Lanes = Array.Empty<Vector4>(),
                    Waves = new[] { W("Rotating Cover Crowd", 14, new[] {
                        Range(-10, 0, 5, 24), Range(10, 0, 5, 24), Range(0, 10, 12, 5), Range(0, -10, 12, 5) }, x: 2, delay: 2) } };
                BuildLevel(13, design, profiles, AssetDatabase.LoadAssetAtPath<Material>(Root + "Materials/Ground.mat"),
                    MaterialAsset("GauntletWalls", new Color(.13f,.19f,.24f)),
                    MaterialAsset("GauntletLanes", new Color(.43f,.48f,.4f)),
                    MaterialAsset("GauntletBackdrop", new Color(.08f,.105f,.13f)));
                var sub = EditorSceneManager.OpenScene(Scenes + "Gauntlet_14/Gauntlet_14 Sub Scene.unity", OpenSceneMode.Single);
                var arena = UnityEngine.Object.FindFirstObjectByType<ArenaAuthoring>();
                var navigation = arena.gameObject.AddComponent<NavigationArenaAuthoring>();
                navigation.settings = AssetDatabase.LoadAssetAtPath<NavigationSettings>(Root + "Data/Settings/NavigationSettings.asset");
                navigation.overrideParticipationAnchor = true; navigation.participationAnchor = new Vector2(0, -10);
                var encounter = UnityEngine.Object.FindFirstObjectByType<EnemyWaveSequenceAuthoring>();
                var encounterData = new SerializedObject(encounter);
                encounterData.FindProperty("minimumPlayerDistance").floatValue = 4;
                encounterData.ApplyModifiedPropertiesWithoutUndo();
                var target = new GameObject("Central Target - four counted hits").AddComponent<BarricadeAuthoring>();
                target.settings = targetSettings; target.size = new Vector3(2.4f, 4, 2.4f);
                target.transform.position = new Vector3(0, 1, 0); target.completeOnDestruction = true;
                encounter.barricade = target;
                var cover = new GameObject("Rotating Cover").AddComponent<RotatingCoverAuthoring>();
                cover.transform.SetParent(arena.transform); cover.transform.position = new Vector3(0, 1, 0);
                cover.settings = tuning; cover.target = target; target.cover = cover;
                var metal = MaterialAsset("RotatingCoverMetal", new Color(.15f, .29f, .36f));
                var edge = MaterialAsset("RotatingCoverEdge", new Color(1, .56f, .12f));
                for (int i = 0; i < CoverGeometry.PanelCount; i++)
                {
                    var panel = Box("Cover panel " + i, cover.transform, cover.transform.position, Vector3.one,
                        i == 0 || i == CoverGeometry.PanelCount - 1 ? edge : metal, false).AddComponent<CoverPanelAuthoring>();
                    panel.cover = cover; panel.index = i;
                }
                var core = MaterialAsset("RotatingTargetCore", new Color(.28f, .85f, .65f));
                var crack = MaterialAsset("BarricadeCracks", new Color(.025f, .035f, .04f));
                BarricadePiece(target, "Target core", Vector3.zero, target.size, core, 0, new Vector3(0, -2, 0));
                for (int stage = 1; stage <= 3; stage++)
                    for (int face = 0; face < 4; face++)
                    {
                        var rotation = Quaternion.Euler(0, face * 90, 0);
                        var piece = BarricadePiece(target, "Damage band " + stage, rotation * new Vector3(0, stage - 2, -1.24f),
                            new Vector3(2.3f, .18f, .07f), crack, stage, rotation * new Vector3(0, -2, -1));
                        piece.transform.localRotation = rotation;
                    }
                // Low plinth makes the walking exclusion legible without visually closing the shot opening.
                var plinth = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                plinth.name = "Central enclosure plinth"; plinth.transform.position = new Vector3(0, -.75f, 0);
                plinth.transform.localScale = new Vector3((tuning.radius - tuning.thickness) * 2, .25f, (tuning.radius - tuning.thickness) * 2);
                UnityEngine.Object.DestroyImmediate(plinth.GetComponent<Collider>());
                plinth.GetComponent<Renderer>().sharedMaterial = metal;
                EditorSceneManager.MarkSceneDirty(sub); EditorSceneManager.SaveScene(sub);
                var main = EditorSceneManager.OpenScene(Scenes + "Gauntlet_14.unity", OpenSceneMode.Single);
                var marker = new SerializedObject(UnityEngine.Object.FindFirstObjectByType<GauntletLevel>());
                marker.FindProperty("openingHint").stringValue = "Launch through the rotating gap to break the core. The shield sends bodies back at you!";
                marker.ApplyModifiedPropertiesWithoutUndo(); EditorSceneManager.MarkSceneDirty(main); EditorSceneManager.SaveScene(main);
                var bootstrap = EditorSceneManager.OpenScene(Root + "Scenes/Bootstrap.unity", OpenSceneMode.Single);
                var sequence = new SerializedObject(UnityEngine.Object.FindFirstObjectByType<GauntletSequence>());
                var names = sequence.FindProperty("levelSceneNames"); var titles = sequence.FindProperty("levelDisplayNames");
                names.arraySize = titles.arraySize = Mathf.Max(14, names.arraySize);
                names.GetArrayElementAtIndex(13).stringValue = "Gauntlet_14";
                titles.GetArrayElementAtIndex(13).stringValue = "14 Return to Sender";
                sequence.ApplyModifiedPropertiesWithoutUndo(); EditorSceneManager.MarkSceneDirty(bootstrap); EditorSceneManager.SaveScene(bootstrap);
                var build = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
                if (!build.Exists(s => s.path == Scenes + "Gauntlet_14.unity"))
                    build.Insert(build.FindIndex(s => s.path == Scenes + "Gauntlet_13.unity") + 1,
                        new EditorBuildSettingsScene(Scenes + "Gauntlet_14.unity", true));
                EditorBuildSettings.scenes = build.ToArray();
                AssetDatabase.SaveAssets(); GauntletNatureEnvironment.ApplyLevel(14);
            }
            finally { EditorSceneManager.RestoreSceneManagerSetup(previous); }
        }
    }
}
