// Fixed production shading; independent of Editor preview globals.
#define CROWD_PUNCH_FULL_CEL
#include "Assets/CrowdPunch/Shaders/ArtLabLighting.hlsl"
#define UniversalFragmentPBR ArtLabFragment
#include "Packages/com.unity.render-pipelines.universal/Shaders/LitForwardPass.hlsl"
#undef UniversalFragmentPBR
