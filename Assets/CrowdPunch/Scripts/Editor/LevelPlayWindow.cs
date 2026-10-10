using System;
using System.Collections.Generic;
using System.IO;
using CrowdPunch.Mono.Levels;
using CrowdPunch.Configuration;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CrowdPunch.Editor
{
    /// <summary>Starts Bootstrap with a one-shot authored gauntlet selection.</summary>
    [InitializeOnLoad]
    public sealed class LevelPlayWindow : EditorWindow
    {
        private const string BootstrapPath = "Assets/CrowdPunch/Scenes/Bootstrap.unity";
        private const string SelectionKey = "CrowdPunch.LevelLauncher.Selection";
        private const string ActiveKey = "CrowdPunch.LevelLauncher.Active";
        private const string PreviousStartKey = "CrowdPunch.LevelLauncher.PreviousStart";
        private readonly List<string> sceneNames = new();
        private readonly List<string> displayNames = new();
        private int selectedIndex;
        private string error;
        private bool legacy;

        static LevelPlayWindow()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        [MenuItem("Window/Crowd Punch/Level Play")]
        [MenuItem("Crowd Punch/Levels/Level Play Window")]
        public static void Open() => GetWindow<LevelPlayWindow>("Level Play").Show();

        private void OnEnable()
        {
            minSize = new Vector2(360, 150);
            // Layout restoration calls OnEnable while scenes are still being restored.
            // Opening a preview scene here can re-enter Unity's scene loading.
            EditorApplication.delayCall -= RefreshLevelsAfterStartup;
            EditorApplication.delayCall += RefreshLevelsAfterStartup;
        }

        private void OnDisable() => EditorApplication.delayCall -= RefreshLevelsAfterStartup;

        private void RefreshLevelsAfterStartup()
        {
            if (this == null || EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += RefreshLevelsAfterStartup;
                return;
            }

            RefreshLevels();
            Repaint();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Start from a level", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Starts Bootstrap with isolated Editor progress. Legacy launches never change campaign saves. Stopping Play Mode returns to your editor scenes.", MessageType.Info);
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            {
                EditorGUI.BeginChangeCheck();
                legacy = GUILayout.Toolbar(legacy ? 1 : 0, new[] { "Campaign", "Legacy" }) == 1;
                if (EditorGUI.EndChangeCheck()) RefreshLevels();
                if (sceneNames.Count > 0)
                {
                    EditorGUI.BeginChangeCheck();
                    selectedIndex = EditorGUILayout.Popup("Level", selectedIndex, displayNames.ToArray());
                    if (EditorGUI.EndChangeCheck()) EditorPrefs.SetString(SelectionKey, sceneNames[selectedIndex]);
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Refresh Levels")) RefreshLevels();
                    using (new EditorGUI.DisabledScope(sceneNames.Count == 0))
                        if (GUILayout.Button("Play Selected Level")) StartSelectedLevel();
                }
            }
            if (!string.IsNullOrEmpty(error)) EditorGUILayout.HelpBox(error, MessageType.Error);
        }

        private void RefreshLevels()
        {
            sceneNames.Clear();
            displayNames.Clear();
            error = null;
            var preview = default(UnityEngine.SceneManagement.Scene);
            try
            {
                preview = EditorSceneManager.OpenPreviewScene(BootstrapPath);
                GauntletSequence sequence = null;
                foreach (GameObject root in preview.GetRootGameObjects())
                {
                    sequence = root.GetComponentInChildren<GauntletSequence>(true);
                    if (sequence != null) break;
                }
                if (sequence == null) throw new InvalidOperationException("Bootstrap has no GauntletSequence.");
                using var serialized = new SerializedObject(sequence);
                if (!legacy)
                {
                    var catalog = serialized.FindProperty("campaign").objectReferenceValue as CampaignCatalog;
                    if (catalog == null) throw new InvalidOperationException("Campaign has not been authored yet.");
                    for (int i = 0; i < catalog.Count; i++)
                    {
                        var level = catalog.Get(i);
                        if (!level.Available) continue;
                        sceneNames.Add(level.scenePath);
                        displayNames.Add($"{i + 1:00} {level.title}");
                    }
                }
                else
                {
                SerializedProperty names = serialized.FindProperty("levelSceneNames");
                SerializedProperty titles = serialized.FindProperty("levelDisplayNames");
                for (int index = 0; index < names.arraySize; index++)
                {
                    sceneNames.Add(names.GetArrayElementAtIndex(index).stringValue);
                    displayNames.Add(index < titles.arraySize ? titles.GetArrayElementAtIndex(index).stringValue : names.GetArrayElementAtIndex(index).stringValue);
                }
                }
                selectedIndex = Mathf.Max(0, sceneNames.IndexOf(EditorPrefs.GetString(SelectionKey, string.Empty)));
                if (sceneNames.Count == 0) error = "Bootstrap's level sequence is empty.";
            }
            catch (Exception exception) { error = exception.Message; }
            finally
            {
                if (preview.IsValid()) EditorSceneManager.ClosePreviewScene(preview);
            }
        }

        private void StartSelectedLevel()
        {
            string selectedName = sceneNames[selectedIndex];
            bool available = File.Exists(legacy ? $"Assets/CrowdPunch/Scenes/Gauntlets/{selectedName}.unity" : selectedName);
            SceneAsset bootstrap = AssetDatabase.LoadAssetAtPath<SceneAsset>(BootstrapPath);
            if (!available || bootstrap == null)
            {
                error = bootstrap == null ? "Bootstrap scene is missing." : $"Scene '{selectedName}' is missing.";
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            error = null;
            EditorPrefs.SetString(SelectionKey, selectedName);
            SessionState.SetString(PreviousStartKey, AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
            SessionState.SetBool(ActiveKey, true);
            SessionState.SetString(GauntletSequence.EditorStartLevelKey, selectedName);
            SessionState.SetBool(GauntletSequence.EditorLegacyKey, legacy);
            EditorSceneManager.playModeStartScene = bootstrap;
            EditorApplication.isPlaying = true;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(ActiveKey, false)) return;
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(
                SessionState.GetString(PreviousStartKey, string.Empty));
            SessionState.EraseBool(ActiveKey);
            SessionState.EraseString(PreviousStartKey);
            SessionState.EraseString(GauntletSequence.EditorStartLevelKey);
            SessionState.EraseBool(GauntletSequence.EditorLegacyKey);
        }
    }
}
