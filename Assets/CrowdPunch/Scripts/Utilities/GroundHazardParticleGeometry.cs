using CrowdPunch.Components;
using Unity.Mathematics;

namespace CrowdPunch.Utilities
{
    public enum GroundHazardParticleKind : byte { Smoke, Flame, Ember }

    public struct GroundHazardParticleSample
    {
        public float3 Position;
        public float Radius, Height, Opacity, Bend;
        public GroundHazardParticleKind Kind;
    }

    // GROUND-007: bounded, stateless presentation lanes reset with the existing wave clock.
    public static class GroundHazardParticleGeometry
    {
        public static int Count(in GroundHazard h, in GroundHazardState state)
        {
            if(state.Introduced==0)return 0;
            if(state.Phase==GroundHazardPhase.Inactive)return 2;
            float area=h.Shape==GroundHazardShape.Circle?math.PI*h.Radius*h.Radius:h.HalfSize.x*h.HalfSize.y*4;
            return state.Phase==GroundHazardPhase.Warning?math.clamp((int)math.ceil(area*.3f),4,10):math.clamp((int)math.ceil(area*.7f),8,20);
        }

        public static bool TrySample(in GroundHazard h, in GroundHazardState state, double now, int lane, out GroundHazardParticleSample sample)
        {
            sample=default;
            if(lane<0 || lane>=Count(h,state))return false;
            double phaseAge=math.max(0,now-state.WaveStartedAt);
            if(h.Operation==GroundHazardOperation.Periodic)
            {
                double cycle=math.max(.01,h.InactiveDuration+h.WarningDuration+h.ActiveDuration);
                phaseAge=(phaseAge+h.CycleOffset)%cycle;if(phaseAge<0)phaseAge+=cycle;
                if(state.Phase==GroundHazardPhase.Warning)phaseAge-=h.InactiveDuration;
                else if(state.Phase==GroundHazardPhase.Active)phaseAge-=h.InactiveDuration+h.WarningDuration;
            }
            uint seed=math.hash(new uint4(math.asuint(h.Position.x),math.asuint(h.Position.z),state.RunGeneration^(uint)state.WaveIndex,(uint)lane));
            float delay=Random(seed)*.28f;
            if(phaseAge<delay)return false;
            bool active=state.Phase==GroundHazardPhase.Active;
            sample.Kind=!active?GroundHazardParticleKind.Smoke:lane%6==0?GroundHazardParticleKind.Ember:lane%6==1?GroundHazardParticleKind.Smoke:GroundHazardParticleKind.Flame;
            float lifetime=sample.Kind==GroundHazardParticleKind.Flame?.68f:sample.Kind==GroundHazardParticleKind.Ember?1.1f:state.Phase==GroundHazardPhase.Inactive?2.2f:1.45f;
            double cycles=(phaseAge-delay)/lifetime;uint emission=(uint)math.floor(cycles);
            float age=(float)(cycles-math.floor(cycles));seed=math.hash(new uint2(seed,emission));
            float minimum=h.Shape==GroundHazardShape.Circle?h.Radius:math.cmin(h.HalfSize);
            float size=sample.Kind==GroundHazardParticleKind.Ember?.04f:sample.Kind==GroundHazardParticleKind.Flame?.22f:state.Phase==GroundHazardPhase.Inactive?.17f:.30f;
            sample.Radius=math.min(size*math.lerp(.8f,1.25f,Random(seed+1))*(sample.Kind==GroundHazardParticleKind.Smoke?math.lerp(.65f,1.25f,age):1),minimum*.16f);
            float margin=sample.Radius*1.7f+math.min(.14f,minimum*.1f);
            float2 local;
            if(h.Shape==GroundHazardShape.Circle)
            {float angle=Random(seed+2)*math.PI*2;local=new float2(math.cos(angle),math.sin(angle))*math.sqrt(Random(seed+3))*math.max(0,h.Radius-margin);}
            else local=(new float2(Random(seed+2),Random(seed+3))*2-1)*math.max(0,h.HalfSize-margin);
            float drift=math.min(.10f,minimum*.08f)*math.sin(age*3+Random(seed+4)*6);
            local+=new float2(drift,-drift*.5f);
            float c=math.cos(h.Angle),s=math.sin(h.Angle);
            float2 world=new float2(c*local.x+s*local.y,-s*local.x+c*local.y);
            float rise=sample.Kind==GroundHazardParticleKind.Flame?.24f:sample.Kind==GroundHazardParticleKind.Ember?1.15f:state.Phase==GroundHazardPhase.Inactive?.40f:1.10f;
            sample.Position=h.Position+new float3(world.x,.06f+age*rise,world.y);
            sample.Height=sample.Kind==GroundHazardParticleKind.Flame?math.lerp(.6f,1.05f,Random(seed+5))*(1-age*.4f):sample.Radius*2;
            sample.Bend=math.sin(age*7+Random(seed+6)*6)*.35f;
            float peak=sample.Kind==GroundHazardParticleKind.Flame?.9f:sample.Kind==GroundHazardParticleKind.Ember?.85f:state.Phase==GroundHazardPhase.Inactive?.10f:active?.19f:.34f;
            sample.Opacity=peak*math.smoothstep(0,.12f,age)*(1-math.smoothstep(.62f,1,age));
            return sample.Opacity>.002f;
        }

        private static float Random(uint seed)=> (math.hash(new uint2(seed,0x9e3779b9u))&0x00ffffffu)/16777216f;
    }
}
