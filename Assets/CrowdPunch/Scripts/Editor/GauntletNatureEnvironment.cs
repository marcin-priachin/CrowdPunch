using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CrowdPunch.Editor
{
    // Visual authoring only: LOOP-002 layouts and VISION-004 collision geometry stay intact.
    public static class GauntletNatureEnvironment
    {
        private const string Kit = "Assets/CrowdPunch/Models/kenney_nature-kit";
        private const string Materials = "Assets/CrowdPunch/Materials/Nature";
        private const string Meshes = "Assets/CrowdPunch/Data/GauntletLayouts/Nature";
        private const string Scenes = "Assets/CrowdPunch/Scenes/Gauntlets";

        [MenuItem("Crowd Punch/Levels/Apply Nature Kit Environment")]
        public static void Apply()
        {
            if (EditorApplication.isPlaying)
                throw new InvalidOperationException("Exit Play mode before authoring environments.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Save open scenes before authoring environments.");

            Directory.CreateDirectory(Materials);
            Directory.CreateDirectory(Meshes);
            AssetDatabase.Refresh();
            var materials = CreateMaterials();
            var setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                for (int i = 1; i <= 10; i++)
                {
                    string id = $"Gauntlet_{i:00}";
                    var scene = EditorSceneManager.OpenScene($"{Scenes}/{id}/{id} Sub Scene.unity", OpenSceneMode.Single);
                    var layout = scene.GetRootGameObjects().Single(g => g.name.StartsWith("Layout - "));
                    int index = 0;
                    foreach (Transform anchor in layout.transform)
                    {
                        var filter = anchor.GetComponent<MeshFilter>();
                        var renderer = anchor.GetComponent<MeshRenderer>();
                        if (filter == null || renderer == null) continue;
                        Mesh mesh;
                        Material[] slots;
                        if (anchor.name == "Continuous Convex Floor")
                        {
                            // Retain the authored polygon, including slanted/clipped edges and floor height.
                            var original = anchor.GetComponent<MeshCollider>().sharedMesh;
                            mesh = UnityEngine.Object.Instantiate(original);
                            var top = new List<int>();
                            var sides = new List<int>();
                            var vertices = original.vertices;
                            var triangles = original.triangles;
                            for (int t = 0; t < triangles.Length; t += 3)
                            {
                                var target = Enumerable.Range(t, 3).All(k => Mathf.Abs(vertices[triangles[k]].y - original.bounds.max.y) < 0.001f) ? top : sides;
                                target.AddRange(new[] { triangles[t], triangles[t + 1], triangles[t + 2] });
                            }
                            mesh.subMeshCount = 2;
                            mesh.SetTriangles(top, 0);
                            mesh.SetTriangles(sides, 1);
                            slots = new[] { materials["grass"], materials["stone"] };
                        }
                        else
                        {
                            // All original primitive visuals occupied a unit box in anchor space.
                            // Never change anchors: their scale also defines existing collider dimensions.
                            bool wall = anchor.name.StartsWith("Perimeter ");
                            bool lane = anchor.name == "Launch Lane";
                            string model = wall ? "rock_largeA" : lane ? "path_stone" :
                                anchor.name == "Entry Apron" ? "ground_pathTile" : "ground_grass";
                            int x = wall ? 1 : lane ? Mathf.Max(1, Mathf.CeilToInt(anchor.localScale.x / 2f)) : 1;
                            int z = wall ? Mathf.Max(1, Mathf.CeilToInt(anchor.localScale.z / 3f)) :
                                lane ? Mathf.Max(1, Mathf.CeilToInt(anchor.localScale.z / 2f)) : 1;
                            mesh = TileModel(model, x, z, out string[] names);
                            slots = names.Select(n => materials[n]).ToArray();
                            if (anchor.name == "Presentation Backdrop") slots = new[] { materials["Backdrop"] };
                        }
                        string path = $"{Meshes}/{id}_{index++:00}.asset";
                        mesh.name = Path.GetFileNameWithoutExtension(path);
                        var saved = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                        if (saved == null) { AssetDatabase.CreateAsset(mesh, path); saved = mesh; }
                        else { EditorUtility.CopySerialized(mesh, saved); UnityEngine.Object.DestroyImmediate(mesh); }
                        filter.sharedMesh = saved;
                        renderer.sharedMaterials = slots;
                        EditorUtility.SetDirty(filter);
                        EditorUtility.SetDirty(renderer);
                    }
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                    Debug.Log($"Nature environment: {id}, {index} visual meshes; original colliders and transforms retained.");
                }
                AssetDatabase.SaveAssets();
            }
            finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }
        }

        private static Dictionary<string, Material> CreateMaterials()
        {
            var result = new Dictionary<string, Material>();
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("URP Lit shader is missing.");
            foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { Kit }))
            foreach (var source in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GUIDToAssetPath(guid)).OfType<Material>())
            {
                if (result.ContainsKey(source.name)) continue;
                string path = $"{Materials}/{source.name}.mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null)
                {
                    material = new Material(shader) { name = source.name, color = Palette(source.name, source.color), enableInstancing = true };
                    material.SetFloat("_Smoothness", 0.08f);
                    material.SetFloat("_Metallic", 0f);
                    AssetDatabase.CreateAsset(material, path);
                }
                result.Add(source.name, material);
            }
            string backdropPath = Materials + "/Backdrop.mat";
            var backdrop = AssetDatabase.LoadAssetAtPath<Material>(backdropPath);
            if (backdrop == null)
            {
                backdrop = new Material(shader) { name = "Backdrop", color = new Color(0.17f, 0.23f, 0.13f), enableInstancing = true };
                backdrop.SetFloat("_Smoothness", 0f);
                AssetDatabase.CreateAsset(backdrop, backdropPath);
            }
            result.Add("Backdrop", backdrop);
            return result;
        }

        private static Color Palette(string name, Color fallback)
        {
            switch (name)
            {
                case "grass": return new Color(0.32f, 0.43f, 0.20f);
                case "leafsGreen": return new Color(0.29f, 0.46f, 0.17f);
                case "leafsDark": return new Color(0.18f, 0.32f, 0.14f);
                case "stone": return new Color(0.53f, 0.55f, 0.48f);
                case "stoneDark": return new Color(0.36f, 0.39f, 0.34f);
                case "dirt": return new Color(0.48f, 0.34f, 0.21f);
                case "dirtDark": return new Color(0.33f, 0.24f, 0.16f);
                default: return fallback;
            }
        }

        private static Mesh TileModel(string model, int columns, int rows, out string[] materialNames)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>($"{Kit}/{model}.fbx");
            var filter = source.GetComponentInChildren<MeshFilter>();
            var mesh = filter.sharedMesh;
            materialNames = filter.GetComponent<MeshRenderer>().sharedMaterials.Select(m => m.name).ToArray();
            var bounds = mesh.bounds;
            var size = bounds.size;
            // Ground tiles are flat; put their surface at the original primitive's top face.
            var scale = new Vector3(1f / columns / size.x, size.y > 0.0001f ? 1f / size.y : 1f, 1f / rows / size.z);
            var submeshes = new List<CombineInstance>();
            var temporary = new List<Mesh>();
            for (int s = 0; s < mesh.subMeshCount; s++)
            {
                var instances = new List<CombineInstance>();
                for (int x = 0; x < columns; x++)
                for (int z = 0; z < rows; z++)
                {
                    var center = new Vector3(-0.5f + (x + 0.5f) / columns, size.y > 0.0001f ? 0f : 0.5f,
                        -0.5f + (z + 0.5f) / rows);
                    instances.Add(new CombineInstance { mesh = mesh, subMeshIndex = s,
                        transform = Matrix4x4.TRS(center - Vector3.Scale(bounds.center, scale), Quaternion.identity, scale) });
                }
                var part = new Mesh();
                part.CombineMeshes(instances.ToArray(), true, true);
                temporary.Add(part);
                submeshes.Add(new CombineInstance { mesh = part, transform = Matrix4x4.identity });
            }
            var combined = new Mesh();
            combined.CombineMeshes(submeshes.ToArray(), false, false);
            foreach (var part in temporary) UnityEngine.Object.DestroyImmediate(part);
            combined.RecalculateBounds();
            return combined;
        }
    }
}
