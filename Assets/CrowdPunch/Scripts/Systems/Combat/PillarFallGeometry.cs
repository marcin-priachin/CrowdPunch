using CrowdPunch.Components;
using Unity.Mathematics;
namespace CrowdPunch.Systems.Combat
{
    internal static class PillarFallGeometry
    {
        internal static quaternion Rotation(float3 direction,float angle) => quaternion.AxisAngle(math.normalizesafe(math.cross(math.up(),direction),new float3(1,0,0)),angle);
        // Subdivide the rotating box arc and conservatively inflate by half the tip travel.
        // Target motion is included between snapshots; fast bodies cannot tunnel through a fall.
        internal static bool SweptContact(in PillarTuning t,in FallingPillar p,float3 previous,float3 current,float radius,float halfHeight)
        {
            float arc=math.abs(p.Angle-p.PreviousAngle);
            int steps=math.max(1,(int)math.ceil(arc/math.radians(2)));
            float inflate=t.Height*arc/(2*steps);
            int motionSteps=math.max(1,(int)math.ceil(math.distance(previous,current)/math.max(.1f,t.Width*.4f)));
            steps=math.max(steps,motionSteps);
            // A vertical capsule is sampled along its spine with conservative spacing padding.
            float spine=math.max(0,halfHeight-radius);
            int verticalSteps=math.max(1,(int)math.ceil(2*spine/math.max(.1f,radius)));
            float verticalPad=spine/verticalSteps;
            float3 extents=new float3(t.Width*.5f,t.Height*.5f,t.Width*.5f);
            for(int i=0;i<=steps;i++)
            {
                float f=i/(float)steps; var rotation=Rotation(p.Direction,math.lerp(p.PreviousAngle,p.Angle,f));
                float3 target=math.lerp(previous,current,f);
                float3 center=t.InitialPosition+math.rotate(rotation,new float3(0,t.Height*.5f,0));
                for(int v=0;v<=verticalSteps;v++)
                {
                    float3 point=target+new float3(0,math.lerp(-spine,spine,v/(float)verticalSteps),0);
                    float3 local=math.rotate(math.inverse(rotation),point-center);
                    float3 outside=math.max(math.abs(local)-extents,0);
                    if(math.lengthsq(outside)<=math.square(radius+inflate+verticalPad)) return true;
                }
            }
            return false;
        }
    }
}
