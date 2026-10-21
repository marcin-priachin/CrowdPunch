using System.Linq;
using CrowdPunch.Authoring;
using CrowdPunch.Utilities;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using static CrowdPunch.Editor.ChapterNineCrownAssets;

namespace CrowdPunch.Editor
{
    internal static class ChapterNineCrownObjectives
    {
        internal static void Apply(Scene scene)
        {
            foreach(var anchor in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<FinaleAnchorAuthoring>(true)))
            {
                var gate = anchor.GetComponent<BarricadeAuthoring>();
                if(gate.GetComponent<ShellTargetAuthoring>() != null) ChapterNineReliquaryVisuals.Build(gate);
                else if(gate.GetComponent<TrackObjectAuthoring>() != null) Cargo(gate);
                else Target(gate);
            }
            foreach(var cover in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<RotatingCoverAuthoring>(true)))
                foreach(var panel in cover.GetComponentsInChildren<CoverPanelAuthoring>(true))
                {
                    bool jamb = panel.index == 0 || panel.index == CoverGeometry.PanelCount - 1;
                    panel.GetComponent<MeshRenderer>().sharedMaterial = Surface(jamb ? "Ochre" : "Fabric",false,false,true);
                    Dynamic(panel.GetComponent<MeshRenderer>());
                }
            foreach(var r in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<MeshRenderer>(true)))
            {
                if(r.name == "Short rail") r.sharedMaterial = Surface("Slate");
                if(r.GetComponent<TrackSocketVisualAuthoring>() != null)
                {r.sharedMaterial = Surface("Sage",false,false,true); Dynamic(r);}
            }
        }

        private static void Target(BarricadeAuthoring gate)
        {
            // Retain the original authoring and poses; the complete segmented skin replaces bars.
            foreach(var visual in gate.GetComponentsInChildren<BarricadeVisualAuthoring>(true)) visual.GetComponent<MeshRenderer>().enabled = false;
            for(int row=0;row<3;row++)for(int col=0;col<2;col++)
            {
                var size = new Vector3(gate.size.x*.5f-.025f,gate.size.y/3-.025f,gate.size.z);
                Piece(gate,"Ivory anchor masonry",new Vector3((col-.5f)*gate.size.x*.5f,(row-1)*gate.size.y/3,0),size,"Limestone",0,row*2+col);
            }
            for(int face=0;face<4;face++)
            {
                var turn=Quaternion.Euler(0,face*90,0);
                float depth=face%2==0?gate.size.z:gate.size.x;
                float width=face%2==0?gate.size.x:gate.size.z;
                var strike=Piece(gate,"Royal copper strike face",turn*new Vector3(0,0,-depth*.5f-.04f),new Vector3(width*.65f,gate.size.y*.5f,.06f),"Impact",0,face+10);
                strike.transform.localRotation=turn;
                foreach(int sign in new[]{-1,1})
                {
                    var trim=Piece(gate,"Gold anchor reinforcement",turn*new Vector3(sign*width*.4f,0,-depth*.5f-.035f),new Vector3(.10f,gate.size.y*.95f,.07f),"Ochre",0,face*2+sign+20);
                    trim.transform.localRotation=turn;
                }
                for(int stage=1;stage<=2;stage++)
                {
                    var crack=Piece(gate,"Branching anchor fracture",turn*new Vector3(0,0,-depth*.5f-.09f),new Vector3(width*.78f,gate.size.y*.72f,1),"Fracture",stage,face*3+stage+30);
                    crack.transform.localRotation=turn;
                    crack.GetComponent<MeshFilter>().sharedMesh=ChapterThreeQuarryDamage.Fracture(stage,Output+"/Damage");
                }
            }
        }

        private static GameObject Piece(BarricadeAuthoring gate,string name,Vector3 center,Vector3 size,string kind,int stage,int seed)
        {
            var go=Shape(gate.transform,name,center-Vector3.up*size.y*.5f,size,Surface(kind,false,false,true));
            var visual=go.AddComponent<BarricadeVisualAuthoring>();visual.barricade=gate;visual.crackStage=stage;
            var motion=go.AddComponent<VillageGatePieceAuthoring>();motion.kind=stage==0?0:1;motion.damageStage=stage;
            motion.seed=seed*.137f%1;motion.duration=1.3f;motion.velocity=new Vector3((motion.seed-.5f)*4,3,2);
            motion.spin=new Vector3(2,3,1);motion.restingHeight=.08f;Dynamic(go.GetComponent<MeshRenderer>());return go;
        }

        private static void Cargo(BarricadeAuthoring gate)
        {
            var track=gate.GetComponent<TrackObjectAuthoring>();
            foreach(var r in track.GetComponentsInChildren<MeshRenderer>(true)) r.enabled=false;
            var cart=new GameObject("Royal anchor transfer trolley").transform;cart.SetParent(track.transform,false);
            void Box(string name,Vector3 center,Vector3 size,string kind)
            {
                var go=Shape(cart,name,center-Vector3.up*size.y*.5f,size,Surface(kind,false,false,true));Dynamic(go.GetComponent<MeshRenderer>());
            }
            Box("Guide roller chassis",new Vector3(0,-1.15f,0),new Vector3(3.55f,.3f,3.1f),"Slate");
            Box("Royal blue carrier",new Vector3(0,-.05f,0),new Vector3(3.25f,1.9f,2.8f),"Fabric");
            Box("Gold carrier lip",new Vector3(0,.95f,0),new Vector3(3.45f,.15f,3),"Ochre");
            for(int side=0;side<4;side++)
            {
                var turn=Quaternion.Euler(0,side*90,0);float depth=side%2==0?1.43f:1.66f;
                var go=Shape(cart,"Ivory trolley strike face",turn*new Vector3(0,-.38f,-depth),new Vector3(1.5f,.75f,.05f),Surface("Chalk",false,false,true));
                go.transform.localRotation=turn;Dynamic(go.GetComponent<MeshRenderer>());
            }
            foreach(int x in new[]{-1,1})foreach(int z in new[]{-1,1})
            {
                var go=Shape(cart,"Low guide roller",new Vector3(x*1.50f,-1.38f,z*1.02f),new Vector3(.48f,.25f,.48f),Surface("Slate",false,false,true),true);
                go.transform.localRotation=Quaternion.Euler(0,0,90);Dynamic(go.GetComponent<MeshRenderer>());
                Box("Ceremonial energy cargo",new Vector3(x*.77f,1.35f,z*.66f),new Vector3(1.4f,.65f,1.2f),"Limestone");
            }
            var fittings=new GameObject("Royal rail fittings").transform;SceneManager.MoveGameObjectToScene(fittings.gameObject,gate.gameObject.scene);
            for(int i=0;i<=7;i++) Shape(fittings,"Ivory guide sleeper",new Vector3(track.transform.position.x,-.98f,track.transform.position.z-1+i*1.5f),new Vector3(3.6f,.04f,.25f),Surface("Limestone"));
            foreach(int sign in new[]{-1,1})
                Shape(fittings,"Gold docking end stop",track.destination.position+new Vector3(sign*1.45f,-1.58f,1.7f),new Vector3(.3f,.4f,.4f),Surface("Ochre"));
        }

        private static void Dynamic(MeshRenderer r)
        {GameObjectUtility.SetStaticEditorFlags(r.gameObject,0);r.shadowCastingMode=ShadowCastingMode.Off;r.receiveGI=ReceiveGI.LightProbes;}
    }
}
