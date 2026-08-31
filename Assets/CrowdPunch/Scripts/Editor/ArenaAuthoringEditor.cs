using CrowdPunch.Authoring;
using UnityEditor;
using UnityEngine;

namespace CrowdPunch.Editor
{
    /// <summary>
    /// Scene and inspector visualization for arena bounds authoring.
    /// </summary>
    [CustomEditor(typeof(ArenaAuthoring))]
    public sealed class ArenaAuthoringEditor : UnityEditor.Editor
    {
        private static readonly Color SpacingBoundsColor = new Color(0.2f, 0.8f, 1f, 0.9f);
        private static readonly Color DefeatBoundsColor = new Color(1f, 0.25f, 0.15f, 0.9f);

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            ArenaAuthoring arena = (ArenaAuthoring)target;
            Vector3 spacingSize = ToVector3(arena.SpacingSize);
            Vector3 defeatSize = ToVector3(arena.DefeatSize);
            Vector3 origin = arena.transform.position;
            Vector3 spacingCenter = origin + ToVector3(arena.SpacingCenterOffset);
            Vector3 defeatCenter = origin + ToVector3(arena.DefeatCenterOffset);

            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.Vector3Field("Spacing Baked Center", spacingCenter);
                EditorGUILayout.Vector3Field("Spacing Baked Extents", spacingSize * 0.5f);
                EditorGUILayout.Vector3Field("Defeat Baked Center", defeatCenter);
                EditorGUILayout.Vector3Field("Defeat Baked Extents", defeatSize * 0.5f);
            }
        }

        private void OnSceneGUI()
        {
            ArenaAuthoring arena = (ArenaAuthoring)target;
            Vector3 spacingSize = ToVector3(arena.SpacingSize);
            Vector3 defeatSize = ToVector3(arena.DefeatSize);
            Vector3 origin = arena.transform.position;
            Vector3 spacingCenter = origin + ToVector3(arena.SpacingCenterOffset);
            Vector3 defeatCenter = origin + ToVector3(arena.DefeatCenterOffset);

            Handles.color = SpacingBoundsColor;
            Handles.DrawWireCube(spacingCenter, spacingSize);
            Handles.Label(
                spacingCenter + new Vector3(0f, spacingSize.y * 0.5f, 0f),
                $"Enemy Spacing: {FormatSize(spacingSize)}");

            Handles.color = DefeatBoundsColor;
            Handles.DrawWireCube(defeatCenter, defeatSize);
            Handles.Label(
                defeatCenter + new Vector3(0f, defeatSize.y * 0.5f, 0f),
                $"Enemy Defeat: {FormatSize(defeatSize)}");
        }

        private static Vector3 ToVector3(Unity.Mathematics.float3 value)
        {
            return new Vector3(value.x, value.y, value.z);
        }

        private static string FormatSize(Vector3 size)
        {
            return $"{size.x:0.##} x {size.y:0.##} x {size.z:0.##}";
        }
    }
}
