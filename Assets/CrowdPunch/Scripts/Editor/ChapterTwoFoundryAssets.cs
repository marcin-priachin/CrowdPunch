using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CrowdPunch.Editor
{
    // VISION-006: owned palette, shared imported geometry, no source-pack edits.
    internal static class ChapterTwoFoundryAssets
    {
        internal const string Output = "Assets/CrowdPunch/Data/ChapterTwoFoundry";
        internal static Material Steel, Copper, Brass, Teal, Brick, Coal, Ground, Earth, Ivory;

        internal static void Prepare()
        {
            Directory.CreateDirectory(Output + "/Materials");
            Directory.CreateDirectory(Output + "/Meshes");
            AssetDatabase.Refresh();
            Steel = Surface("Blue steel", new Color(.24f,.31f,.36f));
            Copper = Surface("Oxidized copper", new Color(.57f,.31f,.18f));
            Brass = Surface("Worn brass", new Color(.72f,.52f,.25f));
            Teal = Surface("Muted teal enamel", new Color(.20f,.39f,.40f));
            Brick = Surface("Kiln brick", new Color(.42f,.29f,.24f), "Stylized_Bricks_005", .25f,.27f);
            Coal = Surface("Coal and dark iron", new Color(.13f,.17f,.19f));
            Ground = Surface("Quiet blue grey paving", new Color(.46f,.51f,.54f), "Stylized_Stone_Floor_002", .19f,.36f);
            Earth = Surface("Foundry earth", new Color(.38f,.34f,.28f), "Stylized_Ground_002", .17f,.35f);
            Ivory = Surface("Ivory industrial paint", new Color(.86f,.83f,.67f));
        }

        internal static Material Surface(string name, Color color, string texture = null, float scale = .2f, float strength = 0)
        {
            string path = Output + "/Materials/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Shader.Find("CrowdPunch/Cel/Village Surface")); AssetDatabase.CreateAsset(m,path); }
            m.shader=Shader.Find(texture==null?"CrowdPunch/Cel/Environment":"CrowdPunch/Cel/Village Surface");
            m.name=name; m.enableInstancing=true; m.SetColor("_BaseColor",color);
            if (texture != null)
            {
                m.SetFloat("_TextureScale",scale);m.SetFloat("_TextureStrength",strength);m.SetFloat("_Variation",.055f);
                m.SetFloat("_ReceiveShadows",1);m.SetFloat("_Meadow",0);
                string source = AssetDatabase.FindAssets(texture+"_basecolor t:Texture2D",new[]{"Assets/CrowdPunch/Textures"})
                    .Select(AssetDatabase.GUIDToAssetPath).First();
                m.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(source));
            }
            EditorUtility.SetDirty(m); return m;
        }

        internal static GameObject Shape(Transform parent, string name, Vector3 bottom, Vector3 size, Material material, bool cylinder=false)
        {
            var go=GameObject.CreatePrimitive(cylinder?PrimitiveType.Cylinder:PrimitiveType.Cube);
            go.name=name; Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent,false); go.transform.localPosition=bottom+Vector3.up*size.y*.5f;
            go.transform.localScale=new Vector3(size.x,cylinder?size.y*.5f:size.y,size.z);
            var r=go.GetComponent<MeshRenderer>(); r.sharedMaterial=material; r.forceMeshLod=0;
            return go;
        }

        internal static GameObject Model(Transform parent, string model, Vector3 bottom, Vector3 size, Material material, float yaw=0)
        {
            string path="Assets/CrowdPunch/Models/kenney_factory-kit_3.0/Models/FBX format/"+model+".fbx";
            var source=AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if(source==null) throw new InvalidOperationException("Missing factory model: "+path);
            var root=new GameObject(model); root.transform.SetParent(parent,false);
            var instance=Object.Instantiate(source,root.transform,false);
            // Factory cogs are supplied flat; stand the governor wheel upright before fitting.
            if(model.StartsWith("cog-",StringComparison.Ordinal)) instance.transform.localRotation=Quaternion.Euler(90,0,0);
            var renderers=root.GetComponentsInChildren<MeshRenderer>(true);
            var bounds=renderers[0].bounds;
            foreach(var r in renderers.Skip(1)) bounds.Encapsulate(r.bounds);
            // Some factory facades are true planes. Keep their zero-thickness axis finite.
            var scale=new Vector3(Fit(size.x,bounds.size.x),Fit(size.y,bounds.size.y),Fit(size.z,bounds.size.z));
            root.transform.localScale=scale; root.transform.localRotation=Quaternion.Euler(0,yaw,0);
            // Bounds are sampled before placement. Anchor the actual model base, not its pivot.
            root.transform.localPosition=bottom-root.transform.localRotation*Vector3.Scale(new Vector3(bounds.center.x,bounds.min.y,bounds.center.z)-parent.position,scale);
            foreach(var collider in root.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);
            foreach(var animator in root.GetComponentsInChildren<Animator>(true)) Object.DestroyImmediate(animator);
            foreach(var lod in root.GetComponentsInChildren<LODGroup>(true)) lod.ForceLOD(0);
            foreach(var r in renderers) { r.sharedMaterials=Enumerable.Repeat(material,r.sharedMaterials.Length).ToArray(); r.forceMeshLod=0; }
            return root;
        }

        private static float Fit(float desired,float extent)=>extent<.0001f?1:desired/extent;

        internal static void GroundPlane(Transform parent,Vector3 center,Vector3 size)
        {
            string path=Output+"/Meshes/Subdivided earth terrace.asset";
            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(mesh==null)
            {
                const int cells=48;var vertices=new Vector3[(cells+1)*(cells+1)];var uv=new Vector2[vertices.Length];
                var triangles=new int[cells*cells*6];
                for(int z=0;z<=cells;z++)for(int x=0;x<=cells;x++){int i=z*(cells+1)+x;vertices[i]=new Vector3(x/(float)cells-.5f,0,z/(float)cells-.5f);uv[i]=new Vector2(x/(float)cells,z/(float)cells);}
                for(int z=0;z<cells;z++)for(int x=0;x<cells;x++){int v=z*(cells+1)+x,t=(z*cells+x)*6;triangles[t]=v;triangles[t+1]=v+cells+1;triangles[t+2]=v+1;triangles[t+3]=v+1;triangles[t+4]=v+cells+1;triangles[t+5]=v+cells+2;}
                mesh=new Mesh{name="Subdivided earth terrace",vertices=vertices,uv=uv,triangles=triangles};mesh.RecalculateNormals();mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,path);
            }
            var go=new GameObject("Continuous exterior earth terrace",typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(parent,false);
            go.transform.localPosition=center;go.transform.localScale=size;go.GetComponent<MeshFilter>().sharedMesh=mesh;
            go.GetComponent<MeshRenderer>().sharedMaterial=Earth;go.GetComponent<MeshRenderer>().forceMeshLod=0;
        }

        internal static void Roof(Transform parent,Vector3 bottom,Vector3 size)
        {
            string path=Output+"/Meshes/Closed pitched roof.asset";
            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(mesh==null)
            {
                var p=new[]{new Vector3(-.5f,0,-.5f),new Vector3(.5f,0,-.5f),new Vector3(-.5f,1,0),
                    new Vector3(.5f,1,0),new Vector3(-.5f,0,.5f),new Vector3(.5f,0,.5f)};
                int[] triangles={0,2,1,1,2,3,2,4,3,3,4,5,0,4,2,1,3,5,0,1,4,1,5,4};
                // Split face vertices keep the cel roof ridge crisp.
                mesh=new Mesh{name="Closed pitched roof"};mesh.vertices=triangles.Select(i=>p[i]).ToArray();
                mesh.triangles=Enumerable.Range(0,triangles.Length).ToArray();mesh.RecalculateNormals();mesh.RecalculateBounds();
                AssetDatabase.CreateAsset(mesh,path);
            }
            var go=new GameObject("Closed metal roof",typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(parent,false);
            go.transform.localPosition=bottom;go.transform.localScale=size;
            go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<MeshRenderer>().sharedMaterial=Teal;go.GetComponent<MeshRenderer>().forceMeshLod=0;
        }
    }
}
