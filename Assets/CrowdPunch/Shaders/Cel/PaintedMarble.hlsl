#ifndef CROWD_PUNCH_PAINTED_MARBLE_INCLUDED
#define CROWD_PUNCH_PAINTED_MARBLE_INCLUDED

// VISION-004/006: continuous stone pigment, with no specular or normal distortion.
// Fixed surfaces use metres in world space; moving opt-in skins retain their paint space.
half3 ApplyPaintedMarble(half3 albedo, float3 position)
{
    float3 q=position*max(_MarbleScale,.001);
    float broad=SpatialNoise(q*.35);
    float curl=SpatialNoise(q*1.71+float3(7.3,2.1,4.7));
    float phase=dot(q,float3(1.3,.71,1.7))+broad*3+curl*.6;
    float footprint=max(length(ddx(q)),length(ddy(q)));
    float fade=1-smoothstep(.25,.85,footprint);
    float distance=abs(sin(phase));
    float aa=max(fwidth(distance)*.75,.012);
    float vein=(1-smoothstep(.05-aa,.05+aa,distance))*min(1,.065/aa);
    float wash=1-smoothstep(.08,.38+aa,distance);
    float branchPhase=phase*2.13+curl*3.1;
    float branchAA=max(fwidth(branchPhase),.018);
    float branch=(1-smoothstep(.025,.025+branchAA,abs(sin(branchPhase))))*smoothstep(.53,.72,broad);
    float ink=saturate(vein+branch*.22)*_MarbleStrength*fade;
    albedo*=1+(broad-.5)*.045*_MarbleStrength;
    albedo=lerp(albedo,albedo*_MarbleVeinColor.rgb,saturate((ink+wash*.12*_MarbleStrength*fade)));

    if(_MarbleSlabStrength>.001)
    {
        float2 uv=position.xz/max(_MarbleSlabSize,.1);
        float2 local=frac(uv);
        float2 edge=min(local,1-local);
        float2 width=max(fwidth(uv),.0005);
        float2 coverage=1-smoothstep(.0035,.0035+width,edge);
        float joint=max(coverage.x,coverage.y)*(1-smoothstep(.06,.18,max(width.x,width.y)));
        float slab=SpatialHash(float3(floor(uv).x,0,floor(uv).y));
        albedo*=1+(slab-.5)*.07*_MarbleSlabStrength;
        albedo*=1-joint*.32*_MarbleSlabStrength;
    }
    return albedo;
}
#endif
