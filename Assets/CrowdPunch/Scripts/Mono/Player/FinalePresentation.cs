using UnityEngine;

namespace CrowdPunch.Mono.Player
{
    // Narrow ECS-to-scene presentation endpoint; no entity identities cross this boundary.
    public sealed class FinalePresentation : MonoBehaviour
    {
        public Configuration.FinaleSettings settings;
        public Transform model;
        public Animator animator;
        public float groundHeight = 3;
        public float groundedModelOffset;
        public float groundedHitOffset, groundedPunchOffset, groundedHeadbuttOffset;
        private float visibleGroundOffset;
        private static FinalePresentation active;
        private LineRenderer warning, tether, vulnerability;
        private Mono.UI.FinaleBossHealthBar healthBar;
        private readonly LineRenderer[] anchors = new LineRenderer[4];
        private Material material;
        private string animationState;
        private int previousPhase = -1, previousAttack;
        private float actionUntil, phaseStarted;
        private string actionClip;
        private Renderer[] renderers;
        private MaterialPropertyBlock colors;
        private Mono.Camera.CameraFollow cameraFollow;
        private void OnEnable() {
            active=this; renderers=model.GetComponentsInChildren<Renderer>(); colors=new MaterialPropertyBlock();
            previousPhase=-1; previousAttack=0; animationState=null; actionUntil=0;
            visibleGroundOffset=groundedModelOffset;
            healthBar=GetComponent<Mono.UI.FinaleBossHealthBar>();
            cameraFollow=FindFirstObjectByType<Mono.Camera.CameraFollow>();
            if(settings!=null) {
                model.localScale=Vector3.one*settings.modelScale;
                groundHeight=settings.colliderSize.y*.5f;
                if(cameraFollow!=null) cameraFollow.EncounterLookHeight=settings.cameraLookHeight;
            }
        }
        private void OnDisable() { if(active==this) active=null; if(cameraFollow!=null) cameraFollow.EncounterLookHeight=0; }
        private void OnDestroy() { if(material!=null) Destroy(material); }

        public static void Publish(Vector3 position,Quaternion rotation,int phase,int attack,Vector3 center,Vector3 direction,
            float length,float width,float progress,float flash,float health,Vector3 landing,float groundedWeight=0)
        {
            if(active==null) return;
            var a=active;
            if(a.previousPhase!=phase) {
                a.phaseStarted=Time.time;
                a.actionUntil=0;
                if(phase==2) { a.actionClip="HitReact"; a.actionUntil=Time.time+.6f; }
            }
            // Commit the strike when the ECS warning resolves, instead of playing it early and freezing at its end.
            bool released=a.previousPhase==phase && a.previousAttack!=0 && attack==0;
            if(released && phase!=4) {
                a.actionClip=a.previousAttack==4?"Headbutt":"Punch";
                a.actionUntil=Time.time+.65f;
            }
            float scale=a.settings!=null?a.settings.modelScale:1;
            float charge=attack!=0 && phase!=1?Mathf.SmoothStep(0,1,progress):0;
            float landingSquash=phase==2?.14f*Mathf.Exp(-(Time.time-a.phaseStarted)*5):0;
            a.model.localScale=new Vector3(1+charge*.035f+landingSquash*.4f,1-charge*.05f-landingSquash,1+charge*.035f+landingSquash*.4f)*scale;
            string clip=phase==4?"Death":phase==1?"HitReact":Time.time<a.actionUntil?a.actionClip:
                phase==2 || attack!=0?"Flying_Idle":"Fast_Flying";
            float offset=clip=="HitReact"?a.groundedHitOffset:clip=="Punch"?a.groundedPunchOffset:
                clip=="Headbutt"?a.groundedHeadbuttOffset:a.groundedModelOffset;
            // Lift immediately for low attack poses, then settle back to the idle support height.
            a.visibleGroundOffset=offset<a.visibleGroundOffset?offset:Mathf.Lerp(a.visibleGroundOffset,offset,1-Mathf.Exp(-24*Time.deltaTime));
            a.model.SetPositionAndRotation(position-Vector3.up*(a.groundHeight+a.visibleGroundOffset*a.model.localScale.y*groundedWeight),rotation);
            if(a.animator!=null) {
                a.animator.speed=Time.time<a.actionUntil && clip!="HitReact"?1.8f:phase==3?1.4f:1;
                if(clip!=a.animationState || released) { a.animator.CrossFadeInFixedTime(clip,.12f,0,0); a.animationState=clip; }
            }
            a.previousPhase=phase; a.previousAttack=attack;
            bool vulnerable=phase==2;
            float pulse=.5f+.5f*Mathf.Sin(Time.time*5);
            Color tint=phase==4?new Color(.3f,.3f,.3f):vulnerable?new Color(1,1.18f,1.22f):Color.Lerp(new Color(1.2f,.88f,.78f),Color.white,health);
            if(flash>0) tint=Color.white*2;
            a.colors.SetColor("_BaseColor",tint);
            a.colors.SetColor("_Color",tint);
            a.colors.SetColor("_EmissionColor",vulnerable?new Color(.02f,.32f,.28f)*(1+pulse*.35f):Color.white*.04f);
            foreach(var r in a.renderers) r.SetPropertyBlock(a.colors);
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
            a.Ensure(ref a.warning,"Finale ground warning");
            a.warning.enabled=attack!=0 && phase!=4;
            if(a.warning.enabled) {
                a.warning.startColor=a.warning.endColor=Color.Lerp(Color.yellow,new Color(1,.15f,.03f),progress);
                a.warning.startWidth=a.warning.endWidth=.18f+progress*.15f;
                center.y=-.9f;
                if(length<=0) Circle(a.warning,center,width);
                else {
                    a.warning.positionCount=4;
                    Vector3 side=Vector3.Cross(Vector3.up,direction)*width*.5f;
                    Vector3 from=center-(attack==2?direction*length*.5f:Vector3.zero);
                    Vector3 to=from+direction*length;
                    a.warning.SetPositions(new[]{from-side,to-side,to+side,from+side});
                }
            }
            a.Ensure(ref a.tether,"Anchor energy tether"); a.tether.enabled=phase==1;
            if(a.tether.enabled) { a.tether.loop=false; a.tether.positionCount=2; a.tether.SetPositions(new[]{landing,position});
                a.tether.startColor=a.tether.endColor=Color.cyan; a.tether.startWidth=a.tether.endWidth=.3f; }
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
