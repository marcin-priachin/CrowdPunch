using UnityEngine;

namespace CrowdPunch.Mono.Player
{
    // Presentation-only bridge. Stores two draw slots, never ECS entities or gameplay state.
    public sealed class BossAttackTelegraphs : MonoBehaviour
    {
        private static BossAttackTelegraphs active;
        private readonly LineRenderer[] lines=new LineRenderer[2];
        private Material material;
        private void OnEnable() { active=this; }
        private void OnDisable() { if(active==this) active=null; foreach(var line in lines) if(line!=null) line.enabled=false; }
        private void OnDestroy() { if(material!=null) Destroy(material); }
        public static void BeginFrame() { if(active!=null) foreach(var line in active.lines) if(line!=null) line.enabled=false; }
        public static void Publish(int slot,Vector3 center,Vector3 start,Vector3 direction,float width,int attack,float progress)
        {
            if(active==null || FeedbackTimeController.IsSuspended) return;
            active.Draw(slot,center,start,direction,width,attack,progress);
        }
        private void Draw(int slot,Vector3 center,Vector3 start,Vector3 direction,float width,int attack,float progress)
        {
            if(lines[slot]==null)
            {
                if(material==null) material=new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                var go=new GameObject("Boss Attack Telegraph "+slot); go.transform.SetParent(transform,false);
                var line=go.AddComponent<LineRenderer>(); lines[slot]=line;
                line.sharedMaterial=material; line.useWorldSpace=true; line.loop=true;
                line.numCornerVertices=4; line.numCapVertices=4; line.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            var lr=lines[slot]; lr.enabled=true; lr.startWidth=lr.endWidth=Mathf.Lerp(.08f,.2f,progress);
            Color color=Color.Lerp(new Color(1,.72f,.08f),new Color(1,.12f,.025f),progress);
            lr.startColor=lr.endColor=color;
            center.y=start.y=-.93f;
            Vector3 side=Vector3.Cross(Vector3.up,direction).normalized;
            if(attack==0)
            {
                lr.positionCount=48;
                for(int i=0;i<48;i++) { float a=i*Mathf.PI*2/48; lr.SetPosition(i,center+new Vector3(Mathf.Sin(a),0,Mathf.Cos(a))*width); }
            }
            else if(attack==1)
            {
                lr.positionCount=4;
                lr.SetPosition(0,start-side*width); lr.SetPosition(1,center-side*width);
                lr.SetPosition(2,center+side*width); lr.SetPosition(3,start+side*width);
            }
            else
            {
                lr.positionCount=48;
                for(int i=0;i<24;i++)
                {
                    float p=i/23f;
                    Vector3 c=center+side*Mathf.Lerp(-width,width,p)-direction*(Mathf.Sin(p*Mathf.PI)*2);
                    lr.SetPosition(i,c-direction*1.5f); lr.SetPosition(47-i,c+direction*1.5f);
                }
            }
        }
    }
}
