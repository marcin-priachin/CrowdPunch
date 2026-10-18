using System.Linq;
using CrowdPunch.Authoring;
using CrowdPunch.Utilities;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;
using static CrowdPunch.Editor.ChapterTwoFoundryAssets;

namespace CrowdPunch.Editor
{
    // Reuse existing ECS visual bindings; all state and collision remain gameplay-owned.
    internal static class ChapterTwoFoundryObjectives
    {
        internal static void Build(Scene scene,int stage)
        {
            foreach(var old in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true))
                .Where(t=>t.name=="Foundry objective fittings" || t.name=="Foundry exit landmark" || t.name=="Foundry exit cues").ToArray())
                if(old!=null) Object.DestroyImmediate(old.gameObject);
            var gates=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<BarricadeAuthoring>(true)).ToArray();
            foreach(var gate in gates)
            {
                if(gate.GetComponent<ShellTargetAuthoring>()!=null) Shell(gate);
                else if(gate.GetComponent<TrackObjectAuthoring>()!=null) Track(gate);
                else if(gate.cover!=null || !gate.completeOnDestruction) SegmentedTarget(gate);
                if(gate.exit!=null && !gate.completeOnDestruction && gate.GetComponent<TrackObjectAuthoring>()==null) Exit(gate);
            }
            foreach(var cover in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<RotatingCoverAuthoring>(true)))
            {
                var shield=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/CrowdPunch/Data/ChapterOneVillage/RotatingTarget/Braced shutter with direction.asset");
                foreach(var panel in cover.GetComponentsInChildren<CoverPanelAuthoring>(true))
                {
                    var r=panel.GetComponent<MeshRenderer>();
                    if(shield!=null) panel.GetComponent<MeshFilter>().sharedMesh=shield;
                    bool edge=panel.index==0 || panel.index==CoverGeometry.PanelCount-1;
                    r.sharedMaterials=shield!=null?new[]{edge?Ivory:Teal,Brass,Coal}:new[]{edge?Ivory:Teal};
                    Dynamic(r);
                }
            }
            foreach(var r in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<MeshRenderer>(true)))
            {
                if(r.name.Contains("Rail" ) || r.name.Contains("rail")) r.sharedMaterials=Enumerable.Repeat(Steel,r.sharedMaterials.Length).ToArray();
                if(r.GetComponent<TrackSocketVisualAuthoring>()!=null) {r.sharedMaterial=Brass;Dynamic(r);}
            }
        }

        private static void SegmentedTarget(BarricadeAuthoring gate)
        {
            foreach(var old in gate.GetComponentsInChildren<BarricadeVisualAuthoring>(true)) Object.DestroyImmediate(old.gameObject);
            int index=0;
            for(int column=0;column<3;column++) for(int row=0;row<3;row++)
            {
                var size=new Vector3(gate.size.x/3-.018f,gate.size.y/3-.018f,gate.size.z);
                var center=new Vector3((column-1)*gate.size.x/3,(row-1)*gate.size.y/3,0);
                Piece(gate,"Segmented copper impact plate",center,size,Copper,index++);
                foreach(int side in new[]{-1,1})
                    Piece(gate,"Plate rivet",center+new Vector3(0,0,side*(gate.size.z*.5f+.025f)),new Vector3(.10f,.10f,.05f),Brass,index++);
            }
            for(int face=-1;face<=1;face+=2) for(int damage=1;damage<=2;damage++)
                Piece(gate,"Progressive metal fracture",new Vector3(0,(damage-1.5f)*gate.size.y/3,face*(gate.size.z*.5f+.055f)),
                    new Vector3(gate.size.x*.95f,.09f,.04f),Coal,index++,damage);
            if(gate.cover!=null)
            {
                var red=Surface("Hittable target red",new Color(.78f,.22f,.12f));
                for(int side=0;side<4;side++)
                {
                    var turn=Quaternion.Euler(0,90*side,0);
                    var p=turn*new Vector3(0,0,-gate.size.z*.5f-.075f);
                    var a=Piece(gate,"Ivory target face",p,new Vector3(gate.size.x*.7f,gate.size.y*.45f,.07f),Ivory,index++);a.transform.localRotation=turn;
                    var b=Piece(gate,"Red impact center",p+turn*Vector3.back*.045f,new Vector3(gate.size.x*.4f,gate.size.y*.24f,.04f),red,index++);b.transform.localRotation=turn;
                }
            }
        }

        private static GameObject Piece(BarricadeAuthoring gate,string name,Vector3 center,Vector3 size,Material material,int index,int stage=0)
        {
            var go=Shape(gate.transform,name,center-Vector3.up*size.y*.5f,size,material);
            var visual=go.AddComponent<BarricadeVisualAuthoring>();visual.barricade=gate;visual.crackStage=stage;
            var piece=go.AddComponent<VillageGatePieceAuthoring>();piece.kind=stage==0?0:1;piece.damageStage=stage;
            piece.seed=(index*37%101)/100f;piece.velocity=new Vector3((piece.seed-.5f)*5,3+piece.seed*2,2+piece.seed*2);
            piece.spin=new Vector3(2+piece.seed*3,1+piece.seed*4,2);piece.restingHeight=size.z*.5f;
            Dynamic(go.GetComponent<MeshRenderer>());return go;
        }

        private static void Shell(BarricadeAuthoring gate)
        {
            foreach(var visual in gate.GetComponentsInChildren<BarricadeVisualAuthoring>(true))
            {
                var r=visual.GetComponent<MeshRenderer>();
                if(r==null) continue;
                r.sharedMaterial=visual.crackStage>0?Coal:visual.shellCore?Copper:Teal;Dynamic(r);
            }
            // The original shell fragment/core and damage-stage bindings remain authoritative.
            var root=new GameObject("Foundry objective fittings").transform;root.SetParent(gate.transform,false);
            int index=0;
            for(int side=-1;side<=1;side+=2) for(int row=-1;row<=1;row+=2)
            {
                var go=Shape(root,"Shell reinforcing bolt",new Vector3(side*gate.size.x*.32f,row*gate.size.y*.28f,gate.size.z*.5f),new Vector3(.22f,.22f,.13f),Brass);
                var visual=go.AddComponent<BarricadeVisualAuthoring>();visual.barricade=gate;visual.shellVisual=true;
                visual.debrisDirection=new Vector3(side,1,1);Dynamic(go.GetComponent<MeshRenderer>());index++;
            }
        }

        private static void Track(BarricadeAuthoring gate)
        {
            foreach(var visual in gate.GetComponentsInChildren<BarricadeVisualAuthoring>(true))
            {
                var r=visual.GetComponent<MeshRenderer>();if(r==null)continue;
                r.sharedMaterial=visual.crackStage>0?Coal:Teal;Dynamic(r);
            }
            var root=new GameObject("Foundry objective fittings").transform;root.SetParent(gate.transform,false);
            foreach(int side in new[]{-1,1})
            {
                var go=Shape(root,"Cargo corner band",new Vector3(side*gate.size.x*.36f,-gate.size.y*.5f,-gate.size.z*.5f-.03f),
                    new Vector3(.18f,gate.size.y,.06f),Brass);
                var visual=go.AddComponent<BarricadeVisualAuthoring>();visual.barricade=gate;Dynamic(go.GetComponent<MeshRenderer>());
            }
        }

        private static void Exit(BarricadeAuthoring gate)
        {
            foreach(var r in gate.exit.GetComponentsInChildren<MeshRenderer>(true)) r.enabled=false;
            var root=new GameObject("Foundry exit landmark").transform;root.SetParent(gate.exit,false);
            root.position=new Vector3(gate.exit.position.x,gate.transform.position.y-gate.size.y*.5f,gate.exit.position.z);
            foreach(int side in new[]{-1,1})
            {
                Shape(root,"Waygate metal footing",new Vector3(side*2.5f,0,1.3f),new Vector3(.8f,.45f,.8f),Steel);
                Shape(root,"Waygate copper upright",new Vector3(side*2.5f,.4f,1.3f),new Vector3(.32f,4,.38f),Copper);
            }
            Shape(root,"Foundry destination lintel",new Vector3(0,4.3f,1.3f),new Vector3(5.6f,.45f,.55f),Teal);
            var arrow=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/CrowdPunch/Data/ChapterOneVillage/Exits/Forward arrow.asset");
            var sign=Shape(root,"Forward exit sign",new Vector3(0,4.15f,.95f),new Vector3(.85f,.85f,.1f),Ivory);
            if(arrow!=null){sign.GetComponent<MeshFilter>().sharedMesh=arrow;sign.transform.localScale=Vector3.one;sign.transform.localRotation=Quaternion.Euler(-90,0,0);}
            var cues=new GameObject("Foundry exit cues").transform;SceneManager.MoveGameObjectToScene(cues.gameObject,gate.gameObject.scene);cues.position=root.position;
            // Radius is read from the real gate; cues are separate from the fixed landmark.
            for(int i=0;i<24;i++)
            {
                float angle=i*Mathf.PI*2/24;
                var p=new Vector3(Mathf.Sin(angle)*(gate.exitRadius-.06f),.05f,Mathf.Cos(angle)*(gate.exitRadius-.06f));
                var go=Shape(cues,"State driven exit perimeter",p,new Vector3(.32f,.035f,.10f),Brass);
                go.transform.localRotation=Quaternion.Euler(0,angle*Mathf.Rad2Deg,0);
                go.AddComponent<VillageExitCueAuthoring>().gate=gate;Dynamic(go.GetComponent<MeshRenderer>());
            }
            if(arrow!=null) for(int i=0;i<3;i++)
            {
                var go=Shape(cues,"Open route arrow",new Vector3(0,.08f,-2.2f+i*.7f),Vector3.one,Brass);
                go.GetComponent<MeshFilter>().sharedMesh=arrow;go.transform.localScale=new Vector3(.65f,1,.65f);
                go.transform.localPosition=new Vector3(0,.08f,-2.2f+i*.7f);
                var cue=go.AddComponent<VillageExitCueAuthoring>();cue.gate=gate;cue.arrow=true;cue.phase=i*.9f;
                Dynamic(go.GetComponent<MeshRenderer>());
            }
        }

        private static void Dynamic(MeshRenderer r)
        {
            r.shadowCastingMode=ShadowCastingMode.Off;r.forceMeshLod=0;
            foreach(var t in r.GetComponentsInParent<Transform>(true)) GameObjectUtility.SetStaticEditorFlags(t.gameObject,0);
        }
    }
}
