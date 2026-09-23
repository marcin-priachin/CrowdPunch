using System;
using System.IO;
using System.Linq;
using CrowdPunch.Authoring;
using CrowdPunch.Configuration;
using CrowdPunch.Systems.Movement;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CrowdPunch.Editor
{
    // BOSS-005: a closed court inside the head's circular route, with its weak spot exposed inward.
    public static class BossRoundArenaBuilder
    {
        public const float CourtRadius = 19f;
        public const float RouteRadius = 20f;
        public const int Segments = 64;
        private const string LayoutName = "Layout - Round Boss Court";
        private const string MeshRoot = "Assets/CrowdPunch/Data/GauntletLayouts/Nature/";

        [MenuItem("Crowd Punch/Levels/Apply Round Boss Arena")]
        public static void Apply()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode before authoring.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save open scenes before authoring.");
            var setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                var scene = EditorSceneManager.OpenScene(BossGauntletBuilder.SubPath, OpenSceneMode.Single);
                string[] oldTerrain = { "Arena Floor", "North Rail", "South Rail", "East Rail", "West Rail", "Backdrop", "Center Lane", "Cross Lane", LayoutName };
                foreach (var root in scene.GetRootGameObjects())
                    if (oldTerrain.Contains(root.name)) UnityEngine.Object.DestroyImmediate(root);
                var arena = UnityEngine.Object.FindFirstObjectByType<ArenaAuthoring>();
                var encounter = UnityEngine.Object.FindFirstObjectByType<BossEncounterAuthoring>();
                Create(arena, encounter.settings);
                var tuning = encounter.settings.Bake();
                encounter.transform.position = BossPerimeterRoute.Position(0, tuning);
                encounter.transform.rotation = Quaternion.LookRotation(Vector3.back);
                foreach (var hand in new[] { encounter.leftHand, encounter.rightHand })
                {
                    float side = hand == encounter.leftHand ? -1 : 1;
                    var p = encounter.transform.position + encounter.transform.right * (side * tuning.OpenOffset) + Vector3.back;
                    p.y = tuning.HandHeight;
                    hand.transform.SetPositionAndRotation(p, encounter.transform.rotation);
                }
                var wave = AssetDatabase.LoadAssetAtPath<EnemyWaveSettings>(BossGauntletBuilder.WavePath);
                var data = new SerializedObject(wave);
                var rectangles = data.FindProperty("spawnRectangles");
                rectangles.arraySize = 1;
                var rectangle = rectangles.GetArrayElementAtIndex(0);
                rectangle.FindPropertyRelative("Center").vector3Value = new Vector3(0, 2, 0);
                rectangle.FindPropertyRelative("Width").floatValue = 24;
                rectangle.FindPropertyRelative("Depth").floatValue = 24;
                data.ApplyModifiedPropertiesWithoutUndo();
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                AssetDatabase.SaveAssets();
            }
            finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }
            Debug.Log("BOSS-005: round nature court authored; closed rock rim, circular boss route, supporting spawns inside court.");
        }

        internal static void Create(ArenaAuthoring arena, BossEncounterSettings settings)
        {
            settings.center = Vector2.zero;
            settings.routeExtents = Vector2.one * RouteRadius;
            settings.cornerRadius = RouteRadius;
            settings.boundsExtents = Vector2.one * 27;
            EditorUtility.SetDirty(settings);
            var data = new SerializedObject(arena);
            data.FindProperty("spacingSize").vector3Value = new Vector3(24, 16, 24);
            data.FindProperty("defeatSize").vector3Value = new Vector3(60, 22, 60);
            data.ApplyModifiedPropertiesWithoutUndo();

            Directory.CreateDirectory(MeshRoot);
            var materials = GauntletNatureEnvironment.CreateMaterials();
            var root = new GameObject(LayoutName).transform;
            var disk = SaveMesh("Gauntlet_11_RoundFloor", Disk());
            var floor = Visual("Continuous Round Floor", root, disk, new[] { materials["grass"], materials["stone"] });
            var floorCollider = floor.AddComponent<MeshCollider>();
            floorCollider.sharedMesh = disk;
            // COMBAT-018: a convex surface avoids radial triangle-edge contacts braking grounded launches.
            floorCollider.convex = true;
            var rocks = SaveMesh("Gauntlet_11_RimRock", GauntletNatureEnvironment.TileModel("rock_largeA", 1, 1, out var slots));
            float thickness = 4f;
            float wallRadius = CourtRadius + thickness * .5f;
            // Tangent boxes overlap at their outer corners. Their inner faces form a closed polygon.
            float length = 2 * (CourtRadius + thickness) * Mathf.Tan(Mathf.PI / Segments) + .05f;
            for (int i = 0; i < Segments; i++)
            {
                float angle = i * 2 * Mathf.PI / Segments;
                var radial = new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle));
                var wall = Visual($"Perimeter Rock {i:00}", root, rocks, slots.Select(s => materials[s]).ToArray());
                wall.transform.position = radial * wallRadius;
                wall.transform.rotation = Quaternion.LookRotation(new Vector3(radial.z, 0, -radial.x));
                wall.transform.localScale = new Vector3(thickness, 2, length);
                var collider = wall.AddComponent<BoxCollider>();
                // Match the earlier gauntlets' elevated player sweep; no gap above low visual rocks.
                collider.center = new Vector3(0, 1, 0);
                collider.size = new Vector3(1, 3, 1);
            }
            var ground = SaveMesh("Gauntlet_11_Backdrop", GauntletNatureEnvironment.TileModel("ground_grass", 1, 1, out _));
            var backdrop = Visual("Presentation Backdrop", root, ground, new[] { materials["Backdrop"] });
            backdrop.transform.position = new Vector3(0, -2, 0);
            backdrop.transform.localScale = new Vector3(200, .1f, 200);
        }

        private static GameObject Visual(string name, Transform parent, Mesh mesh, Material[] materials)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.GetComponent<MeshRenderer>().sharedMaterials = materials;
            return go;
        }

        private static Mesh SaveMesh(string name, Mesh mesh)
        {
            mesh.name = name;
            string path = MeshRoot + name + ".asset";
            var saved = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (saved == null) { AssetDatabase.CreateAsset(mesh, path); return mesh; }
            EditorUtility.CopySerialized(mesh, saved);
            UnityEngine.Object.DestroyImmediate(mesh);
            return saved;
        }

        private static Mesh Disk()
        {
            var vertices = new Vector3[Segments * 2 + 2];
            var top = new int[Segments * 3];
            var sides = new int[Segments * 9];
            vertices[0] = new Vector3(0, -1, 0);
            vertices[1] = new Vector3(0, -1.6f, 0);
            for (int i = 0; i < Segments; i++)
            {
                float a = i * 2 * Mathf.PI / Segments;
                vertices[2 + i * 2] = new Vector3(Mathf.Sin(a) * 24, -1, Mathf.Cos(a) * 24);
                vertices[3 + i * 2] = vertices[2 + i * 2] + Vector3.down * .6f;
                int p = 2 + i * 2, q = 2 + ((i + 1) % Segments) * 2;
                top[i * 3] = 0; top[i * 3 + 1] = p; top[i * 3 + 2] = q;
                int[] triangles = { p, p + 1, q, q, p + 1, q + 1, 1, q + 1, p + 1 };
                Array.Copy(triangles, 0, sides, i * 9, 9);
            }
            var mesh = new Mesh { vertices = vertices, subMeshCount = 2 };
            mesh.SetTriangles(top, 0); mesh.SetTriangles(sides, 1);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return mesh;
        }
    }
}
