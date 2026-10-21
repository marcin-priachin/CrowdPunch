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
    internal static class ChapterSevenVaultAssets
    {
        internal const string Output = "Assets/CrowdPunch/Data/ChapterSevenVault";
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
                // Slate combat stone remains darker and quieter than the frost at the margins.
                case "Court": color = new Color(.43f,.50f,.57f); wash = new Color(.48f,.55f,.60f); texture = "Stylized_Stone_Floor_001_basecolor"; detail = .25f; variation = .015f; scale = .28f; break;
                case "Earth": color = new Color(.65f,.73f,.77f); wash = new Color(.72f,.79f,.81f); texture = "Stylized_Ice_001_basecolor"; detail = .10f; variation = .025f; scale = .28f; break;
                case "Limestone": color = new Color(.67f,.73f,.76f); wash = new Color(.76f,.79f,.78f); texture = "Vol_23_1_Rocks_Base_Color"; detail = .10f; variation = .035f; scale = .40f; break;
                case "Cliff": color = new Color(.40f,.55f,.66f); wash = new Color(.56f,.69f,.74f); texture = "Stylized_Ice_001_basecolor"; detail = .08f; variation = .035f; scale = .32f; break;
                case "Rock": color = new Color(.39f,.47f,.56f); wash = new Color(.47f,.56f,.63f); texture = "Stylized_Rocks_003_basecolor"; detail = .10f; variation = .035f; scale = .55f; break;
                case "Crystal": color = new Color(.38f,.66f,.73f); wash = new Color(.60f,.78f,.81f); texture="Stylized_Crystal_001_color"; detail=.10f; variation=.02f; scale=.55f; break;
                case "Slate": color = new Color(.35f,.44f,.48f); wash = new Color(.48f,.54f,.56f); break;
                case "Sage": color = new Color(.37f,.55f,.59f); wash = new Color(.52f,.67f,.68f); break;
                case "Fabric": color = new Color(.28f,.44f,.53f); wash = new Color(.40f,.55f,.61f); variation = .02f; break;
                case "Timber": color = new Color(.47f,.38f,.27f); wash = new Color(.57f,.48f,.32f); texture = "Stylized_Wood_Wall_001_basecolor"; detail = .24f; break;
                case "Ochre": color = new Color(.62f,.46f,.23f); wash = new Color(.67f,.56f,.37f); break;
                case "Impact": color = new Color(.73f,.37f,.19f); wash = new Color(.67f,.40f,.30f); break;
                case "Chalk": color = new Color(.86f,.88f,.82f); wash = new Color(.73f,.79f,.80f); break;
                case "Fracture": color = new Color(.22f,.28f,.29f); wash = new Color(.31f,.35f,.37f); break;
                default: throw new ArgumentException(kind);
            }
            if (distant) { color = Color.Lerp(color,new Color(.61f,.71f,.78f),.5f); wash=color; detail=.025f; variation=.012f; }
            material.SetColor("_BaseColor", color); material.SetColor("_WashColor", wash);
            material.SetColor("_ShadowColor", new Color(.40f,.48f,.62f));
            material.SetColor("_LightColor", new Color(.91f,.97f,1));
            material.SetFloat("_Contrast", distant ? .48f : .78f);
            material.SetFloat("_WashStrength", kind == "Court" ? .06f : .10f);
            material.SetFloat("_GrainStrength", distant ? .01f : .03f);
            material.SetFloat("_ContinuousPaint",1);
            material.SetFloat("_StrataStrength",kind=="Cliff" ? (distant?.015f:.045f) : 0);
            material.SetFloat("_MineralStrength",kind=="Rock" ? .035f : 0);
            material.SetFloat("_Variation", variation); material.SetFloat("_TextureScale", scale);
            material.SetFloat("_TextureStrength", texture == null ? 0 : detail);
            material.SetFloat("_CameraCutaway", decoration ? 1 : 0);
            material.SetFloat("_SceneryHaze", 0);
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
                    .Where(p=>!p.StartsWith(Output+"/",StringComparison.Ordinal)).OrderBy(p=>p.StartsWith("Assets/CrowdPunch/Textures/",StringComparison.Ordinal)?0:1).ThenBy(p=>p,StringComparer.Ordinal).FirstOrDefault();
                if(source==null || !AssetDatabase.CopyAsset(source,path))throw new InvalidOperationException("Missing vault texture "+name);
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
            if (source == null) throw new InvalidOperationException("Missing vault model: " + path);
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
            if(points.Length==0)throw new InvalidOperationException("Empty vault assembly: "+go.name);
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

        internal static Mesh CrystalMesh()
        {
            string path=Output+"/Meshes/Faceted vault crystal.asset";
            var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(existing!=null)return existing;
            var v=new List<Vector3>();var t=new List<int>();
            for(int i=0;i<6;i++)
            {
                float a=i*Mathf.PI/3,b=(i+1)*Mathf.PI/3;
                var x=new Vector3(Mathf.Cos(a)*.5f,-.5f,Mathf.Sin(a)*.5f);
                var z=new Vector3(Mathf.Cos(b)*.5f,-.5f,Mathf.Sin(b)*.5f);
                Quad(v,t,x,x+Vector3.up*.72f,z+Vector3.up*.72f,z);
                int j=v.Count;v.AddRange(new[]{x+Vector3.up*.72f,Vector3.up*.5f,z+Vector3.up*.72f});t.AddRange(new[]{j,j+1,j+2});
                j=v.Count;v.AddRange(new[]{Vector3.down*.5f,x,z});t.AddRange(new[]{j,j+1,j+2});
            }
            return SaveMesh("Faceted vault crystal",v,t);
        }

        internal static void Quad(List<Vector3> v, List<int> t, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            int i = v.Count; v.AddRange(new[] {a,b,c,d}); t.AddRange(new[] {i,i+1,i+2,i,i+2,i+3});
        }
    }
}

