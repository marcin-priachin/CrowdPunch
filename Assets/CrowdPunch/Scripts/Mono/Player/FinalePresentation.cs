using UnityEngine;

namespace CrowdPunch.Mono.Player
{
    // Narrow ECS-to-scene presentation endpoint; no entity identities cross this boundary.
    [DefaultExecutionOrder(100)]
    public sealed class FinalePresentation : MonoBehaviour
    {
        public Configuration.FinaleSettings settings;
        public Transform model;
        public Animator animator;
        public float groundHeight = 3;
        public float groundedModelOffset;
        public float defeatedModelOffset = -1.2274528f;
        private static FinalePresentation active;
        private LineRenderer vulnerability;
        private Mono.UI.BossHealthBar healthBar;
        private readonly LineRenderer[] anchors = new LineRenderer[4];
        private Material material;
        private int previousPhase = -1;
        private float previousHealth = 1;
        private uint resolvedSequence, restart;
        private FinaleWraithAnimation wraithAnimation;
        private FinaleAttackVisuals attacks;
        private FinaleDowningVisuals downing;
        private Transform attackOrigin, head;
        private bool initialStun;
        private Mono.Camera.CameraFollow cameraFollow;
        private void OnEnable() {
            active=this;
            previousPhase=-1; resolvedSequence=0; restart=Mono.UI.GameRestartRegistry.Sequence;
            wraithAnimation=new FinaleWraithAnimation(animator);
            if(model!=null)
                foreach(var bone in model.GetComponentsInChildren<Transform>(true))
                    if(bone.name=="RigRibcage") attackOrigin=bone;
                    else if(bone.name=="RigHead") head=bone;
            previousHealth=1;
            healthBar=GetComponent<Mono.UI.BossHealthBar>();
            cameraFollow=FindFirstObjectByType<Mono.Camera.CameraFollow>();
            if(settings!=null) {
                groundHeight=settings.colliderSize.y*.5f;
                if(cameraFollow!=null) cameraFollow.EncounterLookHeight=settings.cameraLookHeight;
            }
        }
        private void OnDisable() { if(active==this) active=null; attacks?.Clear(); downing?.Clear(); if(cameraFollow!=null) cameraFollow.EncounterLookHeight=0; }
        private void OnDestroy() { attacks?.Dispose(); downing?.Dispose(); if(material!=null) Destroy(material); }
        private void LateUpdate()
        {
            // Run after CameraFollow so flight always faces the viewer's current position.
            if(model!=null && (previousPhase==0 || previousPhase==1 || previousPhase==3 || initialStun)) FaceViewer();
            if(FeedbackTimeController.IsSuspended) { attacks?.Clear(); downing?.Clear(); return; }
            attacks?.Tick();
            if(model!=null) downing?.Body(attackOrigin!=null?attackOrigin.position:model.position+Vector3.up*2,
                head!=null?head.position:model.position+Vector3.up*5);
            downing?.Tick();
        }

        private void FaceViewer()
        {
            var camera=UnityEngine.Camera.main;
            if(camera==null) return;
            Vector3 facing=camera.transform.position-model.position;
            facing.y=0;
            if(facing.sqrMagnitude>.001f) model.rotation=Quaternion.LookRotation(facing);
        }

        public static void Publish(Vector3 position,Quaternion rotation,int phase,int attack,Vector3 center,Vector3 direction,
            float length,float width,float progress,float health,Vector3 landing,float groundedWeight=0,
            uint attackSequence=0,uint resolvedAttackSequence=0,int resolvedAttack=0,bool stunned=false)
        {
            if(active==null) return;
            var a=active;
            a.initialStun=phase==2 && stunned;
            if(a.restart!=Mono.UI.GameRestartRegistry.Sequence || health>a.previousHealth) {
                a.restart=Mono.UI.GameRestartRegistry.Sequence;
                a.attacks?.Clear(); a.downing?.Clear(); a.wraithAnimation=new FinaleWraithAnimation(a.animator); a.previousPhase=-1;
            }
            bool released=a.previousPhase>=0 && resolvedAttackSequence!=a.resolvedSequence && resolvedAttack!=0
                && phase!=4 && (phase!=1 || resolvedAttack==5);
            if(a.previousPhase!=phase && (phase==1 || phase==3 || phase==4)) a.attacks?.Clear();
            bool damaged=phase==2 && health<a.previousHealth;
            a.wraithAnimation.Present(phase,attack,attackSequence,progress,released,resolvedAttack,damaged,stunned);
            float floorOffset=phase==4?a.defeatedModelOffset:a.groundedModelOffset;
            a.model.SetPositionAndRotation(position-Vector3.up*(a.groundHeight+floorOffset*a.model.localScale.y*groundedWeight),
                Quaternion.RotateTowards(a.model.rotation,rotation,360*Time.deltaTime));
            if(a.attacks==null) a.attacks=new FinaleAttackVisuals(a.transform);
            if(a.downing==null) a.downing=new FinaleDowningVisuals(a.transform);
            if(!FeedbackTimeController.IsSuspended) {
                a.attacks.Present(phase==4?0:attack,center,direction,length,width,progress,
                    a.attackOrigin!=null?a.attackOrigin.position:a.model.position+Vector3.up*2);
                if(released) a.attacks.Resolve(resolvedAttack);
                a.downing.Present(phase,landing,Vector3.zero,Vector3.zero,position,progress,
                    a.settings!=null?Mathf.Max(a.settings.colliderSize.x,a.settings.colliderSize.z)*.5f+1:6,stunned);
            }
            a.previousPhase=phase; a.resolvedSequence=resolvedAttackSequence; a.previousHealth=health;
            bool vulnerable=phase==2;
            float pulse=.5f+.5f*Mathf.Sin(Time.time*5);
            if(a.healthBar!=null) a.healthBar.Present(health);
            a.Ensure(ref a.vulnerability,"Boss vulnerable ring");
            a.vulnerability.enabled=vulnerable;
            if(vulnerable) {
                var floor=position; floor.y=-.86f;
                float radius=a.settings!=null?Mathf.Max(a.settings.colliderSize.x,a.settings.colliderSize.z)*.5f+1:6;
                Circle(a.vulnerability,floor,radius+pulse*.25f);
                a.vulnerability.startColor=a.vulnerability.endColor=new Color(.1f,1,.9f);
                a.vulnerability.startWidth=a.vulnerability.endWidth=.3f+pulse*.15f;
            }
        }

        public static void Anchor(int index,Vector3 position,int status)
        {
            if(active==null || index>=4) return;
            var a=active; a.Ensure(ref a.anchors[index],"Anchor status "+index);
            var line=a.anchors[index]; position.y=-.88f; Circle(line,position,4.5f);
            line.startColor=line.endColor=status==2?new Color(.15f,.17f,.2f):status==1?new Color(.8f,.5f,.1f):Color.cyan;
        }
        private void Ensure(ref LineRenderer line,string label)
        {
            if(line!=null) return;
            if(material==null) material=new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            var go=new GameObject(label); go.transform.SetParent(transform,false);
            line=go.AddComponent<LineRenderer>(); line.sharedMaterial=material; line.useWorldSpace=true; line.loop=true;
            line.startWidth=line.endWidth=.18f; line.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        private static void Circle(LineRenderer line,Vector3 center,float radius)
        {
            line.positionCount=64;
            for(int i=0;i<64;i++) { float angle=i*Mathf.PI*2/64; line.SetPosition(i,center+new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*radius); }
        }
    }
}
