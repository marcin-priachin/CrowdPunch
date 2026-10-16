#define ART_LAB_CHARACTER
#include "Assets/CrowdPunch/Shaders/ArtLabLighting.hlsl"
#define UniversalFragmentPBR ArtLabFragment
#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/PBRForwardPass.hlsl"
#undef UniversalFragmentPBR
