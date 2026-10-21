using System;
using System.IO;
using System.Linq;
using CrowdPunch.Authoring;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace CrowdPunch.Editor
{
    public static class ChapterEightReliquaryVisuals
    {
        internal const string Output = "Assets/CrowdPunch/Data/ChapterEightCitadel/Reliquary76";

        [MenuItem("Crowd Punch/Art Direction/Refresh Level 76 Shell Visuals")]
        public static void Apply()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play before authoring shell visuals.");
            var setup=EditorSceneManager.GetSceneManagerSetup();
            if(setup.Any(s=>UnityEngine.SceneManagement.SceneManager.GetSceneByPath(s.path).isDirty))throw new InvalidOperationException("Save open scenes first.");
            try
            {
                var scene=EditorSceneManager.OpenScene(ChapterEightCitadelBuilder.SubPath(76));
                Build(Object.FindFirstObjectByType<ShellTargetAuthoring>().GetComponent<BarricadeAuthoring>());
                EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            }
            finally{EditorSceneManager.RestoreSceneManagerSetup(setup);}
        }

        internal static void Build(BarricadeAuthoring gate)
        {
            Directory.CreateDirectory(Output);AssetDatabase.Refresh();
            foreach(var visual in gate.GetComponentsInChildren<BarricadeVisualAuthoring>(true))
                if(visual!=null)visual.GetComponent<MeshRenderer>().enabled=false;
            foreach(var t in gate.GetComponentsInChildren<Transform>(true).Where(t=>t.name=="Citadel objective fittings"||t.name=="Citadel shell assembly").ToArray())
                if(t!=null)Object.DestroyImmediate(t.gameObject);
            var root=new GameObject("Citadel shell assembly").transform;root.SetParent(gate.transform,false);
            var plate=ChapterEightReliquaryMeshes.Plate();var flange=ChapterEightReliquaryMeshes.Ring();var chip=ChapterEightReliquaryMeshes.Accent();
            var enamel=Paint("Shell enamel",new Color(.44f,.23f,.25f),new Color(.59f,.37f,.36f));
            var steel=Paint("Shell core blue steel",new Color(.68f,.65f,.51f),new Color(.31f,.40f,.48f));
            var dark=Paint("Shell fracture ink",new Color(.075f,.12f,.17f),new Color(.12f,.17f,.22f));
            var brass=Paint("Shell brass fittings",new Color(.77f,.57f,.29f),new Color(.58f,.40f,.22f));
            var ceramic=Paint("Shell core ceramic",new Color(.78f,.83f,.76f),new Color(.56f,.68f,.68f));
            var power=Paint("Shell core turquoise",new Color(.82f,.47f,.18f),new Color(.65f,.43f,.25f));
            var spark=Paint("Shell amber sparks",new Color(1,.74f,.29f),new Color(1,.87f,.53f));
            var dust=Paint("Shell powder wash",new Color(.60f,.66f,.65f),new Color(.51f,.58f,.62f));
            float floorY=gate.gameObject.scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<MeshRenderer>(true)).Where(r=>r.name=="Continuous Convex Floor" || r.name=="Continuous Round Floor").Max(r=>r.bounds.max.y);
            int seed=0;
            GameObject Piece(string name,Mesh mesh,Vector3 position,Vector3 size,Quaternion rotation,Material material,bool core,Vector3 velocity,int kind=0,int damageStage=0,int group=-1)
            {
                var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(root,false);
                go.transform.localPosition=position;go.transform.localRotation=rotation;go.transform.localScale=size;
                go.GetComponent<MeshFilter>().sharedMesh=mesh;
                var renderer=go.GetComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.forceMeshLod=0;
                var visual=go.AddComponent<BarricadeVisualAuthoring>();visual.barricade=gate;visual.shellVisual=true;visual.shellCore=core;visual.crackStage=damageStage;
                float phase=((group<0?seed:group)*37%101)/100f;seed++;
                var motion=go.AddComponent<ShellFragmentAuthoring>();motion.kind=kind;motion.velocity=velocity;motion.spin=new Vector3(1.8f+phase*3,phase*4-2,2.5f-phase*5);
                motion.duration=kind==2?.32f:kind==3?.65f:1.15f+phase*.28f;motion.seed=phase;
                motion.restingY=floorY-gate.transform.position.y+.06f;
                GameObjectUtility.SetStaticEditorFlags(go,0);return go;
            }
            for(int face=0;face<4;face++)
            {
                var turn=Quaternion.Euler(0,face*90,0);var outward=turn*Vector3.back;
                for(int row=0;row<3;row++)for(int column=0;column<2;column++)
                {
                    int group=face*6+row*2+column;
                    var local=new Vector3(column==0?-.79f:.79f,(row-1)*1.32f,-1.61f);
                    var velocity=outward*(2.3f+row*.55f)+Vector3.up*(2.5f+row*.65f)+turn*Vector3.right*(column==0?-.6f:.6f);
                    Piece("Royal reliquary armour plate",plate,turn*local,new Vector3(1.50f,1.23f,.16f),turn,enamel,false,velocity,group:group);
                    Piece("Captive brass bolt",plate,turn*(local+new Vector3(column==0?-.48f:.48f,.35f,-.11f)),new Vector3(.16f,.16f,.08f),turn,brass,false,velocity*1.12f,group:group);
                }
                for(int stage=1;stage<=2;stage++)
                {
                    float x=stage==1?-.3f:.44f;
                    var crack=Piece("Progressive enamel fracture",plate,turn*new Vector3(x,.35f,-1.71f),new Vector3(.065f,2.75f,.025f),turn*Quaternion.Euler(0,0,stage==1?16:-19),dark,false,outward*3,1,stage);
                    Piece("Fracture branch",plate,crack.transform.localPosition+turn*new Vector3(.18f,.35f,-.012f),new Vector3(.055f,.75f,.025f),turn*Quaternion.Euler(0,0,-38),dark,false,outward*3,1,stage);
                }
                for(int i=0;i<3;i++)
                    Piece("Impact metal fleck",chip,turn*new Vector3((i-1)*.7f,.7f,-1.78f),new Vector3(.10f,.32f,.08f),turn,spark,false,outward*(3+i*.6f)+Vector3.up*(2+i),2);
                // One coherent core casing, built from eight fitted metal sections for its final breakup.
                var velocityCore=outward*1.9f+Vector3.up*2.5f;
                var faceCentre=turn*new Vector3(0,.2f,-1.48f);
                Piece("Ceramic power socket",plate,faceCentre,new Vector3(1.72f,1.85f,.08f),turn,ceramic,true,velocityCore);
                Piece("Brass machined flange",flange,faceCentre+outward*.08f,new Vector3(1.40f,1.40f,.15f),turn,brass,true,velocityCore);
                Piece("Turquoise core lens",plate,faceCentre+outward*.07f,new Vector3(.98f,.98f,.08f),turn*Quaternion.Euler(0,0,45),power,true,velocityCore);
                for(int side=-1;side<=1;side+=2)
                    Piece("Steel socket fastener",plate,faceCentre+turn*new Vector3(side*.67f,.71f,-.09f),new Vector3(.16f,.16f,.10f),turn,dark,true,velocityCore);
                for(int vent=0;vent<3;vent++)
                    Piece("Machined core vent",plate,turn*new Vector3((vent-1)*.42f,-1.03f,-1.54f),new Vector3(.20f,.38f,.065f),turn,dark,true,velocityCore);
                for(int stage=1;stage<=2;stage++)
                    Piece("Core ceramic damage seam",plate,faceCentre+turn*new Vector3(stage==1?-.2f:.27f,0,-.17f),new Vector3(.045f,1.20f,.025f),turn*Quaternion.Euler(0,0,stage==1?25:-18),dark,true,velocityCore,1,stage);
            }
            for(int x=-1;x<=1;x+=2)for(int z=-1;z<=1;z+=2)
            {
                var velocity=new Vector3(x*2,4.8f,z*2);
                Piece("Brass-edged enamel crown",plate,new Vector3(x*.79f,2.02f,z*.79f),new Vector3(1.50f,1.50f,.14f),Quaternion.Euler(90,0,0),enamel,false,velocity);
                for(int y=-1;y<=1;y+=2)
                    Piece("Ivory reliquary core casing",plate,new Vector3(x*.76f,y*.94f,z*.76f),new Vector3(1.50f,1.84f,1.50f),Quaternion.identity,steel,true,new Vector3(x*1.8f,2.6f+y*.4f,z*1.8f));
            }
            for(int i=0;i<6;i++)
            {
                float angle=i*Mathf.PI*2/6;var direction=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));
                Piece("Brief shell powder puff",chip,direction*1.4f+Vector3.up*.5f,new Vector3(.55f,.65f,.55f),Quaternion.Euler(0,i*60,15),dust,false,direction*1.7f+Vector3.up*2,3);
            }
            foreach(var group in root.GetComponentsInChildren<BarricadeVisualAuthoring>().Where(v=>v.crackStage>0).GroupBy(v=>(v.shellCore,v.crackStage,Mathf.RoundToInt(v.transform.localEulerAngles.y/90))))
            {
                var chosen=group.First();foreach(var visual in group.Skip(1)) Object.DestroyImmediate(visual.gameObject);
                var turn=Quaternion.Euler(0,group.Key.Item3*90,0);
                chosen.transform.localPosition=turn*new Vector3(0,.2f,group.Key.shellCore?-1.70f:-1.73f);
                chosen.transform.localRotation=turn;chosen.transform.localScale=new Vector3(group.Key.shellCore?1.3f:2.7f,group.Key.shellCore?1.5f:3.1f,1);
                chosen.GetComponent<MeshFilter>().sharedMesh=ChapterThreeQuarryDamage.Fracture(group.Key.crackStage,Output);
            }
            if(root.GetComponentsInChildren<Collider>().Length!=0)throw new InvalidOperationException("Shell decoration must not add colliders.");
        }

        private static Material Paint(string name,Color color,Color wash)
        {
            string path=Output+"/"+name+".mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(material==null){material=new Material(Shader.Find("CrowdPunch/Cel/Painted Foundry"));AssetDatabase.CreateAsset(material,path);}
            material.enableInstancing=true;material.SetColor("_BaseColor",color);material.SetColor("_WashColor",wash);
            material.SetColor("_ShadowColor",new Color(.43f,.49f,.65f));material.SetColor("_LightColor",new Color(1,.95f,.86f));
            material.SetFloat("_ContinuousPaint",1);material.SetFloat("_WashStrength",.10f);material.SetFloat("_Variation",.035f);material.SetFloat("_GrainStrength",.03f);
            material.SetFloat("_TextureStrength",0);material.SetFloat("_Contrast",.78f);material.SetFloat("_ObjectSpacePaint",1);
            material.SetFloat("_UseEntityColor",1);
            material.SetFloat("_CameraCutaway",0);material.SetFloat("_ReceiveShadows",1);
            EditorUtility.SetDirty(material);return material;
        }
    }
}
