using CrowdPunch.Configuration;
using UnityEditor;
using UnityEngine;

namespace CrowdPunch.Editor
{
    /// <summary>Provides a clear entry point to the persistent wave editing workspace.</summary>
    [CustomEditor(typeof(EnemyWaveSettings))]
    public sealed class EnemyWaveSettingsEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox(
                "Use the Wave Editor to edit composition, timing, and world-space spawn areas without losing the Scene preview when inspecting an enemy profile.",
                MessageType.Info);
            if (GUILayout.Button("Open Wave Editor", GUILayout.Height(30f)))
                EnemyWaveEditorWindow.Open((EnemyWaveSettings)target);

            EditorGUILayout.Space();
            DrawDefaultInspector();
        }
    }
}
