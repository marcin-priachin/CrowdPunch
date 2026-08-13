using CrowdPunch.Configuration;
using UnityEditor;
using UnityEngine;

namespace CrowdPunch.Editor
{
    /// <summary>Inspector selection and Scene-view placement handles for wave spawn rectangles.</summary>
    [CustomEditor(typeof(EnemyWaveSettings))]
    public sealed class EnemyWaveSettingsEditor : UnityEditor.Editor
    {
        private static readonly Color RangeColor = new Color(1f, 0.45f, 0.1f, 0.85f);
        private static readonly Color UnselectedColor = new Color(1f, 0.65f, 0.2f, 0.35f);
        private static readonly Color FillColor = new Color(1f, 0.35f, 0.05f, 0.08f);

        private SerializedProperty spawnRectangles;
        private int selectedRectangle;

        private void OnEnable()
        {
            spawnRectangles = serializedObject.FindProperty("spawnRectangles");
            SceneView.RepaintAll();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawDefaultInspector();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Scene Placement", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Select this wave asset to view its world-space ranges. Choose a rectangle below, then use the Scene view center and orange edge handles to place and resize it.",
                MessageType.Info);

            int count = spawnRectangles.arraySize;
            if (count == 0)
            {
                EditorGUILayout.LabelField("Add a spawn rectangle above to enable Scene handles.");
            }
            else
            {
                selectedRectangle = Mathf.Clamp(selectedRectangle, 0, count - 1);
                string[] labels = new string[count];
                for (int index = 0; index < count; index++)
                    labels[index] = $"Range {index + 1}";
                selectedRectangle = GUILayout.Toolbar(selectedRectangle, labels);
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void OnSceneGUI()
        {
            serializedObject.Update();
            int count = spawnRectangles.arraySize;
            if (count == 0) return;

            selectedRectangle = Mathf.Clamp(selectedRectangle, 0, count - 1);
            for (int index = 0; index < count; index++)
            {
                SerializedProperty rectangle = spawnRectangles.GetArrayElementAtIndex(index);
                Vector3 center = rectangle.FindPropertyRelative("Center").vector3Value;
                float width = Mathf.Max(0f, rectangle.FindPropertyRelative("Width").floatValue);
                float depth = Mathf.Max(0f, rectangle.FindPropertyRelative("Depth").floatValue);
                DrawRange(center, width, depth, index, index == selectedRectangle);
            }

            EditSelectedRange();
            if (serializedObject.ApplyModifiedProperties())
            {
                EditorUtility.SetDirty(target);
                SceneView.RepaintAll();
            }
        }

        private void DrawRange(Vector3 center, float width, float depth, int index, bool selected)
        {
            Vector3 halfX = Vector3.right * width * 0.5f;
            Vector3 halfZ = Vector3.forward * depth * 0.5f;
            Vector3[] corners =
            {
                center - halfX - halfZ,
                center - halfX + halfZ,
                center + halfX + halfZ,
                center + halfX - halfZ
            };

            Handles.DrawSolidRectangleWithOutline(corners, FillColor, selected ? RangeColor : UnselectedColor);
            Handles.color = selected ? RangeColor : UnselectedColor;
            Handles.Label(center + Vector3.up * HandleUtility.GetHandleSize(center) * 0.12f,
                $"Wave Range {index + 1}  {width:0.##} x {depth:0.##}");

            float buttonSize = HandleUtility.GetHandleSize(center) * 0.07f;
            if (Handles.Button(center, Quaternion.identity, buttonSize, buttonSize, Handles.DotHandleCap))
            {
                selectedRectangle = index;
                Repaint();
            }
        }

        private void EditSelectedRange()
        {
            SerializedProperty rectangle = spawnRectangles.GetArrayElementAtIndex(selectedRectangle);
            SerializedProperty centerProperty = rectangle.FindPropertyRelative("Center");
            SerializedProperty widthProperty = rectangle.FindPropertyRelative("Width");
            SerializedProperty depthProperty = rectangle.FindPropertyRelative("Depth");

            Vector3 center = centerProperty.vector3Value;
            float width = Mathf.Max(0f, widthProperty.floatValue);
            float depth = Mathf.Max(0f, depthProperty.floatValue);

            EditorGUI.BeginChangeCheck();
            Vector3 movedCenter = Handles.PositionHandle(center, Quaternion.identity);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(target, "Move Wave Spawn Rectangle");
                center = movedCenter;
            }

            EditorGUI.BeginChangeCheck();
            float positiveX = Handles.Slider(center + Vector3.right * width * 0.5f, Vector3.right).x;
            float negativeX = Handles.Slider(center - Vector3.right * width * 0.5f, Vector3.left).x;
            float positiveZ = Handles.Slider(center + Vector3.forward * depth * 0.5f, Vector3.forward).z;
            float negativeZ = Handles.Slider(center - Vector3.forward * depth * 0.5f, Vector3.back).z;
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(target, "Resize Wave Spawn Rectangle");
                float minimumX = Mathf.Min(negativeX, positiveX);
                float maximumX = Mathf.Max(negativeX, positiveX);
                float minimumZ = Mathf.Min(negativeZ, positiveZ);
                float maximumZ = Mathf.Max(negativeZ, positiveZ);
                center.x = (minimumX + maximumX) * 0.5f;
                center.z = (minimumZ + maximumZ) * 0.5f;
                width = maximumX - minimumX;
                depth = maximumZ - minimumZ;
            }

            centerProperty.vector3Value = center;
            widthProperty.floatValue = width;
            depthProperty.floatValue = depth;
        }
    }
}
