using System.Linq;
using CrowdPunch.Authoring;
using CrowdPunch.Utilities;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using static CrowdPunch.Editor.ChapterThreeQuarryAssets;

namespace CrowdPunch.Editor
{
    // BARRICADE-007..009, TRACK-006, COVER-005: visuals consume existing ECS state.
    internal static class ChapterThreeQuarryObjectives
    {
        internal static void Apply(Scene scene)
        {
            var gates=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<BarricadeAuthoring>(true)).ToArray();
            foreach(var gate in gates)
            {
                bool track=gate.GetComponent<TrackObjectAuthoring>()!=null;
                foreach(var visual in gate.GetComponentsInChildren<BarricadeVisualAuthoring>(true))
                {
                    var r=visual.GetComponent<MeshRenderer>();if(r==null)continue;
                    r.sharedMaterial=Surface(visual.crackStage>0?"Fracture":track?"Limestone":"Impact",false,false,true);
                    Dynamic(r);
                    // Keep original visual authoring/poses for safe regeneration and comparison.
                    // The new segmented target takes over only the opaque intact body.
                    if(!track && visual.crackStage==0)r.enabled=false;
                }
                if(!track)Segmented(gate);
                else
                {
                    foreach(int side in new[]{-1,1})
                        BoundPiece(gate,"Stone cargo iron strap",new Vector3(side*gate.size.x*.34f,0,-gate.size.z*.5f-.025f),new Vector3(.15f,gate.size.y*.95f,.05f),"Slate",0,side+4);
                }
                if(gate.exit!=null && !gate.completeOnDestruction && !track)Exit(gate);
            }
            foreach(var cover in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<RotatingCoverAuthoring>(true)))
            {
                foreach(var panel in cover.GetComponentsInChildren<CoverPanelAuthoring>(true))
                {
                    var r=panel.GetComponent<MeshRenderer>();
                    bool jamb=panel.index==0 || panel.index==CoverGeometry.PanelCount-1;
                    r.sharedMaterials=Enumerable.Repeat(Surface(jamb?"Chalk":"Slate",false,false,true),r.sharedMaterials.Length).ToArray();
                    Dynamic(r);
                }
            }
            foreach(var r in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<MeshRenderer>(true)))
            {
                if(r.name=="Track rail" || r.name=="Socket end stop")r.sharedMaterial=Surface("Slate");
                if(r.name=="Socket rim" || r.GetComponent<TrackSocketVisualAuthoring>()!=null)
                {r.sharedMaterial=Surface("Ochre",false,false,true);Dynamic(r);}
                if(r.name.StartsWith("Gate terrain"))r.sharedMaterial=Surface("Cliff");
            }
        }

        private static void Segmented(BarricadeAuthoring gate)
        {
            for(int row=0;row<3;row++)for(int col=0;col<2;col++)
            {
                var p=new Vector3((col-.5f)*gate.size.x*.5f,(row-1)*gate.size.y/3,0);
                BoundPiece(gate,"Quarry impact masonry",p,new Vector3(gate.size.x*.5f-.025f,gate.size.y/3-.025f,gate.size.z),"Limestone",0,row*2+col);
            }
            foreach(int side in new[]{-1,1})
            {
                BoundPiece(gate,"Ochre reinforcement",new Vector3(side*gate.size.x*.36f,0,-gate.size.z*.5f-.035f),new Vector3(.14f,gate.size.y*.94f,.07f),"Ochre",0,side+10);
                for(int damage=1;damage<=2;damage++)
                {
                    var go=BoundPiece(gate,"Masonry damage fracture",new Vector3((damage-1.5f)*gate.size.x*.22f,(damage-1.5f)*gate.size.y*.24f,side*(gate.size.z*.5f+.055f)),new Vector3(gate.size.x*.65f,.055f,.035f),"Fracture",damage,damage+15);
                    go.transform.localRotation=Quaternion.Euler(0,0,damage==1?23:-29);
                }
                if(gate.cover!=null)
                    BoundPiece(gate,"Contrasting valve strike face",new Vector3(0,0,side*(gate.size.z*.5f+.07f)),new Vector3(gate.size.x*.5f,gate.size.y*.42f,.09f),"Impact",0,side+20);
            }
        }

        private static GameObject BoundPiece(BarricadeAuthoring gate,string name,Vector3 center,Vector3 size,string kind,int damage,int seed)
        {
            var go=Shape(gate.transform,name,center-Vector3.up*size.y*.5f,size,Surface(kind,false,false,true));
            var visual=go.AddComponent<BarricadeVisualAuthoring>();visual.barricade=gate;visual.crackStage=damage;
            var piece=go.AddComponent<VillageGatePieceAuthoring>();piece.kind=damage==0?0:1;piece.damageStage=damage;
            piece.seed=(seed*37%101)/100f;piece.velocity=new Vector3((piece.seed-.5f)*4,2+piece.seed*2,2+piece.seed);
            piece.spin=new Vector3(2+piece.seed,1+piece.seed*3,2);piece.restingHeight=size.z*.5f;piece.duration=1.2f+piece.seed*.25f;
            Dynamic(go.GetComponent<MeshRenderer>());return go;
        }

        private static void Exit(BarricadeAuthoring gate)
        {
            // Intermediate and final destinations bind to their own real gate, including sector 2.
            foreach(var r in gate.exit.GetComponentsInChildren<MeshRenderer>(true))r.enabled=false;
            var root=new GameObject("Quarry exit destination").transform;root.SetParent(gate.exit,false);
            root.position=new Vector3(gate.exit.position.x,gate.transform.position.y-gate.size.y*.5f,gate.exit.position.z);
            root.rotation=gate.exit.rotation;
            foreach(int side in new[]{-1,1})
            {
                Shape(root,"Quarry waygate footing",new Vector3(side*2.4f,0,1),new Vector3(.9f,.4f,1),Surface("Slate"));
                Shape(root,"Quarry waygate pier",new Vector3(side*2.4f,.4f,1),new Vector3(.65f,3.4f,.8f),Surface("Limestone"));
            }
            Shape(root,"Quarry destination lintel",new Vector3(0,3.8f,1),new Vector3(5.5f,.55f,1),Surface("Limestone"));
            var cues=new GameObject("Quarry state-driven exit cues").transform;SceneManager.MoveGameObjectToScene(cues.gameObject,gate.gameObject.scene);
            cues.position=root.position;cues.rotation=root.rotation;
            var cueMaterial=Surface("Chalk",false,false,true);cueMaterial.SetFloat("_UseEntityColor",1);EditorUtility.SetDirty(cueMaterial);
            for(int i=0;i<24;i++)
            {
                float angle=i*Mathf.PI*2/24;
                var go=Shape(cues,"Gate-owned destination boundary",new Vector3(Mathf.Sin(angle)*(gate.exitRadius-.06f),.05f,Mathf.Cos(angle)*(gate.exitRadius-.06f)),new Vector3(.28f,.03f,.10f),cueMaterial);
                go.transform.localRotation=Quaternion.Euler(0,angle*Mathf.Rad2Deg,0);
                go.AddComponent<VillageExitCueAuthoring>().gate=gate;Dynamic(go.GetComponent<MeshRenderer>());
            }
            var arrow=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/CrowdPunch/Data/ChapterOneVillage/Exits/Forward arrow.asset");
            if(arrow!=null)for(int i=0;i<3;i++)
            {
                var go=Shape(cues,"Open quarry route",new Vector3(0,.07f,-1.4f+i*.65f),Vector3.one, cueMaterial);
                go.GetComponent<MeshFilter>().sharedMesh=arrow;go.transform.localScale=new Vector3(.6f,1,.6f);go.transform.localPosition=new Vector3(0,.07f,-1.4f+i*.65f);
                var cue=go.AddComponent<VillageExitCueAuthoring>();cue.gate=gate;cue.arrow=true;cue.phase=i*.9f;Dynamic(go.GetComponent<MeshRenderer>());
            }
        }

        private static void Dynamic(MeshRenderer renderer)
        {
            GameObjectUtility.SetStaticEditorFlags(renderer.gameObject,0);renderer.shadowCastingMode=ShadowCastingMode.Off;
            renderer.receiveGI=ReceiveGI.LightProbes;
        }
    }
}
