using CrowdPunch.Components;
using Unity.Mathematics;

namespace CrowdPunch.Systems.Movement
{
    internal static class BossPerimeterRoute
    {
        // Arc-length parameterized rounded rectangle: continuous tangent and no corner stops.
        public static float3 Position(float distance, in BossTuning t)
        {
            float2 e=t.RouteExtents;
            float r=math.clamp(t.RouteCornerRadius,.1f,math.cmin(e));
            float horizontal=2*(e.x-r), vertical=2*(e.y-r), arc=math.PI*r*.5f;
            float length=2*(horizontal+vertical)+4*arc;
            float d=(distance%length+length)%length;
            float2 p;
            if(d<horizontal) p=new float2(-e.x+r+d,e.y);
            else if((d-=horizontal)<arc) p=new float2(e.x-r,e.y-r)+new float2(math.sin(d/r),math.cos(d/r))*r;
            else if((d-=arc)<vertical) p=new float2(e.x,e.y-r-d);
            else if((d-=vertical)<arc) p=new float2(e.x-r,-e.y+r)+new float2(math.cos(d/r),-math.sin(d/r))*r;
            else if((d-=arc)<horizontal) p=new float2(e.x-r-d,-e.y);
            else if((d-=horizontal)<arc) p=new float2(-e.x+r,-e.y+r)+new float2(-math.sin(d/r),-math.cos(d/r))*r;
            else if((d-=arc)<vertical) p=new float2(-e.x,-e.y+r+d);
            else { d-=vertical; p=new float2(-e.x+r,e.y-r)+new float2(-math.cos(d/r),math.sin(d/r))*r; }
            p+=t.Center;
            return new float3(p.x,t.HeadHeight,p.y);
        }

        public static float3 Clamp(float3 p, in BossTuning t, float radius=1.5f)
        {
            p.xz=math.clamp(p.xz,t.Center-t.BoundsExtents+radius,t.Center+t.BoundsExtents-radius); return p;
        }
    }
}
