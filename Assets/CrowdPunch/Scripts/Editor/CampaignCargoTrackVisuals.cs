using System;
using System.Collections.Generic;
using System.Linq;
using CrowdPunch.Authoring;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

namespace CrowdPunch.Editor
{
    // TRACK-001..006: visual children follow the existing translating ECS objective.
    internal static class CampaignCargoTrackVisuals
    {
        private const string CartRoot="Chapter cargo transfer cart";
        private const string RailRoot="Chapter cargo rail fittings";

        [MenuItem("Crowd Punch/Art Direction/Refresh Campaign 19 and 26 Cargo Tracks")]
        public static void Refresh()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode || Lightmapping.isRunning)
                throw new InvalidOperationException("Stop Play mode and baking before refreshing cargo tracks.");
            for(int i=0;i<SceneManager.sceneCount;i++)if(SceneManager.GetSceneAt(i).isDirty)
                throw new InvalidOperationException("Save scene edits before refreshing cargo tracks.");
            var setup=EditorSceneManager.GetSceneManagerSetup();
            try
            {
                foreach(int stage in new[]{19,26})
                {
                    var scene=EditorSceneManager.OpenScene($"Assets/CrowdPunch/Scenes/Campaign/Campaign_{stage}/Campaign_{stage} Sub Scene.unity");
                    Apply(scene);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
                }
                AssetDatabase.SaveAssets();
            }
            finally {EditorSceneManager.RestoreSceneManagerSetup(setup);}
        }

        internal static void Apply(Scene scene) => Apply(scene,false);

        internal static void ApplyObservatory(Scene scene) => Apply(scene,true);

        internal static void ApplyNecropolis(Scene scene) => Apply(scene,false,true);

        internal static void ApplyCanyon(Scene scene) => Apply(scene,false,false,true);

        private static void Apply(Scene scene,bool observatory,bool necropolis=false,bool canyon=false)
        {
            int stage=canyon ? (scene.path.Contains("/Campaign_54/")?54:0) : necropolis ? (scene.path.Contains("/Campaign_46/")?46:0) : observatory ? (scene.path.Contains("/Campaign_32/")?32:scene.path.Contains("/Campaign_39/")?39:0) : scene.path.Contains("/Campaign_19/")?19:scene.path.Contains("/Campaign_26/")?26:0;
            if(stage==0)return;
            bool quarry=stage==26 || observatory || necropolis || canyon;
            string chapter="Assets/CrowdPunch/Data/"+(canyon?"ChapterSixCanyon":necropolis?"ChapterFiveNecropolis":observatory?"ChapterFourObservatory":quarry?"ChapterThreeQuarry":"ChapterTwoFoundry");
            string folder=chapter+"/TrackCargo";
            System.IO.Directory.CreateDirectory(folder+"/Materials");AssetDatabase.Refresh();
            string Source(string foundry,string stone)=>quarry?chapter+"/Materials/"+stone+".mat":chapter+"/PaintedLook/Materials/Painted "+foundry+"_Near_0.mat";
            Material Finish(string name,string foundry,string stone,bool moving=false,bool state=false)
            {
                var source=AssetDatabase.LoadAssetAtPath<Material>(Source(foundry,stone));
                if(source==null)throw new InvalidOperationException("Missing chapter cargo palette: "+Source(foundry,stone));
                string path=folder+"/Materials/"+name+".mat";
                var material=AssetDatabase.LoadAssetAtPath<Material>(path);
                if(material==null){material=new Material(source);AssetDatabase.CreateAsset(material,path);}
                material.CopyPropertiesFromMaterial(source);material.enableInstancing=true;
                material.SetFloat("_ObjectSpacePaint",moving?1:0);material.SetFloat("_UseEntityColor",state?1:0);
                material.SetFloat("_CameraCutaway",0);material.SetFloat("_UseBakedShadowMask",0);
                EditorUtility.SetDirty(material);return material;
            }
            var frame=Finish("Cart frame","Steel","Slate",true);
            var body=Finish("Cart body","Teal",observatory?"Sage Local":"Slate",true);
            var trim=Finish("Cart edging","Brass","Ochre Local",true);
            var cargo=Finish("Cart cargo","Copper",observatory?"Ochre Local":"Limestone Local",true);
            var face=Finish("Cart strike panels","Ivory","Chalk Local",true);
            var rail=Finish("Rail iron","Steel","Slate");
            var sleeper=Finish("Rail sleepers","Coal","Timber Decorative");
            var fastener=Finish("Rail fasteners","Brass","Ochre Local");
            var socket=Finish("Dock state enamel","Teal","Slate Local",true,true);
            socket.SetColor("_BaseColor",new Color(.22f,.54f,.65f));EditorUtility.SetDirty(socket);
            var track=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<TrackObjectAuthoring>(true)).Single();
            foreach(var r in track.GetComponentsInChildren<MeshRenderer>(true))
            {
                if(r.transform.GetComponentsInParent<Transform>().Any(t=>t.name==CartRoot))continue;
                r.enabled=false;EditorUtility.SetDirty(r);
            }
            foreach(var old in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).Where(t=>t.name==CartRoot || t.name==RailRoot).ToArray())
                Object.DestroyImmediate(old.gameObject);
            var cart=new GameObject(CartRoot).transform;cart.SetParent(track.transform,false);
            Box(cart,"Reinforced transfer deck",new Vector3(0,-1.02f,0),new Vector3(3.48f,.22f,3.04f),frame);
            var hopper=Box(cart,canyon?"Canyon ore transfer carrier":necropolis?"Ceremonial stone carrier":observatory?"Jade instrument carrier":quarry?"Quarry steel carrier":"Foundry enamel carrier",new Vector3(0,-.14f,0),new Vector3(3.22f,1.52f,2.78f),body);
            hopper.GetComponent<MeshFilter>().sharedMesh=Hopper(folder);
            foreach(int x in new[]{-1,1})foreach(int z in new[]{-1,1})
            {
                // Ball transfer rollers can follow either axis without rotating the solid.
                var roller=Box(cart,"Multidirectional guide roller",new Vector3(x*1.55f,-1.34f,z*1.05f),Vector3.one*.50f,frame);
                roller.GetComponent<MeshFilter>().sharedMesh=Roller(folder);
                Box(cart,"Roller housing",new Vector3(x*1.48f,-1.05f,z*1.05f),new Vector3(.46f,.18f,.52f),trim);
                Box(cart,"Corner reinforcement",new Vector3(x*1.49f,-.13f,z*1.27f),new Vector3(.13f,1.55f,.13f),trim);
            }
            for(int side=0;side<4;side++)
            {
                var turn=Quaternion.Euler(0,side*90,0);
                float depth=side%2==0?1.41f:1.63f;
                var panel=Box(cart,"Pale impact face",turn*new Vector3(0,-.08f,-depth),new Vector3(1.52f,.68f,.045f),face);panel.transform.localRotation=turn;
                var rim=Box(cart,"Carrier rolled lip",turn*new Vector3(0,.65f,-depth+.045f),new Vector3(side%2==0?3.25f:2.80f,.12f,.12f),trim);rim.transform.localRotation=turn;
                foreach(int pin in new[]{-1,1})
                    Box(cart,"Impact face fastener",turn*new Vector3(pin*.62f,-.08f,-depth-.035f),new Vector3(.08f,.08f,.055f),trim).transform.localRotation=turn;
            }
            for(int row=0;row<2;row++)for(int col=0;col<3;col++)
            {
                var block=Box(cart,observatory?"Ochre survey instrument cases":quarry?"Cut limestone cargo":"Copper ingot cargo",new Vector3((col-1)*1.0f,.92f,(row-.5f)*1.18f),new Vector3(.93f,.59f,1.10f),cargo);
                block.transform.localRotation=Quaternion.Euler(0,(col+row)%2==0?3:-3,0);
            }
            foreach(int side in new[]{-1,1})
                Box(cart,observatory?"Brass telescope housing":quarry?"Upper dressed stone":"Upper copper billet",new Vector3(side*.49f,1.48f,0),new Vector3(.90f,.43f,1.88f),cargo).transform.localRotation=Quaternion.Euler(0,side*4,0);

            var fittings=new GameObject(RailRoot).transform;SceneManager.MoveGameObjectToScene(fittings.gameObject,scene);
            var rails=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<MeshRenderer>(true)).Where(r=>r.name=="Track rail").OrderBy(r=>r.transform.position.x).ThenBy(r=>r.transform.position.z).ToArray();
            for(int i=0;i<rails.Length;i++)
            {
                var r=rails[i];var previous=r.sharedMaterial;
                bool explicitReceiver=previous.HasProperty("_UseBakedShadowMask") && previous.GetFloat("_UseBakedShadowMask")>.5f;
                var mask=explicitReceiver?previous.GetTexture("_BakedShadowMask"):null;
                var chart=explicitReceiver?previous.GetVector("_BakedShadowST"):Vector4.zero;
                // Keep the existing baked receiver chart and original rail geometry.
                var painted=Finish("Baked rail "+i,"Steel","Slate");
                if(explicitReceiver)
                {
                    painted.SetTexture("_BakedShadowMask",mask);
                    painted.SetVector("_BakedShadowST",chart);painted.SetFloat("_UseBakedShadowMask",1);
                }
                r.sharedMaterial=painted;EditorUtility.SetDirty(r);EditorUtility.SetDirty(painted);
                var cap=Box(fittings,"Polished rail crown",r.transform.position+Vector3.up*.045f,new Vector3(.065f,.018f,r.transform.lossyScale.z),fastener);
                cap.transform.rotation=r.transform.rotation;
            }
            var points=new List<Vector3>{track.transform.position};points.AddRange(track.corners.Select(c=>c.position));points.Add(track.destination.position);
            for(int leg=0;leg<points.Count-1;leg++)
            {
                var direction=(points[leg+1]-points[leg]).normalized;var right=new Vector3(direction.z,0,-direction.x);
                float length=Vector3.Distance(points[leg],points[leg+1]);
                for(float distance=.8f;distance<length-.3f;distance+=1.6f)
                {
                    var p=points[leg]+direction*distance;p.y=rails[0].transform.position.y-.013f;
                    Box(fittings,quarry?"Quarry timber sleeper":"Foundry cross tie",p,new Vector3(3.55f,.045f,.25f),sleeper).transform.rotation=Quaternion.LookRotation(direction);
                    foreach(int side in new[]{-1,1})Box(fittings,"Rail fixing",p+right*(side*1.55f)+Vector3.up*.065f,new Vector3(.25f,.035f,.22f),rail).transform.rotation=Quaternion.LookRotation(direction);
                }
            }
            foreach(var visual in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<TrackSocketVisualAuthoring>(true)))
            {var r=visual.GetComponent<MeshRenderer>();r.sharedMaterial=socket;EditorUtility.SetDirty(r);}
            if(cart.GetComponentsInChildren<Collider>(true).Length!=0 || fittings.GetComponentsInChildren<Collider>(true).Length!=0)
                throw new InvalidOperationException("Cargo presentation must remain noncolliding.");
        }

        private static GameObject Box(Transform parent,string name,Vector3 center,Vector3 size,Material material)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);Object.DestroyImmediate(go.GetComponent<Collider>());
            go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=center;go.transform.localScale=size;
            var r=go.GetComponent<MeshRenderer>();r.sharedMaterial=material;r.shadowCastingMode=ShadowCastingMode.Off;r.receiveGI=ReceiveGI.LightProbes;r.forceMeshLod=0;
            GameObjectUtility.SetStaticEditorFlags(go,0);return go;
        }

        private static Mesh Hopper(string folder)
        {
            string path=folder+"/Tapered cargo carrier.asset";var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(existing!=null)return existing;
            Vector3[] corners={new(-.45f,-.5f,-.45f),new(.45f,-.5f,-.45f),new(.45f,-.5f,.45f),new(-.45f,-.5f,.45f),new(-.5f,.5f,-.5f),new(.5f,.5f,-.5f),new(.5f,.5f,.5f),new(-.5f,.5f,.5f)};
            int[][] faces={new[]{0,1,5,4},new[]{1,2,6,5},new[]{2,3,7,6},new[]{3,0,4,7},new[]{4,5,6,7},new[]{3,2,1,0}};
            var vertices=new List<Vector3>();var indices=new List<int>();
            foreach(var f in faces){int n=vertices.Count;foreach(int i in f)vertices.Add(corners[i]);indices.AddRange(new[]{n,n+2,n+1,n,n+3,n+2});}
            var mesh=new Mesh{name="Tapered cargo carrier"};mesh.SetVertices(vertices);mesh.SetTriangles(indices,0);mesh.RecalculateNormals();mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,path);return mesh;
        }

        private static Mesh Roller(string folder)
        {
            string path=folder+"/Faceted transfer roller.asset";var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(existing!=null)return existing;
            var vertices=new List<Vector3>();var triangles=new List<int>();
            Vector3 Point(int ring,int side){float y=Mathf.Cos(ring*Mathf.PI/4)*.5f,r=Mathf.Sin(ring*Mathf.PI/4)*.5f,a=side*Mathf.PI/4;return new Vector3(Mathf.Cos(a)*r,y,Mathf.Sin(a)*r);}
            for(int ring=0;ring<4;ring++)for(int side=0;side<8;side++)
            {int n=vertices.Count;vertices.AddRange(new[]{Point(ring,side),Point(ring+1,side),Point(ring+1,side+1),Point(ring,side+1)});triangles.AddRange(new[]{n,n+2,n+1,n,n+3,n+2});}
            var mesh=new Mesh{name="Faceted transfer roller"};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,path);return mesh;
        }
    }
}
