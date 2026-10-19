#ifndef CROWD_PUNCH_ARENA_SCENERY_HAZE_INCLUDED
#define CROWD_PUNCH_ARENA_SCENERY_HAZE_INCLUDED
float4 _CrowdPunchSceneryHazeBounds; // Centre XZ, half-size XZ of the full playable envelope.
half4 _CrowdPunchSceneryHazeColor;
float4 _CrowdPunchSceneryHazeSettings; // Enabled, exterior ground height, bank top, opaque scenery distance.

half3 ApplyArenaSceneryHaze(half3 color, float3 positionWS, float role)
{
    if(role<.5 || _CrowdPunchSceneryHazeSettings.x<.5) return color;
    float2 outside=max(abs(positionWS.xz-_CrowdPunchSceneryHazeBounds.xy)-_CrowdPunchSceneryHazeBounds.zw,0);
    float distanceOutside=length(outside);
    // Dense low fog buries nearby scenery instead of uniformly tinting its outline.
    // Everything beyond the bank dissolves completely, including horizon corners.
    float exterior=smoothstep(1,6,distanceOutside);
    float height=positionWS.y-_CrowdPunchSceneryHazeSettings.y;
    half lowBank=1-smoothstep(1.5,_CrowdPunchSceneryHazeSettings.z,height);
    half depthHaze=smoothstep(3,_CrowdPunchSceneryHazeSettings.w,distanceOutside);
    // Ground and scenery share the same bank, hiding baked shadows beneath it too.
    half haze=1-(1-depthHaze)*(1-lowBank*exterior);
    return lerp(color,_CrowdPunchSceneryHazeColor.rgb,haze);
}
#endif
