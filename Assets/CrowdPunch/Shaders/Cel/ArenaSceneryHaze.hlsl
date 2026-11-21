#ifndef CROWD_PUNCH_ARENA_SCENERY_HAZE_INCLUDED
#define CROWD_PUNCH_ARENA_SCENERY_HAZE_INCLUDED
float4 _CrowdPunchSceneryHazeBounds; // Centre XZ, half-size XZ of the full playable envelope.
half4 _CrowdPunchSceneryHazeColor;
float4 _CrowdPunchSceneryHazeSettings; // Enabled, exterior ground height, bank top, scenery fade distance.
float4 _CrowdPunchSceneryHazeEdges[128]; // Counterclockwise edges of each floor polygon.
int _CrowdPunchSceneryHazeEdgeCount;

float ArenaSceneryDistance(float2 sampleXZ)
{
    float2 outside=max(abs(sampleXZ-_CrowdPunchSceneryHazeBounds.xy)-_CrowdPunchSceneryHazeBounds.zw,0);
    if (_CrowdPunchSceneryHazeEdgeCount == 0) return length(outside);
    float distanceSquared=1e20;
    int winding=0;
    [loop] for(int i=0;i<_CrowdPunchSceneryHazeEdgeCount;i++)
    {
        float4 edge=_CrowdPunchSceneryHazeEdges[i];
        float2 delta=edge.zw-edge.xy;
        float2 relative=sampleXZ-edge.xy;
        float2 nearest=relative-delta*saturate(dot(relative,delta)/max(dot(delta,delta),1e-8));
        distanceSquared=min(distanceSquared,dot(nearest,nearest));
        float cross=delta.x*relative.y-delta.y*relative.x;
        if(edge.y<=sampleXZ.y && edge.w>sampleXZ.y && cross>0) winding++;
        if(edge.y>sampleXZ.y && edge.w<=sampleXZ.y && cross<0) winding--;
    }
    // Nonzero winding preserves the union of touching or overlapping floor polygons.
    return winding!=0 ? 0 : sqrt(distanceSquared);
}

half3 ApplyArenaSceneryHaze(half3 color, float3 positionWS, float role, float timeSeconds)
{
    if(role<.5 || _CrowdPunchSceneryHazeSettings.x<.5) return color;
    float distanceOutside=ArenaSceneryDistance(positionWS.xz);
    // Dense low fog buries nearby scenery instead of uniformly tinting its outline.
    float exterior=smoothstep(1,6,distanceOutside);
    float height=positionWS.y-_CrowdPunchSceneryHazeSettings.y;
    float bankTop=_CrowdPunchSceneryHazeSettings.z;
    half depthHaze;
    if(role>2.5)
    {
        // The Necropolis bank rises and thickens together over a 14-second cycle.
        float pulse=sin(timeSeconds*(6.2831853/14.0));
        bankTop+=1.15*pulse;
        float depth=max(0,distanceOutside-3)/(_CrowdPunchSceneryHazeSettings.w-3);
        depthHaze=1-exp2(-2.5*depth*(1+.26*pulse));
        half silhouette=smoothstep(12,32,distanceOutside);
        color=lerp(color,_CrowdPunchSceneryHazeColor.rgb*.10,silhouette);
    }
    else depthHaze=smoothstep(3,_CrowdPunchSceneryHazeSettings.w,distanceOutside);
    half lowBank=1-smoothstep(1.5,bankTop,height);
    // Ground and scenery share the same bank, hiding baked shadows beneath it too.
    half haze=1-(1-depthHaze)*(1-lowBank*exterior);
    return lerp(color,_CrowdPunchSceneryHazeColor.rgb,haze);
}

half3 ApplyArenaSceneryHaze(half3 color, float3 positionWS, float role)
{
    return ApplyArenaSceneryHaze(color,positionWS,role,_Time.y);
}
#endif
