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
    internal static class ChapterFourObservatoryAssets
    {
        internal const string Output = "Assets/CrowdPunch/Data/ChapterFourObservatory";
        private static readonly Dictionary<string, Material> Surfaces = new();
        private static readonly Dictionary<string, Texture2D> Textures = new();

        internal static void Prepare()
        {
            Directory.CreateDirectory(Output + "/Materials");
            Directory.CreateDirectory(Output + "/Meshes");
            Directory.CreateDirectory(Output + "/Textures");
            AssetDatabase.Refresh(); Surfaces.Clear(); Textures.Clear();
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
            string texture = null; float detail = .18f, variation = .035f, scale = .3f;
            switch (kind)
            {
                case "Court": color = new Color(.52f,.58f,.53f); wash = new Color(.50f,.56f,.51f); texture = "Stylized_Stone_Floor_002_basecolor"; detail = .06f; variation = .015f; scale = .23f; break;
                case "Earth": color = new Color(.30f,.39f,.29f); wash = new Color(.40f,.45f,.30f); texture = "Vol_16_2_Sand_Base_Color"; detail = .10f; variation = .025f; scale = .28f; break;
                case "Limestone": color = new Color(.67f,.61f,.43f); wash = new Color(.62f,.58f,.42f); texture = "Vol_23_1_Rocks_Base_Color"; detail = .10f; variation = .035f; scale = .40f; break;
                case "Cliff": color = new Color(.57f,.60f,.53f); wash = new Color(.66f,.62f,.51f); texture = "Stylized_Layered_Rock_001_color"; detail = .08f; variation = .035f; scale = .32f; break;
                case "Rock": color = new Color(.63f,.63f,.56f); wash = new Color(.64f,.61f,.53f); texture = "Stylized_Rocks_003_basecolor"; detail = .10f; variation = .035f; scale = .55f; break;
                case "Slate": color = new Color(.35f,.44f,.48f); wash = new Color(.48f,.54f,.56f); break;
                case "Sage": color = new Color(.27f,.43f,.34f); wash = new Color(.39f,.50f,.35f); break;
                case "Timber": color = new Color(.47f,.38f,.27f); wash = new Color(.57f,.48f,.32f); texture = "Stylized_Wood_Wall_001_basecolor"; detail = .24f; break;
                case "Ochre": color = new Color(.62f,.46f,.23f); wash = new Color(.67f,.56f,.37f); break;
                case "Impact": color = new Color(.73f,.37f,.19f); wash = new Color(.67f,.40f,.30f); break;
                case "Chalk": color = new Color(.88f,.83f,.66f); wash = new Color(.74f,.71f,.57f); break;
                case "Fracture": color = new Color(.22f,.28f,.29f); wash = new Color(.31f,.35f,.37f); break;
                default: throw new ArgumentException(kind);
            }
            if (distant) { color = Color.Lerp(color, new Color(.48f,.61f,.59f), .48f); wash = color; detail *= .22f; variation *= .4f; }
            material.SetColor("_BaseColor", color); material.SetColor("_WashColor", wash);
            material.SetColor("_ShadowColor", new Color(.43f,.54f,.61f));
            material.SetColor("_LightColor", new Color(1,.97f,.86f));
            material.SetFloat("_Contrast", distant ? .48f : .78f);
            material.SetFloat("_WashStrength", kind == "Court" ? .06f : .10f);
            material.SetFloat("_GrainStrength", distant ? .01f : .03f);
            material.SetFloat("_ContinuousPaint",1);
            material.SetFloat("_StrataStrength",kind=="Cliff" ? (distant?.025f:.10f) : 0);
            material.SetFloat("_MineralStrength",kind=="Rock" ? .035f : 0);
            material.SetFloat("_Variation", variation); material.SetFloat("_TextureScale", scale);
            material.SetFloat("_TextureStrength", texture == null ? 0 : detail);
            material.SetFloat("_CameraCutaway", decoration ? 1 : 0);
            material.SetFloat("_ObjectSpacePaint", moving ? 1 : 0);
            material.SetFloat("_UseEntityColor", moving ? 1 : 0);
            material.SetFloat("_ReceiveShadows", 1); material.SetFloat("_Meadow", 0);
            if (texture != null)
            {
                material.SetTexture("_BaseMap", TextureFor(texture));
            }
            EditorUtility.SetDirty(material); Surfaces.Add(key, material); return material;
        }

        private static Texture2D TextureFor(string name)
        {
            if(Textures.TryGetValue(name,out var cached))return cached;
            string path=Output+"/Textures/"+name+".png";
            var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if(texture==null)
            {
                string source=AssetDatabase.FindAssets(name+" t:Texture2D").Select(AssetDatabase.GUIDToAssetPath)
                    .Where(p=>!p.StartsWith(Output+"/",StringComparison.Ordinal)).OrderBy(p=>p,StringComparer.Ordinal).FirstOrDefault();
                if(source==null || !AssetDatabase.CopyAsset(source,path))throw new InvalidOperationException("Missing observatory texture "+name);
            }
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            // Owned copies keep quality tuning local; mip limits still follow the selected tier.
            importer.filterMode=FilterMode.Trilinear;importer.anisoLevel=4;importer.mipmapEnabled=true;
            importer.maxTextureSize=2048;importer.textureCompression=TextureImporterCompression.CompressedHQ;
            importer.wrapMode=TextureWrapMode.Repeat;importer.sRGBTexture=true;
            importer.SaveAndReimport();
            texture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);Textures.Add(name,texture);return texture;
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
            if (source == null) throw new InvalidOperationException("Missing observatory model: " + path);
            var go = new GameObject(model); go.transform.SetParent(parent, false);
            Object.Instantiate(source, go.transform, false);
            var renderers = go.GetComponentsInChildren<MeshRenderer>(true);
            FitModel(go,bottom,envelope,yaw);
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            foreach (var a in go.GetComponentsInChildren<Animator>(true)) Object.DestroyImmediate(a);
            foreach (var lod in go.GetComponentsInChildren<LODGroup>(true)) lod.ForceLOD(0);
            foreach (var r in renderers)
            {
                bool tree=family=="kenney_nature-kit" && model.StartsWith("tree_",StringComparison.Ordinal);
                r.sharedMaterials=r.sharedMaterials.Select(m=>tree && m!=null && (m.name.ToLowerInvariant().Contains("wood") || m.name.ToLowerInvariant().Contains("trunk"))
                    ? Surface("Timber",true,material.name.Contains("Distant")) : material).ToArray();
                r.forceMeshLod=0;
            }
            return go;
        }

        internal static void FitModel(GameObject go,Vector3 bottom,Vector3 envelope,float yaw)
        {
            // Fit in the imported assembly's own axes. World bounds swap width/depth
            // under rotated parents and previously shrank the side-gallery arches.
            go.transform.localScale=Vector3.one;go.transform.localRotation=Quaternion.identity;go.transform.localPosition=Vector3.zero;
            var points=go.GetComponentsInChildren<MeshFilter>(true).Where(f=>f.sharedMesh!=null)
                .SelectMany(f=>f.sharedMesh.vertices.Select(p=>go.transform.InverseTransformPoint(f.transform.TransformPoint(p)))).ToArray();
            if(points.Length==0)throw new InvalidOperationException("Empty observatory assembly: "+go.name);
            var bounds=new Bounds(points[0],Vector3.zero);foreach(var point in points.Skip(1))bounds.Encapsulate(point);
            float fit=Mathf.Min(Fit(envelope.x,bounds.size.x),Fit(envelope.y,bounds.size.y),Fit(envelope.z,bounds.size.z));
            go.transform.localScale=Vector3.one*fit;go.transform.localRotation=Quaternion.Euler(0,yaw,0);
            go.transform.localPosition=bottom-go.transform.localRotation*(new Vector3(bounds.center.x,bounds.min.y,bounds.center.z)*fit);
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
