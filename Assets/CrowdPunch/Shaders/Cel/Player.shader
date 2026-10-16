Shader "CrowdPunch/Cel/Player"
{
Properties
{
[NoScaleOffset]_ColorMap("ColorTexture", 2D) = "white" {}
[NoScaleOffset]_SkinMaskTexture("SkinMaskTexture", 2D) = "white" {}
[NoScaleOffset]_DarkMaskTexture("DarkMaskTexture", 2D) = "white" {}
[NoScaleOffset]_DirtMaskTexture("DirtMaskTexture", 2D) = "white" {}
[NoScaleOffset]_EyeEdgeMaskTexture("EyeEdgeMaskTexture", 2D) = "white" {}
_SkinColor("SkinColor", Color) = (0, 0, 0, 0)
_SkinColorAmount("SkinColorAmount", Range(0, 1)) = 0
_DarkAmount("DarkAmount", Range(0, 1)) = 0.5
_DirtColor("DirtColor", Color) = (0, 0, 0, 0)
_DirtAmount("DirtAmount", Range(0, 1)) = 0
_EyelinerAmount("EyelinerAmount", Range(0, 1)) = 0
[HideInInspector]_QueueOffset("_QueueOffset", Float) = 0
[HideInInspector]_QueueControl("_QueueControl", Float) = -1
[HideInInspector][NoScaleOffset]unity_Lightmaps("unity_Lightmaps", 2DArray) = "" {}
[HideInInspector][NoScaleOffset]unity_LightmapsInd("unity_LightmapsInd", 2DArray) = "" {}
[HideInInspector][NoScaleOffset]unity_ShadowMasks("unity_ShadowMasks", 2DArray) = "" {}
[HideInInspector]_BUILTIN_QueueOffset("Float", Float) = 0
[HideInInspector]_BUILTIN_QueueControl("Float", Float) = -1
}
SubShader
{
Tags
{
"RenderPipeline"="UniversalPipeline"
"RenderType"="Opaque"
"UniversalMaterialType" = "Lit"
"Queue"="Geometry"
"DisableBatching"="False"
"ShaderGraphShader"="true"
"ShaderGraphTargetId"="UniversalLitSubTarget"
}
Pass
{
    Name "Universal Forward"
    Tags
    {
        "LightMode" = "UniversalForwardOnly"
    }

// Render State
Cull Back
Blend One Zero
ZTest LEqual
ZWrite On

// Debug
// <None>

// --------------------------------------------------
// Pass

HLSLPROGRAM

// Pragmas
#pragma target 2.0
#pragma multi_compile_instancing
#pragma instancing_options renderinglayer
#pragma vertex vert
#pragma fragment frag

// Keywords
#pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
#pragma multi_compile_fragment _ _SCREEN_SPACE_IRRADIANCE
#pragma multi_compile _ LIGHTMAP_ON
#pragma multi_compile _ DYNAMICLIGHTMAP_ON
#pragma multi_compile _ DIRLIGHTMAP_COMBINED
#pragma multi_compile _ USE_LEGACY_LIGHTMAPS
#pragma multi_compile _ LIGHTMAP_BICUBIC_SAMPLING
#pragma multi_compile _ REFLECTION_PROBE_ROTATION
#pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
#pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
#pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
#pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
#pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
#pragma multi_compile_fragment _ _REFLECTION_PROBE_ATLAS
#pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
#pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
#pragma multi_compile _ SHADOWS_SHADOWMASK
#pragma multi_compile_fragment _ _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3
#pragma multi_compile_fragment _ _LIGHT_LAYERS
#pragma multi_compile_fragment _ DEBUG_DISPLAY
#pragma multi_compile_fragment _ _LIGHT_COOKIES
#pragma multi_compile _ _CLUSTER_LIGHT_LOOP
#pragma multi_compile _ EVALUATE_SH_MIXED EVALUATE_SH_VERTEX
// GraphKeywords: <None>

// Defines

#define _NORMALMAP 1
#define _NORMAL_DROPOFF_TS 1
#define ATTRIBUTES_NEED_NORMAL
#define ATTRIBUTES_NEED_TANGENT
#define ATTRIBUTES_NEED_TEXCOORD0
#define ATTRIBUTES_NEED_TEXCOORD1
#define ATTRIBUTES_NEED_TEXCOORD2
#define FEATURES_GRAPH_VERTEX_NORMAL_OUTPUT
#define FEATURES_GRAPH_VERTEX_TANGENT_OUTPUT
#define VARYINGS_NEED_POSITION_WS
#define VARYINGS_NEED_NORMAL_WS
#define VARYINGS_NEED_TANGENT_WS
#define VARYINGS_NEED_TEXCOORD0
#define VARYINGS_NEED_FOG_AND_VERTEX_LIGHT
#define VARYINGS_NEED_SHADOW_COORD
#define FEATURES_GRAPH_VERTEX
/* WARNING: $splice Could not find named fragment 'PassInstancing' */
#define SHADERPASS SHADERPASS_FORWARD


// custom interpolator pre-include
/* WARNING: $splice Could not find named fragment 'sgci_CustomInterpolatorPreInclude' */

// Includes
#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ProbeVolumeVariants.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DebugMipmapStreamingMacros.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DBuffer.hlsl"
#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"

// --------------------------------------------------
// Structs and Packing

// custom interpolators pre packing
/* WARNING: $splice Could not find named fragment 'CustomInterpolatorPrePacking' */

struct Attributes
{
 float3 positionOS : POSITION;
 float3 normalOS : NORMAL;
 float4 tangentOS : TANGENT;
 float4 uv0 : TEXCOORD0;
 float4 uv1 : TEXCOORD1;
 float4 uv2 : TEXCOORD2;
#if UNITY_ANY_INSTANCING_ENABLED || defined(ATTRIBUTES_NEED_INSTANCEID)
 uint instanceID : INSTANCEID_SEMANTIC;
#endif
};
struct Varyings
{
 float4 positionCS : SV_POSITION;
 float3 positionWS;
 float3 normalWS;
 float4 tangentWS;
 float4 texCoord0;
#if defined(LIGHTMAP_ON)
 float2 staticLightmapUV;
#endif
#if defined(DYNAMICLIGHTMAP_ON)
 float2 dynamicLightmapUV;
#endif
#if !defined(LIGHTMAP_ON)
 float3 sh;
#endif
#if defined(USE_APV_PROBE_OCCLUSION)
 float4 probeOcclusion;
#endif
 float4 fogFactorAndVertexLight;
#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
 float4 shadowCoord;
#endif
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
 uint instanceID : CUSTOM_INSTANCE_ID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
 uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
 uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
 FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
#endif
};
struct SurfaceDescriptionInputs
{
 float3 TangentSpaceNormal;
 float4 uv0;
};
struct VertexDescriptionInputs
{
 float3 ObjectSpaceNormal;
 float3 ObjectSpaceTangent;
 float3 ObjectSpacePosition;
};
struct PackedVaryings
{
 float4 positionCS : SV_POSITION;
#if defined(LIGHTMAP_ON)
 float2 staticLightmapUV : INTERP0;
#endif
#if defined(DYNAMICLIGHTMAP_ON)
 float2 dynamicLightmapUV : INTERP1;
#endif
#if !defined(LIGHTMAP_ON)
 float3 sh : INTERP2;
#endif
#if defined(USE_APV_PROBE_OCCLUSION)
 float4 probeOcclusion : INTERP3;
#endif
#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
 float4 shadowCoord : INTERP4;
#endif
 float4 tangentWS : INTERP5;
 float4 texCoord0 : INTERP6;
 float4 fogFactorAndVertexLight : INTERP7;
 float3 positionWS : INTERP8;
 float3 normalWS : INTERP9;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
 uint instanceID : CUSTOM_INSTANCE_ID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
 uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
 uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
 FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
#endif
};

PackedVaryings PackVaryings (Varyings input)
{
PackedVaryings output;
ZERO_INITIALIZE(PackedVaryings, output);
output.positionCS = input.positionCS;
#if defined(LIGHTMAP_ON)
output.staticLightmapUV = input.staticLightmapUV;
#endif
#if defined(DYNAMICLIGHTMAP_ON)
output.dynamicLightmapUV = input.dynamicLightmapUV;
#endif
#if !defined(LIGHTMAP_ON)
output.sh = input.sh;
#endif
#if defined(USE_APV_PROBE_OCCLUSION)
output.probeOcclusion = input.probeOcclusion;
#endif
#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
output.shadowCoord = input.shadowCoord;
#endif
output.tangentWS.xyzw = input.tangentWS;
output.texCoord0.xyzw = input.texCoord0;
output.fogFactorAndVertexLight.xyzw = input.fogFactorAndVertexLight;
output.positionWS.xyz = input.positionWS;
output.normalWS.xyz = input.normalWS;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
output.instanceID = input.instanceID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
output.cullFace = input.cullFace;
#endif
return output;
}

Varyings UnpackVaryings (PackedVaryings input)
{
Varyings output;
output.positionCS = input.positionCS;
#if defined(LIGHTMAP_ON)
output.staticLightmapUV = input.staticLightmapUV;
#endif
#if defined(DYNAMICLIGHTMAP_ON)
output.dynamicLightmapUV = input.dynamicLightmapUV;
#endif
#if !defined(LIGHTMAP_ON)
output.sh = input.sh;
#endif
#if defined(USE_APV_PROBE_OCCLUSION)
output.probeOcclusion = input.probeOcclusion;
#endif
#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
output.shadowCoord = input.shadowCoord;
#endif
output.tangentWS = input.tangentWS.xyzw;
output.texCoord0 = input.texCoord0.xyzw;
output.fogFactorAndVertexLight = input.fogFactorAndVertexLight.xyzw;
output.positionWS = input.positionWS.xyz;
output.normalWS = input.normalWS.xyz;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
output.instanceID = input.instanceID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
output.cullFace = input.cullFace;
#endif
return output;
}


// --------------------------------------------------
// Graph

// Graph Properties
CBUFFER_START(UnityPerMaterial)
float4 _ColorMap_TexelSize;
float4 _SkinMaskTexture_TexelSize;
float4 _DarkMaskTexture_TexelSize;
half4 _DirtColor;
half4 _SkinColor;
half _DarkAmount;
float4 _DirtMaskTexture_TexelSize;
half _DirtAmount;
float4 _EyeEdgeMaskTexture_TexelSize;
half _EyelinerAmount;
half _SkinColorAmount;
UNITY_TEXTURE_STREAMING_DEBUG_VARS;
CBUFFER_END


// Object and Global properties
SAMPLER(SamplerState_Linear_Repeat);
TEXTURE2D(_ColorMap);
SAMPLER(sampler_ColorMap);
TEXTURE2D(_SkinMaskTexture);
SAMPLER(sampler_SkinMaskTexture);
TEXTURE2D(_DarkMaskTexture);
SAMPLER(sampler_DarkMaskTexture);
TEXTURE2D(_CutsMaskTexture);
SAMPLER(sampler_CutsMaskTexture);
float4 _CutsMaskTexture_TexelSize;
TEXTURE2D(_MetallicMap);
SAMPLER(sampler_MetallicMap);
float4 _MetallicMap_TexelSize;
TEXTURE2D(_SmoothnessMap);
SAMPLER(sampler_SmoothnessMap);
float4 _SmoothnessMap_TexelSize;
TEXTURE2D(_EmissionMap);
SAMPLER(sampler_EmissionMap);
float4 _EmissionMap_TexelSize;
TEXTURE2D(_DirtMaskTexture);
SAMPLER(sampler_DirtMaskTexture);
half _CutsAmount;
half4 _CutsColor;
TEXTURE2D(_EyeEdgeMaskTexture);
SAMPLER(sampler_EyeEdgeMaskTexture);

// Graph Includes
// GraphIncludes: <None>

// -- Property used by ScenePickingPass
#ifdef SCENEPICKINGPASS
float4 _SelectionID;
#endif

// -- Properties used by SceneSelectionPass
#ifdef SCENESELECTIONPASS
int _ObjectId;
int _PassValue;
#endif

// Graph Functions

void Unity_Lerp_half4(half4 A, half4 B, half4 T, out half4 Out)
{
    Out = lerp(A, B, T);
}

void Unity_OneMinus_half(half In, out half Out)
{
    Out = 1 - In;
}

void Unity_Clamp_half(half In, half Min, half Max, out half Out)
{
    Out = clamp(In, Min, Max);
}

// Custom interpolators pre vertex
/* WARNING: $splice Could not find named fragment 'CustomInterpolatorPreVertex' */

// Graph Vertex
struct VertexDescription
{
half3 Position;
half3 Normal;
half3 Tangent;
};

VertexDescription VertexDescriptionFunction(VertexDescriptionInputs IN)
{
VertexDescription description = (VertexDescription)0;
description.Position = IN.ObjectSpacePosition;
description.Normal = IN.ObjectSpaceNormal;
description.Tangent = IN.ObjectSpaceTangent;
return description;
}

// Custom interpolators, pre surface
#ifdef FEATURES_GRAPH_VERTEX
Varyings CustomInterpolatorPassThroughFunc(inout Varyings output, VertexDescription input)
{
return output;
}
#define CUSTOMINTERPOLATOR_VARYPASSTHROUGH_FUNC
#endif

// Graph Pixel
struct SurfaceDescription
{
half3 BaseColor;
half3 NormalTS;
half3 Emission;
half Metallic;
half Smoothness;
half Occlusion;
};

SurfaceDescription SurfaceDescriptionFunction(SurfaceDescriptionInputs IN)
{
SurfaceDescription surface = (SurfaceDescription)0;
half4 Color_9e0f8909aabe4b33b699f1d9bf4443dd = IsGammaSpace() ? half4(0, 0, 0, 0) : half4(SRGBToLinear(half3(0, 0, 0)), 0);
half4 _Property_ecf2fe5309e54977b870684c8d900a70_Out_0_Vector4 = _SkinColor;
half4 _Property_dee2f69732114c579841770d3a5f1ce9_Out_0_Vector4 = _CutsColor;
half4 _Property_2152e36dab12470385b94206ce043312_Out_0_Vector4 = _DirtColor;
half4 Color_1d5d22b78f8b4dc6b677715e3c97350d = IsGammaSpace() ? half4(0, 0, 0, 0) : half4(SRGBToLinear(half3(0, 0, 0)), 0);
UnityTexture2D _Property_29217945295d45b482fdf43563ceafe1_Out_0_Texture2D = UnityBuildTexture2DStructNoScale(_ColorMap);
half4 _SampleTexture2D_84a95f63ac39426f99979a9f9613953a_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_29217945295d45b482fdf43563ceafe1_Out_0_Texture2D.tex, _Property_29217945295d45b482fdf43563ceafe1_Out_0_Texture2D.samplerstate, _Property_29217945295d45b482fdf43563ceafe1_Out_0_Texture2D.GetTransformedUV(IN.uv0.xy) );
half _SampleTexture2D_84a95f63ac39426f99979a9f9613953a_R_4_Float = _SampleTexture2D_84a95f63ac39426f99979a9f9613953a_RGBA_0_Vector4.r;
half _SampleTexture2D_84a95f63ac39426f99979a9f9613953a_G_5_Float = _SampleTexture2D_84a95f63ac39426f99979a9f9613953a_RGBA_0_Vector4.g;
half _SampleTexture2D_84a95f63ac39426f99979a9f9613953a_B_6_Float = _SampleTexture2D_84a95f63ac39426f99979a9f9613953a_RGBA_0_Vector4.b;
half _SampleTexture2D_84a95f63ac39426f99979a9f9613953a_A_7_Float = _SampleTexture2D_84a95f63ac39426f99979a9f9613953a_RGBA_0_Vector4.a;
half4 Color_22af752f56df4b69a15a74bd003266a0 = IsGammaSpace() ? half4(1, 1, 1, 0) : half4(SRGBToLinear(half3(1, 1, 1)), 0);
UnityTexture2D _Property_c18bcc123f5545dda6a4416e976dce17_Out_0_Texture2D = UnityBuildTexture2DStructNoScale(_DarkMaskTexture);
half4 _SampleTexture2D_6ccf5a041d9a4379a08064554c835a43_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_c18bcc123f5545dda6a4416e976dce17_Out_0_Texture2D.tex, _Property_c18bcc123f5545dda6a4416e976dce17_Out_0_Texture2D.samplerstate, _Property_c18bcc123f5545dda6a4416e976dce17_Out_0_Texture2D.GetTransformedUV(IN.uv0.xy) );
half _SampleTexture2D_6ccf5a041d9a4379a08064554c835a43_R_4_Float = _SampleTexture2D_6ccf5a041d9a4379a08064554c835a43_RGBA_0_Vector4.r;
half _SampleTexture2D_6ccf5a041d9a4379a08064554c835a43_G_5_Float = _SampleTexture2D_6ccf5a041d9a4379a08064554c835a43_RGBA_0_Vector4.g;
half _SampleTexture2D_6ccf5a041d9a4379a08064554c835a43_B_6_Float = _SampleTexture2D_6ccf5a041d9a4379a08064554c835a43_RGBA_0_Vector4.b;
half _SampleTexture2D_6ccf5a041d9a4379a08064554c835a43_A_7_Float = _SampleTexture2D_6ccf5a041d9a4379a08064554c835a43_RGBA_0_Vector4.a;
half _Property_f460bfa9d43848bba59ba7460f20c9c2_Out_0_Float = _DarkAmount;
half4 _Lerp_b97b4bab53a04c6ea870c54dde30e56e_Out_3_Vector4;
Unity_Lerp_half4(Color_22af752f56df4b69a15a74bd003266a0, _SampleTexture2D_6ccf5a041d9a4379a08064554c835a43_RGBA_0_Vector4, (_Property_f460bfa9d43848bba59ba7460f20c9c2_Out_0_Float.xxxx), _Lerp_b97b4bab53a04c6ea870c54dde30e56e_Out_3_Vector4);
half4 _Lerp_5fb58a8630424f0aa299b82d3c6b7cf2_Out_3_Vector4;
Unity_Lerp_half4(Color_1d5d22b78f8b4dc6b677715e3c97350d, _SampleTexture2D_84a95f63ac39426f99979a9f9613953a_RGBA_0_Vector4, _Lerp_b97b4bab53a04c6ea870c54dde30e56e_Out_3_Vector4, _Lerp_5fb58a8630424f0aa299b82d3c6b7cf2_Out_3_Vector4);
UnityTexture2D _Property_f8239e9549eb4afdb04e0add697a9e63_Out_0_Texture2D = UnityBuildTexture2DStructNoScale(_DirtMaskTexture);
half4 _SampleTexture2D_7a735da5e71c436f82fc33c38b3cb722_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_f8239e9549eb4afdb04e0add697a9e63_Out_0_Texture2D.tex, _Property_f8239e9549eb4afdb04e0add697a9e63_Out_0_Texture2D.samplerstate, _Property_f8239e9549eb4afdb04e0add697a9e63_Out_0_Texture2D.GetTransformedUV(IN.uv0.xy) );
half _SampleTexture2D_7a735da5e71c436f82fc33c38b3cb722_R_4_Float = _SampleTexture2D_7a735da5e71c436f82fc33c38b3cb722_RGBA_0_Vector4.r;
half _SampleTexture2D_7a735da5e71c436f82fc33c38b3cb722_G_5_Float = _SampleTexture2D_7a735da5e71c436f82fc33c38b3cb722_RGBA_0_Vector4.g;
half _SampleTexture2D_7a735da5e71c436f82fc33c38b3cb722_B_6_Float = _SampleTexture2D_7a735da5e71c436f82fc33c38b3cb722_RGBA_0_Vector4.b;
half _SampleTexture2D_7a735da5e71c436f82fc33c38b3cb722_A_7_Float = _SampleTexture2D_7a735da5e71c436f82fc33c38b3cb722_RGBA_0_Vector4.a;
half _Property_ef41be1c8664497cbdfb806842079a89_Out_0_Float = _DirtAmount;
half4 _Lerp_aec65eee28314809a1410cf73b04a889_Out_3_Vector4;
Unity_Lerp_half4(Color_22af752f56df4b69a15a74bd003266a0, _SampleTexture2D_7a735da5e71c436f82fc33c38b3cb722_RGBA_0_Vector4, (_Property_ef41be1c8664497cbdfb806842079a89_Out_0_Float.xxxx), _Lerp_aec65eee28314809a1410cf73b04a889_Out_3_Vector4);
half4 _Lerp_1422a096bdfb4539b217cd9460b0b019_Out_3_Vector4;
Unity_Lerp_half4(_Property_2152e36dab12470385b94206ce043312_Out_0_Vector4, _Lerp_5fb58a8630424f0aa299b82d3c6b7cf2_Out_3_Vector4, _Lerp_aec65eee28314809a1410cf73b04a889_Out_3_Vector4, _Lerp_1422a096bdfb4539b217cd9460b0b019_Out_3_Vector4);
UnityTexture2D _Property_cad87ecf436a44458da862f7575dfeec_Out_0_Texture2D = UnityBuildTexture2DStructNoScale(_CutsMaskTexture);
half4 _SampleTexture2D_1937dc0552af4944a98689fd9d6fa9cc_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_cad87ecf436a44458da862f7575dfeec_Out_0_Texture2D.tex, _Property_cad87ecf436a44458da862f7575dfeec_Out_0_Texture2D.samplerstate, _Property_cad87ecf436a44458da862f7575dfeec_Out_0_Texture2D.GetTransformedUV(IN.uv0.xy) );
half _SampleTexture2D_1937dc0552af4944a98689fd9d6fa9cc_R_4_Float = _SampleTexture2D_1937dc0552af4944a98689fd9d6fa9cc_RGBA_0_Vector4.r;
half _SampleTexture2D_1937dc0552af4944a98689fd9d6fa9cc_G_5_Float = _SampleTexture2D_1937dc0552af4944a98689fd9d6fa9cc_RGBA_0_Vector4.g;
half _SampleTexture2D_1937dc0552af4944a98689fd9d6fa9cc_B_6_Float = _SampleTexture2D_1937dc0552af4944a98689fd9d6fa9cc_RGBA_0_Vector4.b;
half _SampleTexture2D_1937dc0552af4944a98689fd9d6fa9cc_A_7_Float = _SampleTexture2D_1937dc0552af4944a98689fd9d6fa9cc_RGBA_0_Vector4.a;
half _Property_092064d1a1804dcb88349a0dfa930d34_Out_0_Float = _CutsAmount;
half4 _Lerp_587eb00b83614171a84a065eb4475d69_Out_3_Vector4;
Unity_Lerp_half4(Color_22af752f56df4b69a15a74bd003266a0, _SampleTexture2D_1937dc0552af4944a98689fd9d6fa9cc_RGBA_0_Vector4, (_Property_092064d1a1804dcb88349a0dfa930d34_Out_0_Float.xxxx), _Lerp_587eb00b83614171a84a065eb4475d69_Out_3_Vector4);
half4 _Lerp_480d292819cd497f84da826a5234e813_Out_3_Vector4;
Unity_Lerp_half4(_Property_dee2f69732114c579841770d3a5f1ce9_Out_0_Vector4, _Lerp_1422a096bdfb4539b217cd9460b0b019_Out_3_Vector4, _Lerp_587eb00b83614171a84a065eb4475d69_Out_3_Vector4, _Lerp_480d292819cd497f84da826a5234e813_Out_3_Vector4);
UnityTexture2D _Property_b2cfc554a3654888b085ea4dadd11e99_Out_0_Texture2D = UnityBuildTexture2DStructNoScale(_SkinMaskTexture);
half4 _SampleTexture2D_864cfb091b7d4331bd7218fdeaf8d858_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_b2cfc554a3654888b085ea4dadd11e99_Out_0_Texture2D.tex, _Property_b2cfc554a3654888b085ea4dadd11e99_Out_0_Texture2D.samplerstate, _Property_b2cfc554a3654888b085ea4dadd11e99_Out_0_Texture2D.GetTransformedUV(IN.uv0.xy) );
half _SampleTexture2D_864cfb091b7d4331bd7218fdeaf8d858_R_4_Float = _SampleTexture2D_864cfb091b7d4331bd7218fdeaf8d858_RGBA_0_Vector4.r;
half _SampleTexture2D_864cfb091b7d4331bd7218fdeaf8d858_G_5_Float = _SampleTexture2D_864cfb091b7d4331bd7218fdeaf8d858_RGBA_0_Vector4.g;
half _SampleTexture2D_864cfb091b7d4331bd7218fdeaf8d858_B_6_Float = _SampleTexture2D_864cfb091b7d4331bd7218fdeaf8d858_RGBA_0_Vector4.b;
half _SampleTexture2D_864cfb091b7d4331bd7218fdeaf8d858_A_7_Float = _SampleTexture2D_864cfb091b7d4331bd7218fdeaf8d858_RGBA_0_Vector4.a;
half _Property_495de44ef11d40cb91ca34953cc899b1_Out_0_Float = _SkinColorAmount;
half4 _Lerp_82953a0631914e78ba370c51d143d891_Out_3_Vector4;
Unity_Lerp_half4(Color_22af752f56df4b69a15a74bd003266a0, _SampleTexture2D_864cfb091b7d4331bd7218fdeaf8d858_RGBA_0_Vector4, (_Property_495de44ef11d40cb91ca34953cc899b1_Out_0_Float.xxxx), _Lerp_82953a0631914e78ba370c51d143d891_Out_3_Vector4);
half4 _Lerp_56dd8066e70d4c85a8d72c30ef200bb7_Out_3_Vector4;
Unity_Lerp_half4(_Property_ecf2fe5309e54977b870684c8d900a70_Out_0_Vector4, _Lerp_480d292819cd497f84da826a5234e813_Out_3_Vector4, _Lerp_82953a0631914e78ba370c51d143d891_Out_3_Vector4, _Lerp_56dd8066e70d4c85a8d72c30ef200bb7_Out_3_Vector4);
half _Property_4208ddc25284429fba969ec5b8970ca4_Out_0_Float = _EyelinerAmount;
half _OneMinus_dfa88fc204374b35898331eae5eba835_Out_1_Float;
Unity_OneMinus_half(_Property_4208ddc25284429fba969ec5b8970ca4_Out_0_Float, _OneMinus_dfa88fc204374b35898331eae5eba835_Out_1_Float);
half _Clamp_a3567345de7b4ba6850279acb3e1bbda_Out_3_Float;
Unity_Clamp_half(_OneMinus_dfa88fc204374b35898331eae5eba835_Out_1_Float, half(0), half(0.5), _Clamp_a3567345de7b4ba6850279acb3e1bbda_Out_3_Float);
UnityTexture2D _Property_0dfa773f4d6f47bc85db4d5cfb627c23_Out_0_Texture2D = UnityBuildTexture2DStructNoScale(_EyeEdgeMaskTexture);
half4 _SampleTexture2D_1e5e254c29e84e2ebe790f4de0afeab7_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_0dfa773f4d6f47bc85db4d5cfb627c23_Out_0_Texture2D.tex, _Property_0dfa773f4d6f47bc85db4d5cfb627c23_Out_0_Texture2D.samplerstate, _Property_0dfa773f4d6f47bc85db4d5cfb627c23_Out_0_Texture2D.GetTransformedUV(IN.uv0.xy) );
half _SampleTexture2D_1e5e254c29e84e2ebe790f4de0afeab7_R_4_Float = _SampleTexture2D_1e5e254c29e84e2ebe790f4de0afeab7_RGBA_0_Vector4.r;
half _SampleTexture2D_1e5e254c29e84e2ebe790f4de0afeab7_G_5_Float = _SampleTexture2D_1e5e254c29e84e2ebe790f4de0afeab7_RGBA_0_Vector4.g;
half _SampleTexture2D_1e5e254c29e84e2ebe790f4de0afeab7_B_6_Float = _SampleTexture2D_1e5e254c29e84e2ebe790f4de0afeab7_RGBA_0_Vector4.b;
half _SampleTexture2D_1e5e254c29e84e2ebe790f4de0afeab7_A_7_Float = _SampleTexture2D_1e5e254c29e84e2ebe790f4de0afeab7_RGBA_0_Vector4.a;
half4 _Lerp_3fc533c198e74a1ebb2c7f7bad6d9762_Out_3_Vector4;
Unity_Lerp_half4(Color_22af752f56df4b69a15a74bd003266a0, (_Clamp_a3567345de7b4ba6850279acb3e1bbda_Out_3_Float.xxxx), _SampleTexture2D_1e5e254c29e84e2ebe790f4de0afeab7_RGBA_0_Vector4, _Lerp_3fc533c198e74a1ebb2c7f7bad6d9762_Out_3_Vector4);
half4 _Lerp_7b945e4c623b447f84028beaef139c78_Out_3_Vector4;
Unity_Lerp_half4(Color_9e0f8909aabe4b33b699f1d9bf4443dd, _Lerp_56dd8066e70d4c85a8d72c30ef200bb7_Out_3_Vector4, _Lerp_3fc533c198e74a1ebb2c7f7bad6d9762_Out_3_Vector4, _Lerp_7b945e4c623b447f84028beaef139c78_Out_3_Vector4);
surface.BaseColor = (_Lerp_7b945e4c623b447f84028beaef139c78_Out_3_Vector4.xyz);
surface.NormalTS = IN.TangentSpaceNormal;
surface.Emission = half3(0, 0, 0);
surface.Metallic = half(0);
surface.Smoothness = half(0.3);
surface.Occlusion = half(1);
return surface;
}

// --------------------------------------------------
// Build Graph Inputs
#ifdef HAVE_VFX_MODIFICATION
#define VFX_SRP_ATTRIBUTES Attributes
#define VFX_SRP_VARYINGS Varyings
#define VFX_SRP_SURFACE_INPUTS SurfaceDescriptionInputs
#endif
VertexDescriptionInputs BuildVertexDescriptionInputs(Attributes input)
{
    VertexDescriptionInputs output;
    ZERO_INITIALIZE(VertexDescriptionInputs, output);

    output.ObjectSpaceNormal =                          input.normalOS;
    output.ObjectSpaceTangent =                         input.tangentOS.xyz;
    output.ObjectSpacePosition =                        input.positionOS;
#if UNITY_ANY_INSTANCING_ENABLED
#else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.
#endif

    return output;
}
SurfaceDescriptionInputs BuildSurfaceDescriptionInputs(Varyings input)
{
    SurfaceDescriptionInputs output;
    ZERO_INITIALIZE(SurfaceDescriptionInputs, output);

#ifdef HAVE_VFX_MODIFICATION
#if VFX_USE_GRAPH_VALUES
    uint instanceActiveIndex = asuint(UNITY_ACCESS_INSTANCED_PROP(PerInstance, _InstanceActiveIndex));
    /* WARNING: $splice Could not find named fragment 'VFXLoadGraphValues' */
#endif
    /* WARNING: $splice Could not find named fragment 'VFXSetFragInputs' */

#endif

    



    output.TangentSpaceNormal = float3(0.0f, 0.0f, 1.0f);



    #if UNITY_UV_STARTS_AT_TOP
    #else
    #endif


    output.uv0 = input.texCoord0;
#if UNITY_ANY_INSTANCING_ENABLED
#else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
#define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN output.FaceSign =                    IS_FRONT_VFACE(input.cullFace, true, false);
#else
#define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
#endif
#undef BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN

        return output;
}

// --------------------------------------------------
// Main

#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/Varyings.hlsl"
#include "Assets/CrowdPunch/Shaders/CelGraphForward.hlsl"

// --------------------------------------------------
// Visual Effect Vertex Invocations
#ifdef HAVE_VFX_MODIFICATION
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/VisualEffectVertex.hlsl"
#endif

ENDHLSL
}
Pass
{
    Name "GBuffer"
    Tags
    {
        "LightMode" = "ArtLabUnusedGBuffer"
    }

// Render State
Cull Back
Blend One Zero
ZTest LEqual
ZWrite On

// Debug
// <None>

// --------------------------------------------------
// Pass

HLSLPROGRAM

// Pragmas
#pragma target 4.5
#pragma exclude_renderers gles3 glcore
#pragma multi_compile_instancing
#pragma instancing_options renderinglayer
#pragma vertex vert
#pragma fragment frag

// Keywords
#pragma multi_compile_fragment _ _SCREEN_SPACE_IRRADIANCE
#pragma multi_compile _ LIGHTMAP_ON
#pragma multi_compile _ DYNAMICLIGHTMAP_ON
#pragma multi_compile _ DIRLIGHTMAP_COMBINED
#pragma multi_compile _ USE_LEGACY_LIGHTMAPS
#pragma multi_compile _ LIGHTMAP_BICUBIC_SAMPLING
#pragma multi_compile _ REFLECTION_PROBE_ROTATION
#pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
#pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
#pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
#pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
#pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
#pragma multi_compile _ SHADOWS_SHADOWMASK
#pragma multi_compile _ _MIXED_LIGHTING_SUBTRACTIVE
#pragma multi_compile_fragment _ _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3
#pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
#pragma multi_compile_fragment _ _RENDER_PASS_ENABLED
#pragma multi_compile_fragment _ DEBUG_DISPLAY
#pragma multi_compile _ _CLUSTER_LIGHT_LOOP
// GraphKeywords: <None>

// Defines

#define _NORMALMAP 1
#define _NORMAL_DROPOFF_TS 1
#define ATTRIBUTES_NEED_NORMAL
#define ATTRIBUTES_NEED_TANGENT
#define ATTRIBUTES_NEED_TEXCOORD0
#define ATTRIBUTES_NEED_TEXCOORD1
#define ATTRIBUTES_NEED_TEXCOORD2
#define FEATURES_GRAPH_VERTEX_NORMAL_OUTPUT
#define FEATURES_GRAPH_VERTEX_TANGENT_OUTPUT
#define VARYINGS_NEED_POSITION_WS
#define VARYINGS_NEED_NORMAL_WS
#define VARYINGS_NEED_TANGENT_WS
#define VARYINGS_NEED_TEXCOORD0
#define VARYINGS_NEED_FOG_AND_VERTEX_LIGHT
#define VARYINGS_NEED_SHADOW_COORD
#define FEATURES_GRAPH_VERTEX
/* WARNING: $splice Could not find named fragment 'PassInstancing' */
#define SHADERPASS SHADERPASS_GBUFFER
#define _FOG_FRAGMENT 1


// custom interpolator pre-include
/* WARNING: $splice Could not find named fragment 'sgci_CustomInterpolatorPreInclude' */

// Includes
#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ProbeVolumeVariants.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DebugMipmapStreamingMacros.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DBuffer.hlsl"
#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"

// --------------------------------------------------
// Structs and Packing

// custom interpolators pre packing
/* WARNING: $splice Could not find named fragment 'CustomInterpolatorPrePacking' */

struct Attributes
{
 float3 positionOS : POSITION;
 float3 normalOS : NORMAL;
 float4 tangentOS : TANGENT;
 float4 uv0 : TEXCOORD0;
 float4 uv1 : TEXCOORD1;
 float4 uv2 : TEXCOORD2;
#if UNITY_ANY_INSTANCING_ENABLED || defined(ATTRIBUTES_NEED_INSTANCEID)
 uint instanceID : INSTANCEID_SEMANTIC;
#endif
};
struct Varyings
{
 float4 positionCS : SV_POSITION;
 float3 positionWS;
 float3 normalWS;
 float4 tangentWS;
 float4 texCoord0;
#if defined(LIGHTMAP_ON)
 float2 staticLightmapUV;
#endif
#if defined(DYNAMICLIGHTMAP_ON)
 float2 dynamicLightmapUV;
#endif
#if !defined(LIGHTMAP_ON)
 float3 sh;
#endif
#if defined(USE_APV_PROBE_OCCLUSION)
 float4 probeOcclusion;
#endif
 float4 fogFactorAndVertexLight;
#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
 float4 shadowCoord;
#endif
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
 uint instanceID : CUSTOM_INSTANCE_ID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
 uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
 uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
 FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
#endif
};
struct SurfaceDescriptionInputs
{
 float3 TangentSpaceNormal;
 float4 uv0;
};
struct VertexDescriptionInputs
{
 float3 ObjectSpaceNormal;
 float3 ObjectSpaceTangent;
 float3 ObjectSpacePosition;
};
struct PackedVaryings
{
 float4 positionCS : SV_POSITION;
#if defined(LIGHTMAP_ON)
 float2 staticLightmapUV : INTERP0;
#endif
#if defined(DYNAMICLIGHTMAP_ON)
 float2 dynamicLightmapUV : INTERP1;
#endif
#if !defined(LIGHTMAP_ON)
 float3 sh : INTERP2;
#endif
#if defined(USE_APV_PROBE_OCCLUSION)
 float4 probeOcclusion : INTERP3;
#endif
#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
 float4 shadowCoord : INTERP4;
#endif
 float4 tangentWS : INTERP5;
 float4 texCoord0 : INTERP6;
 float4 fogFactorAndVertexLight : INTERP7;
 float3 positionWS : INTERP8;
 float3 normalWS : INTERP9;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
 uint instanceID : CUSTOM_INSTANCE_ID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
 uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
 uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
 FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
#endif
};

PackedVaryings PackVaryings (Varyings input)
{
PackedVaryings output;
ZERO_INITIALIZE(PackedVaryings, output);
output.positionCS = input.positionCS;
#if defined(LIGHTMAP_ON)
output.staticLightmapUV = input.staticLightmapUV;
#endif
#if defined(DYNAMICLIGHTMAP_ON)
output.dynamicLightmapUV = input.dynamicLightmapUV;
#endif
#if !defined(LIGHTMAP_ON)
output.sh = input.sh;
#endif
#if defined(USE_APV_PROBE_OCCLUSION)
output.probeOcclusion = input.probeOcclusion;
#endif
#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
output.shadowCoord = input.shadowCoord;
#endif
output.tangentWS.xyzw = input.tangentWS;
output.texCoord0.xyzw = input.texCoord0;
output.fogFactorAndVertexLight.xyzw = input.fogFactorAndVertexLight;
output.positionWS.xyz = input.positionWS;
output.normalWS.xyz = input.normalWS;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
output.instanceID = input.instanceID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
output.cullFace = input.cullFace;
#endif
return output;
}

Varyings UnpackVaryings (PackedVaryings input)
{
Varyings output;
output.positionCS = input.positionCS;
#if defined(LIGHTMAP_ON)
output.staticLightmapUV = input.staticLightmapUV;
#endif
#if defined(DYNAMICLIGHTMAP_ON)
output.dynamicLightmapUV = input.dynamicLightmapUV;
#endif
#if !defined(LIGHTMAP_ON)
output.sh = input.sh;
#endif
#if defined(USE_APV_PROBE_OCCLUSION)
output.probeOcclusion = input.probeOcclusion;
#endif
#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
output.shadowCoord = input.shadowCoord;
#endif
output.tangentWS = input.tangentWS.xyzw;
output.texCoord0 = input.texCoord0.xyzw;
output.fogFactorAndVertexLight = input.fogFactorAndVertexLight.xyzw;
output.positionWS = input.positionWS.xyz;
output.normalWS = input.normalWS.xyz;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
output.instanceID = input.instanceID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
output.cullFace = input.cullFace;
#endif
return output;
}


// --------------------------------------------------
// Graph

// Graph Properties
CBUFFER_START(UnityPerMaterial)
float4 _ColorMap_TexelSize;
float4 _SkinMaskTexture_TexelSize;
float4 _DarkMaskTexture_TexelSize;
half4 _DirtColor;
half4 _SkinColor;
half _DarkAmount;
float4 _DirtMaskTexture_TexelSize;
half _DirtAmount;
float4 _EyeEdgeMaskTexture_TexelSize;
half _EyelinerAmount;
half _SkinColorAmount;
UNITY_TEXTURE_STREAMING_DEBUG_VARS;
CBUFFER_END


// Object and Global properties
SAMPLER(SamplerState_Linear_Repeat);
TEXTURE2D(_ColorMap);
SAMPLER(sampler_ColorMap);
TEXTURE2D(_SkinMaskTexture);
SAMPLER(sampler_SkinMaskTexture);
TEXTURE2D(_DarkMaskTexture);
SAMPLER(sampler_DarkMaskTexture);
TEXTURE2D(_CutsMaskTexture);
SAMPLER(sampler_CutsMaskTexture);
float4 _CutsMaskTexture_TexelSize;
TEXTURE2D(_MetallicMap);
SAMPLER(sampler_MetallicMap);
float4 _MetallicMap_TexelSize;
TEXTURE2D(_SmoothnessMap);
SAMPLER(sampler_SmoothnessMap);
float4 _SmoothnessMap_TexelSize;
TEXTURE2D(_EmissionMap);
SAMPLER(sampler_EmissionMap);
float4 _EmissionMap_TexelSize;
TEXTURE2D(_DirtMaskTexture);
SAMPLER(sampler_DirtMaskTexture);
half _CutsAmount;
half4 _CutsColor;
TEXTURE2D(_EyeEdgeMaskTexture);
SAMPLER(sampler_EyeEdgeMaskTexture);

// Graph Includes
// GraphIncludes: <None>

// -- Property used by ScenePickingPass
#ifdef SCENEPICKINGPASS
float4 _SelectionID;
#endif

// -- Properties used by SceneSelectionPass
#ifdef SCENESELECTIONPASS
int _ObjectId;
int _PassValue;
#endif

// Graph Functions

void Unity_Lerp_half4(half4 A, half4 B, half4 T, out half4 Out)
{
    Out = lerp(A, B, T);
}

void Unity_OneMinus_half(half In, out half Out)
{
    Out = 1 - In;
}

void Unity_Clamp_half(half In, half Min, half Max, out half Out)
{
    Out = clamp(In, Min, Max);
}

// Custom interpolators pre vertex
/* WARNING: $splice Could not find named fragment 'CustomInterpolatorPreVertex' */

// Graph Vertex
struct VertexDescription
{
half3 Position;
half3 Normal;
half3 Tangent;
};

VertexDescription VertexDescriptionFunction(VertexDescriptionInputs IN)
{
VertexDescription description = (VertexDescription)0;
description.Position = IN.ObjectSpacePosition;
description.Normal = IN.ObjectSpaceNormal;
description.Tangent = IN.ObjectSpaceTangent;
return description;
}

// Custom interpolators, pre surface
#ifdef FEATURES_GRAPH_VERTEX
Varyings CustomInterpolatorPassThroughFunc(inout Varyings output, VertexDescription input)
{
return output;
}
#define CUSTOMINTERPOLATOR_VARYPASSTHROUGH_FUNC
#endif

// Graph Pixel
struct SurfaceDescription
{
half3 BaseColor;
half3 NormalTS;
half3 Emission;
half Metallic;
half Smoothness;
half Occlusion;
};

SurfaceDescription SurfaceDescriptionFunction(SurfaceDescriptionInputs IN)
{
SurfaceDescription surface = (SurfaceDescription)0;
half4 Color_9e0f8909aabe4b33b699f1d9bf4443dd = IsGammaSpace() ? half4(0, 0, 0, 0) : half4(SRGBToLinear(half3(0, 0, 0)), 0);
half4 _Property_ecf2fe5309e54977b870684c8d900a70_Out_0_Vector4 = _SkinColor;
half4 _Property_dee2f69732114c579841770d3a5f1ce9_Out_0_Vector4 = _CutsColor;
half4 _Property_2152e36dab12470385b94206ce043312_Out_0_Vector4 = _DirtColor;
half4 Color_1d5d22b78f8b4dc6b677715e3c97350d = IsGammaSpace() ? half4(0, 0, 0, 0) : half4(SRGBToLinear(half3(0, 0, 0)), 0);
UnityTexture2D _Property_29217945295d45b482fdf43563ceafe1_Out_0_Texture2D = UnityBuildTexture2DStructNoScale(_ColorMap);
half4 _SampleTexture2D_84a95f63ac39426f99979a9f9613953a_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_29217945295d45b482fdf43563ceafe1_Out_0_Texture2D.tex, _Property_29217945295d45b482fdf43563ceafe1_Out_0_Texture2D.samplerstate, _Property_29217945295d45b482fdf43563ceafe1_Out_0_Texture2D.GetTransformedUV(IN.uv0.xy) );
half _SampleTexture2D_84a95f63ac39426f99979a9f9613953a_R_4_Float = _SampleTexture2D_84a95f63ac39426f99979a9f9613953a_RGBA_0_Vector4.r;
half _SampleTexture2D_84a95f63ac39426f99979a9f9613953a_G_5_Float = _SampleTexture2D_84a95f63ac39426f99979a9f9613953a_RGBA_0_Vector4.g;
half _SampleTexture2D_84a95f63ac39426f99979a9f9613953a_B_6_Float = _SampleTexture2D_84a95f63ac39426f99979a9f9613953a_RGBA_0_Vector4.b;
half _SampleTexture2D_84a95f63ac39426f99979a9f9613953a_A_7_Float = _SampleTexture2D_84a95f63ac39426f99979a9f9613953a_RGBA_0_Vector4.a;
half4 Color_22af752f56df4b69a15a74bd003266a0 = IsGammaSpace() ? half4(1, 1, 1, 0) : half4(SRGBToLinear(half3(1, 1, 1)), 0);
UnityTexture2D _Property_c18bcc123f5545dda6a4416e976dce17_Out_0_Texture2D = UnityBuildTexture2DStructNoScale(_DarkMaskTexture);
half4 _SampleTexture2D_6ccf5a041d9a4379a08064554c835a43_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_c18bcc123f5545dda6a4416e976dce17_Out_0_Texture2D.tex, _Property_c18bcc123f5545dda6a4416e976dce17_Out_0_Texture2D.samplerstate, _Property_c18bcc123f5545dda6a4416e976dce17_Out_0_Texture2D.GetTransformedUV(IN.uv0.xy) );
half _SampleTexture2D_6ccf5a041d9a4379a08064554c835a43_R_4_Float = _SampleTexture2D_6ccf5a041d9a4379a08064554c835a43_RGBA_0_Vector4.r;
half _SampleTexture2D_6ccf5a041d9a4379a08064554c835a43_G_5_Float = _SampleTexture2D_6ccf5a041d9a4379a08064554c835a43_RGBA_0_Vector4.g;
half _SampleTexture2D_6ccf5a041d9a4379a08064554c835a43_B_6_Float = _SampleTexture2D_6ccf5a041d9a4379a08064554c835a43_RGBA_0_Vector4.b;
half _SampleTexture2D_6ccf5a041d9a4379a08064554c835a43_A_7_Float = _SampleTexture2D_6ccf5a041d9a4379a08064554c835a43_RGBA_0_Vector4.a;
half _Property_f460bfa9d43848bba59ba7460f20c9c2_Out_0_Float = _DarkAmount;
half4 _Lerp_b97b4bab53a04c6ea870c54dde30e56e_Out_3_Vector4;
Unity_Lerp_half4(Color_22af752f56df4b69a15a74bd003266a0, _SampleTexture2D_6ccf5a041d9a4379a08064554c835a43_RGBA_0_Vector4, (_Property_f460bfa9d43848bba59ba7460f20c9c2_Out_0_Float.xxxx), _Lerp_b97b4bab53a04c6ea870c54dde30e56e_Out_3_Vector4);
half4 _Lerp_5fb58a8630424f0aa299b82d3c6b7cf2_Out_3_Vector4;
Unity_Lerp_half4(Color_1d5d22b78f8b4dc6b677715e3c97350d, _SampleTexture2D_84a95f63ac39426f99979a9f9613953a_RGBA_0_Vector4, _Lerp_b97b4bab53a04c6ea870c54dde30e56e_Out_3_Vector4, _Lerp_5fb58a8630424f0aa299b82d3c6b7cf2_Out_3_Vector4);
UnityTexture2D _Property_f8239e9549eb4afdb04e0add697a9e63_Out_0_Texture2D = UnityBuildTexture2DStructNoScale(_DirtMaskTexture);
half4 _SampleTexture2D_7a735da5e71c436f82fc33c38b3cb722_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_f8239e9549eb4afdb04e0add697a9e63_Out_0_Texture2D.tex, _Property_f8239e9549eb4afdb04e0add697a9e63_Out_0_Texture2D.samplerstate, _Property_f8239e9549eb4afdb04e0add697a9e63_Out_0_Texture2D.GetTransformedUV(IN.uv0.xy) );
half _SampleTexture2D_7a735da5e71c436f82fc33c38b3cb722_R_4_Float = _SampleTexture2D_7a735da5e71c436f82fc33c38b3cb722_RGBA_0_Vector4.r;
half _SampleTexture2D_7a735da5e71c436f82fc33c38b3cb722_G_5_Float = _SampleTexture2D_7a735da5e71c436f82fc33c38b3cb722_RGBA_0_Vector4.g;
half _SampleTexture2D_7a735da5e71c436f82fc33c38b3cb722_B_6_Float = _SampleTexture2D_7a735da5e71c436f82fc33c38b3cb722_RGBA_0_Vector4.b;
half _SampleTexture2D_7a735da5e71c436f82fc33c38b3cb722_A_7_Float = _SampleTexture2D_7a735da5e71c436f82fc33c38b3cb722_RGBA_0_Vector4.a;
half _Property_ef41be1c8664497cbdfb806842079a89_Out_0_Float = _DirtAmount;
half4 _Lerp_aec65eee28314809a1410cf73b04a889_Out_3_Vector4;
Unity_Lerp_half4(Color_22af752f56df4b69a15a74bd003266a0, _SampleTexture2D_7a735da5e71c436f82fc33c38b3cb722_RGBA_0_Vector4, (_Property_ef41be1c8664497cbdfb806842079a89_Out_0_Float.xxxx), _Lerp_aec65eee28314809a1410cf73b04a889_Out_3_Vector4);
half4 _Lerp_1422a096bdfb4539b217cd9460b0b019_Out_3_Vector4;
Unity_Lerp_half4(_Property_2152e36dab12470385b94206ce043312_Out_0_Vector4, _Lerp_5fb58a8630424f0aa299b82d3c6b7cf2_Out_3_Vector4, _Lerp_aec65eee28314809a1410cf73b04a889_Out_3_Vector4, _Lerp_1422a096bdfb4539b217cd9460b0b019_Out_3_Vector4);
UnityTexture2D _Property_cad87ecf436a44458da862f7575dfeec_Out_0_Texture2D = UnityBuildTexture2DStructNoScale(_CutsMaskTexture);
half4 _SampleTexture2D_1937dc0552af4944a98689fd9d6fa9cc_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_cad87ecf436a44458da862f7575dfeec_Out_0_Texture2D.tex, _Property_cad87ecf436a44458da862f7575dfeec_Out_0_Texture2D.samplerstate, _Property_cad87ecf436a44458da862f7575dfeec_Out_0_Texture2D.GetTransformedUV(IN.uv0.xy) );
half _SampleTexture2D_1937dc0552af4944a98689fd9d6fa9cc_R_4_Float = _SampleTexture2D_1937dc0552af4944a98689fd9d6fa9cc_RGBA_0_Vector4.r;
half _SampleTexture2D_1937dc0552af4944a98689fd9d6fa9cc_G_5_Float = _SampleTexture2D_1937dc0552af4944a98689fd9d6fa9cc_RGBA_0_Vector4.g;
half _SampleTexture2D_1937dc0552af4944a98689fd9d6fa9cc_B_6_Float = _SampleTexture2D_1937dc0552af4944a98689fd9d6fa9cc_RGBA_0_Vector4.b;
half _SampleTexture2D_1937dc0552af4944a98689fd9d6fa9cc_A_7_Float = _SampleTexture2D_1937dc0552af4944a98689fd9d6fa9cc_RGBA_0_Vector4.a;
half _Property_092064d1a1804dcb88349a0dfa930d34_Out_0_Float = _CutsAmount;
half4 _Lerp_587eb00b83614171a84a065eb4475d69_Out_3_Vector4;
Unity_Lerp_half4(Color_22af752f56df4b69a15a74bd003266a0, _SampleTexture2D_1937dc0552af4944a98689fd9d6fa9cc_RGBA_0_Vector4, (_Property_092064d1a1804dcb88349a0dfa930d34_Out_0_Float.xxxx), _Lerp_587eb00b83614171a84a065eb4475d69_Out_3_Vector4);
half4 _Lerp_480d292819cd497f84da826a5234e813_Out_3_Vector4;
Unity_Lerp_half4(_Property_dee2f69732114c579841770d3a5f1ce9_Out_0_Vector4, _Lerp_1422a096bdfb4539b217cd9460b0b019_Out_3_Vector4, _Lerp_587eb00b83614171a84a065eb4475d69_Out_3_Vector4, _Lerp_480d292819cd497f84da826a5234e813_Out_3_Vector4);
UnityTexture2D _Property_b2cfc554a3654888b085ea4dadd11e99_Out_0_Texture2D = UnityBuildTexture2DStructNoScale(_SkinMaskTexture);
half4 _SampleTexture2D_864cfb091b7d4331bd7218fdeaf8d858_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_b2cfc554a3654888b085ea4dadd11e99_Out_0_Texture2D.tex, _Property_b2cfc554a3654888b085ea4dadd11e99_Out_0_Texture2D.samplerstate, _Property_b2cfc554a3654888b085ea4dadd11e99_Out_0_Texture2D.GetTransformedUV(IN.uv0.xy) );
half _SampleTexture2D_864cfb091b7d4331bd7218fdeaf8d858_R_4_Float = _SampleTexture2D_864cfb091b7d4331bd7218fdeaf8d858_RGBA_0_Vector4.r;
half _SampleTexture2D_864cfb091b7d4331bd7218fdeaf8d858_G_5_Float = _SampleTexture2D_864cfb091b7d4331bd7218fdeaf8d858_RGBA_0_Vector4.g;
half _SampleTexture2D_864cfb091b7d4331bd7218fdeaf8d858_B_6_Float = _SampleTexture2D_864cfb091b7d4331bd7218fdeaf8d858_RGBA_0_Vector4.b;
half _SampleTexture2D_864cfb091b7d4331bd7218fdeaf8d858_A_7_Float = _SampleTexture2D_864cfb091b7d4331bd7218fdeaf8d858_RGBA_0_Vector4.a;
half _Property_495de44ef11d40cb91ca34953cc899b1_Out_0_Float = _SkinColorAmount;
half4 _Lerp_82953a0631914e78ba370c51d143d891_Out_3_Vector4;
Unity_Lerp_half4(Color_22af752f56df4b69a15a74bd003266a0, _SampleTexture2D_864cfb091b7d4331bd7218fdeaf8d858_RGBA_0_Vector4, (_Property_495de44ef11d40cb91ca34953cc899b1_Out_0_Float.xxxx), _Lerp_82953a0631914e78ba370c51d143d891_Out_3_Vector4);
half4 _Lerp_56dd8066e70d4c85a8d72c30ef200bb7_Out_3_Vector4;
Unity_Lerp_half4(_Property_ecf2fe5309e54977b870684c8d900a70_Out_0_Vector4, _Lerp_480d292819cd497f84da826a5234e813_Out_3_Vector4, _Lerp_82953a0631914e78ba370c51d143d891_Out_3_Vector4, _Lerp_56dd8066e70d4c85a8d72c30ef200bb7_Out_3_Vector4);
half _Property_4208ddc25284429fba969ec5b8970ca4_Out_0_Float = _EyelinerAmount;
half _OneMinus_dfa88fc204374b35898331eae5eba835_Out_1_Float;
Unity_OneMinus_half(_Property_4208ddc25284429fba969ec5b8970ca4_Out_0_Float, _OneMinus_dfa88fc204374b35898331eae5eba835_Out_1_Float);
half _Clamp_a3567345de7b4ba6850279acb3e1bbda_Out_3_Float;
Unity_Clamp_half(_OneMinus_dfa88fc204374b35898331eae5eba835_Out_1_Float, half(0), half(0.5), _Clamp_a3567345de7b4ba6850279acb3e1bbda_Out_3_Float);
UnityTexture2D _Property_0dfa773f4d6f47bc85db4d5cfb627c23_Out_0_Texture2D = UnityBuildTexture2DStructNoScale(_EyeEdgeMaskTexture);
half4 _SampleTexture2D_1e5e254c29e84e2ebe790f4de0afeab7_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_0dfa773f4d6f47bc85db4d5cfb627c23_Out_0_Texture2D.tex, _Property_0dfa773f4d6f47bc85db4d5cfb627c23_Out_0_Texture2D.samplerstate, _Property_0dfa773f4d6f47bc85db4d5cfb627c23_Out_0_Texture2D.GetTransformedUV(IN.uv0.xy) );
half _SampleTexture2D_1e5e254c29e84e2ebe790f4de0afeab7_R_4_Float = _SampleTexture2D_1e5e254c29e84e2ebe790f4de0afeab7_RGBA_0_Vector4.r;
half _SampleTexture2D_1e5e254c29e84e2ebe790f4de0afeab7_G_5_Float = _SampleTexture2D_1e5e254c29e84e2ebe790f4de0afeab7_RGBA_0_Vector4.g;
half _SampleTexture2D_1e5e254c29e84e2ebe790f4de0afeab7_B_6_Float = _SampleTexture2D_1e5e254c29e84e2ebe790f4de0afeab7_RGBA_0_Vector4.b;
half _SampleTexture2D_1e5e254c29e84e2ebe790f4de0afeab7_A_7_Float = _SampleTexture2D_1e5e254c29e84e2ebe790f4de0afeab7_RGBA_0_Vector4.a;
half4 _Lerp_3fc533c198e74a1ebb2c7f7bad6d9762_Out_3_Vector4;
Unity_Lerp_half4(Color_22af752f56df4b69a15a74bd003266a0, (_Clamp_a3567345de7b4ba6850279acb3e1bbda_Out_3_Float.xxxx), _SampleTexture2D_1e5e254c29e84e2ebe790f4de0afeab7_RGBA_0_Vector4, _Lerp_3fc533c198e74a1ebb2c7f7bad6d9762_Out_3_Vector4);
half4 _Lerp_7b945e4c623b447f84028beaef139c78_Out_3_Vector4;
Unity_Lerp_half4(Color_9e0f8909aabe4b33b699f1d9bf4443dd, _Lerp_56dd8066e70d4c85a8d72c30ef200bb7_Out_3_Vector4, _Lerp_3fc533c198e74a1ebb2c7f7bad6d9762_Out_3_Vector4, _Lerp_7b945e4c623b447f84028beaef139c78_Out_3_Vector4);
surface.BaseColor = (_Lerp_7b945e4c623b447f84028beaef139c78_Out_3_Vector4.xyz);
surface.NormalTS = IN.TangentSpaceNormal;
surface.Emission = half3(0, 0, 0);
surface.Metallic = half(0);
surface.Smoothness = half(0.3);
surface.Occlusion = half(1);
return surface;
}

// --------------------------------------------------
// Build Graph Inputs
#ifdef HAVE_VFX_MODIFICATION
#define VFX_SRP_ATTRIBUTES Attributes
#define VFX_SRP_VARYINGS Varyings
#define VFX_SRP_SURFACE_INPUTS SurfaceDescriptionInputs
#endif
VertexDescriptionInputs BuildVertexDescriptionInputs(Attributes input)
{
    VertexDescriptionInputs output;
    ZERO_INITIALIZE(VertexDescriptionInputs, output);

    output.ObjectSpaceNormal =                          input.normalOS;
    output.ObjectSpaceTangent =                         input.tangentOS.xyz;
    output.ObjectSpacePosition =                        input.positionOS;
#if UNITY_ANY_INSTANCING_ENABLED
#else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.
#endif

    return output;
}
SurfaceDescriptionInputs BuildSurfaceDescriptionInputs(Varyings input)
{
    SurfaceDescriptionInputs output;
    ZERO_INITIALIZE(SurfaceDescriptionInputs, output);

#ifdef HAVE_VFX_MODIFICATION
#if VFX_USE_GRAPH_VALUES
    uint instanceActiveIndex = asuint(UNITY_ACCESS_INSTANCED_PROP(PerInstance, _InstanceActiveIndex));
    /* WARNING: $splice Could not find named fragment 'VFXLoadGraphValues' */
#endif
    /* WARNING: $splice Could not find named fragment 'VFXSetFragInputs' */

#endif

    



    output.TangentSpaceNormal = float3(0.0f, 0.0f, 1.0f);



    #if UNITY_UV_STARTS_AT_TOP
    #else
    #endif


    output.uv0 = input.texCoord0;
#if UNITY_ANY_INSTANCING_ENABLED
#else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
#define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN output.FaceSign =                    IS_FRONT_VFACE(input.cullFace, true, false);
#else
#define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
#endif
#undef BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN

        return output;
}

// --------------------------------------------------
// Main

#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/Varyings.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/GBufferOutput.hlsl"
#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/PBRGBufferPass.hlsl"

// --------------------------------------------------
// Visual Effect Vertex Invocations
#ifdef HAVE_VFX_MODIFICATION
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/VisualEffectVertex.hlsl"
#endif

ENDHLSL
}
Pass
{
    Name "ShadowCaster"
    Tags
    {
        "LightMode" = "ShadowCaster"
    }

// Render State
Cull Back
ZTest LEqual
ZWrite On
ColorMask 0

// Debug
// <None>

// --------------------------------------------------
// Pass

HLSLPROGRAM

// Pragmas
#pragma target 2.0
#pragma multi_compile_instancing
#pragma vertex vert
#pragma fragment frag

// Keywords
#pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
// GraphKeywords: <None>

// Defines

#define _NORMALMAP 1
#define _NORMAL_DROPOFF_TS 1
#define ATTRIBUTES_NEED_NORMAL
#define ATTRIBUTES_NEED_TANGENT
#define FEATURES_GRAPH_VERTEX_NORMAL_OUTPUT
#define FEATURES_GRAPH_VERTEX_TANGENT_OUTPUT
#define VARYINGS_NEED_NORMAL_WS
#define FEATURES_GRAPH_VERTEX
/* WARNING: $splice Could not find named fragment 'PassInstancing' */
#define SHADERPASS SHADERPASS_SHADOWCASTER


// custom interpolator pre-include
/* WARNING: $splice Could not find named fragment 'sgci_CustomInterpolatorPreInclude' */

// Includes
#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DebugMipmapStreamingMacros.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"

// --------------------------------------------------
// Structs and Packing

// custom interpolators pre packing
/* WARNING: $splice Could not find named fragment 'CustomInterpolatorPrePacking' */

struct Attributes
{
 float3 positionOS : POSITION;
 float3 normalOS : NORMAL;
 float4 tangentOS : TANGENT;
#if UNITY_ANY_INSTANCING_ENABLED || defined(ATTRIBUTES_NEED_INSTANCEID)
 uint instanceID : INSTANCEID_SEMANTIC;
#endif
};
struct Varyings
{
 float4 positionCS : SV_POSITION;
 float3 normalWS;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
 uint instanceID : CUSTOM_INSTANCE_ID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
 uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
 uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
 FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
#endif
};
struct SurfaceDescriptionInputs
{
};
struct VertexDescriptionInputs
{
 float3 ObjectSpaceNormal;
 float3 ObjectSpaceTangent;
 float3 ObjectSpacePosition;
};
struct PackedVaryings
{
 float4 positionCS : SV_POSITION;
 float3 normalWS : INTERP0;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
 uint instanceID : CUSTOM_INSTANCE_ID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
 uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
 uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
 FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
#endif
};

PackedVaryings PackVaryings (Varyings input)
{
PackedVaryings output;
ZERO_INITIALIZE(PackedVaryings, output);
output.positionCS = input.positionCS;
output.normalWS.xyz = input.normalWS;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
output.instanceID = input.instanceID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
output.cullFace = input.cullFace;
#endif
return output;
}

Varyings UnpackVaryings (PackedVaryings input)
{
Varyings output;
output.positionCS = input.positionCS;
output.normalWS = input.normalWS.xyz;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
output.instanceID = input.instanceID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
output.cullFace = input.cullFace;
#endif
return output;
}


// --------------------------------------------------
// Graph

// Graph Properties
CBUFFER_START(UnityPerMaterial)
float4 _ColorMap_TexelSize;
float4 _SkinMaskTexture_TexelSize;
float4 _DarkMaskTexture_TexelSize;
half4 _DirtColor;
half4 _SkinColor;
half _DarkAmount;
float4 _DirtMaskTexture_TexelSize;
half _DirtAmount;
float4 _EyeEdgeMaskTexture_TexelSize;
half _EyelinerAmount;
half _SkinColorAmount;
UNITY_TEXTURE_STREAMING_DEBUG_VARS;
CBUFFER_END


// Object and Global properties
SAMPLER(SamplerState_Linear_Repeat);
TEXTURE2D(_ColorMap);
SAMPLER(sampler_ColorMap);
TEXTURE2D(_SkinMaskTexture);
SAMPLER(sampler_SkinMaskTexture);
TEXTURE2D(_DarkMaskTexture);
SAMPLER(sampler_DarkMaskTexture);
TEXTURE2D(_CutsMaskTexture);
SAMPLER(sampler_CutsMaskTexture);
float4 _CutsMaskTexture_TexelSize;
TEXTURE2D(_MetallicMap);
SAMPLER(sampler_MetallicMap);
float4 _MetallicMap_TexelSize;
TEXTURE2D(_SmoothnessMap);
SAMPLER(sampler_SmoothnessMap);
float4 _SmoothnessMap_TexelSize;
TEXTURE2D(_EmissionMap);
SAMPLER(sampler_EmissionMap);
float4 _EmissionMap_TexelSize;
TEXTURE2D(_DirtMaskTexture);
SAMPLER(sampler_DirtMaskTexture);
half _CutsAmount;
half4 _CutsColor;
TEXTURE2D(_EyeEdgeMaskTexture);
SAMPLER(sampler_EyeEdgeMaskTexture);

// Graph Includes
// GraphIncludes: <None>

// -- Property used by ScenePickingPass
#ifdef SCENEPICKINGPASS
float4 _SelectionID;
#endif

// -- Properties used by SceneSelectionPass
#ifdef SCENESELECTIONPASS
int _ObjectId;
int _PassValue;
#endif

// Graph Functions
// GraphFunctions: <None>

// Custom interpolators pre vertex
/* WARNING: $splice Could not find named fragment 'CustomInterpolatorPreVertex' */

// Graph Vertex
struct VertexDescription
{
half3 Position;
half3 Normal;
half3 Tangent;
};

VertexDescription VertexDescriptionFunction(VertexDescriptionInputs IN)
{
VertexDescription description = (VertexDescription)0;
description.Position = IN.ObjectSpacePosition;
description.Normal = IN.ObjectSpaceNormal;
description.Tangent = IN.ObjectSpaceTangent;
return description;
}

// Custom interpolators, pre surface
#ifdef FEATURES_GRAPH_VERTEX
Varyings CustomInterpolatorPassThroughFunc(inout Varyings output, VertexDescription input)
{
return output;
}
#define CUSTOMINTERPOLATOR_VARYPASSTHROUGH_FUNC
#endif

// Graph Pixel
struct SurfaceDescription
{
};

SurfaceDescription SurfaceDescriptionFunction(SurfaceDescriptionInputs IN)
{
SurfaceDescription surface = (SurfaceDescription)0;
return surface;
}

// --------------------------------------------------
// Build Graph Inputs
#ifdef HAVE_VFX_MODIFICATION
#define VFX_SRP_ATTRIBUTES Attributes
#define VFX_SRP_VARYINGS Varyings
#define VFX_SRP_SURFACE_INPUTS SurfaceDescriptionInputs
#endif
VertexDescriptionInputs BuildVertexDescriptionInputs(Attributes input)
{
    VertexDescriptionInputs output;
    ZERO_INITIALIZE(VertexDescriptionInputs, output);

    output.ObjectSpaceNormal =                          input.normalOS;
    output.ObjectSpaceTangent =                         input.tangentOS.xyz;
    output.ObjectSpacePosition =                        input.positionOS;
#if UNITY_ANY_INSTANCING_ENABLED
#else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.
#endif

    return output;
}
SurfaceDescriptionInputs BuildSurfaceDescriptionInputs(Varyings input)
{
    SurfaceDescriptionInputs output;
    ZERO_INITIALIZE(SurfaceDescriptionInputs, output);

#ifdef HAVE_VFX_MODIFICATION
#if VFX_USE_GRAPH_VALUES
    uint instanceActiveIndex = asuint(UNITY_ACCESS_INSTANCED_PROP(PerInstance, _InstanceActiveIndex));
    /* WARNING: $splice Could not find named fragment 'VFXLoadGraphValues' */
#endif
    /* WARNING: $splice Could not find named fragment 'VFXSetFragInputs' */

#endif

    






    #if UNITY_UV_STARTS_AT_TOP
    #else
    #endif


#if UNITY_ANY_INSTANCING_ENABLED
#else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
#define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN output.FaceSign =                    IS_FRONT_VFACE(input.cullFace, true, false);
#else
#define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
#endif
#undef BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN

        return output;
}

// --------------------------------------------------
// Main

#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/Varyings.hlsl"
#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShadowCasterPass.hlsl"

// --------------------------------------------------
// Visual Effect Vertex Invocations
#ifdef HAVE_VFX_MODIFICATION
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/VisualEffectVertex.hlsl"
#endif

ENDHLSL
}
Pass
{
    Name "MotionVectors"
    Tags
    {
        "LightMode" = "MotionVectors"
    }

// Render State
Cull Back
ZTest LEqual
ZWrite On
ColorMask RG

// Debug
// <None>

// --------------------------------------------------
// Pass

HLSLPROGRAM

// Pragmas
#pragma target 3.5
#pragma multi_compile_instancing
#pragma vertex vert
#pragma fragment frag

// Keywords
// PassKeywords: <None>
// GraphKeywords: <None>

// Defines

#define _NORMALMAP 1
#define _NORMAL_DROPOFF_TS 1
#define FEATURES_GRAPH_VERTEX
/* WARNING: $splice Could not find named fragment 'PassInstancing' */
#define SHADERPASS SHADERPASS_MOTION_VECTORS


// custom interpolator pre-include
/* WARNING: $splice Could not find named fragment 'sgci_CustomInterpolatorPreInclude' */

// Includes
#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DebugMipmapStreamingMacros.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"

// --------------------------------------------------
// Structs and Packing

// custom interpolators pre packing
/* WARNING: $splice Could not find named fragment 'CustomInterpolatorPrePacking' */

struct Attributes
{
 float3 positionOS : POSITION;
#if UNITY_ANY_INSTANCING_ENABLED || defined(ATTRIBUTES_NEED_INSTANCEID)
 uint instanceID : INSTANCEID_SEMANTIC;
#endif
};
struct Varyings
{
 float4 positionCS : SV_POSITION;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
 uint instanceID : CUSTOM_INSTANCE_ID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
 uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
 uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
 FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
#endif
};
struct SurfaceDescriptionInputs
{
};
struct VertexDescriptionInputs
{
 float3 ObjectSpacePosition;
};
struct PackedVaryings
{
 float4 positionCS : SV_POSITION;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
 uint instanceID : CUSTOM_INSTANCE_ID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
 uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
 uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
 FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
#endif
};

PackedVaryings PackVaryings (Varyings input)
{
PackedVaryings output;
ZERO_INITIALIZE(PackedVaryings, output);
output.positionCS = input.positionCS;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
output.instanceID = input.instanceID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
output.cullFace = input.cullFace;
#endif
return output;
}

Varyings UnpackVaryings (PackedVaryings input)
{
Varyings output;
output.positionCS = input.positionCS;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
output.instanceID = input.instanceID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
output.cullFace = input.cullFace;
#endif
return output;
}


// --------------------------------------------------
// Graph

// Graph Properties
CBUFFER_START(UnityPerMaterial)
float4 _ColorMap_TexelSize;
float4 _SkinMaskTexture_TexelSize;
float4 _DarkMaskTexture_TexelSize;
half4 _DirtColor;
half4 _SkinColor;
half _DarkAmount;
float4 _DirtMaskTexture_TexelSize;
half _DirtAmount;
float4 _EyeEdgeMaskTexture_TexelSize;
half _EyelinerAmount;
half _SkinColorAmount;
UNITY_TEXTURE_STREAMING_DEBUG_VARS;
CBUFFER_END


// Object and Global properties
SAMPLER(SamplerState_Linear_Repeat);
TEXTURE2D(_ColorMap);
SAMPLER(sampler_ColorMap);
TEXTURE2D(_SkinMaskTexture);
SAMPLER(sampler_SkinMaskTexture);
TEXTURE2D(_DarkMaskTexture);
SAMPLER(sampler_DarkMaskTexture);
TEXTURE2D(_CutsMaskTexture);
SAMPLER(sampler_CutsMaskTexture);
float4 _CutsMaskTexture_TexelSize;
TEXTURE2D(_MetallicMap);
SAMPLER(sampler_MetallicMap);
float4 _MetallicMap_TexelSize;
TEXTURE2D(_SmoothnessMap);
SAMPLER(sampler_SmoothnessMap);
float4 _SmoothnessMap_TexelSize;
TEXTURE2D(_EmissionMap);
SAMPLER(sampler_EmissionMap);
float4 _EmissionMap_TexelSize;
TEXTURE2D(_DirtMaskTexture);
SAMPLER(sampler_DirtMaskTexture);
half _CutsAmount;
half4 _CutsColor;
TEXTURE2D(_EyeEdgeMaskTexture);
SAMPLER(sampler_EyeEdgeMaskTexture);

// Graph Includes
// GraphIncludes: <None>

// -- Property used by ScenePickingPass
#ifdef SCENEPICKINGPASS
float4 _SelectionID;
#endif

// -- Properties used by SceneSelectionPass
#ifdef SCENESELECTIONPASS
int _ObjectId;
int _PassValue;
#endif

// Graph Functions
// GraphFunctions: <None>

// Custom interpolators pre vertex
/* WARNING: $splice Could not find named fragment 'CustomInterpolatorPreVertex' */

// Graph Vertex
struct VertexDescription
{
half3 Position;
};

VertexDescription VertexDescriptionFunction(VertexDescriptionInputs IN)
{
VertexDescription description = (VertexDescription)0;
description.Position = IN.ObjectSpacePosition;
return description;
}

// Custom interpolators, pre surface
#ifdef FEATURES_GRAPH_VERTEX
Varyings CustomInterpolatorPassThroughFunc(inout Varyings output, VertexDescription input)
{
return output;
}
#define CUSTOMINTERPOLATOR_VARYPASSTHROUGH_FUNC
#endif

// Graph Pixel
struct SurfaceDescription
{
};

SurfaceDescription SurfaceDescriptionFunction(SurfaceDescriptionInputs IN)
{
SurfaceDescription surface = (SurfaceDescription)0;
return surface;
}

// --------------------------------------------------
// Build Graph Inputs
#ifdef HAVE_VFX_MODIFICATION
#define VFX_SRP_ATTRIBUTES Attributes
#define VFX_SRP_VARYINGS Varyings
#define VFX_SRP_SURFACE_INPUTS SurfaceDescriptionInputs
#endif
VertexDescriptionInputs BuildVertexDescriptionInputs(Attributes input)
{
    VertexDescriptionInputs output;
    ZERO_INITIALIZE(VertexDescriptionInputs, output);

    output.ObjectSpacePosition =                        input.positionOS;
#if UNITY_ANY_INSTANCING_ENABLED
#else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.
#endif

    return output;
}
SurfaceDescriptionInputs BuildSurfaceDescriptionInputs(Varyings input)
{
    SurfaceDescriptionInputs output;
    ZERO_INITIALIZE(SurfaceDescriptionInputs, output);

#ifdef HAVE_VFX_MODIFICATION
#if VFX_USE_GRAPH_VALUES
    uint instanceActiveIndex = asuint(UNITY_ACCESS_INSTANCED_PROP(PerInstance, _InstanceActiveIndex));
    /* WARNING: $splice Could not find named fragment 'VFXLoadGraphValues' */
#endif
    /* WARNING: $splice Could not find named fragment 'VFXSetFragInputs' */

#endif

    






    #if UNITY_UV_STARTS_AT_TOP
    #else
    #endif


#if UNITY_ANY_INSTANCING_ENABLED
#else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
#define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN output.FaceSign =                    IS_FRONT_VFACE(input.cullFace, true, false);
#else
#define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
#endif
#undef BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN

        return output;
}

// --------------------------------------------------
// Main

#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/Varyings.hlsl"
#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/MotionVectorPass.hlsl"

// --------------------------------------------------
// Visual Effect Vertex Invocations
#ifdef HAVE_VFX_MODIFICATION
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/VisualEffectVertex.hlsl"
#endif

ENDHLSL
}
Pass
{
    Name "DepthOnly"
    Tags
    {
        "LightMode" = "DepthOnly"
    }

// Render State
Cull Back
ZTest LEqual
ZWrite On
ColorMask R

// Debug
// <None>

// --------------------------------------------------
// Pass

HLSLPROGRAM

// Pragmas
#pragma target 2.0
#pragma multi_compile_instancing
#pragma vertex vert
#pragma fragment frag

// Keywords
// PassKeywords: <None>
// GraphKeywords: <None>

// Defines

#define _NORMALMAP 1
#define _NORMAL_DROPOFF_TS 1
#define ATTRIBUTES_NEED_NORMAL
#define ATTRIBUTES_NEED_TANGENT
#define FEATURES_GRAPH_VERTEX_NORMAL_OUTPUT
#define FEATURES_GRAPH_VERTEX_TANGENT_OUTPUT
#define FEATURES_GRAPH_VERTEX
/* WARNING: $splice Could not find named fragment 'PassInstancing' */
#define SHADERPASS SHADERPASS_DEPTHONLY


// custom interpolator pre-include
/* WARNING: $splice Could not find named fragment 'sgci_CustomInterpolatorPreInclude' */

// Includes
#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DebugMipmapStreamingMacros.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"

// --------------------------------------------------
// Structs and Packing

// custom interpolators pre packing
/* WARNING: $splice Could not find named fragment 'CustomInterpolatorPrePacking' */

struct Attributes
{
 float3 positionOS : POSITION;
 float3 normalOS : NORMAL;
 float4 tangentOS : TANGENT;
#if UNITY_ANY_INSTANCING_ENABLED || defined(ATTRIBUTES_NEED_INSTANCEID)
 uint instanceID : INSTANCEID_SEMANTIC;
#endif
};
struct Varyings
{
 float4 positionCS : SV_POSITION;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
 uint instanceID : CUSTOM_INSTANCE_ID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
 uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
 uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
 FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
#endif
};
struct SurfaceDescriptionInputs
{
};
struct VertexDescriptionInputs
{
 float3 ObjectSpaceNormal;
 float3 ObjectSpaceTangent;
 float3 ObjectSpacePosition;
};
struct PackedVaryings
{
 float4 positionCS : SV_POSITION;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
 uint instanceID : CUSTOM_INSTANCE_ID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
 uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
 uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
 FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
#endif
};

PackedVaryings PackVaryings (Varyings input)
{
PackedVaryings output;
ZERO_INITIALIZE(PackedVaryings, output);
output.positionCS = input.positionCS;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
output.instanceID = input.instanceID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
output.cullFace = input.cullFace;
#endif
return output;
}

Varyings UnpackVaryings (PackedVaryings input)
{
Varyings output;
output.positionCS = input.positionCS;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
output.instanceID = input.instanceID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
output.cullFace = input.cullFace;
#endif
return output;
}


// --------------------------------------------------
// Graph

// Graph Properties
CBUFFER_START(UnityPerMaterial)
float4 _ColorMap_TexelSize;
float4 _SkinMaskTexture_TexelSize;
float4 _DarkMaskTexture_TexelSize;
half4 _DirtColor;
half4 _SkinColor;
half _DarkAmount;
float4 _DirtMaskTexture_TexelSize;
half _DirtAmount;
float4 _EyeEdgeMaskTexture_TexelSize;
half _EyelinerAmount;
half _SkinColorAmount;
UNITY_TEXTURE_STREAMING_DEBUG_VARS;
CBUFFER_END


// Object and Global properties
SAMPLER(SamplerState_Linear_Repeat);
TEXTURE2D(_ColorMap);
SAMPLER(sampler_ColorMap);
TEXTURE2D(_SkinMaskTexture);
SAMPLER(sampler_SkinMaskTexture);
TEXTURE2D(_DarkMaskTexture);
SAMPLER(sampler_DarkMaskTexture);
TEXTURE2D(_CutsMaskTexture);
SAMPLER(sampler_CutsMaskTexture);
float4 _CutsMaskTexture_TexelSize;
TEXTURE2D(_MetallicMap);
SAMPLER(sampler_MetallicMap);
float4 _MetallicMap_TexelSize;
TEXTURE2D(_SmoothnessMap);
SAMPLER(sampler_SmoothnessMap);
float4 _SmoothnessMap_TexelSize;
TEXTURE2D(_EmissionMap);
SAMPLER(sampler_EmissionMap);
float4 _EmissionMap_TexelSize;
TEXTURE2D(_DirtMaskTexture);
SAMPLER(sampler_DirtMaskTexture);
half _CutsAmount;
half4 _CutsColor;
TEXTURE2D(_EyeEdgeMaskTexture);
SAMPLER(sampler_EyeEdgeMaskTexture);

// Graph Includes
// GraphIncludes: <None>

// -- Property used by ScenePickingPass
#ifdef SCENEPICKINGPASS
float4 _SelectionID;
#endif

// -- Properties used by SceneSelectionPass
#ifdef SCENESELECTIONPASS
int _ObjectId;
int _PassValue;
#endif

// Graph Functions
// GraphFunctions: <None>

// Custom interpolators pre vertex
/* WARNING: $splice Could not find named fragment 'CustomInterpolatorPreVertex' */

// Graph Vertex
struct VertexDescription
{
half3 Position;
half3 Normal;
half3 Tangent;
};

VertexDescription VertexDescriptionFunction(VertexDescriptionInputs IN)
{
VertexDescription description = (VertexDescription)0;
description.Position = IN.ObjectSpacePosition;
description.Normal = IN.ObjectSpaceNormal;
description.Tangent = IN.ObjectSpaceTangent;
return description;
}

// Custom interpolators, pre surface
#ifdef FEATURES_GRAPH_VERTEX
Varyings CustomInterpolatorPassThroughFunc(inout Varyings output, VertexDescription input)
{
return output;
}
#define CUSTOMINTERPOLATOR_VARYPASSTHROUGH_FUNC
#endif

// Graph Pixel
struct SurfaceDescription
{
};

SurfaceDescription SurfaceDescriptionFunction(SurfaceDescriptionInputs IN)
{
SurfaceDescription surface = (SurfaceDescription)0;
return surface;
}

// --------------------------------------------------
// Build Graph Inputs
#ifdef HAVE_VFX_MODIFICATION
#define VFX_SRP_ATTRIBUTES Attributes
#define VFX_SRP_VARYINGS Varyings
#define VFX_SRP_SURFACE_INPUTS SurfaceDescriptionInputs
#endif
VertexDescriptionInputs BuildVertexDescriptionInputs(Attributes input)
{
    VertexDescriptionInputs output;
    ZERO_INITIALIZE(VertexDescriptionInputs, output);

    output.ObjectSpaceNormal =                          input.normalOS;
    output.ObjectSpaceTangent =                         input.tangentOS.xyz;
    output.ObjectSpacePosition =                        input.positionOS;
#if UNITY_ANY_INSTANCING_ENABLED
#else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.
#endif

    return output;
}
SurfaceDescriptionInputs BuildSurfaceDescriptionInputs(Varyings input)
{
    SurfaceDescriptionInputs output;
    ZERO_INITIALIZE(SurfaceDescriptionInputs, output);

#ifdef HAVE_VFX_MODIFICATION
#if VFX_USE_GRAPH_VALUES
    uint instanceActiveIndex = asuint(UNITY_ACCESS_INSTANCED_PROP(PerInstance, _InstanceActiveIndex));
    /* WARNING: $splice Could not find named fragment 'VFXLoadGraphValues' */
#endif
    /* WARNING: $splice Could not find named fragment 'VFXSetFragInputs' */

#endif

    






    #if UNITY_UV_STARTS_AT_TOP
    #else
    #endif


#if UNITY_ANY_INSTANCING_ENABLED
#else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
#define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN output.FaceSign =                    IS_FRONT_VFACE(input.cullFace, true, false);
#else
#define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
#endif
#undef BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN

        return output;
}

// --------------------------------------------------
// Main

#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/Varyings.hlsl"
#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/DepthOnlyPass.hlsl"

// --------------------------------------------------
// Visual Effect Vertex Invocations
#ifdef HAVE_VFX_MODIFICATION
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/VisualEffectVertex.hlsl"
#endif

ENDHLSL
}
Pass
{
    Name "DepthNormals"
    Tags
    {
        "LightMode" = "DepthNormals"
    }

// Render State
Cull Back
ZTest LEqual
ZWrite On

// Debug
// <None>

// --------------------------------------------------
// Pass

HLSLPROGRAM

// Pragmas
#pragma target 2.0
#pragma multi_compile_instancing
#pragma vertex vert
#pragma fragment frag

// Keywords
// PassKeywords: <None>
// GraphKeywords: <None>

// Defines

#define _NORMALMAP 1
#define _NORMAL_DROPOFF_TS 1
#define ATTRIBUTES_NEED_NORMAL
#define ATTRIBUTES_NEED_TANGENT
#define ATTRIBUTES_NEED_TEXCOORD1
#define FEATURES_GRAPH_VERTEX_NORMAL_OUTPUT
#define FEATURES_GRAPH_VERTEX_TANGENT_OUTPUT
#define VARYINGS_NEED_NORMAL_WS
#define VARYINGS_NEED_TANGENT_WS
#define FEATURES_GRAPH_VERTEX
/* WARNING: $splice Could not find named fragment 'PassInstancing' */
#define SHADERPASS SHADERPASS_DEPTHNORMALS


// custom interpolator pre-include
/* WARNING: $splice Could not find named fragment 'sgci_CustomInterpolatorPreInclude' */

// Includes
#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DebugMipmapStreamingMacros.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"

// --------------------------------------------------
// Structs and Packing

// custom interpolators pre packing
/* WARNING: $splice Could not find named fragment 'CustomInterpolatorPrePacking' */

struct Attributes
{
 float3 positionOS : POSITION;
 float3 normalOS : NORMAL;
 float4 tangentOS : TANGENT;
 float4 uv1 : TEXCOORD1;
#if UNITY_ANY_INSTANCING_ENABLED || defined(ATTRIBUTES_NEED_INSTANCEID)
 uint instanceID : INSTANCEID_SEMANTIC;
#endif
};
struct Varyings
{
 float4 positionCS : SV_POSITION;
 float3 normalWS;
 float4 tangentWS;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
 uint instanceID : CUSTOM_INSTANCE_ID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
 uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
 uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
 FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
#endif
};
struct SurfaceDescriptionInputs
{
 float3 TangentSpaceNormal;
};
struct VertexDescriptionInputs
{
 float3 ObjectSpaceNormal;
 float3 ObjectSpaceTangent;
 float3 ObjectSpacePosition;
};
struct PackedVaryings
{
 float4 positionCS : SV_POSITION;
 float4 tangentWS : INTERP0;
 float3 normalWS : INTERP1;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
 uint instanceID : CUSTOM_INSTANCE_ID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
 uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
 uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
 FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
#endif
};

PackedVaryings PackVaryings (Varyings input)
{
PackedVaryings output;
ZERO_INITIALIZE(PackedVaryings, output);
output.positionCS = input.positionCS;
output.tangentWS.xyzw = input.tangentWS;
output.normalWS.xyz = input.normalWS;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
output.instanceID = input.instanceID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
output.cullFace = input.cullFace;
#endif
return output;
}

Varyings UnpackVaryings (PackedVaryings input)
{
Varyings output;
output.positionCS = input.positionCS;
output.tangentWS = input.tangentWS.xyzw;
output.normalWS = input.normalWS.xyz;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
output.instanceID = input.instanceID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
output.cullFace = input.cullFace;
#endif
return output;
}


// --------------------------------------------------
// Graph

// Graph Properties
CBUFFER_START(UnityPerMaterial)
float4 _ColorMap_TexelSize;
float4 _SkinMaskTexture_TexelSize;
float4 _DarkMaskTexture_TexelSize;
half4 _DirtColor;
half4 _SkinColor;
half _DarkAmount;
float4 _DirtMaskTexture_TexelSize;
half _DirtAmount;
float4 _EyeEdgeMaskTexture_TexelSize;
half _EyelinerAmount;
half _SkinColorAmount;
UNITY_TEXTURE_STREAMING_DEBUG_VARS;
CBUFFER_END


// Object and Global properties
SAMPLER(SamplerState_Linear_Repeat);
TEXTURE2D(_ColorMap);
SAMPLER(sampler_ColorMap);
TEXTURE2D(_SkinMaskTexture);
SAMPLER(sampler_SkinMaskTexture);
TEXTURE2D(_DarkMaskTexture);
SAMPLER(sampler_DarkMaskTexture);
TEXTURE2D(_CutsMaskTexture);
SAMPLER(sampler_CutsMaskTexture);
float4 _CutsMaskTexture_TexelSize;
TEXTURE2D(_MetallicMap);
SAMPLER(sampler_MetallicMap);
float4 _MetallicMap_TexelSize;
TEXTURE2D(_SmoothnessMap);
SAMPLER(sampler_SmoothnessMap);
float4 _SmoothnessMap_TexelSize;
TEXTURE2D(_EmissionMap);
SAMPLER(sampler_EmissionMap);
float4 _EmissionMap_TexelSize;
TEXTURE2D(_DirtMaskTexture);
SAMPLER(sampler_DirtMaskTexture);
half _CutsAmount;
half4 _CutsColor;
TEXTURE2D(_EyeEdgeMaskTexture);
SAMPLER(sampler_EyeEdgeMaskTexture);

// Graph Includes
// GraphIncludes: <None>

// -- Property used by ScenePickingPass
#ifdef SCENEPICKINGPASS
float4 _SelectionID;
#endif

// -- Properties used by SceneSelectionPass
#ifdef SCENESELECTIONPASS
int _ObjectId;
int _PassValue;
#endif

// Graph Functions
// GraphFunctions: <None>

// Custom interpolators pre vertex
/* WARNING: $splice Could not find named fragment 'CustomInterpolatorPreVertex' */

// Graph Vertex
struct VertexDescription
{
half3 Position;
half3 Normal;
half3 Tangent;
};

VertexDescription VertexDescriptionFunction(VertexDescriptionInputs IN)
{
VertexDescription description = (VertexDescription)0;
description.Position = IN.ObjectSpacePosition;
description.Normal = IN.ObjectSpaceNormal;
description.Tangent = IN.ObjectSpaceTangent;
return description;
}

// Custom interpolators, pre surface
#ifdef FEATURES_GRAPH_VERTEX
Varyings CustomInterpolatorPassThroughFunc(inout Varyings output, VertexDescription input)
{
return output;
}
#define CUSTOMINTERPOLATOR_VARYPASSTHROUGH_FUNC
#endif

// Graph Pixel
struct SurfaceDescription
{
half3 NormalTS;
};

SurfaceDescription SurfaceDescriptionFunction(SurfaceDescriptionInputs IN)
{
SurfaceDescription surface = (SurfaceDescription)0;
surface.NormalTS = IN.TangentSpaceNormal;
return surface;
}

// --------------------------------------------------
// Build Graph Inputs
#ifdef HAVE_VFX_MODIFICATION
#define VFX_SRP_ATTRIBUTES Attributes
#define VFX_SRP_VARYINGS Varyings
#define VFX_SRP_SURFACE_INPUTS SurfaceDescriptionInputs
#endif
VertexDescriptionInputs BuildVertexDescriptionInputs(Attributes input)
{
    VertexDescriptionInputs output;
    ZERO_INITIALIZE(VertexDescriptionInputs, output);

    output.ObjectSpaceNormal =                          input.normalOS;
    output.ObjectSpaceTangent =                         input.tangentOS.xyz;
    output.ObjectSpacePosition =                        input.positionOS;
#if UNITY_ANY_INSTANCING_ENABLED
#else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.
#endif

    return output;
}
SurfaceDescriptionInputs BuildSurfaceDescriptionInputs(Varyings input)
{
    SurfaceDescriptionInputs output;
    ZERO_INITIALIZE(SurfaceDescriptionInputs, output);

#ifdef HAVE_VFX_MODIFICATION
#if VFX_USE_GRAPH_VALUES
    uint instanceActiveIndex = asuint(UNITY_ACCESS_INSTANCED_PROP(PerInstance, _InstanceActiveIndex));
    /* WARNING: $splice Could not find named fragment 'VFXLoadGraphValues' */
#endif
    /* WARNING: $splice Could not find named fragment 'VFXSetFragInputs' */

#endif

    



    output.TangentSpaceNormal = float3(0.0f, 0.0f, 1.0f);



    #if UNITY_UV_STARTS_AT_TOP
    #else
    #endif


#if UNITY_ANY_INSTANCING_ENABLED
#else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
#define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN output.FaceSign =                    IS_FRONT_VFACE(input.cullFace, true, false);
#else
#define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
#endif
#undef BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN

        return output;
}

// --------------------------------------------------
// Main

#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/Varyings.hlsl"
#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/DepthNormalsOnlyPass.hlsl"

// --------------------------------------------------
// Visual Effect Vertex Invocations
#ifdef HAVE_VFX_MODIFICATION
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/VisualEffectVertex.hlsl"
#endif

ENDHLSL
}
Pass
{
    Name "Meta"
    Tags
    {
        "LightMode" = "Meta"
    }

// Render State
Cull Off

// Debug
// <None>

// --------------------------------------------------
// Pass

HLSLPROGRAM

// Pragmas
#pragma target 2.0
#pragma vertex vert
#pragma fragment frag

// Keywords
#pragma shader_feature _ EDITOR_VISUALIZATION
// GraphKeywords: <None>

// Defines

#define _NORMALMAP 1
#define _NORMAL_DROPOFF_TS 1
#define ATTRIBUTES_NEED_NORMAL
#define ATTRIBUTES_NEED_TANGENT
#define ATTRIBUTES_NEED_TEXCOORD0
#define ATTRIBUTES_NEED_TEXCOORD1
#define ATTRIBUTES_NEED_TEXCOORD2
#define ATTRIBUTES_NEED_INSTANCEID
#define FEATURES_GRAPH_VERTEX_NORMAL_OUTPUT
#define FEATURES_GRAPH_VERTEX_TANGENT_OUTPUT
#define VARYINGS_NEED_TEXCOORD0
#define VARYINGS_NEED_TEXCOORD1
#define VARYINGS_NEED_TEXCOORD2
#define FEATURES_GRAPH_VERTEX
/* WARNING: $splice Could not find named fragment 'PassInstancing' */
#define SHADERPASS SHADERPASS_META
#define _FOG_FRAGMENT 1


// custom interpolator pre-include
/* WARNING: $splice Could not find named fragment 'sgci_CustomInterpolatorPreInclude' */

// Includes
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DebugMipmapStreamingMacros.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/MetaInput.hlsl"
#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"

// --------------------------------------------------
// Structs and Packing

// custom interpolators pre packing
/* WARNING: $splice Could not find named fragment 'CustomInterpolatorPrePacking' */

struct Attributes
{
 float3 positionOS : POSITION;
 float3 normalOS : NORMAL;
 float4 tangentOS : TANGENT;
 float4 uv0 : TEXCOORD0;
 float4 uv1 : TEXCOORD1;
 float4 uv2 : TEXCOORD2;
#if UNITY_ANY_INSTANCING_ENABLED || defined(ATTRIBUTES_NEED_INSTANCEID)
 uint instanceID : INSTANCEID_SEMANTIC;
#endif
};
struct Varyings
{
 float4 positionCS : SV_POSITION;
 float4 texCoord0;
 float4 texCoord1;
 float4 texCoord2;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
 uint instanceID : CUSTOM_INSTANCE_ID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
 uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
 uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
 FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
#endif
};
struct SurfaceDescriptionInputs
{
 float4 uv0;
};
struct VertexDescriptionInputs
{
 float3 ObjectSpaceNormal;
 float3 ObjectSpaceTangent;
 float3 ObjectSpacePosition;
};
struct PackedVaryings
{
 float4 positionCS : SV_POSITION;
 float4 texCoord0 : INTERP0;
 float4 texCoord1 : INTERP1;
 float4 texCoord2 : INTERP2;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
 uint instanceID : CUSTOM_INSTANCE_ID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
 uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
 uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
 FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
#endif
};

PackedVaryings PackVaryings (Varyings input)
{
PackedVaryings output;
ZERO_INITIALIZE(PackedVaryings, output);
output.positionCS = input.positionCS;
output.texCoord0.xyzw = input.texCoord0;
output.texCoord1.xyzw = input.texCoord1;
output.texCoord2.xyzw = input.texCoord2;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
output.instanceID = input.instanceID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
output.cullFace = input.cullFace;
#endif
return output;
}

Varyings UnpackVaryings (PackedVaryings input)
{
Varyings output;
output.positionCS = input.positionCS;
output.texCoord0 = input.texCoord0.xyzw;
output.texCoord1 = input.texCoord1.xyzw;
output.texCoord2 = input.texCoord2.xyzw;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
output.instanceID = input.instanceID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
output.cullFace = input.cullFace;
#endif
return output;
}


// --------------------------------------------------
// Graph

// Graph Properties
CBUFFER_START(UnityPerMaterial)
float4 _ColorMap_TexelSize;
float4 _SkinMaskTexture_TexelSize;
float4 _DarkMaskTexture_TexelSize;
half4 _DirtColor;
half4 _SkinColor;
half _DarkAmount;
float4 _DirtMaskTexture_TexelSize;
half _DirtAmount;
float4 _EyeEdgeMaskTexture_TexelSize;
half _EyelinerAmount;
half _SkinColorAmount;
UNITY_TEXTURE_STREAMING_DEBUG_VARS;
CBUFFER_END


// Object and Global properties
SAMPLER(SamplerState_Linear_Repeat);
TEXTURE2D(_ColorMap);
SAMPLER(sampler_ColorMap);
TEXTURE2D(_SkinMaskTexture);
SAMPLER(sampler_SkinMaskTexture);
TEXTURE2D(_DarkMaskTexture);
SAMPLER(sampler_DarkMaskTexture);
TEXTURE2D(_CutsMaskTexture);
SAMPLER(sampler_CutsMaskTexture);
float4 _CutsMaskTexture_TexelSize;
TEXTURE2D(_MetallicMap);
SAMPLER(sampler_MetallicMap);
float4 _MetallicMap_TexelSize;
TEXTURE2D(_SmoothnessMap);
SAMPLER(sampler_SmoothnessMap);
float4 _SmoothnessMap_TexelSize;
TEXTURE2D(_EmissionMap);
SAMPLER(sampler_EmissionMap);
float4 _EmissionMap_TexelSize;
TEXTURE2D(_DirtMaskTexture);
SAMPLER(sampler_DirtMaskTexture);
half _CutsAmount;
half4 _CutsColor;
TEXTURE2D(_EyeEdgeMaskTexture);
SAMPLER(sampler_EyeEdgeMaskTexture);

// Graph Includes
// GraphIncludes: <None>

// -- Property used by ScenePickingPass
#ifdef SCENEPICKINGPASS
float4 _SelectionID;
#endif

// -- Properties used by SceneSelectionPass
#ifdef SCENESELECTIONPASS
int _ObjectId;
int _PassValue;
#endif

// Graph Functions

void Unity_Lerp_half4(half4 A, half4 B, half4 T, out half4 Out)
{
    Out = lerp(A, B, T);
}

void Unity_OneMinus_half(half In, out half Out)
{
    Out = 1 - In;
}

void Unity_Clamp_half(half In, half Min, half Max, out half Out)
{
    Out = clamp(In, Min, Max);
}

// Custom interpolators pre vertex
/* WARNING: $splice Could not find named fragment 'CustomInterpolatorPreVertex' */

// Graph Vertex
struct VertexDescription
{
half3 Position;
half3 Normal;
half3 Tangent;
};

VertexDescription VertexDescriptionFunction(VertexDescriptionInputs IN)
{
VertexDescription description = (VertexDescription)0;
description.Position = IN.ObjectSpacePosition;
description.Normal = IN.ObjectSpaceNormal;
description.Tangent = IN.ObjectSpaceTangent;
return description;
}

// Custom interpolators, pre surface
#ifdef FEATURES_GRAPH_VERTEX
Varyings CustomInterpolatorPassThroughFunc(inout Varyings output, VertexDescription input)
{
return output;
}
#define CUSTOMINTERPOLATOR_VARYPASSTHROUGH_FUNC
#endif

// Graph Pixel
struct SurfaceDescription
{
half3 BaseColor;
half3 Emission;
};

SurfaceDescription SurfaceDescriptionFunction(SurfaceDescriptionInputs IN)
{
SurfaceDescription surface = (SurfaceDescription)0;
half4 Color_9e0f8909aabe4b33b699f1d9bf4443dd = IsGammaSpace() ? half4(0, 0, 0, 0) : half4(SRGBToLinear(half3(0, 0, 0)), 0);
half4 _Property_ecf2fe5309e54977b870684c8d900a70_Out_0_Vector4 = _SkinColor;
half4 _Property_dee2f69732114c579841770d3a5f1ce9_Out_0_Vector4 = _CutsColor;
half4 _Property_2152e36dab12470385b94206ce043312_Out_0_Vector4 = _DirtColor;
half4 Color_1d5d22b78f8b4dc6b677715e3c97350d = IsGammaSpace() ? half4(0, 0, 0, 0) : half4(SRGBToLinear(half3(0, 0, 0)), 0);
UnityTexture2D _Property_29217945295d45b482fdf43563ceafe1_Out_0_Texture2D = UnityBuildTexture2DStructNoScale(_ColorMap);
half4 _SampleTexture2D_84a95f63ac39426f99979a9f9613953a_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_29217945295d45b482fdf43563ceafe1_Out_0_Texture2D.tex, _Property_29217945295d45b482fdf43563ceafe1_Out_0_Texture2D.samplerstate, _Property_29217945295d45b482fdf43563ceafe1_Out_0_Texture2D.GetTransformedUV(IN.uv0.xy) );
half _SampleTexture2D_84a95f63ac39426f99979a9f9613953a_R_4_Float = _SampleTexture2D_84a95f63ac39426f99979a9f9613953a_RGBA_0_Vector4.r;
half _SampleTexture2D_84a95f63ac39426f99979a9f9613953a_G_5_Float = _SampleTexture2D_84a95f63ac39426f99979a9f9613953a_RGBA_0_Vector4.g;
half _SampleTexture2D_84a95f63ac39426f99979a9f9613953a_B_6_Float = _SampleTexture2D_84a95f63ac39426f99979a9f9613953a_RGBA_0_Vector4.b;
half _SampleTexture2D_84a95f63ac39426f99979a9f9613953a_A_7_Float = _SampleTexture2D_84a95f63ac39426f99979a9f9613953a_RGBA_0_Vector4.a;
half4 Color_22af752f56df4b69a15a74bd003266a0 = IsGammaSpace() ? half4(1, 1, 1, 0) : half4(SRGBToLinear(half3(1, 1, 1)), 0);
UnityTexture2D _Property_c18bcc123f5545dda6a4416e976dce17_Out_0_Texture2D = UnityBuildTexture2DStructNoScale(_DarkMaskTexture);
half4 _SampleTexture2D_6ccf5a041d9a4379a08064554c835a43_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_c18bcc123f5545dda6a4416e976dce17_Out_0_Texture2D.tex, _Property_c18bcc123f5545dda6a4416e976dce17_Out_0_Texture2D.samplerstate, _Property_c18bcc123f5545dda6a4416e976dce17_Out_0_Texture2D.GetTransformedUV(IN.uv0.xy) );
half _SampleTexture2D_6ccf5a041d9a4379a08064554c835a43_R_4_Float = _SampleTexture2D_6ccf5a041d9a4379a08064554c835a43_RGBA_0_Vector4.r;
half _SampleTexture2D_6ccf5a041d9a4379a08064554c835a43_G_5_Float = _SampleTexture2D_6ccf5a041d9a4379a08064554c835a43_RGBA_0_Vector4.g;
half _SampleTexture2D_6ccf5a041d9a4379a08064554c835a43_B_6_Float = _SampleTexture2D_6ccf5a041d9a4379a08064554c835a43_RGBA_0_Vector4.b;
half _SampleTexture2D_6ccf5a041d9a4379a08064554c835a43_A_7_Float = _SampleTexture2D_6ccf5a041d9a4379a08064554c835a43_RGBA_0_Vector4.a;
half _Property_f460bfa9d43848bba59ba7460f20c9c2_Out_0_Float = _DarkAmount;
half4 _Lerp_b97b4bab53a04c6ea870c54dde30e56e_Out_3_Vector4;
Unity_Lerp_half4(Color_22af752f56df4b69a15a74bd003266a0, _SampleTexture2D_6ccf5a041d9a4379a08064554c835a43_RGBA_0_Vector4, (_Property_f460bfa9d43848bba59ba7460f20c9c2_Out_0_Float.xxxx), _Lerp_b97b4bab53a04c6ea870c54dde30e56e_Out_3_Vector4);
half4 _Lerp_5fb58a8630424f0aa299b82d3c6b7cf2_Out_3_Vector4;
Unity_Lerp_half4(Color_1d5d22b78f8b4dc6b677715e3c97350d, _SampleTexture2D_84a95f63ac39426f99979a9f9613953a_RGBA_0_Vector4, _Lerp_b97b4bab53a04c6ea870c54dde30e56e_Out_3_Vector4, _Lerp_5fb58a8630424f0aa299b82d3c6b7cf2_Out_3_Vector4);
UnityTexture2D _Property_f8239e9549eb4afdb04e0add697a9e63_Out_0_Texture2D = UnityBuildTexture2DStructNoScale(_DirtMaskTexture);
half4 _SampleTexture2D_7a735da5e71c436f82fc33c38b3cb722_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_f8239e9549eb4afdb04e0add697a9e63_Out_0_Texture2D.tex, _Property_f8239e9549eb4afdb04e0add697a9e63_Out_0_Texture2D.samplerstate, _Property_f8239e9549eb4afdb04e0add697a9e63_Out_0_Texture2D.GetTransformedUV(IN.uv0.xy) );
half _SampleTexture2D_7a735da5e71c436f82fc33c38b3cb722_R_4_Float = _SampleTexture2D_7a735da5e71c436f82fc33c38b3cb722_RGBA_0_Vector4.r;
half _SampleTexture2D_7a735da5e71c436f82fc33c38b3cb722_G_5_Float = _SampleTexture2D_7a735da5e71c436f82fc33c38b3cb722_RGBA_0_Vector4.g;
half _SampleTexture2D_7a735da5e71c436f82fc33c38b3cb722_B_6_Float = _SampleTexture2D_7a735da5e71c436f82fc33c38b3cb722_RGBA_0_Vector4.b;
half _SampleTexture2D_7a735da5e71c436f82fc33c38b3cb722_A_7_Float = _SampleTexture2D_7a735da5e71c436f82fc33c38b3cb722_RGBA_0_Vector4.a;
half _Property_ef41be1c8664497cbdfb806842079a89_Out_0_Float = _DirtAmount;
half4 _Lerp_aec65eee28314809a1410cf73b04a889_Out_3_Vector4;
Unity_Lerp_half4(Color_22af752f56df4b69a15a74bd003266a0, _SampleTexture2D_7a735da5e71c436f82fc33c38b3cb722_RGBA_0_Vector4, (_Property_ef41be1c8664497cbdfb806842079a89_Out_0_Float.xxxx), _Lerp_aec65eee28314809a1410cf73b04a889_Out_3_Vector4);
half4 _Lerp_1422a096bdfb4539b217cd9460b0b019_Out_3_Vector4;
Unity_Lerp_half4(_Property_2152e36dab12470385b94206ce043312_Out_0_Vector4, _Lerp_5fb58a8630424f0aa299b82d3c6b7cf2_Out_3_Vector4, _Lerp_aec65eee28314809a1410cf73b04a889_Out_3_Vector4, _Lerp_1422a096bdfb4539b217cd9460b0b019_Out_3_Vector4);
UnityTexture2D _Property_cad87ecf436a44458da862f7575dfeec_Out_0_Texture2D = UnityBuildTexture2DStructNoScale(_CutsMaskTexture);
half4 _SampleTexture2D_1937dc0552af4944a98689fd9d6fa9cc_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_cad87ecf436a44458da862f7575dfeec_Out_0_Texture2D.tex, _Property_cad87ecf436a44458da862f7575dfeec_Out_0_Texture2D.samplerstate, _Property_cad87ecf436a44458da862f7575dfeec_Out_0_Texture2D.GetTransformedUV(IN.uv0.xy) );
half _SampleTexture2D_1937dc0552af4944a98689fd9d6fa9cc_R_4_Float = _SampleTexture2D_1937dc0552af4944a98689fd9d6fa9cc_RGBA_0_Vector4.r;
half _SampleTexture2D_1937dc0552af4944a98689fd9d6fa9cc_G_5_Float = _SampleTexture2D_1937dc0552af4944a98689fd9d6fa9cc_RGBA_0_Vector4.g;
half _SampleTexture2D_1937dc0552af4944a98689fd9d6fa9cc_B_6_Float = _SampleTexture2D_1937dc0552af4944a98689fd9d6fa9cc_RGBA_0_Vector4.b;
half _SampleTexture2D_1937dc0552af4944a98689fd9d6fa9cc_A_7_Float = _SampleTexture2D_1937dc0552af4944a98689fd9d6fa9cc_RGBA_0_Vector4.a;
half _Property_092064d1a1804dcb88349a0dfa930d34_Out_0_Float = _CutsAmount;
half4 _Lerp_587eb00b83614171a84a065eb4475d69_Out_3_Vector4;
Unity_Lerp_half4(Color_22af752f56df4b69a15a74bd003266a0, _SampleTexture2D_1937dc0552af4944a98689fd9d6fa9cc_RGBA_0_Vector4, (_Property_092064d1a1804dcb88349a0dfa930d34_Out_0_Float.xxxx), _Lerp_587eb00b83614171a84a065eb4475d69_Out_3_Vector4);
half4 _Lerp_480d292819cd497f84da826a5234e813_Out_3_Vector4;
Unity_Lerp_half4(_Property_dee2f69732114c579841770d3a5f1ce9_Out_0_Vector4, _Lerp_1422a096bdfb4539b217cd9460b0b019_Out_3_Vector4, _Lerp_587eb00b83614171a84a065eb4475d69_Out_3_Vector4, _Lerp_480d292819cd497f84da826a5234e813_Out_3_Vector4);
UnityTexture2D _Property_b2cfc554a3654888b085ea4dadd11e99_Out_0_Texture2D = UnityBuildTexture2DStructNoScale(_SkinMaskTexture);
half4 _SampleTexture2D_864cfb091b7d4331bd7218fdeaf8d858_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_b2cfc554a3654888b085ea4dadd11e99_Out_0_Texture2D.tex, _Property_b2cfc554a3654888b085ea4dadd11e99_Out_0_Texture2D.samplerstate, _Property_b2cfc554a3654888b085ea4dadd11e99_Out_0_Texture2D.GetTransformedUV(IN.uv0.xy) );
half _SampleTexture2D_864cfb091b7d4331bd7218fdeaf8d858_R_4_Float = _SampleTexture2D_864cfb091b7d4331bd7218fdeaf8d858_RGBA_0_Vector4.r;
half _SampleTexture2D_864cfb091b7d4331bd7218fdeaf8d858_G_5_Float = _SampleTexture2D_864cfb091b7d4331bd7218fdeaf8d858_RGBA_0_Vector4.g;
half _SampleTexture2D_864cfb091b7d4331bd7218fdeaf8d858_B_6_Float = _SampleTexture2D_864cfb091b7d4331bd7218fdeaf8d858_RGBA_0_Vector4.b;
half _SampleTexture2D_864cfb091b7d4331bd7218fdeaf8d858_A_7_Float = _SampleTexture2D_864cfb091b7d4331bd7218fdeaf8d858_RGBA_0_Vector4.a;
half _Property_495de44ef11d40cb91ca34953cc899b1_Out_0_Float = _SkinColorAmount;
half4 _Lerp_82953a0631914e78ba370c51d143d891_Out_3_Vector4;
Unity_Lerp_half4(Color_22af752f56df4b69a15a74bd003266a0, _SampleTexture2D_864cfb091b7d4331bd7218fdeaf8d858_RGBA_0_Vector4, (_Property_495de44ef11d40cb91ca34953cc899b1_Out_0_Float.xxxx), _Lerp_82953a0631914e78ba370c51d143d891_Out_3_Vector4);
half4 _Lerp_56dd8066e70d4c85a8d72c30ef200bb7_Out_3_Vector4;
Unity_Lerp_half4(_Property_ecf2fe5309e54977b870684c8d900a70_Out_0_Vector4, _Lerp_480d292819cd497f84da826a5234e813_Out_3_Vector4, _Lerp_82953a0631914e78ba370c51d143d891_Out_3_Vector4, _Lerp_56dd8066e70d4c85a8d72c30ef200bb7_Out_3_Vector4);
half _Property_4208ddc25284429fba969ec5b8970ca4_Out_0_Float = _EyelinerAmount;
half _OneMinus_dfa88fc204374b35898331eae5eba835_Out_1_Float;
Unity_OneMinus_half(_Property_4208ddc25284429fba969ec5b8970ca4_Out_0_Float, _OneMinus_dfa88fc204374b35898331eae5eba835_Out_1_Float);
half _Clamp_a3567345de7b4ba6850279acb3e1bbda_Out_3_Float;
Unity_Clamp_half(_OneMinus_dfa88fc204374b35898331eae5eba835_Out_1_Float, half(0), half(0.5), _Clamp_a3567345de7b4ba6850279acb3e1bbda_Out_3_Float);
UnityTexture2D _Property_0dfa773f4d6f47bc85db4d5cfb627c23_Out_0_Texture2D = UnityBuildTexture2DStructNoScale(_EyeEdgeMaskTexture);
half4 _SampleTexture2D_1e5e254c29e84e2ebe790f4de0afeab7_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_0dfa773f4d6f47bc85db4d5cfb627c23_Out_0_Texture2D.tex, _Property_0dfa773f4d6f47bc85db4d5cfb627c23_Out_0_Texture2D.samplerstate, _Property_0dfa773f4d6f47bc85db4d5cfb627c23_Out_0_Texture2D.GetTransformedUV(IN.uv0.xy) );
half _SampleTexture2D_1e5e254c29e84e2ebe790f4de0afeab7_R_4_Float = _SampleTexture2D_1e5e254c29e84e2ebe790f4de0afeab7_RGBA_0_Vector4.r;
half _SampleTexture2D_1e5e254c29e84e2ebe790f4de0afeab7_G_5_Float = _SampleTexture2D_1e5e254c29e84e2ebe790f4de0afeab7_RGBA_0_Vector4.g;
half _SampleTexture2D_1e5e254c29e84e2ebe790f4de0afeab7_B_6_Float = _SampleTexture2D_1e5e254c29e84e2ebe790f4de0afeab7_RGBA_0_Vector4.b;
half _SampleTexture2D_1e5e254c29e84e2ebe790f4de0afeab7_A_7_Float = _SampleTexture2D_1e5e254c29e84e2ebe790f4de0afeab7_RGBA_0_Vector4.a;
half4 _Lerp_3fc533c198e74a1ebb2c7f7bad6d9762_Out_3_Vector4;
Unity_Lerp_half4(Color_22af752f56df4b69a15a74bd003266a0, (_Clamp_a3567345de7b4ba6850279acb3e1bbda_Out_3_Float.xxxx), _SampleTexture2D_1e5e254c29e84e2ebe790f4de0afeab7_RGBA_0_Vector4, _Lerp_3fc533c198e74a1ebb2c7f7bad6d9762_Out_3_Vector4);
half4 _Lerp_7b945e4c623b447f84028beaef139c78_Out_3_Vector4;
Unity_Lerp_half4(Color_9e0f8909aabe4b33b699f1d9bf4443dd, _Lerp_56dd8066e70d4c85a8d72c30ef200bb7_Out_3_Vector4, _Lerp_3fc533c198e74a1ebb2c7f7bad6d9762_Out_3_Vector4, _Lerp_7b945e4c623b447f84028beaef139c78_Out_3_Vector4);
surface.BaseColor = (_Lerp_7b945e4c623b447f84028beaef139c78_Out_3_Vector4.xyz);
surface.Emission = half3(0, 0, 0);
return surface;
}

// --------------------------------------------------
// Build Graph Inputs
#ifdef HAVE_VFX_MODIFICATION
#define VFX_SRP_ATTRIBUTES Attributes
#define VFX_SRP_VARYINGS Varyings
#define VFX_SRP_SURFACE_INPUTS SurfaceDescriptionInputs
#endif
VertexDescriptionInputs BuildVertexDescriptionInputs(Attributes input)
{
    VertexDescriptionInputs output;
    ZERO_INITIALIZE(VertexDescriptionInputs, output);

    output.ObjectSpaceNormal =                          input.normalOS;
    output.ObjectSpaceTangent =                         input.tangentOS.xyz;
    output.ObjectSpacePosition =                        input.positionOS;
#if UNITY_ANY_INSTANCING_ENABLED
#else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.
#endif

    return output;
}
SurfaceDescriptionInputs BuildSurfaceDescriptionInputs(Varyings input)
{
    SurfaceDescriptionInputs output;
    ZERO_INITIALIZE(SurfaceDescriptionInputs, output);

#ifdef HAVE_VFX_MODIFICATION
#if VFX_USE_GRAPH_VALUES
    uint instanceActiveIndex = asuint(UNITY_ACCESS_INSTANCED_PROP(PerInstance, _InstanceActiveIndex));
    /* WARNING: $splice Could not find named fragment 'VFXLoadGraphValues' */
#endif
    /* WARNING: $splice Could not find named fragment 'VFXSetFragInputs' */

#endif

    






    #if UNITY_UV_STARTS_AT_TOP
    #else
    #endif


    output.uv0 = input.texCoord0;
#if UNITY_ANY_INSTANCING_ENABLED
#else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
#define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN output.FaceSign =                    IS_FRONT_VFACE(input.cullFace, true, false);
#else
#define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
#endif
#undef BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN

        return output;
}

// --------------------------------------------------
// Main

#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/Varyings.hlsl"
#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/LightingMetaPass.hlsl"

// --------------------------------------------------
// Visual Effect Vertex Invocations
#ifdef HAVE_VFX_MODIFICATION
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/VisualEffectVertex.hlsl"
#endif

ENDHLSL
}
Pass
{
    Name "SceneSelectionPass"
    Tags
    {
        "LightMode" = "SceneSelectionPass"
    }

// Render State
Cull Off

// Debug
// <None>

// --------------------------------------------------
// Pass

HLSLPROGRAM

// Pragmas
#pragma target 2.0
#pragma vertex vert
#pragma fragment frag

// Keywords
// PassKeywords: <None>
// GraphKeywords: <None>

// Defines

#define _NORMALMAP 1
#define _NORMAL_DROPOFF_TS 1
#define ATTRIBUTES_NEED_NORMAL
#define ATTRIBUTES_NEED_TANGENT
#define FEATURES_GRAPH_VERTEX_NORMAL_OUTPUT
#define FEATURES_GRAPH_VERTEX_TANGENT_OUTPUT
#define FEATURES_GRAPH_VERTEX
/* WARNING: $splice Could not find named fragment 'PassInstancing' */
#define SHADERPASS SHADERPASS_DEPTHONLY
#define SCENESELECTIONPASS 1
#define ALPHA_CLIP_THRESHOLD 1


// custom interpolator pre-include
/* WARNING: $splice Could not find named fragment 'sgci_CustomInterpolatorPreInclude' */

// Includes
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DebugMipmapStreamingMacros.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"

// --------------------------------------------------
// Structs and Packing

// custom interpolators pre packing
/* WARNING: $splice Could not find named fragment 'CustomInterpolatorPrePacking' */

struct Attributes
{
 float3 positionOS : POSITION;
 float3 normalOS : NORMAL;
 float4 tangentOS : TANGENT;
#if UNITY_ANY_INSTANCING_ENABLED || defined(ATTRIBUTES_NEED_INSTANCEID)
 uint instanceID : INSTANCEID_SEMANTIC;
#endif
};
struct Varyings
{
 float4 positionCS : SV_POSITION;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
 uint instanceID : CUSTOM_INSTANCE_ID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
 uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
 uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
 FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
#endif
};
struct SurfaceDescriptionInputs
{
};
struct VertexDescriptionInputs
{
 float3 ObjectSpaceNormal;
 float3 ObjectSpaceTangent;
 float3 ObjectSpacePosition;
};
struct PackedVaryings
{
 float4 positionCS : SV_POSITION;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
 uint instanceID : CUSTOM_INSTANCE_ID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
 uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
 uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
 FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
#endif
};

PackedVaryings PackVaryings (Varyings input)
{
PackedVaryings output;
ZERO_INITIALIZE(PackedVaryings, output);
output.positionCS = input.positionCS;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
output.instanceID = input.instanceID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
output.cullFace = input.cullFace;
#endif
return output;
}

Varyings UnpackVaryings (PackedVaryings input)
{
Varyings output;
output.positionCS = input.positionCS;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
output.instanceID = input.instanceID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
output.cullFace = input.cullFace;
#endif
return output;
}


// --------------------------------------------------
// Graph

// Graph Properties
CBUFFER_START(UnityPerMaterial)
float4 _ColorMap_TexelSize;
float4 _SkinMaskTexture_TexelSize;
float4 _DarkMaskTexture_TexelSize;
half4 _DirtColor;
half4 _SkinColor;
half _DarkAmount;
float4 _DirtMaskTexture_TexelSize;
half _DirtAmount;
float4 _EyeEdgeMaskTexture_TexelSize;
half _EyelinerAmount;
half _SkinColorAmount;
UNITY_TEXTURE_STREAMING_DEBUG_VARS;
CBUFFER_END


// Object and Global properties
SAMPLER(SamplerState_Linear_Repeat);
TEXTURE2D(_ColorMap);
SAMPLER(sampler_ColorMap);
TEXTURE2D(_SkinMaskTexture);
SAMPLER(sampler_SkinMaskTexture);
TEXTURE2D(_DarkMaskTexture);
SAMPLER(sampler_DarkMaskTexture);
TEXTURE2D(_CutsMaskTexture);
SAMPLER(sampler_CutsMaskTexture);
float4 _CutsMaskTexture_TexelSize;
TEXTURE2D(_MetallicMap);
SAMPLER(sampler_MetallicMap);
float4 _MetallicMap_TexelSize;
TEXTURE2D(_SmoothnessMap);
SAMPLER(sampler_SmoothnessMap);
float4 _SmoothnessMap_TexelSize;
TEXTURE2D(_EmissionMap);
SAMPLER(sampler_EmissionMap);
float4 _EmissionMap_TexelSize;
TEXTURE2D(_DirtMaskTexture);
SAMPLER(sampler_DirtMaskTexture);
half _CutsAmount;
half4 _CutsColor;
TEXTURE2D(_EyeEdgeMaskTexture);
SAMPLER(sampler_EyeEdgeMaskTexture);

// Graph Includes
// GraphIncludes: <None>

// -- Property used by ScenePickingPass
#ifdef SCENEPICKINGPASS
float4 _SelectionID;
#endif

// -- Properties used by SceneSelectionPass
#ifdef SCENESELECTIONPASS
int _ObjectId;
int _PassValue;
#endif

// Graph Functions
// GraphFunctions: <None>

// Custom interpolators pre vertex
/* WARNING: $splice Could not find named fragment 'CustomInterpolatorPreVertex' */

// Graph Vertex
struct VertexDescription
{
half3 Position;
half3 Normal;
half3 Tangent;
};

VertexDescription VertexDescriptionFunction(VertexDescriptionInputs IN)
{
VertexDescription description = (VertexDescription)0;
description.Position = IN.ObjectSpacePosition;
description.Normal = IN.ObjectSpaceNormal;
description.Tangent = IN.ObjectSpaceTangent;
return description;
}

// Custom interpolators, pre surface
#ifdef FEATURES_GRAPH_VERTEX
Varyings CustomInterpolatorPassThroughFunc(inout Varyings output, VertexDescription input)
{
return output;
}
#define CUSTOMINTERPOLATOR_VARYPASSTHROUGH_FUNC
#endif

// Graph Pixel
struct SurfaceDescription
{
};

SurfaceDescription SurfaceDescriptionFunction(SurfaceDescriptionInputs IN)
{
SurfaceDescription surface = (SurfaceDescription)0;
return surface;
}

// --------------------------------------------------
// Build Graph Inputs
#ifdef HAVE_VFX_MODIFICATION
#define VFX_SRP_ATTRIBUTES Attributes
#define VFX_SRP_VARYINGS Varyings
#define VFX_SRP_SURFACE_INPUTS SurfaceDescriptionInputs
#endif
VertexDescriptionInputs BuildVertexDescriptionInputs(Attributes input)
{
    VertexDescriptionInputs output;
    ZERO_INITIALIZE(VertexDescriptionInputs, output);

    output.ObjectSpaceNormal =                          input.normalOS;
    output.ObjectSpaceTangent =                         input.tangentOS.xyz;
    output.ObjectSpacePosition =                        input.positionOS;
#if UNITY_ANY_INSTANCING_ENABLED
#else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.
#endif

    return output;
}
SurfaceDescriptionInputs BuildSurfaceDescriptionInputs(Varyings input)
{
    SurfaceDescriptionInputs output;
    ZERO_INITIALIZE(SurfaceDescriptionInputs, output);

#ifdef HAVE_VFX_MODIFICATION
#if VFX_USE_GRAPH_VALUES
    uint instanceActiveIndex = asuint(UNITY_ACCESS_INSTANCED_PROP(PerInstance, _InstanceActiveIndex));
    /* WARNING: $splice Could not find named fragment 'VFXLoadGraphValues' */
#endif
    /* WARNING: $splice Could not find named fragment 'VFXSetFragInputs' */

#endif

    






    #if UNITY_UV_STARTS_AT_TOP
    #else
    #endif


#if UNITY_ANY_INSTANCING_ENABLED
#else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
#define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN output.FaceSign =                    IS_FRONT_VFACE(input.cullFace, true, false);
#else
#define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
#endif
#undef BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN

        return output;
}

// --------------------------------------------------
// Main

#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/Varyings.hlsl"
#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/SelectionPickingPass.hlsl"

// --------------------------------------------------
// Visual Effect Vertex Invocations
#ifdef HAVE_VFX_MODIFICATION
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/VisualEffectVertex.hlsl"
#endif

ENDHLSL
}
Pass
{
    Name "ScenePickingPass"
    Tags
    {
        "LightMode" = "Picking"
    }

// Render State
Cull Back

// Debug
// <None>

// --------------------------------------------------
// Pass

HLSLPROGRAM

// Pragmas
#pragma target 2.0
#pragma vertex vert
#pragma fragment frag

// Keywords
// PassKeywords: <None>
// GraphKeywords: <None>

// Defines

#define _NORMALMAP 1
#define _NORMAL_DROPOFF_TS 1
#define ATTRIBUTES_NEED_NORMAL
#define ATTRIBUTES_NEED_TANGENT
#define ATTRIBUTES_NEED_TEXCOORD0
#define FEATURES_GRAPH_VERTEX_NORMAL_OUTPUT
#define FEATURES_GRAPH_VERTEX_TANGENT_OUTPUT
#define VARYINGS_NEED_TEXCOORD0
#define FEATURES_GRAPH_VERTEX
/* WARNING: $splice Could not find named fragment 'PassInstancing' */
#define SHADERPASS SHADERPASS_DEPTHONLY
#define SCENEPICKINGPASS 1
#define ALPHA_CLIP_THRESHOLD 1


// custom interpolator pre-include
/* WARNING: $splice Could not find named fragment 'sgci_CustomInterpolatorPreInclude' */

// Includes
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DebugMipmapStreamingMacros.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"

// --------------------------------------------------
// Structs and Packing

// custom interpolators pre packing
/* WARNING: $splice Could not find named fragment 'CustomInterpolatorPrePacking' */

struct Attributes
{
 float3 positionOS : POSITION;
 float3 normalOS : NORMAL;
 float4 tangentOS : TANGENT;
 float4 uv0 : TEXCOORD0;
#if UNITY_ANY_INSTANCING_ENABLED || defined(ATTRIBUTES_NEED_INSTANCEID)
 uint instanceID : INSTANCEID_SEMANTIC;
#endif
};
struct Varyings
{
 float4 positionCS : SV_POSITION;
 float4 texCoord0;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
 uint instanceID : CUSTOM_INSTANCE_ID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
 uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
 uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
 FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
#endif
};
struct SurfaceDescriptionInputs
{
 float4 uv0;
};
struct VertexDescriptionInputs
{
 float3 ObjectSpaceNormal;
 float3 ObjectSpaceTangent;
 float3 ObjectSpacePosition;
};
struct PackedVaryings
{
 float4 positionCS : SV_POSITION;
 float4 texCoord0 : INTERP0;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
 uint instanceID : CUSTOM_INSTANCE_ID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
 uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
 uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
 FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
#endif
};

PackedVaryings PackVaryings (Varyings input)
{
PackedVaryings output;
ZERO_INITIALIZE(PackedVaryings, output);
output.positionCS = input.positionCS;
output.texCoord0.xyzw = input.texCoord0;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
output.instanceID = input.instanceID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
output.cullFace = input.cullFace;
#endif
return output;
}

Varyings UnpackVaryings (PackedVaryings input)
{
Varyings output;
output.positionCS = input.positionCS;
output.texCoord0 = input.texCoord0.xyzw;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
output.instanceID = input.instanceID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
output.cullFace = input.cullFace;
#endif
return output;
}


// --------------------------------------------------
// Graph

// Graph Properties
CBUFFER_START(UnityPerMaterial)
float4 _ColorMap_TexelSize;
float4 _SkinMaskTexture_TexelSize;
float4 _DarkMaskTexture_TexelSize;
half4 _DirtColor;
half4 _SkinColor;
half _DarkAmount;
float4 _DirtMaskTexture_TexelSize;
half _DirtAmount;
float4 _EyeEdgeMaskTexture_TexelSize;
half _EyelinerAmount;
half _SkinColorAmount;
UNITY_TEXTURE_STREAMING_DEBUG_VARS;
CBUFFER_END


// Object and Global properties
SAMPLER(SamplerState_Linear_Repeat);
TEXTURE2D(_ColorMap);
SAMPLER(sampler_ColorMap);
TEXTURE2D(_SkinMaskTexture);
SAMPLER(sampler_SkinMaskTexture);
TEXTURE2D(_DarkMaskTexture);
SAMPLER(sampler_DarkMaskTexture);
TEXTURE2D(_CutsMaskTexture);
SAMPLER(sampler_CutsMaskTexture);
float4 _CutsMaskTexture_TexelSize;
TEXTURE2D(_MetallicMap);
SAMPLER(sampler_MetallicMap);
float4 _MetallicMap_TexelSize;
TEXTURE2D(_SmoothnessMap);
SAMPLER(sampler_SmoothnessMap);
float4 _SmoothnessMap_TexelSize;
TEXTURE2D(_EmissionMap);
SAMPLER(sampler_EmissionMap);
float4 _EmissionMap_TexelSize;
TEXTURE2D(_DirtMaskTexture);
SAMPLER(sampler_DirtMaskTexture);
half _CutsAmount;
half4 _CutsColor;
TEXTURE2D(_EyeEdgeMaskTexture);
SAMPLER(sampler_EyeEdgeMaskTexture);

// Graph Includes
// GraphIncludes: <None>

// -- Property used by ScenePickingPass
#ifdef SCENEPICKINGPASS
float4 _SelectionID;
#endif

// -- Properties used by SceneSelectionPass
#ifdef SCENESELECTIONPASS
int _ObjectId;
int _PassValue;
#endif

// Graph Functions

void Unity_Lerp_half4(half4 A, half4 B, half4 T, out half4 Out)
{
    Out = lerp(A, B, T);
}

void Unity_OneMinus_half(half In, out half Out)
{
    Out = 1 - In;
}

void Unity_Clamp_half(half In, half Min, half Max, out half Out)
{
    Out = clamp(In, Min, Max);
}

// Custom interpolators pre vertex
/* WARNING: $splice Could not find named fragment 'CustomInterpolatorPreVertex' */

// Graph Vertex
struct VertexDescription
{
half3 Position;
half3 Normal;
half3 Tangent;
};

VertexDescription VertexDescriptionFunction(VertexDescriptionInputs IN)
{
VertexDescription description = (VertexDescription)0;
description.Position = IN.ObjectSpacePosition;
description.Normal = IN.ObjectSpaceNormal;
description.Tangent = IN.ObjectSpaceTangent;
return description;
}

// Custom interpolators, pre surface
#ifdef FEATURES_GRAPH_VERTEX
Varyings CustomInterpolatorPassThroughFunc(inout Varyings output, VertexDescription input)
{
return output;
}
#define CUSTOMINTERPOLATOR_VARYPASSTHROUGH_FUNC
#endif

// Graph Pixel
struct SurfaceDescription
{
half3 BaseColor;
};

SurfaceDescription SurfaceDescriptionFunction(SurfaceDescriptionInputs IN)
{
SurfaceDescription surface = (SurfaceDescription)0;
half4 Color_9e0f8909aabe4b33b699f1d9bf4443dd = IsGammaSpace() ? half4(0, 0, 0, 0) : half4(SRGBToLinear(half3(0, 0, 0)), 0);
half4 _Property_ecf2fe5309e54977b870684c8d900a70_Out_0_Vector4 = _SkinColor;
half4 _Property_dee2f69732114c579841770d3a5f1ce9_Out_0_Vector4 = _CutsColor;
half4 _Property_2152e36dab12470385b94206ce043312_Out_0_Vector4 = _DirtColor;
half4 Color_1d5d22b78f8b4dc6b677715e3c97350d = IsGammaSpace() ? half4(0, 0, 0, 0) : half4(SRGBToLinear(half3(0, 0, 0)), 0);
UnityTexture2D _Property_29217945295d45b482fdf43563ceafe1_Out_0_Texture2D = UnityBuildTexture2DStructNoScale(_ColorMap);
half4 _SampleTexture2D_84a95f63ac39426f99979a9f9613953a_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_29217945295d45b482fdf43563ceafe1_Out_0_Texture2D.tex, _Property_29217945295d45b482fdf43563ceafe1_Out_0_Texture2D.samplerstate, _Property_29217945295d45b482fdf43563ceafe1_Out_0_Texture2D.GetTransformedUV(IN.uv0.xy) );
half _SampleTexture2D_84a95f63ac39426f99979a9f9613953a_R_4_Float = _SampleTexture2D_84a95f63ac39426f99979a9f9613953a_RGBA_0_Vector4.r;
half _SampleTexture2D_84a95f63ac39426f99979a9f9613953a_G_5_Float = _SampleTexture2D_84a95f63ac39426f99979a9f9613953a_RGBA_0_Vector4.g;
half _SampleTexture2D_84a95f63ac39426f99979a9f9613953a_B_6_Float = _SampleTexture2D_84a95f63ac39426f99979a9f9613953a_RGBA_0_Vector4.b;
half _SampleTexture2D_84a95f63ac39426f99979a9f9613953a_A_7_Float = _SampleTexture2D_84a95f63ac39426f99979a9f9613953a_RGBA_0_Vector4.a;
half4 Color_22af752f56df4b69a15a74bd003266a0 = IsGammaSpace() ? half4(1, 1, 1, 0) : half4(SRGBToLinear(half3(1, 1, 1)), 0);
UnityTexture2D _Property_c18bcc123f5545dda6a4416e976dce17_Out_0_Texture2D = UnityBuildTexture2DStructNoScale(_DarkMaskTexture);
half4 _SampleTexture2D_6ccf5a041d9a4379a08064554c835a43_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_c18bcc123f5545dda6a4416e976dce17_Out_0_Texture2D.tex, _Property_c18bcc123f5545dda6a4416e976dce17_Out_0_Texture2D.samplerstate, _Property_c18bcc123f5545dda6a4416e976dce17_Out_0_Texture2D.GetTransformedUV(IN.uv0.xy) );
half _SampleTexture2D_6ccf5a041d9a4379a08064554c835a43_R_4_Float = _SampleTexture2D_6ccf5a041d9a4379a08064554c835a43_RGBA_0_Vector4.r;
half _SampleTexture2D_6ccf5a041d9a4379a08064554c835a43_G_5_Float = _SampleTexture2D_6ccf5a041d9a4379a08064554c835a43_RGBA_0_Vector4.g;
half _SampleTexture2D_6ccf5a041d9a4379a08064554c835a43_B_6_Float = _SampleTexture2D_6ccf5a041d9a4379a08064554c835a43_RGBA_0_Vector4.b;
half _SampleTexture2D_6ccf5a041d9a4379a08064554c835a43_A_7_Float = _SampleTexture2D_6ccf5a041d9a4379a08064554c835a43_RGBA_0_Vector4.a;
half _Property_f460bfa9d43848bba59ba7460f20c9c2_Out_0_Float = _DarkAmount;
half4 _Lerp_b97b4bab53a04c6ea870c54dde30e56e_Out_3_Vector4;
Unity_Lerp_half4(Color_22af752f56df4b69a15a74bd003266a0, _SampleTexture2D_6ccf5a041d9a4379a08064554c835a43_RGBA_0_Vector4, (_Property_f460bfa9d43848bba59ba7460f20c9c2_Out_0_Float.xxxx), _Lerp_b97b4bab53a04c6ea870c54dde30e56e_Out_3_Vector4);
half4 _Lerp_5fb58a8630424f0aa299b82d3c6b7cf2_Out_3_Vector4;
Unity_Lerp_half4(Color_1d5d22b78f8b4dc6b677715e3c97350d, _SampleTexture2D_84a95f63ac39426f99979a9f9613953a_RGBA_0_Vector4, _Lerp_b97b4bab53a04c6ea870c54dde30e56e_Out_3_Vector4, _Lerp_5fb58a8630424f0aa299b82d3c6b7cf2_Out_3_Vector4);
UnityTexture2D _Property_f8239e9549eb4afdb04e0add697a9e63_Out_0_Texture2D = UnityBuildTexture2DStructNoScale(_DirtMaskTexture);
half4 _SampleTexture2D_7a735da5e71c436f82fc33c38b3cb722_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_f8239e9549eb4afdb04e0add697a9e63_Out_0_Texture2D.tex, _Property_f8239e9549eb4afdb04e0add697a9e63_Out_0_Texture2D.samplerstate, _Property_f8239e9549eb4afdb04e0add697a9e63_Out_0_Texture2D.GetTransformedUV(IN.uv0.xy) );
half _SampleTexture2D_7a735da5e71c436f82fc33c38b3cb722_R_4_Float = _SampleTexture2D_7a735da5e71c436f82fc33c38b3cb722_RGBA_0_Vector4.r;
half _SampleTexture2D_7a735da5e71c436f82fc33c38b3cb722_G_5_Float = _SampleTexture2D_7a735da5e71c436f82fc33c38b3cb722_RGBA_0_Vector4.g;
half _SampleTexture2D_7a735da5e71c436f82fc33c38b3cb722_B_6_Float = _SampleTexture2D_7a735da5e71c436f82fc33c38b3cb722_RGBA_0_Vector4.b;
half _SampleTexture2D_7a735da5e71c436f82fc33c38b3cb722_A_7_Float = _SampleTexture2D_7a735da5e71c436f82fc33c38b3cb722_RGBA_0_Vector4.a;
half _Property_ef41be1c8664497cbdfb806842079a89_Out_0_Float = _DirtAmount;
half4 _Lerp_aec65eee28314809a1410cf73b04a889_Out_3_Vector4;
Unity_Lerp_half4(Color_22af752f56df4b69a15a74bd003266a0, _SampleTexture2D_7a735da5e71c436f82fc33c38b3cb722_RGBA_0_Vector4, (_Property_ef41be1c8664497cbdfb806842079a89_Out_0_Float.xxxx), _Lerp_aec65eee28314809a1410cf73b04a889_Out_3_Vector4);
half4 _Lerp_1422a096bdfb4539b217cd9460b0b019_Out_3_Vector4;
Unity_Lerp_half4(_Property_2152e36dab12470385b94206ce043312_Out_0_Vector4, _Lerp_5fb58a8630424f0aa299b82d3c6b7cf2_Out_3_Vector4, _Lerp_aec65eee28314809a1410cf73b04a889_Out_3_Vector4, _Lerp_1422a096bdfb4539b217cd9460b0b019_Out_3_Vector4);
UnityTexture2D _Property_cad87ecf436a44458da862f7575dfeec_Out_0_Texture2D = UnityBuildTexture2DStructNoScale(_CutsMaskTexture);
half4 _SampleTexture2D_1937dc0552af4944a98689fd9d6fa9cc_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_cad87ecf436a44458da862f7575dfeec_Out_0_Texture2D.tex, _Property_cad87ecf436a44458da862f7575dfeec_Out_0_Texture2D.samplerstate, _Property_cad87ecf436a44458da862f7575dfeec_Out_0_Texture2D.GetTransformedUV(IN.uv0.xy) );
half _SampleTexture2D_1937dc0552af4944a98689fd9d6fa9cc_R_4_Float = _SampleTexture2D_1937dc0552af4944a98689fd9d6fa9cc_RGBA_0_Vector4.r;
half _SampleTexture2D_1937dc0552af4944a98689fd9d6fa9cc_G_5_Float = _SampleTexture2D_1937dc0552af4944a98689fd9d6fa9cc_RGBA_0_Vector4.g;
half _SampleTexture2D_1937dc0552af4944a98689fd9d6fa9cc_B_6_Float = _SampleTexture2D_1937dc0552af4944a98689fd9d6fa9cc_RGBA_0_Vector4.b;
half _SampleTexture2D_1937dc0552af4944a98689fd9d6fa9cc_A_7_Float = _SampleTexture2D_1937dc0552af4944a98689fd9d6fa9cc_RGBA_0_Vector4.a;
half _Property_092064d1a1804dcb88349a0dfa930d34_Out_0_Float = _CutsAmount;
half4 _Lerp_587eb00b83614171a84a065eb4475d69_Out_3_Vector4;
Unity_Lerp_half4(Color_22af752f56df4b69a15a74bd003266a0, _SampleTexture2D_1937dc0552af4944a98689fd9d6fa9cc_RGBA_0_Vector4, (_Property_092064d1a1804dcb88349a0dfa930d34_Out_0_Float.xxxx), _Lerp_587eb00b83614171a84a065eb4475d69_Out_3_Vector4);
half4 _Lerp_480d292819cd497f84da826a5234e813_Out_3_Vector4;
Unity_Lerp_half4(_Property_dee2f69732114c579841770d3a5f1ce9_Out_0_Vector4, _Lerp_1422a096bdfb4539b217cd9460b0b019_Out_3_Vector4, _Lerp_587eb00b83614171a84a065eb4475d69_Out_3_Vector4, _Lerp_480d292819cd497f84da826a5234e813_Out_3_Vector4);
UnityTexture2D _Property_b2cfc554a3654888b085ea4dadd11e99_Out_0_Texture2D = UnityBuildTexture2DStructNoScale(_SkinMaskTexture);
half4 _SampleTexture2D_864cfb091b7d4331bd7218fdeaf8d858_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_b2cfc554a3654888b085ea4dadd11e99_Out_0_Texture2D.tex, _Property_b2cfc554a3654888b085ea4dadd11e99_Out_0_Texture2D.samplerstate, _Property_b2cfc554a3654888b085ea4dadd11e99_Out_0_Texture2D.GetTransformedUV(IN.uv0.xy) );
half _SampleTexture2D_864cfb091b7d4331bd7218fdeaf8d858_R_4_Float = _SampleTexture2D_864cfb091b7d4331bd7218fdeaf8d858_RGBA_0_Vector4.r;
half _SampleTexture2D_864cfb091b7d4331bd7218fdeaf8d858_G_5_Float = _SampleTexture2D_864cfb091b7d4331bd7218fdeaf8d858_RGBA_0_Vector4.g;
half _SampleTexture2D_864cfb091b7d4331bd7218fdeaf8d858_B_6_Float = _SampleTexture2D_864cfb091b7d4331bd7218fdeaf8d858_RGBA_0_Vector4.b;
half _SampleTexture2D_864cfb091b7d4331bd7218fdeaf8d858_A_7_Float = _SampleTexture2D_864cfb091b7d4331bd7218fdeaf8d858_RGBA_0_Vector4.a;
half _Property_495de44ef11d40cb91ca34953cc899b1_Out_0_Float = _SkinColorAmount;
half4 _Lerp_82953a0631914e78ba370c51d143d891_Out_3_Vector4;
Unity_Lerp_half4(Color_22af752f56df4b69a15a74bd003266a0, _SampleTexture2D_864cfb091b7d4331bd7218fdeaf8d858_RGBA_0_Vector4, (_Property_495de44ef11d40cb91ca34953cc899b1_Out_0_Float.xxxx), _Lerp_82953a0631914e78ba370c51d143d891_Out_3_Vector4);
half4 _Lerp_56dd8066e70d4c85a8d72c30ef200bb7_Out_3_Vector4;
Unity_Lerp_half4(_Property_ecf2fe5309e54977b870684c8d900a70_Out_0_Vector4, _Lerp_480d292819cd497f84da826a5234e813_Out_3_Vector4, _Lerp_82953a0631914e78ba370c51d143d891_Out_3_Vector4, _Lerp_56dd8066e70d4c85a8d72c30ef200bb7_Out_3_Vector4);
half _Property_4208ddc25284429fba969ec5b8970ca4_Out_0_Float = _EyelinerAmount;
half _OneMinus_dfa88fc204374b35898331eae5eba835_Out_1_Float;
Unity_OneMinus_half(_Property_4208ddc25284429fba969ec5b8970ca4_Out_0_Float, _OneMinus_dfa88fc204374b35898331eae5eba835_Out_1_Float);
half _Clamp_a3567345de7b4ba6850279acb3e1bbda_Out_3_Float;
Unity_Clamp_half(_OneMinus_dfa88fc204374b35898331eae5eba835_Out_1_Float, half(0), half(0.5), _Clamp_a3567345de7b4ba6850279acb3e1bbda_Out_3_Float);
UnityTexture2D _Property_0dfa773f4d6f47bc85db4d5cfb627c23_Out_0_Texture2D = UnityBuildTexture2DStructNoScale(_EyeEdgeMaskTexture);
half4 _SampleTexture2D_1e5e254c29e84e2ebe790f4de0afeab7_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_0dfa773f4d6f47bc85db4d5cfb627c23_Out_0_Texture2D.tex, _Property_0dfa773f4d6f47bc85db4d5cfb627c23_Out_0_Texture2D.samplerstate, _Property_0dfa773f4d6f47bc85db4d5cfb627c23_Out_0_Texture2D.GetTransformedUV(IN.uv0.xy) );
half _SampleTexture2D_1e5e254c29e84e2ebe790f4de0afeab7_R_4_Float = _SampleTexture2D_1e5e254c29e84e2ebe790f4de0afeab7_RGBA_0_Vector4.r;
half _SampleTexture2D_1e5e254c29e84e2ebe790f4de0afeab7_G_5_Float = _SampleTexture2D_1e5e254c29e84e2ebe790f4de0afeab7_RGBA_0_Vector4.g;
half _SampleTexture2D_1e5e254c29e84e2ebe790f4de0afeab7_B_6_Float = _SampleTexture2D_1e5e254c29e84e2ebe790f4de0afeab7_RGBA_0_Vector4.b;
half _SampleTexture2D_1e5e254c29e84e2ebe790f4de0afeab7_A_7_Float = _SampleTexture2D_1e5e254c29e84e2ebe790f4de0afeab7_RGBA_0_Vector4.a;
half4 _Lerp_3fc533c198e74a1ebb2c7f7bad6d9762_Out_3_Vector4;
Unity_Lerp_half4(Color_22af752f56df4b69a15a74bd003266a0, (_Clamp_a3567345de7b4ba6850279acb3e1bbda_Out_3_Float.xxxx), _SampleTexture2D_1e5e254c29e84e2ebe790f4de0afeab7_RGBA_0_Vector4, _Lerp_3fc533c198e74a1ebb2c7f7bad6d9762_Out_3_Vector4);
half4 _Lerp_7b945e4c623b447f84028beaef139c78_Out_3_Vector4;
Unity_Lerp_half4(Color_9e0f8909aabe4b33b699f1d9bf4443dd, _Lerp_56dd8066e70d4c85a8d72c30ef200bb7_Out_3_Vector4, _Lerp_3fc533c198e74a1ebb2c7f7bad6d9762_Out_3_Vector4, _Lerp_7b945e4c623b447f84028beaef139c78_Out_3_Vector4);
surface.BaseColor = (_Lerp_7b945e4c623b447f84028beaef139c78_Out_3_Vector4.xyz);
return surface;
}

// --------------------------------------------------
// Build Graph Inputs
#ifdef HAVE_VFX_MODIFICATION
#define VFX_SRP_ATTRIBUTES Attributes
#define VFX_SRP_VARYINGS Varyings
#define VFX_SRP_SURFACE_INPUTS SurfaceDescriptionInputs
#endif
VertexDescriptionInputs BuildVertexDescriptionInputs(Attributes input)
{
    VertexDescriptionInputs output;
    ZERO_INITIALIZE(VertexDescriptionInputs, output);

    output.ObjectSpaceNormal =                          input.normalOS;
    output.ObjectSpaceTangent =                         input.tangentOS.xyz;
    output.ObjectSpacePosition =                        input.positionOS;
#if UNITY_ANY_INSTANCING_ENABLED
#else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.
#endif

    return output;
}
SurfaceDescriptionInputs BuildSurfaceDescriptionInputs(Varyings input)
{
    SurfaceDescriptionInputs output;
    ZERO_INITIALIZE(SurfaceDescriptionInputs, output);

#ifdef HAVE_VFX_MODIFICATION
#if VFX_USE_GRAPH_VALUES
    uint instanceActiveIndex = asuint(UNITY_ACCESS_INSTANCED_PROP(PerInstance, _InstanceActiveIndex));
    /* WARNING: $splice Could not find named fragment 'VFXLoadGraphValues' */
#endif
    /* WARNING: $splice Could not find named fragment 'VFXSetFragInputs' */

#endif

    






    #if UNITY_UV_STARTS_AT_TOP
    #else
    #endif


    output.uv0 = input.texCoord0;
#if UNITY_ANY_INSTANCING_ENABLED
#else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
#define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN output.FaceSign =                    IS_FRONT_VFACE(input.cullFace, true, false);
#else
#define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
#endif
#undef BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN

        return output;
}

// --------------------------------------------------
// Main

#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/Varyings.hlsl"
#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/SelectionPickingPass.hlsl"

// --------------------------------------------------
// Visual Effect Vertex Invocations
#ifdef HAVE_VFX_MODIFICATION
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/VisualEffectVertex.hlsl"
#endif

ENDHLSL
}
Pass
{
    Name "Universal 2D"
    Tags
    {
        "LightMode" = "Universal2D"
    }

// Render State
Cull Back
Blend One Zero
ZTest LEqual
ZWrite On

// Debug
// <None>

// --------------------------------------------------
// Pass

HLSLPROGRAM

// Pragmas
#pragma target 2.0
#pragma vertex vert
#pragma fragment frag

// Keywords
// PassKeywords: <None>
// GraphKeywords: <None>

// Defines

#define _NORMALMAP 1
#define _NORMAL_DROPOFF_TS 1
#define ATTRIBUTES_NEED_NORMAL
#define ATTRIBUTES_NEED_TANGENT
#define ATTRIBUTES_NEED_TEXCOORD0
#define FEATURES_GRAPH_VERTEX_NORMAL_OUTPUT
#define FEATURES_GRAPH_VERTEX_TANGENT_OUTPUT
#define VARYINGS_NEED_TEXCOORD0
#define FEATURES_GRAPH_VERTEX
/* WARNING: $splice Could not find named fragment 'PassInstancing' */
#define SHADERPASS SHADERPASS_2D


// custom interpolator pre-include
/* WARNING: $splice Could not find named fragment 'sgci_CustomInterpolatorPreInclude' */

// Includes
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DebugMipmapStreamingMacros.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"

// --------------------------------------------------
// Structs and Packing

// custom interpolators pre packing
/* WARNING: $splice Could not find named fragment 'CustomInterpolatorPrePacking' */

struct Attributes
{
 float3 positionOS : POSITION;
 float3 normalOS : NORMAL;
 float4 tangentOS : TANGENT;
 float4 uv0 : TEXCOORD0;
#if UNITY_ANY_INSTANCING_ENABLED || defined(ATTRIBUTES_NEED_INSTANCEID)
 uint instanceID : INSTANCEID_SEMANTIC;
#endif
};
struct Varyings
{
 float4 positionCS : SV_POSITION;
 float4 texCoord0;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
 uint instanceID : CUSTOM_INSTANCE_ID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
 uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
 uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
 FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
#endif
};
struct SurfaceDescriptionInputs
{
 float4 uv0;
};
struct VertexDescriptionInputs
{
 float3 ObjectSpaceNormal;
 float3 ObjectSpaceTangent;
 float3 ObjectSpacePosition;
};
struct PackedVaryings
{
 float4 positionCS : SV_POSITION;
 float4 texCoord0 : INTERP0;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
 uint instanceID : CUSTOM_INSTANCE_ID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
 uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
 uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
 FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
#endif
};

PackedVaryings PackVaryings (Varyings input)
{
PackedVaryings output;
ZERO_INITIALIZE(PackedVaryings, output);
output.positionCS = input.positionCS;
output.texCoord0.xyzw = input.texCoord0;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
output.instanceID = input.instanceID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
output.cullFace = input.cullFace;
#endif
return output;
}

Varyings UnpackVaryings (PackedVaryings input)
{
Varyings output;
output.positionCS = input.positionCS;
output.texCoord0 = input.texCoord0.xyzw;
#if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
output.instanceID = input.instanceID;
#endif
#if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
#endif
#if (defined(UNITY_STEREO_INSTANCING_ENABLED))
output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
output.cullFace = input.cullFace;
#endif
return output;
}


// --------------------------------------------------
// Graph

// Graph Properties
CBUFFER_START(UnityPerMaterial)
float4 _ColorMap_TexelSize;
float4 _SkinMaskTexture_TexelSize;
float4 _DarkMaskTexture_TexelSize;
half4 _DirtColor;
half4 _SkinColor;
half _DarkAmount;
float4 _DirtMaskTexture_TexelSize;
half _DirtAmount;
float4 _EyeEdgeMaskTexture_TexelSize;
half _EyelinerAmount;
half _SkinColorAmount;
UNITY_TEXTURE_STREAMING_DEBUG_VARS;
CBUFFER_END


// Object and Global properties
SAMPLER(SamplerState_Linear_Repeat);
TEXTURE2D(_ColorMap);
SAMPLER(sampler_ColorMap);
TEXTURE2D(_SkinMaskTexture);
SAMPLER(sampler_SkinMaskTexture);
TEXTURE2D(_DarkMaskTexture);
SAMPLER(sampler_DarkMaskTexture);
TEXTURE2D(_CutsMaskTexture);
SAMPLER(sampler_CutsMaskTexture);
float4 _CutsMaskTexture_TexelSize;
TEXTURE2D(_MetallicMap);
SAMPLER(sampler_MetallicMap);
float4 _MetallicMap_TexelSize;
TEXTURE2D(_SmoothnessMap);
SAMPLER(sampler_SmoothnessMap);
float4 _SmoothnessMap_TexelSize;
TEXTURE2D(_EmissionMap);
SAMPLER(sampler_EmissionMap);
float4 _EmissionMap_TexelSize;
TEXTURE2D(_DirtMaskTexture);
SAMPLER(sampler_DirtMaskTexture);
half _CutsAmount;
half4 _CutsColor;
TEXTURE2D(_EyeEdgeMaskTexture);
SAMPLER(sampler_EyeEdgeMaskTexture);

// Graph Includes
// GraphIncludes: <None>

// -- Property used by ScenePickingPass
#ifdef SCENEPICKINGPASS
float4 _SelectionID;
#endif

// -- Properties used by SceneSelectionPass
#ifdef SCENESELECTIONPASS
int _ObjectId;
int _PassValue;
#endif

// Graph Functions

void Unity_Lerp_half4(half4 A, half4 B, half4 T, out half4 Out)
{
    Out = lerp(A, B, T);
}

void Unity_OneMinus_half(half In, out half Out)
{
    Out = 1 - In;
}

void Unity_Clamp_half(half In, half Min, half Max, out half Out)
{
    Out = clamp(In, Min, Max);
}

// Custom interpolators pre vertex
/* WARNING: $splice Could not find named fragment 'CustomInterpolatorPreVertex' */

// Graph Vertex
struct VertexDescription
{
half3 Position;
half3 Normal;
half3 Tangent;
};

VertexDescription VertexDescriptionFunction(VertexDescriptionInputs IN)
{
VertexDescription description = (VertexDescription)0;
description.Position = IN.ObjectSpacePosition;
description.Normal = IN.ObjectSpaceNormal;
description.Tangent = IN.ObjectSpaceTangent;
return description;
}

// Custom interpolators, pre surface
#ifdef FEATURES_GRAPH_VERTEX
Varyings CustomInterpolatorPassThroughFunc(inout Varyings output, VertexDescription input)
{
return output;
}
#define CUSTOMINTERPOLATOR_VARYPASSTHROUGH_FUNC
#endif

// Graph Pixel
struct SurfaceDescription
{
half3 BaseColor;
};

SurfaceDescription SurfaceDescriptionFunction(SurfaceDescriptionInputs IN)
{
SurfaceDescription surface = (SurfaceDescription)0;
half4 Color_9e0f8909aabe4b33b699f1d9bf4443dd = IsGammaSpace() ? half4(0, 0, 0, 0) : half4(SRGBToLinear(half3(0, 0, 0)), 0);
half4 _Property_ecf2fe5309e54977b870684c8d900a70_Out_0_Vector4 = _SkinColor;
half4 _Property_dee2f69732114c579841770d3a5f1ce9_Out_0_Vector4 = _CutsColor;
half4 _Property_2152e36dab12470385b94206ce043312_Out_0_Vector4 = _DirtColor;
half4 Color_1d5d22b78f8b4dc6b677715e3c97350d = IsGammaSpace() ? half4(0, 0, 0, 0) : half4(SRGBToLinear(half3(0, 0, 0)), 0);
UnityTexture2D _Property_29217945295d45b482fdf43563ceafe1_Out_0_Texture2D = UnityBuildTexture2DStructNoScale(_ColorMap);
half4 _SampleTexture2D_84a95f63ac39426f99979a9f9613953a_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_29217945295d45b482fdf43563ceafe1_Out_0_Texture2D.tex, _Property_29217945295d45b482fdf43563ceafe1_Out_0_Texture2D.samplerstate, _Property_29217945295d45b482fdf43563ceafe1_Out_0_Texture2D.GetTransformedUV(IN.uv0.xy) );
half _SampleTexture2D_84a95f63ac39426f99979a9f9613953a_R_4_Float = _SampleTexture2D_84a95f63ac39426f99979a9f9613953a_RGBA_0_Vector4.r;
half _SampleTexture2D_84a95f63ac39426f99979a9f9613953a_G_5_Float = _SampleTexture2D_84a95f63ac39426f99979a9f9613953a_RGBA_0_Vector4.g;
half _SampleTexture2D_84a95f63ac39426f99979a9f9613953a_B_6_Float = _SampleTexture2D_84a95f63ac39426f99979a9f9613953a_RGBA_0_Vector4.b;
half _SampleTexture2D_84a95f63ac39426f99979a9f9613953a_A_7_Float = _SampleTexture2D_84a95f63ac39426f99979a9f9613953a_RGBA_0_Vector4.a;
half4 Color_22af752f56df4b69a15a74bd003266a0 = IsGammaSpace() ? half4(1, 1, 1, 0) : half4(SRGBToLinear(half3(1, 1, 1)), 0);
UnityTexture2D _Property_c18bcc123f5545dda6a4416e976dce17_Out_0_Texture2D = UnityBuildTexture2DStructNoScale(_DarkMaskTexture);
half4 _SampleTexture2D_6ccf5a041d9a4379a08064554c835a43_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_c18bcc123f5545dda6a4416e976dce17_Out_0_Texture2D.tex, _Property_c18bcc123f5545dda6a4416e976dce17_Out_0_Texture2D.samplerstate, _Property_c18bcc123f5545dda6a4416e976dce17_Out_0_Texture2D.GetTransformedUV(IN.uv0.xy) );
half _SampleTexture2D_6ccf5a041d9a4379a08064554c835a43_R_4_Float = _SampleTexture2D_6ccf5a041d9a4379a08064554c835a43_RGBA_0_Vector4.r;
half _SampleTexture2D_6ccf5a041d9a4379a08064554c835a43_G_5_Float = _SampleTexture2D_6ccf5a041d9a4379a08064554c835a43_RGBA_0_Vector4.g;
half _SampleTexture2D_6ccf5a041d9a4379a08064554c835a43_B_6_Float = _SampleTexture2D_6ccf5a041d9a4379a08064554c835a43_RGBA_0_Vector4.b;
half _SampleTexture2D_6ccf5a041d9a4379a08064554c835a43_A_7_Float = _SampleTexture2D_6ccf5a041d9a4379a08064554c835a43_RGBA_0_Vector4.a;
half _Property_f460bfa9d43848bba59ba7460f20c9c2_Out_0_Float = _DarkAmount;
half4 _Lerp_b97b4bab53a04c6ea870c54dde30e56e_Out_3_Vector4;
Unity_Lerp_half4(Color_22af752f56df4b69a15a74bd003266a0, _SampleTexture2D_6ccf5a041d9a4379a08064554c835a43_RGBA_0_Vector4, (_Property_f460bfa9d43848bba59ba7460f20c9c2_Out_0_Float.xxxx), _Lerp_b97b4bab53a04c6ea870c54dde30e56e_Out_3_Vector4);
half4 _Lerp_5fb58a8630424f0aa299b82d3c6b7cf2_Out_3_Vector4;
Unity_Lerp_half4(Color_1d5d22b78f8b4dc6b677715e3c97350d, _SampleTexture2D_84a95f63ac39426f99979a9f9613953a_RGBA_0_Vector4, _Lerp_b97b4bab53a04c6ea870c54dde30e56e_Out_3_Vector4, _Lerp_5fb58a8630424f0aa299b82d3c6b7cf2_Out_3_Vector4);
UnityTexture2D _Property_f8239e9549eb4afdb04e0add697a9e63_Out_0_Texture2D = UnityBuildTexture2DStructNoScale(_DirtMaskTexture);
half4 _SampleTexture2D_7a735da5e71c436f82fc33c38b3cb722_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_f8239e9549eb4afdb04e0add697a9e63_Out_0_Texture2D.tex, _Property_f8239e9549eb4afdb04e0add697a9e63_Out_0_Texture2D.samplerstate, _Property_f8239e9549eb4afdb04e0add697a9e63_Out_0_Texture2D.GetTransformedUV(IN.uv0.xy) );
half _SampleTexture2D_7a735da5e71c436f82fc33c38b3cb722_R_4_Float = _SampleTexture2D_7a735da5e71c436f82fc33c38b3cb722_RGBA_0_Vector4.r;
half _SampleTexture2D_7a735da5e71c436f82fc33c38b3cb722_G_5_Float = _SampleTexture2D_7a735da5e71c436f82fc33c38b3cb722_RGBA_0_Vector4.g;
half _SampleTexture2D_7a735da5e71c436f82fc33c38b3cb722_B_6_Float = _SampleTexture2D_7a735da5e71c436f82fc33c38b3cb722_RGBA_0_Vector4.b;
half _SampleTexture2D_7a735da5e71c436f82fc33c38b3cb722_A_7_Float = _SampleTexture2D_7a735da5e71c436f82fc33c38b3cb722_RGBA_0_Vector4.a;
half _Property_ef41be1c8664497cbdfb806842079a89_Out_0_Float = _DirtAmount;
half4 _Lerp_aec65eee28314809a1410cf73b04a889_Out_3_Vector4;
Unity_Lerp_half4(Color_22af752f56df4b69a15a74bd003266a0, _SampleTexture2D_7a735da5e71c436f82fc33c38b3cb722_RGBA_0_Vector4, (_Property_ef41be1c8664497cbdfb806842079a89_Out_0_Float.xxxx), _Lerp_aec65eee28314809a1410cf73b04a889_Out_3_Vector4);
half4 _Lerp_1422a096bdfb4539b217cd9460b0b019_Out_3_Vector4;
Unity_Lerp_half4(_Property_2152e36dab12470385b94206ce043312_Out_0_Vector4, _Lerp_5fb58a8630424f0aa299b82d3c6b7cf2_Out_3_Vector4, _Lerp_aec65eee28314809a1410cf73b04a889_Out_3_Vector4, _Lerp_1422a096bdfb4539b217cd9460b0b019_Out_3_Vector4);
UnityTexture2D _Property_cad87ecf436a44458da862f7575dfeec_Out_0_Texture2D = UnityBuildTexture2DStructNoScale(_CutsMaskTexture);
half4 _SampleTexture2D_1937dc0552af4944a98689fd9d6fa9cc_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_cad87ecf436a44458da862f7575dfeec_Out_0_Texture2D.tex, _Property_cad87ecf436a44458da862f7575dfeec_Out_0_Texture2D.samplerstate, _Property_cad87ecf436a44458da862f7575dfeec_Out_0_Texture2D.GetTransformedUV(IN.uv0.xy) );
half _SampleTexture2D_1937dc0552af4944a98689fd9d6fa9cc_R_4_Float = _SampleTexture2D_1937dc0552af4944a98689fd9d6fa9cc_RGBA_0_Vector4.r;
half _SampleTexture2D_1937dc0552af4944a98689fd9d6fa9cc_G_5_Float = _SampleTexture2D_1937dc0552af4944a98689fd9d6fa9cc_RGBA_0_Vector4.g;
half _SampleTexture2D_1937dc0552af4944a98689fd9d6fa9cc_B_6_Float = _SampleTexture2D_1937dc0552af4944a98689fd9d6fa9cc_RGBA_0_Vector4.b;
half _SampleTexture2D_1937dc0552af4944a98689fd9d6fa9cc_A_7_Float = _SampleTexture2D_1937dc0552af4944a98689fd9d6fa9cc_RGBA_0_Vector4.a;
half _Property_092064d1a1804dcb88349a0dfa930d34_Out_0_Float = _CutsAmount;
half4 _Lerp_587eb00b83614171a84a065eb4475d69_Out_3_Vector4;
Unity_Lerp_half4(Color_22af752f56df4b69a15a74bd003266a0, _SampleTexture2D_1937dc0552af4944a98689fd9d6fa9cc_RGBA_0_Vector4, (_Property_092064d1a1804dcb88349a0dfa930d34_Out_0_Float.xxxx), _Lerp_587eb00b83614171a84a065eb4475d69_Out_3_Vector4);
half4 _Lerp_480d292819cd497f84da826a5234e813_Out_3_Vector4;
Unity_Lerp_half4(_Property_dee2f69732114c579841770d3a5f1ce9_Out_0_Vector4, _Lerp_1422a096bdfb4539b217cd9460b0b019_Out_3_Vector4, _Lerp_587eb00b83614171a84a065eb4475d69_Out_3_Vector4, _Lerp_480d292819cd497f84da826a5234e813_Out_3_Vector4);
UnityTexture2D _Property_b2cfc554a3654888b085ea4dadd11e99_Out_0_Texture2D = UnityBuildTexture2DStructNoScale(_SkinMaskTexture);
half4 _SampleTexture2D_864cfb091b7d4331bd7218fdeaf8d858_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_b2cfc554a3654888b085ea4dadd11e99_Out_0_Texture2D.tex, _Property_b2cfc554a3654888b085ea4dadd11e99_Out_0_Texture2D.samplerstate, _Property_b2cfc554a3654888b085ea4dadd11e99_Out_0_Texture2D.GetTransformedUV(IN.uv0.xy) );
half _SampleTexture2D_864cfb091b7d4331bd7218fdeaf8d858_R_4_Float = _SampleTexture2D_864cfb091b7d4331bd7218fdeaf8d858_RGBA_0_Vector4.r;
half _SampleTexture2D_864cfb091b7d4331bd7218fdeaf8d858_G_5_Float = _SampleTexture2D_864cfb091b7d4331bd7218fdeaf8d858_RGBA_0_Vector4.g;
half _SampleTexture2D_864cfb091b7d4331bd7218fdeaf8d858_B_6_Float = _SampleTexture2D_864cfb091b7d4331bd7218fdeaf8d858_RGBA_0_Vector4.b;
half _SampleTexture2D_864cfb091b7d4331bd7218fdeaf8d858_A_7_Float = _SampleTexture2D_864cfb091b7d4331bd7218fdeaf8d858_RGBA_0_Vector4.a;
half _Property_495de44ef11d40cb91ca34953cc899b1_Out_0_Float = _SkinColorAmount;
half4 _Lerp_82953a0631914e78ba370c51d143d891_Out_3_Vector4;
Unity_Lerp_half4(Color_22af752f56df4b69a15a74bd003266a0, _SampleTexture2D_864cfb091b7d4331bd7218fdeaf8d858_RGBA_0_Vector4, (_Property_495de44ef11d40cb91ca34953cc899b1_Out_0_Float.xxxx), _Lerp_82953a0631914e78ba370c51d143d891_Out_3_Vector4);
half4 _Lerp_56dd8066e70d4c85a8d72c30ef200bb7_Out_3_Vector4;
Unity_Lerp_half4(_Property_ecf2fe5309e54977b870684c8d900a70_Out_0_Vector4, _Lerp_480d292819cd497f84da826a5234e813_Out_3_Vector4, _Lerp_82953a0631914e78ba370c51d143d891_Out_3_Vector4, _Lerp_56dd8066e70d4c85a8d72c30ef200bb7_Out_3_Vector4);
half _Property_4208ddc25284429fba969ec5b8970ca4_Out_0_Float = _EyelinerAmount;
half _OneMinus_dfa88fc204374b35898331eae5eba835_Out_1_Float;
Unity_OneMinus_half(_Property_4208ddc25284429fba969ec5b8970ca4_Out_0_Float, _OneMinus_dfa88fc204374b35898331eae5eba835_Out_1_Float);
half _Clamp_a3567345de7b4ba6850279acb3e1bbda_Out_3_Float;
Unity_Clamp_half(_OneMinus_dfa88fc204374b35898331eae5eba835_Out_1_Float, half(0), half(0.5), _Clamp_a3567345de7b4ba6850279acb3e1bbda_Out_3_Float);
UnityTexture2D _Property_0dfa773f4d6f47bc85db4d5cfb627c23_Out_0_Texture2D = UnityBuildTexture2DStructNoScale(_EyeEdgeMaskTexture);
half4 _SampleTexture2D_1e5e254c29e84e2ebe790f4de0afeab7_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_0dfa773f4d6f47bc85db4d5cfb627c23_Out_0_Texture2D.tex, _Property_0dfa773f4d6f47bc85db4d5cfb627c23_Out_0_Texture2D.samplerstate, _Property_0dfa773f4d6f47bc85db4d5cfb627c23_Out_0_Texture2D.GetTransformedUV(IN.uv0.xy) );
half _SampleTexture2D_1e5e254c29e84e2ebe790f4de0afeab7_R_4_Float = _SampleTexture2D_1e5e254c29e84e2ebe790f4de0afeab7_RGBA_0_Vector4.r;
half _SampleTexture2D_1e5e254c29e84e2ebe790f4de0afeab7_G_5_Float = _SampleTexture2D_1e5e254c29e84e2ebe790f4de0afeab7_RGBA_0_Vector4.g;
half _SampleTexture2D_1e5e254c29e84e2ebe790f4de0afeab7_B_6_Float = _SampleTexture2D_1e5e254c29e84e2ebe790f4de0afeab7_RGBA_0_Vector4.b;
half _SampleTexture2D_1e5e254c29e84e2ebe790f4de0afeab7_A_7_Float = _SampleTexture2D_1e5e254c29e84e2ebe790f4de0afeab7_RGBA_0_Vector4.a;
half4 _Lerp_3fc533c198e74a1ebb2c7f7bad6d9762_Out_3_Vector4;
Unity_Lerp_half4(Color_22af752f56df4b69a15a74bd003266a0, (_Clamp_a3567345de7b4ba6850279acb3e1bbda_Out_3_Float.xxxx), _SampleTexture2D_1e5e254c29e84e2ebe790f4de0afeab7_RGBA_0_Vector4, _Lerp_3fc533c198e74a1ebb2c7f7bad6d9762_Out_3_Vector4);
half4 _Lerp_7b945e4c623b447f84028beaef139c78_Out_3_Vector4;
Unity_Lerp_half4(Color_9e0f8909aabe4b33b699f1d9bf4443dd, _Lerp_56dd8066e70d4c85a8d72c30ef200bb7_Out_3_Vector4, _Lerp_3fc533c198e74a1ebb2c7f7bad6d9762_Out_3_Vector4, _Lerp_7b945e4c623b447f84028beaef139c78_Out_3_Vector4);
surface.BaseColor = (_Lerp_7b945e4c623b447f84028beaef139c78_Out_3_Vector4.xyz);
return surface;
}

// --------------------------------------------------
// Build Graph Inputs
#ifdef HAVE_VFX_MODIFICATION
#define VFX_SRP_ATTRIBUTES Attributes
#define VFX_SRP_VARYINGS Varyings
#define VFX_SRP_SURFACE_INPUTS SurfaceDescriptionInputs
#endif
VertexDescriptionInputs BuildVertexDescriptionInputs(Attributes input)
{
    VertexDescriptionInputs output;
    ZERO_INITIALIZE(VertexDescriptionInputs, output);

    output.ObjectSpaceNormal =                          input.normalOS;
    output.ObjectSpaceTangent =                         input.tangentOS.xyz;
    output.ObjectSpacePosition =                        input.positionOS;
#if UNITY_ANY_INSTANCING_ENABLED
#else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.
#endif

    return output;
}
SurfaceDescriptionInputs BuildSurfaceDescriptionInputs(Varyings input)
{
    SurfaceDescriptionInputs output;
    ZERO_INITIALIZE(SurfaceDescriptionInputs, output);

#ifdef HAVE_VFX_MODIFICATION
#if VFX_USE_GRAPH_VALUES
    uint instanceActiveIndex = asuint(UNITY_ACCESS_INSTANCED_PROP(PerInstance, _InstanceActiveIndex));
    /* WARNING: $splice Could not find named fragment 'VFXLoadGraphValues' */
#endif
    /* WARNING: $splice Could not find named fragment 'VFXSetFragInputs' */

#endif

    






    #if UNITY_UV_STARTS_AT_TOP
    #else
    #endif


    output.uv0 = input.texCoord0;
#if UNITY_ANY_INSTANCING_ENABLED
#else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.
#endif
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
#define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN output.FaceSign =                    IS_FRONT_VFACE(input.cullFace, true, false);
#else
#define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
#endif
#undef BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN

        return output;
}

// --------------------------------------------------
// Main

#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/Varyings.hlsl"
#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/PBR2DPass.hlsl"

// --------------------------------------------------
// Visual Effect Vertex Invocations
#ifdef HAVE_VFX_MODIFICATION
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/VisualEffectVertex.hlsl"
#endif

ENDHLSL
}
}
CustomEditor "UnityEditor.ShaderGraph.GenericShaderGraphMaterialGUI"
CustomEditorForRenderPipeline "UnityEditor.Rendering.BuiltIn.ShaderGraph.BuiltInLitGUI" ""
CustomEditorForRenderPipeline "UnityEditor.ShaderGraphLitGUI" "UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset"
FallBack "Hidden/Shader Graph/FallbackError"
}