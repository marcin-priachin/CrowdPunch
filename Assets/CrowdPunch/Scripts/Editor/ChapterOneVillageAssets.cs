using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CrowdPunch.Editor
{
    // Game-owned copies keep imported source packs and other chapters untouched.
    internal static class ChapterOneVillageAssets
    {
        internal const string Output = "Assets/CrowdPunch/Data/ChapterOneVillage";
        private static string[] models;

        internal static void Prepare()
        {
            Directory.CreateDirectory(Output + "/Materials");
            Directory.CreateDirectory(Output + "/Landscape");
            AssetDatabase.Refresh();
            ChapterOneVillageSurfaces.Prepare();
            models = AssetDatabase.FindAssets("t:Model", new[] { "Assets/CrowdPunch/Models" })
                .Select(AssetDatabase.GUIDToAssetPath).Where(p => p.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase)).ToArray();
        }

        internal static Material Surface(string name, Color color)
        {
            string path = Output + "/Materials/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
            {
                ChapterOneVillageSurfaces.Configure(material,name,color);
                return material;
            }
            material = new Material(Shader.Find("CrowdPunch/Cel/Environment"))
                { name = name, color = color, enableInstancing = true };
            material.SetFloat("_Smoothness", .06f);
            material.SetFloat("_Metallic", 0);
            AssetDatabase.CreateAsset(material, path);
            ChapterOneVillageSurfaces.Configure(material,name,color);
            return material;
        }

        private static Material Imported(Material source, string kit)
        {
            if (source == null) return Surface("Limestone", new Color(.64f,.59f,.43f));
            string key = AssetDatabase.GetAssetPath(source) + "/" + source.name;
            string path = Output + "/Materials/Imported_" + Hash128.Compute(key) + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool create = material == null;
            if (create) material = new Material(source) { name = source.name + " Village Cel", enableInstancing = true };
            material.shader = Shader.Find("CrowdPunch/Cel/Environment");
            if (source.mainTexture != null) material.SetTexture("_BaseMap", source.mainTexture);
            material.SetFloat("_Smoothness", .06f);
            material.SetFloat("_Metallic", 0);
            if(kit=="Stylized Nature" && source.name=="Grass")
            {
                material.SetTexture("_BaseMap",null);
                material.color=new Color(.33f,.45f,.15f);
                material.SetFloat("_Cull",0);
            }
            // Opaque leaf canopies in this pack have a deliberately solid cel silhouette.
            if (kit == "Stylized Nature" && source.name.StartsWith("Leaves"))
            {
                material.SetTexture("_BaseMap", null);
                material.color = source.name.Contains("Bush") ? new Color(.32f,.45f,.17f) : new Color(.38f,.52f,.19f);
                material.SetFloat("_Cull", 0);
                material.SetFloat("_AlphaClip", 0);
                material.DisableKeyword("_ALPHATEST_ON");
            }
            if (create) AssetDatabase.CreateAsset(material, path);
            else EditorUtility.SetDirty(material);
            return material;
        }

        internal static GameObject Model(Transform parent, string kit, string name, Vector3 bottom,
            Vector3 size, float yaw = 0, Material material = null)
        {
            string path = models.Where(p => p.Contains(kit) && Path.GetFileNameWithoutExtension(p) == name)
                .OrderByDescending(p => p.Contains("FBX (Unity)") || p.Contains("fbx(unity)")).ThenBy(p => p.Length).FirstOrDefault();
            if (path == null) throw new InvalidOperationException("Missing model: " + kit + "/" + name);
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path), root.transform, false);
            var renderers = root.GetComponentsInChildren<MeshRenderer>();
            if (renderers.Length == 0) throw new InvalidOperationException("Empty model: " + path);
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            var scale = new Vector3(size.x / bounds.size.x, size.y / Mathf.Max(.001f,bounds.size.y), size.z / bounds.size.z);
            root.transform.localScale = scale;
            root.transform.rotation = Quaternion.Euler(0,yaw,0);
            root.transform.position = bottom - root.transform.rotation * Vector3.Scale(new Vector3(bounds.center.x,bounds.min.y,bounds.center.z),scale);
            foreach (var collider in root.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(collider);
            foreach (var animator in root.GetComponentsInChildren<Animator>()) Object.DestroyImmediate(animator);
            foreach (var renderer in renderers)
            {
                renderer.sharedMaterials = renderer.sharedMaterials.Select(m => material != null ? material : Imported(m,kit)).ToArray();
                renderer.gameObject.isStatic = true;
            }
            return root;
        }

        internal static GameObject Block(Transform parent, string name, Vector3 bottom, Vector3 size, Material material)
        {
            var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = name;
            block.transform.SetParent(parent, false);
            block.transform.position = bottom + Vector3.up * size.y * .5f;
            block.transform.localScale = size;
            Object.DestroyImmediate(block.GetComponent<Collider>());
            block.GetComponent<Renderer>().sharedMaterial = material;
            block.isStatic = true;
            return block;
        }
    }
}
