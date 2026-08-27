using CrowdPunch.Configuration;
using UnityEditor;
using UnityEngine;

namespace CrowdPunch.Editor
{
    /// <summary>Persistent workspace for editing a wave while previewing its spawn areas.</summary>
    public sealed class EnemyWaveEditorWindow : EditorWindow
    {
        private static readonly Color SelectedColor = new(1f, 0.45f, 0.1f, 0.95f);
        private static readonly Color OtherColor = new(1f, 0.65f, 0.2f, 0.4f);
        private static readonly Color FillColor = new(1f, 0.35f, 0.05f, 0.08f);

        private EnemyWaveSettings wave;
        private SerializedObject serializedWave;
        private Vector2 scroll;
        private int selectedArea;
        private bool showScenePreview = true;

        [MenuItem("Window/Crowd Punch/Wave Editor")]
        public static void Open() => GetWindow<EnemyWaveEditorWindow>("Wave Editor").Show();

        public static void Open(EnemyWaveSettings selectedWave)
        {
            EnemyWaveEditorWindow window = GetWindow<EnemyWaveEditorWindow>("Wave Editor");
            window.SetWave(selectedWave);
            window.Show();
            window.Focus();
        }

        private void OnEnable() => SceneView.duringSceneGui += DrawScenePreview;
        private void OnDisable() => SceneView.duringSceneGui -= DrawScenePreview;

        private void OnGUI()
        {
            EditorGUI.BeginChangeCheck();
            EnemyWaveSettings selected = (EnemyWaveSettings)EditorGUILayout.ObjectField(
                "Wave", wave, typeof(EnemyWaveSettings), false);
            if (EditorGUI.EndChangeCheck()) SetWave(selected);

            if (wave == null || serializedWave == null)
            {
                EditorGUILayout.HelpBox("Choose a wave asset to begin editing.", MessageType.Info);
                return;
            }

            serializedWave.Update();
            scroll = EditorGUILayout.BeginScrollView(scroll);
            DrawSummary();
            DrawSection("Normal enemies", "totalEnemyCount", "enemies");
            DrawSection("Fixed elite enemies", "eliteEnemies");
            DrawSpawnAreas();
            DrawTiming();
            EditorGUILayout.EndScrollView();

            if (serializedWave.ApplyModifiedProperties())
            {
                EditorUtility.SetDirty(wave);
                SceneView.RepaintAll();
            }
        }

        private void DrawSummary()
        {
            int normalCount = serializedWave.FindProperty("totalEnemyCount").intValue;
            SerializedProperty elites = serializedWave.FindProperty("eliteEnemies");
            int eliteCount = 0;
            for (int index = 0; index < elites.arraySize; index++)
                eliteCount += Mathf.Max(0, elites.GetArrayElementAtIndex(index).FindPropertyRelative("Count").intValue);

            EditorGUILayout.HelpBox(
                $"Requested population: {normalCount} normal + {eliteCount} elite. " +
                "Normal profiles use relative weights; elite profiles use exact counts.", MessageType.None);
        }

        private void DrawSection(string title, params string[] propertyNames)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            foreach (string propertyName in propertyNames)
                EditorGUILayout.PropertyField(serializedWave.FindProperty(propertyName), true);
        }

        private void DrawSpawnAreas()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("World-space spawn areas", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Select an area below, then move its center or drag its orange edge handles in the Scene view. The preview remains visible while you inspect referenced enemy profiles.",
                MessageType.Info);
            showScenePreview = EditorGUILayout.ToggleLeft("Show and edit in Scene view", showScenePreview);

            SerializedProperty areas = serializedWave.FindProperty("spawnRectangles");
            for (int index = 0; index < areas.arraySize; index++)
            {
                SerializedProperty area = areas.GetArrayElementAtIndex(index);
                using (new EditorGUILayout.VerticalScope(index == selectedArea ? "SelectionRect" : "box"))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Toggle(index == selectedArea, $"Area {index + 1}", "Button")) selectedArea = index;
                        if (GUILayout.Button("Remove", GUILayout.Width(64f)))
                        {
                            areas.DeleteArrayElementAtIndex(index);
                            selectedArea = Mathf.Clamp(selectedArea, 0, areas.arraySize - 1);
                            serializedWave.ApplyModifiedProperties();
                            EditorUtility.SetDirty(wave);
                            SceneView.RepaintAll();
                            GUIUtility.ExitGUI();
                        }
                    }
                    EditorGUILayout.PropertyField(area.FindPropertyRelative("Center"));
                    EditorGUILayout.PropertyField(area.FindPropertyRelative("Width"));
                    EditorGUILayout.PropertyField(area.FindPropertyRelative("Depth"));
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Add Spawn Area"))
                {
                    int index = areas.arraySize;
                    areas.InsertArrayElementAtIndex(index);
                    SerializedProperty added = areas.GetArrayElementAtIndex(index);
                    added.FindPropertyRelative("Center").vector3Value = SceneView.lastActiveSceneView == null
                        ? Vector3.zero : SceneView.lastActiveSceneView.pivot;
                    added.FindPropertyRelative("Width").floatValue = 10f;
                    added.FindPropertyRelative("Depth").floatValue = 10f;
                    selectedArea = index;
                    showScenePreview = true;
                }
                using (new EditorGUI.DisabledScope(areas.arraySize == 0))
                    if (GUILayout.Button("Frame Selected Area")) FrameArea(areas);
            }
        }

        private void DrawTiming()
        {
            DrawSection("Timing and cadence", "delayBeforeWave", "activationMode");
            EnemyWaveActivationMode activationMode =
                (EnemyWaveActivationMode)serializedWave.FindProperty("activationMode").enumValueIndex;
            if (activationMode == EnemyWaveActivationMode.DurationElapsed)
                EditorGUILayout.PropertyField(serializedWave.FindProperty("duration"));

            EditorGUILayout.PropertyField(serializedWave.FindProperty("spawnMode"));
            EnemyWaveSpawnMode mode = (EnemyWaveSpawnMode)serializedWave.FindProperty("spawnMode").enumValueIndex;
            if (mode == EnemyWaveSpawnMode.Batched)
            {
                EditorGUILayout.PropertyField(serializedWave.FindProperty("batchSize"));
                EditorGUILayout.PropertyField(serializedWave.FindProperty("batchInterval"));
            }
            else
            {
                EditorGUILayout.HelpBox("All enemies are queued together after the pre-wave delay.", MessageType.None);
            }
        }

        private void DrawScenePreview(SceneView sceneView)
        {
            if (!showScenePreview || wave == null || serializedWave == null) return;
            serializedWave.Update();
            SerializedProperty areas = serializedWave.FindProperty("spawnRectangles");
            if (areas.arraySize == 0) return;
            selectedArea = Mathf.Clamp(selectedArea, 0, areas.arraySize - 1);

            for (int index = 0; index < areas.arraySize; index++)
                DrawArea(areas.GetArrayElementAtIndex(index), index, index == selectedArea);
            EditArea(areas.GetArrayElementAtIndex(selectedArea));

            if (serializedWave.ApplyModifiedProperties())
            {
                EditorUtility.SetDirty(wave);
                Repaint();
            }
        }

        private void DrawArea(SerializedProperty area, int index, bool selected)
        {
            Vector3 center = area.FindPropertyRelative("Center").vector3Value;
            float width = Mathf.Max(0f, area.FindPropertyRelative("Width").floatValue);
            float depth = Mathf.Max(0f, area.FindPropertyRelative("Depth").floatValue);
            Vector3 halfX = Vector3.right * width * 0.5f;
            Vector3 halfZ = Vector3.forward * depth * 0.5f;
            Vector3[] corners = { center - halfX - halfZ, center - halfX + halfZ, center + halfX + halfZ, center + halfX - halfZ };
            Handles.DrawSolidRectangleWithOutline(corners, FillColor, selected ? SelectedColor : OtherColor);
            Handles.color = selected ? SelectedColor : OtherColor;
            Handles.Label(center + Vector3.up * HandleUtility.GetHandleSize(center) * 0.12f,
                $"{wave.name} / Area {index + 1}  ({width:0.##} x {depth:0.##})");
            float size = HandleUtility.GetHandleSize(center) * 0.07f;
            if (Handles.Button(center, Quaternion.identity, size, size, Handles.DotHandleCap))
            {
                selectedArea = index;
                Repaint();
            }
        }

        private void EditArea(SerializedProperty area)
        {
            SerializedProperty centerProperty = area.FindPropertyRelative("Center");
            SerializedProperty widthProperty = area.FindPropertyRelative("Width");
            SerializedProperty depthProperty = area.FindPropertyRelative("Depth");
            Vector3 center = centerProperty.vector3Value;
            float width = Mathf.Max(0f, widthProperty.floatValue);
            float depth = Mathf.Max(0f, depthProperty.floatValue);

            EditorGUI.BeginChangeCheck();
            Vector3 moved = Handles.PositionHandle(center, Quaternion.identity);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(wave, "Move Wave Spawn Area");
                center = moved;
            }

            EditorGUI.BeginChangeCheck();
            float maxX = Handles.Slider(center + Vector3.right * width * 0.5f, Vector3.right).x;
            float minX = Handles.Slider(center - Vector3.right * width * 0.5f, Vector3.left).x;
            float maxZ = Handles.Slider(center + Vector3.forward * depth * 0.5f, Vector3.forward).z;
            float minZ = Handles.Slider(center - Vector3.forward * depth * 0.5f, Vector3.back).z;
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(wave, "Resize Wave Spawn Area");
                float lowX = Mathf.Min(minX, maxX);
                float highX = Mathf.Max(minX, maxX);
                float lowZ = Mathf.Min(minZ, maxZ);
                float highZ = Mathf.Max(minZ, maxZ);
                center.x = (lowX + highX) * 0.5f;
                center.z = (lowZ + highZ) * 0.5f;
                width = highX - lowX;
                depth = highZ - lowZ;
            }

            centerProperty.vector3Value = center;
            widthProperty.floatValue = width;
            depthProperty.floatValue = depth;
        }

        private void SetWave(EnemyWaveSettings selected)
        {
            wave = selected;
            serializedWave = wave == null ? null : new SerializedObject(wave);
            selectedArea = 0;
            SceneView.RepaintAll();
            Repaint();
        }

        private void FrameArea(SerializedProperty areas)
        {
            selectedArea = Mathf.Clamp(selectedArea, 0, areas.arraySize - 1);
            SerializedProperty area = areas.GetArrayElementAtIndex(selectedArea);
            Vector3 center = area.FindPropertyRelative("Center").vector3Value;
            float width = Mathf.Max(1f, area.FindPropertyRelative("Width").floatValue);
            float depth = Mathf.Max(1f, area.FindPropertyRelative("Depth").floatValue);
            SceneView.lastActiveSceneView?.Frame(new Bounds(center, new Vector3(width, 1f, depth)), false);
        }
    }
}
