using System;
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
    // PROTECT-001..004: the marked paving matches the existing zone, without adding obstacles.
    internal static class ChapterThreeProtectedPointVisuals
    {
        private const string RootName="Quarry protected supply inlay";
        private const string Folder="Assets/CrowdPunch/Data/ChapterThreeQuarry/ProtectedPoint";

        [MenuItem("Crowd Punch/Art Direction/Refresh Chapter Three Protected Point")]
        public static void Refresh()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode || Lightmapping.isRunning)
                throw new InvalidOperationException("Stop Play mode and baking before refreshing the protected point.");
            for(int i=0;i<SceneManager.sceneCount;i++)if(SceneManager.GetSceneAt(i).isDirty)
                throw new InvalidOperationException("Save scene edits before refreshing the protected point.");
            var setup=EditorSceneManager.GetSceneManagerSetup();
            try
            {
                var scene=EditorSceneManager.OpenScene(ChapterThreeQuarryBuilder.SubPath(24));Apply(scene);
                EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            }
            finally{EditorSceneManager.RestoreSceneManagerSetup(setup);}
        }

        internal static void Apply(Scene scene)
        {
            if(!scene.path.Contains("/Campaign_24/"))return;
            var objective=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<ProtectedPointAuthoring>(true)).Single();
            var marker=objective.GetComponentsInChildren<MeshRenderer>(true).Single(r=>r.name.StartsWith("Protected Zone",StringComparison.Ordinal));
            foreach(var old in scene.GetRootGameObjects().Where(g=>g.name==RootName).ToArray())Object.DestroyImmediate(old);
            System.IO.Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
            var baseMaterial=Finish("Turquoise foundation","Court",new Color(.24f,.39f,.39f));
            var stone=Finish("Turquoise cut stone","Court",new Color(.35f,.53f,.51f));
            var alternate=Finish("Turquoise cut stone alternate","Court",new Color(.38f,.55f,.52f));
            var border=Finish("Limestone edge","Limestone",new Color(.76f,.73f,.61f));
            var chalk=Finish("Supply stamp chalk","Chalk Local",new Color(.88f,.83f,.66f));
            marker.sharedMaterial=baseMaterial;EditorUtility.SetDirty(marker);
            var root=new GameObject(RootName).transform;SceneManager.MoveGameObjectToScene(root.gameObject,scene);
            root.position=new Vector3(objective.zoneCenter.x,marker.bounds.max.y,objective.zoneCenter.y);
            var size=objective.zoneSize;var half=size*.5f;
            const float edge=.13f;
            for(int x=0;x<4;x++)for(int z=0;z<2;z++)
            {
                float width=(size.x-edge*2)/4,depth=(size.y-edge*2)/2;
                Box(root,"Protected turquoise paving",new Vector3(-half.x+edge+width*(x+.5f),.006f,-half.y+edge+depth*(z+.5f)),new Vector3(width-.035f,.008f,depth-.035f),(x+z)%2==0?stone:alternate);
            }
            foreach(int side in new[]{-1,1})
            {
                // The outer limestone edge is exactly on the authored breach boundary.
                Box(root,"Exact protected side boundary",new Vector3(side*(half.x-edge*.5f),.008f,0),new Vector3(edge,.012f,size.y),border);
                Box(root,"Exact protected end boundary",new Vector3(0,.008f,side*(half.y-edge*.5f)),new Vector3(size.x-edge*2,.012f,edge),border);
                foreach(int end in new[]{-1,1})
                    Box(root,"Protected corner inlay",new Vector3(side*(half.x-.17f),.018f,end*(half.y-.17f)),new Vector3(.34f,.008f,.34f),chalk);
            }
            // An inlaid cargo stamp identifies the supply endpoint without a raised prop.
            float y=.020f;
            foreach(int side in new[]{-1,1})
            {
                Box(root,"Carved cargo stamp edge",new Vector3(side*.88f,y,0),new Vector3(.065f,.007f,1.18f),chalk);
                Box(root,"Carved cargo stamp edge",new Vector3(0,y,side*.56f),new Vector3(1.76f,.007f,.065f),chalk);
                var diagonal=Box(root,"Carved cargo brace",new Vector3(0,y+.001f,0),new Vector3(.065f,.007f,2.04f),chalk);
                diagonal.transform.localRotation=Quaternion.Euler(0,side*57.5f,0);
            }
            if(root.GetComponentsInChildren<Collider>(true).Length!=0)throw new InvalidOperationException("Protected paving must not add collision.");
        }

        private static Material Finish(string name,string sourceName,Color color)
        {
            string path=Folder+"/"+name+".mat";
            var source=AssetDatabase.LoadAssetAtPath<Material>(ChapterThreeQuarryAssets.Output+"/Materials/"+sourceName+".mat");
            if(source==null)throw new InvalidOperationException("Missing protected point palette: "+sourceName);
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(material==null){material=new Material(source);AssetDatabase.CreateAsset(material,path);}
            material.CopyPropertiesFromMaterial(source);material.SetColor("_BaseColor",color);material.SetColor("_WashColor",Color.Lerp(color,Color.white,.08f));
            material.SetFloat("_TextureStrength",.045f);material.SetFloat("_WashStrength",.045f);material.SetFloat("_Variation",.015f);
            material.SetFloat("_CameraCutaway",0);material.SetFloat("_UseEntityColor",0);material.SetFloat("_ObjectSpacePaint",0);material.SetFloat("_UseBakedShadowMask",0);
            EditorUtility.SetDirty(material);return material;
        }

        private static GameObject Box(Transform parent,string name,Vector3 position,Vector3 size,Material material)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);Object.DestroyImmediate(go.GetComponent<Collider>());go.name=name;
            go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localScale=size;
            var renderer=go.GetComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveGI=ReceiveGI.LightProbes;renderer.forceMeshLod=0;
            GameObjectUtility.SetStaticEditorFlags(go,0);return go;
        }
    }
}
