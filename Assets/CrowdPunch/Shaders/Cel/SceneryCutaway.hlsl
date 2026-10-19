#ifndef CROWD_PUNCH_SCENERY_CUTAWAY_INCLUDED
#define CROWD_PUNCH_SCENERY_CUTAWAY_INCLUDED

float4 _CrowdPunchCutawayCamera;
float4 _CrowdPunchCutawayFocus;
float4 _CrowdPunchCutawaySettings;

void ClipCameraSideScenery(float3 positionWS, float2 pixelPosition, float eligible)
{
    if (eligible < .5 || _CrowdPunchCutawaySettings.x < .5) return;
    float3 segment = _CrowdPunchCutawayFocus.xyz - _CrowdPunchCutawayCamera.xyz;
    float distanceToFocus = length(segment);
    float3 direction = segment / max(distanceToFocus, .001);
    float3 fromCamera = positionWS - _CrowdPunchCutawayCamera.xyz;
    float along = dot(fromCamera, direction);
    // Keep scenery beyond the player. Never move geometry or change collision/shadow data.
    if (along < 0 || along >= distanceToFocus) return;
    float radius = lerp(_CrowdPunchCutawayCamera.w, _CrowdPunchCutawayFocus.w, along / max(distanceToFocus, .001));
    float radial = length(fromCamera - direction * along);
    float visibility = smoothstep(radius, radius + _CrowdPunchCutawaySettings.y, radial);
    // Ordered coverage has no animated noise and matches in color, depth and normal passes.
    uint2 cell = (uint2)pixelPosition & 3;
    const float coverage[16] = { .5,8.5,2.5,10.5, 12.5,4.5,14.5,6.5, 3.5,11.5,1.5,9.5, 15.5,7.5,13.5,5.5 };
    clip(visibility - coverage[cell.y * 4 + cell.x] / 16);
}
#endif
