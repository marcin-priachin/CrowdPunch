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
        private static readonly Color BoundsColor = new Color(0.2f, 0.8f, 1f, 0.9f);

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            ArenaAuthoring arena = (ArenaAuthoring)target;
            Vector3 size = ToVector3(arena.Size);
            Vector3 center = arena.transform.position;

            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.Vector3Field("Baked Center", center);
                EditorGUILayout.Vector3Field("Baked Extents", size * 0.5f);
            }
        }

        private void OnSceneGUI()
        {
            ArenaAuthoring arena = (ArenaAuthoring)target;
            Vector3 size = ToVector3(arena.Size);
            Vector3 center = arena.transform.position;

            Handles.color = BoundsColor;
            Handles.DrawWireCube(center, size);

            Vector3 labelPosition = center + new Vector3(0f, size.y * 0.5f, 0f);
            Handles.Label(labelPosition, $"Arena Size: {FormatSize(size)}");
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
