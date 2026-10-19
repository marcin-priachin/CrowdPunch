using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CrowdPunch.Editor
{
    // VISION-006: owned pigment recipes reuse the production shader's DOTS/bake/cutaway passes.
    internal static class ChapterThreeQuarryAssets
    {
        internal const string Output = "Assets/CrowdPunch/Data/ChapterThreeQuarry";
        private static readonly Dictionary<string, Material> Surfaces = new();

        internal static void Prepare()
        {
            Directory.CreateDirectory(Output + "/Materials");
            Directory.CreateDirectory(Output + "/Meshes");
            AssetDatabase.Refresh(); Surfaces.Clear();
        }

        internal static Material Surface(string kind, bool decoration = false, bool distant = false, bool moving = false)
        {
            string key = kind + (decoration ? " Decorative" : "") + (distant ? " Distant" : "") + (moving ? " Local" : "");
            if (Surfaces.TryGetValue(key, out var cached)) return cached;
            string path = Output + "/Materials/" + key + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            var shader = Shader.Find("CrowdPunch/Cel/Painted Foundry");
            if (shader == null) throw new InvalidOperationException("Production painted shader is missing.");
            if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, path); }
            material.shader = shader; material.enableInstancing = true;
            Color color, wash;
            string texture = null; float detail = .30f, variation = .13f, scale = .18f;
            switch (kind)
            {
                case "Court": color = new Color(.65f,.65f,.58f); wash = new Color(.58f,.60f,.59f); texture = "Stylized_Stone_Floor_002_basecolor"; detail = .10f; variation = .025f; scale = .14f; break;
                case "Gravel": color = new Color(.53f,.56f,.45f); wash = new Color(.62f,.58f,.45f); texture = "Vol_16_2_Sand_Base_Color"; detail = .18f; variation = .07f; scale = .12f; break;
                case "Limestone": color = new Color(.76f,.72f,.59f); wash = new Color(.63f,.60f,.51f); texture = "Vol_23_1_Rocks_Base_Color"; break;
                case "Cliff": color = new Color(.57f,.60f,.53f); wash = new Color(.66f,.62f,.51f); texture = "Vol_23_1_Rocks_Base_Color"; detail = .28f; scale = .09f; break;
                case "Slate": color = new Color(.35f,.44f,.48f); wash = new Color(.48f,.54f,.56f); break;
                case "Sage": color = new Color(.39f,.51f,.40f); wash = new Color(.55f,.59f,.44f); break;
                case "Timber": color = new Color(.47f,.38f,.27f); wash = new Color(.57f,.48f,.32f); texture = "Stylized_Wood_Wall_001_basecolor"; detail = .24f; break;
                case "Ochre": color = new Color(.69f,.49f,.25f); wash = new Color(.67f,.56f,.37f); break;
                case "Impact": color = new Color(.75f,.29f,.18f); wash = new Color(.67f,.40f,.30f); break;
                case "Chalk": color = new Color(.88f,.83f,.66f); wash = new Color(.74f,.71f,.57f); break;
                case "Fracture": color = new Color(.22f,.28f,.29f); wash = new Color(.31f,.35f,.37f); break;
                default: throw new ArgumentException(kind);
            }
            if (distant) { color = Color.Lerp(color, new Color(.56f,.64f,.68f), .48f); wash = color; detail *= .22f; variation *= .4f; }
            material.SetColor("_BaseColor", color); material.SetColor("_WashColor", wash);
            material.SetColor("_ShadowColor", new Color(.47f,.54f,.66f));
            material.SetColor("_LightColor", new Color(1,.97f,.86f));
            material.SetFloat("_Contrast", distant ? .48f : .78f);
            material.SetFloat("_WashStrength", kind == "Court" ? .12f : .26f);
            material.SetFloat("_GrainStrength", distant ? .02f : .08f);
            material.SetFloat("_Variation", variation); material.SetFloat("_TextureScale", scale);
            material.SetFloat("_TextureStrength", texture == null ? 0 : detail);
            material.SetFloat("_CameraCutaway", decoration ? 1 : 0);
            material.SetFloat("_ObjectSpacePaint", moving ? 1 : 0);
            material.SetFloat("_UseEntityColor", moving ? 1 : 0);
            material.SetFloat("_ReceiveShadows", 1); material.SetFloat("_Meadow", 0);
            if (texture != null)
            {
                string source = AssetDatabase.FindAssets(texture + " t:Texture2D").Select(AssetDatabase.GUIDToAssetPath).FirstOrDefault();
                if (source == null) throw new InvalidOperationException("Missing texture " + texture);
                material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(source));
            }
            EditorUtility.SetDirty(material); Surfaces.Add(key, material); return material;
        }

        internal static GameObject Shape(Transform parent, string name, Vector3 bottom, Vector3 size, Material material, bool cylinder = false)
        {
            var go = GameObject.CreatePrimitive(cylinder ? PrimitiveType.Cylinder : PrimitiveType.Cube);
            go.name = name; Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false); go.transform.localPosition = bottom + Vector3.up * size.y * .5f;
            go.transform.localScale = new Vector3(size.x, cylinder ? size.y * .5f : size.y, size.z);
            go.GetComponent<MeshRenderer>().sharedMaterial = material; go.GetComponent<MeshRenderer>().forceMeshLod = 0;
            return go;
        }

        internal static GameObject Model(Transform parent, string family, string model, Vector3 bottom, Vector3 envelope, Material material, float yaw = 0)
        {
            string path = "Assets/CrowdPunch/Models/" + family + "/" + model + ".fbx";
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (source == null) throw new InvalidOperationException("Missing quarry model: " + path);
            var go = new GameObject(model); go.transform.SetParent(parent, false);
            Object.Instantiate(source, go.transform, false);
            var renderers = go.GetComponentsInChildren<MeshRenderer>(true);
            var bounds = renderers[0].bounds; foreach (var r in renderers.Skip(1)) bounds.Encapsulate(r.bounds);
            float fit = Mathf.Min(Fit(envelope.x,bounds.size.x), Fit(envelope.y,bounds.size.y), Fit(envelope.z,bounds.size.z));
            go.transform.localScale = Vector3.one * fit; go.transform.localRotation = Quaternion.Euler(0,yaw,0);
            go.transform.position = parent.TransformPoint(bottom) - go.transform.rotation * ((new Vector3(bounds.center.x,bounds.min.y,bounds.center.z) - parent.position) * fit);
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            foreach (var a in go.GetComponentsInChildren<Animator>(true)) Object.DestroyImmediate(a);
            foreach (var lod in go.GetComponentsInChildren<LODGroup>(true)) lod.ForceLOD(0);
            foreach (var r in renderers) { r.sharedMaterials = Enumerable.Repeat(material,r.sharedMaterials.Length).ToArray(); r.forceMeshLod = 0; }
            return go;
        }

        private static float Fit(float desired, float extent) => extent < .0001f ? float.PositiveInfinity : desired / extent;

        internal static GameObject MeshObject(Transform parent, string name, Mesh mesh, Material material)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer)); go.transform.SetParent(parent,false);
            go.GetComponent<MeshFilter>().sharedMesh = mesh; go.GetComponent<MeshRenderer>().sharedMaterial = material;
            go.GetComponent<MeshRenderer>().forceMeshLod = 0; return go;
        }

        internal static Mesh SaveMesh(string name, List<Vector3> vertices, List<int> triangles)
        {
            string path = Output + "/Meshes/" + name + ".asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            bool create=mesh==null;
            if(create)mesh=new Mesh {name=name}; else mesh.Clear();
            mesh.SetVertices(vertices); mesh.SetTriangles(triangles,0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            if(create)AssetDatabase.CreateAsset(mesh,path);else EditorUtility.SetDirty(mesh);
            return mesh;
        }

        internal static void Quad(List<Vector3> v, List<int> t, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            int i = v.Count; v.AddRange(new[] {a,b,c,d}); t.AddRange(new[] {i,i+1,i+2,i,i+2,i+3});
        }
    }
}
