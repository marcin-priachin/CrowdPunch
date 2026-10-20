#ifndef CROWD_PUNCH_ARENA_SCENERY_HAZE_INCLUDED
#define CROWD_PUNCH_ARENA_SCENERY_HAZE_INCLUDED
float4 _CrowdPunchSceneryHazeBounds; // Centre XZ, half-size XZ of the full playable envelope.
half4 _CrowdPunchSceneryHazeColor;
float4 _CrowdPunchSceneryHazeSettings; // Enabled, exterior ground height, bank top, opaque scenery distance.
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

half3 ApplyArenaSceneryHaze(half3 color, float3 positionWS, float role)
{
    if(role<.5 || _CrowdPunchSceneryHazeSettings.x<.5) return color;
    float distanceOutside=ArenaSceneryDistance(positionWS.xz);
    // Dense low fog buries nearby scenery instead of uniformly tinting its outline.
    // Standard scenery beyond the bank dissolves completely, including horizon corners.
    float exterior=smoothstep(1,6,distanceOutside);
    float height=positionWS.y-_CrowdPunchSceneryHazeSettings.y;
    half lowBank=1-smoothstep(1.5,_CrowdPunchSceneryHazeSettings.z,height);
    half depthHaze=smoothstep(3,_CrowdPunchSceneryHazeSettings.w,distanceOutside);
    // Ground and scenery share the same bank, hiding baked shadows beneath it too.
    half haze=1-(1-depthHaze)*(1-lowBank*exterior);
    // Opt-in necropolis scenery keeps a dark land/architecture silhouette behind
    // the low bank. Earlier scenery roles retain their complete fog dissolve.
    if(role>2.5)
    {
        half silhouette=smoothstep(12,32,distanceOutside);
        color=lerp(color,_CrowdPunchSceneryHazeColor.rgb*.10,silhouette);
        haze*=lerp(1,.16,silhouette);
    }
    return lerp(color,_CrowdPunchSceneryHazeColor.rgb,haze);
}
#endif
