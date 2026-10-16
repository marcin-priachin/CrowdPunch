Shader "CrowdPunch/Cel/Enemy"
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
[MainColor]_BaseColor("Enemy Body Tint", Color) = (1, 1, 1, 1)
_ExploderPulse("Exploder Pulse", Range(0, 3)) = 0
[HideInInspector]_ComputeMeshIndex("Compute Mesh Buffer Index Offset", Float) = 0
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
#define ATTRIBUTES_NEED_VERTEXID
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
 uint vertexID : VERTEXID_SEMANTIC;
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
 float3 TimeParameters;
};
struct VertexDescriptionInputs
{
 float3 ObjectSpaceNormal;
 float3 ObjectSpaceTangent;
 float3 ObjectSpacePosition;
 uint VertexID;
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
float _ComputeMeshIndex;
float4 _ColorMap_TexelSize;
float4 _SkinMaskTexture_TexelSize;
float4 _DarkMaskTexture_TexelSize;
float4 _DirtColor;
float4 _SkinColor;
float _DarkAmount;
float4 _DirtMaskTexture_TexelSize;
float _DirtAmount;
float4 _EyeEdgeMaskTexture_TexelSize;
float _EyelinerAmount;
float _SkinColorAmount;
float4 _BaseColor;
float _ExploderPulse;
UNITY_TEXTURE_STREAMING_DEBUG_VARS;
CBUFFER_END

#if defined(DOTS_INSTANCING_ON)
// DOTS instancing definitions
UNITY_DOTS_INSTANCING_START(MaterialPropertyMetadata)
    UNITY_DOTS_INSTANCED_PROP_OVERRIDE_SUPPORTED(float, _ComputeMeshIndex)
    UNITY_DOTS_INSTANCED_PROP_OVERRIDE_SUPPORTED(float4, _SkinColor)
    UNITY_DOTS_INSTANCED_PROP_OVERRIDE_SUPPORTED(float4, _BaseColor)
UNITY_DOTS_INSTANCING_END(MaterialPropertyMetadata)
// DOTS instancing usage macros
#define UNITY_ACCESS_HYBRID_INSTANCED_PROP(var, type) UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(type, var)
#elif defined(UNITY_INSTANCING_ENABLED)
// Unity instancing definitions
UNITY_INSTANCING_BUFFER_START(SGPerInstanceData)
    UNITY_DEFINE_INSTANCED_PROP(float, _ComputeMeshIndex)
    UNITY_DEFINE_INSTANCED_PROP(float4, _SkinColor)
    UNITY_DEFINE_INSTANCED_PROP(float4, _BaseColor)
UNITY_INSTANCING_BUFFER_END(SGPerInstanceData)
// Unity instancing usage macros
#define UNITY_ACCESS_HYBRID_INSTANCED_PROP(var, type) UNITY_ACCESS_INSTANCED_PROP(SGPerInstanceData, var)
#else
#define UNITY_ACCESS_HYBRID_INSTANCED_PROP(var, type) var
#endif

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
float _CutsAmount;
float4 _CutsColor;
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

struct DeformedVertexData
{
float3 Position;
float3 Normal;
float3 Tangent;
};
uniform StructuredBuffer<DeformedVertexData> _DeformedMeshData : register(t1);

void Unity_ComputeDeformedVertex_float(uint vertexID, out float3 positionOut, out float3 normalOut, out float3 tangentOut)
{
const DeformedVertexData vertexData = _DeformedMeshData[asuint(UNITY_ACCESS_HYBRID_INSTANCED_PROP(_ComputeMeshIndex, float)) + vertexID];
positionOut = vertexData.Position;
normalOut = vertexData.Normal;
tangentOut = vertexData.Tangent;
}

void Unity_Lerp_float4(float4 A, float4 B, float4 T, out float4 Out)
{
    Out = lerp(A, B, T);
}

void Unity_OneMinus_float(float In, out float Out)
{
    Out = 1 - In;
}

void Unity_Clamp_float(float In, float Min, float Max, out float Out)
{
    Out = clamp(In, Min, Max);
}

void Unity_Multiply_float4_float4(float4 A, float4 B, out float4 Out)
{
Out = A * B;
}

void Unity_Multiply_float_float(float A, float B, out float Out)
{
Out = A * B;
}

void Unity_Sine_float(float In, out float Out)
{
    Out = sin(In);
}

void Unity_Add_float(float A, float B, out float Out)
{
    Out = A + B;
}

// Custom interpolators pre vertex
/* WARNING: $splice Could not find named fragment 'CustomInterpolatorPreVertex' */

// Graph Vertex
struct VertexDescription
{
float3 Position;
float3 Normal;
float3 Tangent;
};

VertexDescription VertexDescriptionFunction(VertexDescriptionInputs IN)
{
VertexDescription description = (VertexDescription)0;
#if defined(UNITY_DOTS_INSTANCING_ENABLED)
float3 _ComputeDeformation_1d37a9a23f19400eb3356a05ea2721bc_DeformedPosition_0_Vector3 = 0;
float3 _ComputeDeformation_1d37a9a23f19400eb3356a05ea2721bc_DeformedNormal_1_Vector3 = 0;
float3 _ComputeDeformation_1d37a9a23f19400eb3356a05ea2721bc_DeformedTangent_2_Vector3 = 0;
Unity_ComputeDeformedVertex_float(IN.VertexID, _ComputeDeformation_1d37a9a23f19400eb3356a05ea2721bc_DeformedPosition_0_Vector3, _ComputeDeformation_1d37a9a23f19400eb3356a05ea2721bc_DeformedNormal_1_Vector3, _ComputeDeformation_1d37a9a23f19400eb3356a05ea2721bc_DeformedTangent_2_Vector3);
#else
float3 _ComputeDeformation_1d37a9a23f19400eb3356a05ea2721bc_DeformedPosition_0_Vector3 = IN.ObjectSpacePosition;
float3 _ComputeDeformation_1d37a9a23f19400eb3356a05ea2721bc_DeformedNormal_1_Vector3 = IN.ObjectSpaceNormal;
float3 _ComputeDeformation_1d37a9a23f19400eb3356a05ea2721bc_DeformedTangent_2_Vector3 = IN.ObjectSpaceTangent;
#endif
description.Position = _ComputeDeformation_1d37a9a23f19400eb3356a05ea2721bc_DeformedPosition_0_Vector3;
description.Normal = _ComputeDeformation_1d37a9a23f19400eb3356a05ea2721bc_DeformedNormal_1_Vector3;
description.Tangent = _ComputeDeformation_1d37a9a23f19400eb3356a05ea2721bc_DeformedTangent_2_Vector3;
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
float3 BaseColor;
float3 NormalTS;
float3 Emission;
float Metallic;
float Smoothness;
float Occlusion;
};

SurfaceDescription SurfaceDescriptionFunction(SurfaceDescriptionInputs IN)
{
SurfaceDescription surface = (SurfaceDescription)0;
float4 Color_9e0f8909aabe4b33b699f1d9bf4443dd = IsGammaSpace() ? float4(0, 0, 0, 0) : float4(SRGBToLinear(float3(0, 0, 0)), 0);
float4 _Property_ecf2fe5309e54977b870684c8d900a70_Out_0_Vector4 = UNITY_ACCESS_HYBRID_INSTANCED_PROP(_SkinColor, float4);
float4 _Property_dee2f69732114c579841770d3a5f1ce9_Out_0_Vector4 = _CutsColor;
float4 _Property_2152e36dab12470385b94206ce043312_Out_0_Vector4 = _DirtColor;
float4 Color_1d5d22b78f8b4dc6b677715e3c97350d = IsGammaSpace() ? float4(0, 0, 0, 0) : float4(SRGBToLinear(float3(0, 0, 0)), 0);
UnityTexture2D _Property_29217945295d45b482fdf43563ceafe1_Out_0_Texture2D = UnityBuildTexture2DStructNoScale(_ColorMap);
float4 _SampleTexture2D_84a95f63ac39426f99979a9f9613953a_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_29217945295d45b482fdf43563ceafe1_Out_0_Texture2D.tex, _Property_29217945295d45b482fdf43563ceafe1_Out_0_Texture2D.samplerstate, _Property_29217945295d45b482fdf43563ceafe1_Out_0_Texture2D.GetTransformedUV(IN.uv0.xy) );
float _SampleTexture2D_84a95f63ac39426f99979a9f9613953a_R_4_Float = _SampleTexture2D_84a95f63ac39426f99979a9f9613953a_RGBA_0_Vector4.r;
float _SampleTexture2D_84a95f63ac39426f99979a9f9613953a_G_5_Float = _SampleTexture2D_84a95f63ac39426f99979a9f9613953a_RGBA_0_Vector4.g;
float _SampleTexture2D_84a95f63ac39426f99979a9f9613953a_B_6_Float = _SampleTexture2D_84a95f63ac39426f99979a9f9613953a_RGBA_0_Vector4.b;
float _SampleTexture2D_84a95f63ac39426f99979a9f9613953a_A_7_Float = _SampleTexture2D_84a95f63ac39426f99979a9f9613953a_RGBA_0_Vector4.a;
float4 Color_22af752f56df4b69a15a74bd003266a0 = IsGammaSpace() ? float4(1, 1, 1, 0) : float4(SRGBToLinear(float3(1, 1, 1)), 0);
UnityTexture2D _Property_c18bcc123f5545dda6a4416e976dce17_Out_0_Texture2D = UnityBuildTexture2DStructNoScale(_DarkMaskTexture);
float4 _SampleTexture2D_6ccf5a041d9a4379a08064554c835a43_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_c18bcc123f5545dda6a4416e976dce17_Out_0_Texture2D.tex, _Property_c18bcc123f5545dda6a4416e976dce17_Out_0_Texture2D.samplerstate, _Property_c18bcc123f5545dda6a4416e976dce17_Out_0_Texture2D.GetTransformedUV(IN.uv0.xy) );
float _SampleTexture2D_6ccf5a041d9a4379a08064554c835a43_R_4_Float = _SampleTexture2D_6ccf5a041d9a4379a08064554c835a43_RGBA_0_Vector4.r;
float _SampleTexture2D_6ccf5a041d9a4379a08064554c835a43_G_5_Float = _SampleTexture2D_6ccf5a041d9a4379a08064554c835a43_RGBA_0_Vector4.g;
float _SampleTexture2D_6ccf5a041d9a4379a08064554c835a43_B_6_Float = _SampleTexture2D_6ccf5a041d9a4379a08064554c835a43_RGBA_0_Vector4.b;
float _SampleTexture2D_6ccf5a041d9a4379a08064554c835a43_A_7_Float = _SampleTexture2D_6ccf5a041d9a4379a08064554c835a43_RGBA_0_Vector4.a;
float _Property_f460bfa9d43848bba59ba7460f20c9c2_Out_0_Float = _DarkAmount;
float4 _Lerp_b97b4bab53a04c6ea870c54dde30e56e_Out_3_Vector4;
Unity_Lerp_float4(Color_22af752f56df4b69a15a74bd003266a0, _SampleTexture2D_6ccf5a041d9a4379a08064554c835a43_RGBA_0_Vector4, (_Property_f460bfa9d43848bba59ba7460f20c9c2_Out_0_Float.xxxx), _Lerp_b97b4bab53a04c6ea870c54dde30e56e_Out_3_Vector4);
float4 _Lerp_5fb58a8630424f0aa299b82d3c6b7cf2_Out_3_Vector4;
Unity_Lerp_float4(Color_1d5d22b78f8b4dc6b677715e3c97350d, _SampleTexture2D_84a95f63ac39426f99979a9f9613953a_RGBA_0_Vector4, _Lerp_b97b4bab53a04c6ea870c54dde30e56e_Out_3_Vector4, _Lerp_5fb58a8630424f0aa299b82d3c6b7cf2_Out_3_Vector4);
UnityTexture2D _Property_f8239e9549eb4afdb04e0add697a9e63_Out_0_Texture2D = UnityBuildTexture2DStructNoScale(_DirtMaskTexture);
float4 _SampleTexture2D_7a735da5e71c436f82fc33c38b3cb722_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_f8239e9549eb4afdb04e0add697a9e63_Out_0_Texture2D.tex, _Property_f8239e9549eb4afdb04e0add697a9e63_Out_0_Texture2D.samplerstate, _Property_f8239e9549eb4afdb04e0add697a9e63_Out_0_Texture2D.GetTransformedUV(IN.uv0.xy) );
float _SampleTexture2D_7a735da5e71c436f82fc33c38b3cb722_R_4_Float = _SampleTexture2D_7a735da5e71c436f82fc33c38b3cb722_RGBA_0_Vector4.r;
float _SampleTexture2D_7a735da5e71c436f82fc33c38b3cb722_G_5_Float = _SampleTexture2D_7a735da5e71c436f82fc33c38b3cb722_RGBA_0_Vector4.g;
float _SampleTexture2D_7a735da5e71c436f82fc33c38b3cb722_B_6_Float = _SampleTexture2D_7a735da5e71c436f82fc33c38b3cb722_RGBA_0_Vector4.b;
float _SampleTexture2D_7a735da5e71c436f82fc33c38b3cb722_A_7_Float = _SampleTexture2D_7a735da5e71c436f82fc33c38b3cb722_RGBA_0_Vector4.a;
float _Property_ef41be1c8664497cbdfb806842079a89_Out_0_Float = _DirtAmount;
float4 _Lerp_aec65eee28314809a1410cf73b04a889_Out_3_Vector4;
Unity_Lerp_float4(Color_22af752f56df4b69a15a74bd003266a0, _SampleTexture2D_7a735da5e71c436f82fc33c38b3cb722_RGBA_0_Vector4, (_Property_ef41be1c8664497cbdfb806842079a89_Out_0_Float.xxxx), _Lerp_aec65eee28314809a1410cf73b04a889_Out_3_Vector4);
float4 _Lerp_1422a096bdfb4539b217cd9460b0b019_Out_3_Vector4;
Unity_Lerp_float4(_Property_2152e36dab12470385b94206ce043312_Out_0_Vector4, _Lerp_5fb58a8630424f0aa299b82d3c6b7cf2_Out_3_Vector4, _Lerp_aec65eee28314809a1410cf73b04a889_Out_3_Vector4, _Lerp_1422a096bdfb4539b217cd9460b0b019_Out_3_Vector4);
UnityTexture2D _Property_cad87ecf436a44458da862f7575dfeec_Out_0_Texture2D = UnityBuildTexture2DStructNoScale(_CutsMaskTexture);
float4 _SampleTexture2D_1937dc0552af4944a98689fd9d6fa9cc_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_cad87ecf436a44458da862f7575dfeec_Out_0_Texture2D.tex, _Property_cad87ecf436a44458da862f7575dfeec_Out_0_Texture2D.samplerstate, _Property_cad87ecf436a44458da862f7575dfeec_Out_0_Texture2D.GetTransformedUV(IN.uv0.xy) );
float _SampleTexture2D_1937dc0552af4944a98689fd9d6fa9cc_R_4_Float = _SampleTexture2D_1937dc0552af4944a98689fd9d6fa9cc_RGBA_0_Vector4.r;
float _SampleTexture2D_1937dc0552af4944a98689fd9d6fa9cc_G_5_Float = _SampleTexture2D_1937dc0552af4944a98689fd9d6fa9cc_RGBA_0_Vector4.g;
float _SampleTexture2D_1937dc0552af4944a98689fd9d6fa9cc_B_6_Float = _SampleTexture2D_1937dc0552af4944a98689fd9d6fa9cc_RGBA_0_Vector4.b;
float _SampleTexture2D_1937dc0552af4944a98689fd9d6fa9cc_A_7_Float = _SampleTexture2D_1937dc0552af4944a98689fd9d6fa9cc_RGBA_0_Vector4.a;
float _Property_092064d1a1804dcb88349a0dfa930d34_Out_0_Float = _CutsAmount;
float4 _Lerp_587eb00b83614171a84a065eb4475d69_Out_3_Vector4;
Unity_Lerp_float4(Color_22af752f56df4b69a15a74bd003266a0, _SampleTexture2D_1937dc0552af4944a98689fd9d6fa9cc_RGBA_0_Vector4, (_Property_092064d1a1804dcb88349a0dfa930d34_Out_0_Float.xxxx), _Lerp_587eb00b83614171a84a065eb4475d69_Out_3_Vector4);
float4 _Lerp_480d292819cd497f84da826a5234e813_Out_3_Vector4;
Unity_Lerp_float4(_Property_dee2f69732114c579841770d3a5f1ce9_Out_0_Vector4, _Lerp_1422a096bdfb4539b217cd9460b0b019_Out_3_Vector4, _Lerp_587eb00b83614171a84a065eb4475d69_Out_3_Vector4, _Lerp_480d292819cd497f84da826a5234e813_Out_3_Vector4);
UnityTexture2D _Property_b2cfc554a3654888b085ea4dadd11e99_Out_0_Texture2D = UnityBuildTexture2DStructNoScale(_SkinMaskTexture);
float4 _SampleTexture2D_864cfb091b7d4331bd7218fdeaf8d858_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_b2cfc554a3654888b085ea4dadd11e99_Out_0_Texture2D.tex, _Property_b2cfc554a3654888b085ea4dadd11e99_Out_0_Texture2D.samplerstate, _Property_b2cfc554a3654888b085ea4dadd11e99_Out_0_Texture2D.GetTransformedUV(IN.uv0.xy) );
float _SampleTexture2D_864cfb091b7d4331bd7218fdeaf8d858_R_4_Float = _SampleTexture2D_864cfb091b7d4331bd7218fdeaf8d858_RGBA_0_Vector4.r;
float _SampleTexture2D_864cfb091b7d4331bd7218fdeaf8d858_G_5_Float = _SampleTexture2D_864cfb091b7d4331bd7218fdeaf8d858_RGBA_0_Vector4.g;
float _SampleTexture2D_864cfb091b7d4331bd7218fdeaf8d858_B_6_Float = _SampleTexture2D_864cfb091b7d4331bd7218fdeaf8d858_RGBA_0_Vector4.b;
float _SampleTexture2D_864cfb091b7d4331bd7218fdeaf8d858_A_7_Float = _SampleTexture2D_864cfb091b7d4331bd7218fdeaf8d858_RGBA_0_Vector4.a;
float _Property_495de44ef11d40cb91ca34953cc899b1_Out_0_Float = _SkinColorAmount;
float4 _Lerp_82953a0631914e78ba370c51d143d891_Out_3_Vector4;
Unity_Lerp_float4(Color_22af752f56df4b69a15a74bd003266a0, _SampleTexture2D_864cfb091b7d4331bd7218fdeaf8d858_RGBA_0_Vector4, (_Property_495de44ef11d40cb91ca34953cc899b1_Out_0_Float.xxxx), _Lerp_82953a0631914e78ba370c51d143d891_Out_3_Vector4);
float4 _Lerp_56dd8066e70d4c85a8d72c30ef200bb7_Out_3_Vector4;
Unity_Lerp_float4(_Property_ecf2fe5309e54977b870684c8d900a70_Out_0_Vector4, _Lerp_480d292819cd497f84da826a5234e813_Out_3_Vector4, _Lerp_82953a0631914e78ba370c51d143d891_Out_3_Vector4, _Lerp_56dd8066e70d4c85a8d72c30ef200bb7_Out_3_Vector4);
float _Property_4208ddc25284429fba969ec5b8970ca4_Out_0_Float = _EyelinerAmount;
float _OneMinus_dfa88fc204374b35898331eae5eba835_Out_1_Float;
Unity_OneMinus_float(_Property_4208ddc25284429fba969ec5b8970ca4_Out_0_Float, _OneMinus_dfa88fc204374b35898331eae5eba835_Out_1_Float);
float _Clamp_a3567345de7b4ba6850279acb3e1bbda_Out_3_Float;
Unity_Clamp_float(_OneMinus_dfa88fc204374b35898331eae5eba835_Out_1_Float, float(0), float(0.5), _Clamp_a3567345de7b4ba6850279acb3e1bbda_Out_3_Float);
UnityTexture2D _Property_0dfa773f4d6f47bc85db4d5cfb627c23_Out_0_Texture2D = UnityBuildTexture2DStructNoScale(_EyeEdgeMaskTexture);
float4 _SampleTexture2D_1e5e254c29e84e2ebe790f4de0afeab7_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_0dfa773f4d6f47bc85db4d5cfb627c23_Out_0_Texture2D.tex, _Property_0dfa773f4d6f47bc85db4d5cfb627c23_Out_0_Texture2D.samplerstate, _Property_0dfa773f4d6f47bc85db4d5cfb627c23_Out_0_Texture2D.GetTransformedUV(IN.uv0.xy) );
float _SampleTexture2D_1e5e254c29e84e2ebe790f4de0afeab7_R_4_Float = _SampleTexture2D_1e5e254c29e84e2ebe790f4de0afeab7_RGBA_0_Vector4.r;
float _SampleTexture2D_1e5e254c29e84e2ebe790f4de0afeab7_G_5_Float = _SampleTexture2D_1e5e254c29e84e2ebe790f4de0afeab7_RGBA_0_Vector4.g;
float _SampleTexture2D_1e5e254c29e84e2ebe790f4de0afeab7_B_6_Float = _SampleTexture2D_1e5e254c29e84e2ebe790f4de0afeab7_RGBA_0_Vector4.b;
float _SampleTexture2D_1e5e254c29e84e2ebe790f4de0afeab7_A_7_Float = _SampleTexture2D_1e5e254c29e84e2ebe790f4de0afeab7_RGBA_0_Vector4.a;
float4 _Lerp_3fc533c198e74a1ebb2c7f7bad6d9762_Out_3_Vector4;
Unity_Lerp_float4(Color_22af752f56df4b69a15a74bd003266a0, (_Clamp_a3567345de7b4ba6850279acb3e1bbda_Out_3_Float.xxxx), _SampleTexture2D_1e5e254c29e84e2ebe790f4de0afeab7_RGBA_0_Vector4, _Lerp_3fc533c198e74a1ebb2c7f7bad6d9762_Out_3_Vector4);
float4 _Lerp_7b945e4c623b447f84028beaef139c78_Out_3_Vector4;
Unity_Lerp_float4(Color_9e0f8909aabe4b33b699f1d9bf4443dd, _Lerp_56dd8066e70d4c85a8d72c30ef200bb7_Out_3_Vector4, _Lerp_3fc533c198e74a1ebb2c7f7bad6d9762_Out_3_Vector4, _Lerp_7b945e4c623b447f84028beaef139c78_Out_3_Vector4);
float4 _EnemyBodyTint_7c6b1708145240cfb235df3b94d53325_Out_0_Vector4 = UNITY_ACCESS_HYBRID_INSTANCED_PROP(_BaseColor, float4);
float4 _BodyReadability_84a204bce97045fc9c6824c3328d4e49_Out_2_Vector4;
Unity_Multiply_float4_float4(_Lerp_7b945e4c623b447f84028beaef139c78_Out_3_Vector4, _EnemyBodyTint_7c6b1708145240cfb235df3b94d53325_Out_0_Vector4, _BodyReadability_84a204bce97045fc9c6824c3328d4e49_Out_2_Vector4);
float _Pulseperiod12seconds_8c95b2a85a75428ea0e1a96e4244cd75_Out_2_Float;
Unity_Multiply_float_float(IN.TimeParameters.x, 5.235988, _Pulseperiod12seconds_8c95b2a85a75428ea0e1a96e4244cd75_Out_2_Float);
float _Exploderrhythm_e111e0eb61ec43eab84b2040404dfcc1_Out_1_Float;
Unity_Sine_float(_Pulseperiod12seconds_8c95b2a85a75428ea0e1a96e4244cd75_Out_2_Float, _Exploderrhythm_e111e0eb61ec43eab84b2040404dfcc1_Out_1_Float);
float _Halfwave_e0c9105d5a604220acf6e06d34303944_Out_2_Float;
Unity_Multiply_float_float(_Exploderrhythm_e111e0eb61ec43eab84b2040404dfcc1_Out_1_Float, 0.5, _Halfwave_e0c9105d5a604220acf6e06d34303944_Out_2_Float);
float _Positivepulse_a9c4de5ed7454cf9b723fa518a26c658_Out_2_Float;
Unity_Add_float(_Halfwave_e0c9105d5a604220acf6e06d34303944_Out_2_Float, float(0.5), _Positivepulse_a9c4de5ed7454cf9b723fa518a26c658_Out_2_Float);
float _Softheartbeat_60e3ab0a1bd6410b8fccad5484f2bd45_Out_2_Float;
Unity_Multiply_float_float(_Positivepulse_a9c4de5ed7454cf9b723fa518a26c658_Out_2_Float, _Positivepulse_a9c4de5ed7454cf9b723fa518a26c658_Out_2_Float, _Softheartbeat_60e3ab0a1bd6410b8fccad5484f2bd45_Out_2_Float);
float _ExploderPulse_78b25de6c19f40b385c67b258dfbd5de_Out_0_Float = _ExploderPulse;
float _Exploderglowstrength_60532f0cb28f4812a2be39e3de2ee660_Out_2_Float;
Unity_Multiply_float_float(_Softheartbeat_60e3ab0a1bd6410b8fccad5484f2bd45_Out_2_Float, _ExploderPulse_78b25de6c19f40b385c67b258dfbd5de_Out_0_Float, _Exploderglowstrength_60532f0cb28f4812a2be39e3de2ee660_Out_2_Float);
float _Pulseandshadowfill_6d61d073f3f3403a9c853596880987e2_Out_2_Float;
Unity_Add_float(_Exploderglowstrength_60532f0cb28f4812a2be39e3de2ee660_Out_2_Float, float(0.075), _Pulseandshadowfill_6d61d073f3f3403a9c853596880987e2_Out_2_Float);
float4 _PaletteShadowFill_b77d58bc87fe4a2e98ca8c704eb8dafc_Out_2_Vector4;
Unity_Multiply_float4_float4(_BodyReadability_84a204bce97045fc9c6824c3328d4e49_Out_2_Vector4, (_Pulseandshadowfill_6d61d073f3f3403a9c853596880987e2_Out_2_Float.xxxx), _PaletteShadowFill_b77d58bc87fe4a2e98ca8c704eb8dafc_Out_2_Vector4);
surface.BaseColor = (_BodyReadability_84a204bce97045fc9c6824c3328d4e49_Out_2_Vector4.xyz);
surface.NormalTS = IN.TangentSpaceNormal;
surface.Emission = (_PaletteShadowFill_b77d58bc87fe4a2e98ca8c704eb8dafc_Out_2_Vector4.xyz);
surface.Metallic = float(0);
surface.Smoothness = float(0.18);
surface.Occlusion = float(1);
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
    output.VertexID =                                   input.vertexID;
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
    output.TimeParameters = _TimeParameters.xyz; // This is mainly for LW as HD overwrite this value
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
#define ATTRIBUTES_NEED_VERTEXID
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
 uint vertexID : VERTEXID_SEMANTIC;
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
 float3 TimeParameters;
};
struct VertexDescriptionInputs
{
 float3 ObjectSpaceNormal;
 float3 ObjectSpaceTangent;
 float3 ObjectSpacePosition;
 uint VertexID;
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
float _ComputeMeshIndex;
float4 _ColorMap_TexelSize;
float4 _SkinMaskTexture_TexelSize;
float4 _DarkMaskTexture_TexelSize;
float4 _DirtColor;
float4 _SkinColor;
float _DarkAmount;
float4 _DirtMaskTexture_TexelSize;
float _DirtAmount;
float4 _EyeEdgeMaskTexture_TexelSize;
float _EyelinerAmount;
float _SkinColorAmount;
float4 _BaseColor;
float _ExploderPulse;
UNITY_TEXTURE_STREAMING_DEBUG_VARS;
CBUFFER_END

#if defined(DOTS_INSTANCING_ON)
// DOTS instancing definitions
UNITY_DOTS_INSTANCING_START(MaterialPropertyMetadata)
    UNITY_DOTS_INSTANCED_PROP_OVERRIDE_SUPPORTED(float, _ComputeMeshIndex)
    UNITY_DOTS_INSTANCED_PROP_OVERRIDE_SUPPORTED(float4, _SkinColor)
    UNITY_DOTS_INSTANCED_PROP_OVERRIDE_SUPPORTED(float4, _BaseColor)
UNITY_DOTS_INSTANCING_END(MaterialPropertyMetadata)
// DOTS instancing usage macros
#define UNITY_ACCESS_HYBRID_INSTANCED_PROP(var, type) UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(type, var)
#elif defined(UNITY_INSTANCING_ENABLED)
// Unity instancing definitions
UNITY_INSTANCING_BUFFER_START(SGPerInstanceData)
    UNITY_DEFINE_INSTANCED_PROP(float, _ComputeMeshIndex)
    UNITY_DEFINE_INSTANCED_PROP(float4, _SkinColor)
    UNITY_DEFINE_INSTANCED_PROP(float4, _BaseColor)
UNITY_INSTANCING_BUFFER_END(SGPerInstanceData)
// Unity instancing usage macros
#define UNITY_ACCESS_HYBRID_INSTANCED_PROP(var, type) UNITY_ACCESS_INSTANCED_PROP(SGPerInstanceData, var)
#else
#define UNITY_ACCESS_HYBRID_INSTANCED_PROP(var, type) var
#endif

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
float _CutsAmount;
float4 _CutsColor;
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

struct DeformedVertexData
{
float3 Position;
float3 Normal;
float3 Tangent;
};
uniform StructuredBuffer<DeformedVertexData> _DeformedMeshData : register(t1);

void Unity_ComputeDeformedVertex_float(uint vertexID, out float3 positionOut, out float3 normalOut, out float3 tangentOut)
{
const DeformedVertexData vertexData = _DeformedMeshData[asuint(UNITY_ACCESS_HYBRID_INSTANCED_PROP(_ComputeMeshIndex, float)) + vertexID];
positionOut = vertexData.Position;
normalOut = vertexData.Normal;
tangentOut = vertexData.Tangent;
}

void Unity_Lerp_float4(float4 A, float4 B, float4 T, out float4 Out)
{
    Out = lerp(A, B, T);
}

void Unity_OneMinus_float(float In, out float Out)
{
    Out = 1 - In;
}

void Unity_Clamp_float(float In, float Min, float Max, out float Out)
{
    Out = clamp(In, Min, Max);
}

void Unity_Multiply_float4_float4(float4 A, float4 B, out float4 Out)
{
Out = A * B;
}

void Unity_Multiply_float_float(float A, float B, out float Out)
{
Out = A * B;
}

void Unity_Sine_float(float In, out float Out)
{
    Out = sin(In);
}

void Unity_Add_float(float A, float B, out float Out)
{
    Out = A + B;
}

// Custom interpolators pre vertex
/* WARNING: $splice Could not find named fragment 'CustomInterpolatorPreVertex' */

// Graph Vertex
struct VertexDescription
{
float3 Position;
float3 Normal;
float3 Tangent;
};

VertexDescription VertexDescriptionFunction(VertexDescriptionInputs IN)
{
VertexDescription description = (VertexDescription)0;
#if defined(UNITY_DOTS_INSTANCING_ENABLED)
float3 _ComputeDeformation_1d37a9a23f19400eb3356a05ea2721bc_DeformedPosition_0_Vector3 = 0;
float3 _ComputeDeformation_1d37a9a23f19400eb3356a05ea2721bc_DeformedNormal_1_Vector3 = 0;
float3 _ComputeDeformation_1d37a9a23f19400eb3356a05ea2721bc_DeformedTangent_2_Vector3 = 0;
Unity_ComputeDeformedVertex_float(IN.VertexID, _ComputeDeformation_1d37a9a23f19400eb3356a05ea2721bc_DeformedPosition_0_Vector3, _ComputeDeformation_1d37a9a23f19400eb3356a05ea2721bc_DeformedNormal_1_Vector3, _ComputeDeformation_1d37a9a23f19400eb3356a05ea2721bc_DeformedTangent_2_Vector3);
#else
float3 _ComputeDeformation_1d37a9a23f19400eb3356a05ea2721bc_DeformedPosition_0_Vector3 = IN.ObjectSpacePosition;
float3 _ComputeDeformation_1d37a9a23f19400eb3356a05ea2721bc_DeformedNormal_1_Vector3 = IN.ObjectSpaceNormal;
float3 _ComputeDeformation_1d37a9a23f19400eb3356a05ea2721bc_DeformedTangent_2_Vector3 = IN.ObjectSpaceTangent;
#endif
description.Position = _ComputeDeformation_1d37a9a23f19400eb3356a05ea2721bc_DeformedPosition_0_Vector3;
description.Normal = _ComputeDeformation_1d37a9a23f19400eb3356a05ea2721bc_DeformedNormal_1_Vector3;
description.Tangent = _ComputeDeformation_1d37a9a23f19400eb3356a05ea2721bc_DeformedTangent_2_Vector3;
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
float3 BaseColor;
float3 NormalTS;
float3 Emission;
float Metallic;
float Smoothness;
float Occlusion;
};

SurfaceDescription SurfaceDescriptionFunction(SurfaceDescriptionInputs IN)
{
SurfaceDescription surface = (SurfaceDescription)0;
float4 Color_9e0f8909aabe4b33b699f1d9bf4443dd = IsGammaSpace() ? float4(0, 0, 0, 0) : float4(SRGBToLinear(float3(0, 0, 0)), 0);
float4 _Property_ecf2fe5309e54977b870684c8d900a70_Out_0_Vector4 = UNITY_ACCESS_HYBRID_INSTANCED_PROP(_SkinColor, float4);
float4 _Property_dee2f69732114c579841770d3a5f1ce9_Out_0_Vector4 = _CutsColor;
float4 _Property_2152e36dab12470385b94206ce043312_Out_0_Vector4 = _DirtColor;
float4 Color_1d5d22b78f8b4dc6b677715e3c97350d = IsGammaSpace() ? float4(0, 0, 0, 0) : float4(SRGBToLinear(float3(0, 0, 0)), 0);
UnityTexture2D _Property_29217945295d45b482fdf43563ceafe1_Out_0_Texture2D = UnityBuildTexture2DStructNoScale(_ColorMap);
float4 _SampleTexture2D_84a95f63ac39426f99979a9f9613953a_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_29217945295d45b482fdf43563ceafe1_Out_0_Texture2D.tex, _Property_29217945295d45b482fdf43563ceafe1_Out_0_Texture2D.samplerstate, _Property_29217945295d45b482fdf43563ceafe1_Out_0_Texture2D.GetTransformedUV(IN.uv0.xy) );
float _SampleTexture2D_84a95f63ac39426f99979a9f9613953a_R_4_Float = _SampleTexture2D_84a95f63ac39426f99979a9f9613953a_RGBA_0_Vector4.r;
float _SampleTexture2D_84a95f63ac39426f99979a9f9613953a_G_5_Float = _SampleTexture2D_84a95f63ac39426f99979a9f9613953a_RGBA_0_Vector4.g;
float _SampleTexture2D_84a95f63ac39426f99979a9f9613953a_B_6_Float = _SampleTexture2D_84a95f63ac39426f99979a9f9613953a_RGBA_0_Vector4.b;
float _SampleTexture2D_84a95f63ac39426f99979a9f9613953a_A_7_Float = _SampleTexture2D_84a95f63ac39426f99979a9f9613953a_RGBA_0_Vector4.a;
float4 Color_22af752f56df4b69a15a74bd003266a0 = IsGammaSpace() ? float4(1, 1, 1, 0) : float4(SRGBToLinear(float3(1, 1, 1)), 0);
UnityTexture2D _Property_c18bcc123f5545dda6a4416e976dce17_Out_0_Texture2D = UnityBuildTexture2DStructNoScale(_DarkMaskTexture);
float4 _SampleTexture2D_6ccf5a041d9a4379a08064554c835a43_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_c18bcc123f5545dda6a4416e976dce17_Out_0_Texture2D.tex, _Property_c18bcc123f5545dda6a4416e976dce17_Out_0_Texture2D.samplerstate, _Property_c18bcc123f5545dda6a4416e976dce17_Out_0_Texture2D.GetTransformedUV(IN.uv0.xy) );
float _SampleTexture2D_6ccf5a041d9a4379a08064554c835a43_R_4_Float = _SampleTexture2D_6ccf5a041d9a4379a08064554c835a43_RGBA_0_Vector4.r;
float _SampleTexture2D_6ccf5a041d9a4379a08064554c835a43_G_5_Float = _SampleTexture2D_6ccf5a041d9a4379a08064554c835a43_RGBA_0_Vector4.g;
float _SampleTexture2D_6ccf5a041d9a4379a08064554c835a43_B_6_Float = _SampleTexture2D_6ccf5a041d9a4379a08064554c835a43_RGBA_0_Vector4.b;
float _SampleTexture2D_6ccf5a041d9a4379a08064554c835a43_A_7_Float = _SampleTexture2D_6ccf5a041d9a4379a08064554c835a43_RGBA_0_Vector4.a;
float _Property_f460bfa9d43848bba59ba7460f20c9c2_Out_0_Float = _DarkAmount;
float4 _Lerp_b97b4bab53a04c6ea870c54dde30e56e_Out_3_Vector4;
Unity_Lerp_float4(Color_22af752f56df4b69a15a74bd003266a0, _SampleTexture2D_6ccf5a041d9a4379a08064554c835a43_RGBA_0_Vector4, (_Property_f460bfa9d43848bba59ba7460f20c9c2_Out_0_Float.xxxx), _Lerp_b97b4bab53a04c6ea870c54dde30e56e_Out_3_Vector4);
float4 _Lerp_5fb58a8630424f0aa299b82d3c6b7cf2_Out_3_Vector4;
Unity_Lerp_float4(Color_1d5d22b78f8b4dc6b677715e3c97350d, _SampleTexture2D_84a95f63ac39426f99979a9f9613953a_RGBA_0_Vector4, _Lerp_b97b4bab53a04c6ea870c54dde30e56e_Out_3_Vector4, _Lerp_5fb58a8630424f0aa299b82d3c6b7cf2_Out_3_Vector4);
UnityTexture2D _Property_f8239e9549eb4afdb04e0add697a9e63_Out_0_Texture2D = UnityBuildTexture2DStructNoScale(_DirtMaskTexture);
float4 _SampleTexture2D_7a735da5e71c436f82fc33c38b3cb722_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_f8239e9549eb4afdb04e0add697a9e63_Out_0_Texture2D.tex, _Property_f8239e9549eb4afdb04e0add697a9e63_Out_0_Texture2D.samplerstate, _Property_f8239e9549eb4afdb04e0add697a9e63_Out_0_Texture2D.GetTransformedUV(IN.uv0.xy) );
float _SampleTexture2D_7a735da5e71c436f82fc33c38b3cb722_R_4_Float = _SampleTexture2D_7a735da5e71c436f82fc33c38b3cb722_RGBA_0_Vector4.r;
float _SampleTexture2D_7a735da5e71c436f82fc33c38b3cb722_G_5_Float = _SampleTexture2D_7a735da5e71c436f82fc33c38b3cb722_RGBA_0_Vector4.g;
float _SampleTexture2D_7a735da5e71c436f82fc33c38b3cb722_B_6_Float = _SampleTexture2D_7a735da5e71c436f82fc33c38b3cb722_RGBA_0_Vector4.b;
float _SampleTexture2D_7a735da5e71c436f82fc33c38b3cb722_A_7_Float = _SampleTexture2D_7a735da5e71c436f82fc33c38b3cb722_RGBA_0_Vector4.a;
float _Property_ef41be1c8664497cbdfb806842079a89_Out_0_Float = _DirtAmount;
float4 _Lerp_aec65eee28314809a1410cf73b04a889_Out_3_Vector4;
Unity_Lerp_float4(Color_22af752f56df4b69a15a74bd003266a0, _SampleTexture2D_7a735da5e71c436f82fc33c38b3cb722_RGBA_0_Vector4, (_Property_ef41be1c8664497cbdfb806842079a89_Out_0_Float.xxxx), _Lerp_aec65eee28314809a1410cf73b04a889_Out_3_Vector4);
float4 _Lerp_1422a096bdfb4539b217cd9460b0b019_Out_3_Vector4;
Unity_Lerp_float4(_Property_2152e36dab12470385b94206ce043312_Out_0_Vector4, _Lerp_5fb58a8630424f0aa299b82d3c6b7cf2_Out_3_Vector4, _Lerp_aec65eee28314809a1410cf73b04a889_Out_3_Vector4, _Lerp_1422a096bdfb4539b217cd9460b0b019_Out_3_Vector4);
UnityTexture2D _Property_cad87ecf436a44458da862f7575dfeec_Out_0_Texture2D = UnityBuildTexture2DStructNoScale(_CutsMaskTexture);
float4 _SampleTexture2D_1937dc0552af4944a98689fd9d6fa9cc_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_cad87ecf436a44458da862f7575dfeec_Out_0_Texture2D.tex, _Property_cad87ecf436a44458da862f7575dfeec_Out_0_Texture2D.samplerstate, _Property_cad87ecf436a44458da862f7575dfeec_Out_0_Texture2D.GetTransformedUV(IN.uv0.xy) );
float _SampleTexture2D_1937dc0552af4944a98689fd9d6fa9cc_R_4_Float = _SampleTexture2D_1937dc0552af4944a98689fd9d6fa9cc_RGBA_0_Vector4.r;
float _SampleTexture2D_1937dc0552af4944a98689fd9d6fa9cc_G_5_Float = _SampleTexture2D_1937dc0552af4944a98689fd9d6fa9cc_RGBA_0_Vector4.g;
float _SampleTexture2D_1937dc0552af4944a98689fd9d6fa9cc_B_6_Float = _SampleTexture2D_1937dc0552af4944a98689fd9d6fa9cc_RGBA_0_Vector4.b;
float _SampleTexture2D_1937dc0552af4944a98689fd9d6fa9cc_A_7_Float = _SampleTexture2D_1937dc0552af4944a98689fd9d6fa9cc_RGBA_0_Vector4.a;
float _Property_092064d1a1804dcb88349a0dfa930d34_Out_0_Float = _CutsAmount;
float4 _Lerp_587eb00b83614171a84a065eb4475d69_Out_3_Vector4;
Unity_Lerp_float4(Color_22af752f56df4b69a15a74bd003266a0, _SampleTexture2D_1937dc0552af4944a98689fd9d6fa9cc_RGBA_0_Vector4, (_Property_092064d1a1804dcb88349a0dfa930d34_Out_0_Float.xxxx), _Lerp_587eb00b83614171a84a065eb4475d69_Out_3_Vector4);
float4 _Lerp_480d292819cd497f84da826a5234e813_Out_3_Vector4;
Unity_Lerp_float4(_Property_dee2f69732114c579841770d3a5f1ce9_Out_0_Vector4, _Lerp_1422a096bdfb4539b217cd9460b0b019_Out_3_Vector4, _Lerp_587eb00b83614171a84a065eb4475d69_Out_3_Vector4, _Lerp_480d292819cd497f84da826a5234e813_Out_3_Vector4);
UnityTexture2D _Property_b2cfc554a3654888b085ea4dadd11e99_Out_0_Texture2D = UnityBuildTexture2DStructNoScale(_SkinMaskTexture);
float4 _SampleTexture2D_864cfb091b7d4331bd7218fdeaf8d858_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_b2cfc554a3654888b085ea4dadd11e99_Out_0_Texture2D.tex, _Property_b2cfc554a3654888b085ea4dadd11e99_Out_0_Texture2D.samplerstate, _Property_b2cfc554a3654888b085ea4dadd11e99_Out_0_Texture2D.GetTransformedUV(IN.uv0.xy) );
float _SampleTexture2D_864cfb091b7d4331bd7218fdeaf8d858_R_4_Float = _SampleTexture2D_864cfb091b7d4331bd7218fdeaf8d858_RGBA_0_Vector4.r;
float _SampleTexture2D_864cfb091b7d4331bd7218fdeaf8d858_G_5_Float = _SampleTexture2D_864cfb091b7d4331bd7218fdeaf8d858_RGBA_0_Vector4.g;
float _SampleTexture2D_864cfb091b7d4331bd7218fdeaf8d858_B_6_Float = _SampleTexture2D_864cfb091b7d4331bd7218fdeaf8d858_RGBA_0_Vector4.b;
float _SampleTexture2D_864cfb091b7d4331bd7218fdeaf8d858_A_7_Float = _SampleTexture2D_864cfb091b7d4331bd7218fdeaf8d858_RGBA_0_Vector4.a;
float _Property_495de44ef11d40cb91ca34953cc899b1_Out_0_Float = _SkinColorAmount;
float4 _Lerp_82953a0631914e78ba370c51d143d891_Out_3_Vector4;
Unity_Lerp_float4(Color_22af752f56df4b69a15a74bd003266a0, _SampleTexture2D_864cfb091b7d4331bd7218fdeaf8d858_RGBA_0_Vector4, (_Property_495de44ef11d40cb91ca34953cc899b1_Out_0_Float.xxxx), _Lerp_82953a0631914e78ba370c51d143d891_Out_3_Vector4);
float4 _Lerp_56dd8066e70d4c85a8d72c30ef200bb7_Out_3_Vector4;
Unity_Lerp_float4(_Property_ecf2fe5309e54977b870684c8d900a70_Out_0_Vector4, _Lerp_480d292819cd497f84da826a5234e813_Out_3_Vector4, _Lerp_82953a0631914e78ba370c51d143d891_Out_3_Vector4, _Lerp_56dd8066e70d4c85a8d72c30ef200bb7_Out_3_Vector4);
float _Property_4208ddc25284429fba969ec5b8970ca4_Out_0_Float = _EyelinerAmount;
float _OneMinus_dfa88fc204374b35898331eae5eba835_Out_1_Float;
Unity_OneMinus_float(_Property_4208ddc25284429fba969ec5b8970ca4_Out_0_Float, _OneMinus_dfa88fc204374b35898331eae5eba835_Out_1_Float);
float _Clamp_a3567345de7b4ba6850279acb3e1bbda_Out_3_Float;
Unity_Clamp_float(_OneMinus_dfa88fc204374b35898331eae5eba835_Out_1_Float, float(0), float(0.5), _Clamp_a3567345de7b4ba6850279acb3e1bbda_Out_3_Float);
UnityTexture2D _Property_0dfa773f4d6f47bc85db4d5cfb627c23_Out_0_Texture2D = UnityBuildTexture2DStructNoScale(_EyeEdgeMaskTexture);
float4 _SampleTexture2D_1e5e254c29e84e2ebe790f4de0afeab7_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_0dfa773f4d6f47bc85db4d5cfb627c23_Out_0_Texture2D.tex, _Property_0dfa773f4d6f47bc85db4d5cfb627c23_Out_0_Texture2D.samplerstate, _Property_0dfa773f4d6f47bc85db4d5cfb627c23_Out_0_Texture2D.GetTransformedUV(IN.uv0.xy) );
float _SampleTexture2D_1e5e254c29e84e2ebe790f4de0afeab7_R_4_Float = _SampleTexture2D_1e5e254c29e84e2ebe790f4de0afeab7_RGBA_0_Vector4.r;
float _SampleTexture2D_1e5e254c29e84e2ebe790f4de0afeab7_G_5_Float = _SampleTexture2D_1e5e254c29e84e2ebe790f4de0afeab7_RGBA_0_Vector4.g;
float _SampleTexture2D_1e5e254c29e84e2ebe790f4de0afeab7_B_6_Float = _SampleTexture2D_1e5e254c29e84e2ebe790f4de0afeab7_RGBA_0_Vector4.b;
float _SampleTexture2D_1e5e254c29e84e2ebe790f4de0afeab7_A_7_Float = _SampleTexture2D_1e5e254c29e84e2ebe790f4de0afeab7_RGBA_0_Vector4.a;
float4 _Lerp_3fc533c198e74a1ebb2c7f7bad6d9762_Out_3_Vector4;
Unity_Lerp_float4(Color_22af752f56df4b69a15a74bd003266a0, (_Clamp_a3567345de7b4ba6850279acb3e1bbda_Out_3_Float.xxxx), _SampleTexture2D_1e5e254c29e84e2ebe790f4de0afeab7_RGBA_0_Vector4, _Lerp_3fc533c198e74a1ebb2c7f7bad6d9762_Out_3_Vector4);
float4 _Lerp_7b945e4c623b447f84028beaef139c78_Out_3_Vector4;
Unity_Lerp_float4(Color_9e0f8909aabe4b33b699f1d9bf4443dd, _Lerp_56dd8066e70d4c85a8d72c30ef200bb7_Out_3_Vector4, _Lerp_3fc533c198e74a1ebb2c7f7bad6d9762_Out_3_Vector4, _Lerp_7b945e4c623b447f84028beaef139c78_Out_3_Vector4);
float4 _EnemyBodyTint_7c6b1708145240cfb235df3b94d53325_Out_0_Vector4 = UNITY_ACCESS_HYBRID_INSTANCED_PROP(_BaseColor, float4);
float4 _BodyReadability_84a204bce97045fc9c6824c3328d4e49_Out_2_Vector4;
Unity_Multiply_float4_float4(_Lerp_7b945e4c623b447f84028beaef139c78_Out_3_Vector4, _EnemyBodyTint_7c6b1708145240cfb235df3b94d53325_Out_0_Vector4, _BodyReadability_84a204bce97045fc9c6824c3328d4e49_Out_2_Vector4);
float _Pulseperiod12seconds_8c95b2a85a75428ea0e1a96e4244cd75_Out_2_Float;
Unity_Multiply_float_float(IN.TimeParameters.x, 5.235988, _Pulseperiod12seconds_8c95b2a85a75428ea0e1a96e4244cd75_Out_2_Float);
float _Exploderrhythm_e111e0eb61ec43eab84b2040404dfcc1_Out_1_Float;
Unity_Sine_float(_Pulseperiod12seconds_8c95b2a85a75428ea0e1a96e4244cd75_Out_2_Float, _Exploderrhythm_e111e0eb61ec43eab84b2040404dfcc1_Out_1_Float);
float _Halfwave_e0c9105d5a604220acf6e06d34303944_Out_2_Float;
Unity_Multiply_float_float(_Exploderrhythm_e111e0eb61ec43eab84b2040404dfcc1_Out_1_Float, 0.5, _Halfwave_e0c9105d5a604220acf6e06d34303944_Out_2_Float);
float _Positivepulse_a9c4de5ed7454cf9b723fa518a26c658_Out_2_Float;
Unity_Add_float(_Halfwave_e0c9105d5a604220acf6e06d34303944_Out_2_Float, float(0.5), _Positivepulse_a9c4de5ed7454cf9b723fa518a26c658_Out_2_Float);
float _Softheartbeat_60e3ab0a1bd6410b8fccad5484f2bd45_Out_2_Float;
Unity_Multiply_float_float(_Positivepulse_a9c4de5ed7454cf9b723fa518a26c658_Out_2_Float, _Positivepulse_a9c4de5ed7454cf9b723fa518a26c658_Out_2_Float, _Softheartbeat_60e3ab0a1bd6410b8fccad5484f2bd45_Out_2_Float);
float _ExploderPulse_78b25de6c19f40b385c67b258dfbd5de_Out_0_Float = _ExploderPulse;
float _Exploderglowstrength_60532f0cb28f4812a2be39e3de2ee660_Out_2_Float;
Unity_Multiply_float_float(_Softheartbeat_60e3ab0a1bd6410b8fccad5484f2bd45_Out_2_Float, _ExploderPulse_78b25de6c19f40b385c67b258dfbd5de_Out_0_Float, _Exploderglowstrength_60532f0cb28f4812a2be39e3de2ee660_Out_2_Float);
float _Pulseandshadowfill_6d61d073f3f3403a9c853596880987e2_Out_2_Float;
Unity_Add_float(_Exploderglowstrength_60532f0cb28f4812a2be39e3de2ee660_Out_2_Float, float(0.075), _Pulseandshadowfill_6d61d073f3f3403a9c853596880987e2_Out_2_Float);
float4 _PaletteShadowFill_b77d58bc87fe4a2e98ca8c704eb8dafc_Out_2_Vector4;
Unity_Multiply_float4_float4(_BodyReadability_84a204bce97045fc9c6824c3328d4e49_Out_2_Vector4, (_Pulseandshadowfill_6d61d073f3f3403a9c853596880987e2_Out_2_Float.xxxx), _PaletteShadowFill_b77d58bc87fe4a2e98ca8c704eb8dafc_Out_2_Vector4);
surface.BaseColor = (_BodyReadability_84a204bce97045fc9c6824c3328d4e49_Out_2_Vector4.xyz);
surface.NormalTS = IN.TangentSpaceNormal;
surface.Emission = (_PaletteShadowFill_b77d58bc87fe4a2e98ca8c704eb8dafc_Out_2_Vector4.xyz);
surface.Metallic = float(0);
surface.Smoothness = float(0.18);
surface.Occlusion = float(1);
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
    output.VertexID =                                   input.vertexID;
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
    output.TimeParameters = _TimeParameters.xyz; // This is mainly for LW as HD overwrite this value
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
#define ATTRIBUTES_NEED_VERTEXID
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
 uint vertexID : VERTEXID_SEMANTIC;
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
 uint VertexID;
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
float _ComputeMeshIndex;
float4 _ColorMap_TexelSize;
float4 _SkinMaskTexture_TexelSize;
float4 _DarkMaskTexture_TexelSize;
float4 _DirtColor;
float4 _SkinColor;
float _DarkAmount;
float4 _DirtMaskTexture_TexelSize;
float _DirtAmount;
float4 _EyeEdgeMaskTexture_TexelSize;
float _EyelinerAmount;
float _SkinColorAmount;
float4 _BaseColor;
float _ExploderPulse;
UNITY_TEXTURE_STREAMING_DEBUG_VARS;
CBUFFER_END

#if defined(DOTS_INSTANCING_ON)
// DOTS instancing definitions
UNITY_DOTS_INSTANCING_START(MaterialPropertyMetadata)
    UNITY_DOTS_INSTANCED_PROP_OVERRIDE_SUPPORTED(float, _ComputeMeshIndex)
    UNITY_DOTS_INSTANCED_PROP_OVERRIDE_SUPPORTED(float4, _SkinColor)
    UNITY_DOTS_INSTANCED_PROP_OVERRIDE_SUPPORTED(float4, _BaseColor)
UNITY_DOTS_INSTANCING_END(MaterialPropertyMetadata)
// DOTS instancing usage macros
#define UNITY_ACCESS_HYBRID_INSTANCED_PROP(var, type) UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(type, var)
#elif defined(UNITY_INSTANCING_ENABLED)
// Unity instancing definitions
UNITY_INSTANCING_BUFFER_START(SGPerInstanceData)
    UNITY_DEFINE_INSTANCED_PROP(float, _ComputeMeshIndex)
    UNITY_DEFINE_INSTANCED_PROP(float4, _SkinColor)
    UNITY_DEFINE_INSTANCED_PROP(float4, _BaseColor)
UNITY_INSTANCING_BUFFER_END(SGPerInstanceData)
// Unity instancing usage macros
#define UNITY_ACCESS_HYBRID_INSTANCED_PROP(var, type) UNITY_ACCESS_INSTANCED_PROP(SGPerInstanceData, var)
#else
#define UNITY_ACCESS_HYBRID_INSTANCED_PROP(var, type) var
#endif

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
float _CutsAmount;
float4 _CutsColor;
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

struct DeformedVertexData
{
float3 Position;
float3 Normal;
float3 Tangent;
};
uniform StructuredBuffer<DeformedVertexData> _DeformedMeshData : register(t1);

void Unity_ComputeDeformedVertex_float(uint vertexID, out float3 positionOut, out float3 normalOut, out float3 tangentOut)
{
const DeformedVertexData vertexData = _DeformedMeshData[asuint(UNITY_ACCESS_HYBRID_INSTANCED_PROP(_ComputeMeshIndex, float)) + vertexID];
positionOut = vertexData.Position;
normalOut = vertexData.Normal;
tangentOut = vertexData.Tangent;
}

// Custom interpolators pre vertex
/* WARNING: $splice Could not find named fragment 'CustomInterpolatorPreVertex' */

// Graph Vertex
struct VertexDescription
{
float3 Position;
float3 Normal;
float3 Tangent;
};

VertexDescription VertexDescriptionFunction(VertexDescriptionInputs IN)
{
VertexDescription description = (VertexDescription)0;
#if defined(UNITY_DOTS_INSTANCING_ENABLED)
float3 _ComputeDeformation_1d37a9a23f19400eb3356a05ea2721bc_DeformedPosition_0_Vector3 = 0;
float3 _ComputeDeformation_1d37a9a23f19400eb3356a05ea2721bc_DeformedNormal_1_Vector3 = 0;
float3 _ComputeDeformation_1d37a9a23f19400eb3356a05ea2721bc_DeformedTangent_2_Vector3 = 0;
Unity_ComputeDeformedVertex_float(IN.VertexID, _ComputeDeformation_1d37a9a23f19400eb3356a05ea2721bc_DeformedPosition_0_Vector3, _ComputeDeformation_1d37a9a23f19400eb3356a05ea2721bc_DeformedNormal_1_Vector3, _ComputeDeformation_1d37a9a23f19400eb3356a05ea2721bc_DeformedTangent_2_Vector3);
#else
float3 _ComputeDeformation_1d37a9a23f19400eb3356a05ea2721bc_DeformedPosition_0_Vector3 = IN.ObjectSpacePosition;
float3 _ComputeDeformation_1d37a9a23f19400eb3356a05ea2721bc_DeformedNormal_1_Vector3 = IN.ObjectSpaceNormal;
float3 _ComputeDeformation_1d37a9a23f19400eb3356a05ea2721bc_DeformedTangent_2_Vector3 = IN.ObjectSpaceTangent;
#endif
description.Position = _ComputeDeformation_1d37a9a23f19400eb3356a05ea2721bc_DeformedPosition_0_Vector3;
description.Normal = _ComputeDeformation_1d37a9a23f19400eb3356a05ea2721bc_DeformedNormal_1_Vector3;
description.Tangent = _ComputeDeformation_1d37a9a23f19400eb3356a05ea2721bc_DeformedTangent_2_Vector3;
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
    output.VertexID =                                   input.vertexID;
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
#define ATTRIBUTES_NEED_NORMAL
#define ATTRIBUTES_NEED_TANGENT
#define ATTRIBUTES_NEED_VERTEXID
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
 float3 normalOS : NORMAL;
 float4 tangentOS : TANGENT;
#if UNITY_ANY_INSTANCING_ENABLED || defined(ATTRIBUTES_NEED_INSTANCEID)
 uint instanceID : INSTANCEID_SEMANTIC;
#endif
 uint vertexID : VERTEXID_SEMANTIC;
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
 uint VertexID;
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
float _ComputeMeshIndex;
float4 _ColorMap_TexelSize;
float4 _SkinMaskTexture_TexelSize;
float4 _DarkMaskTexture_TexelSize;
float4 _DirtColor;
float4 _SkinColor;
float _DarkAmount;
float4 _DirtMaskTexture_TexelSize;
float _DirtAmount;
float4 _EyeEdgeMaskTexture_TexelSize;
float _EyelinerAmount;
float _SkinColorAmount;
float4 _BaseColor;
float _ExploderPulse;
UNITY_TEXTURE_STREAMING_DEBUG_VARS;
CBUFFER_END

#if defined(DOTS_INSTANCING_ON)
// DOTS instancing definitions
UNITY_DOTS_INSTANCING_START(MaterialPropertyMetadata)
    UNITY_DOTS_INSTANCED_PROP_OVERRIDE_SUPPORTED(float, _ComputeMeshIndex)
    UNITY_DOTS_INSTANCED_PROP_OVERRIDE_SUPPORTED(float4, _SkinColor)
    UNITY_DOTS_INSTANCED_PROP_OVERRIDE_SUPPORTED(float4, _BaseColor)
UNITY_DOTS_INSTANCING_END(MaterialPropertyMetadata)
// DOTS instancing usage macros
#define UNITY_ACCESS_HYBRID_INSTANCED_PROP(var, type) UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(type, var)
#elif defined(UNITY_INSTANCING_ENABLED)
// Unity instancing definitions
UNITY_INSTANCING_BUFFER_START(SGPerInstanceData)
    UNITY_DEFINE_INSTANCED_PROP(float, _ComputeMeshIndex)
    UNITY_DEFINE_INSTANCED_PROP(float4, _SkinColor)
    UNITY_DEFINE_INSTANCED_PROP(float4, _BaseColor)
UNITY_INSTANCING_BUFFER_END(SGPerInstanceData)
// Unity instancing usage macros
#define UNITY_ACCESS_HYBRID_INSTANCED_PROP(var, type) UNITY_ACCESS_INSTANCED_PROP(SGPerInstanceData, var)
#else
#define UNITY_ACCESS_HYBRID_INSTANCED_PROP(var, type) var
#endif

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
float _CutsAmount;
float4 _CutsColor;
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

struct DeformedVertexData
{
float3 Position;
float3 Normal;
float3 Tangent;
};
uniform StructuredBuffer<DeformedVertexData> _DeformedMeshData : register(t1);

void Unity_ComputeDeformedVertex_float(uint vertexID, out float3 positionOut, out float3 normalOut, out float3 tangentOut)
{
const DeformedVertexData vertexData = _DeformedMeshData[asuint(UNITY_ACCESS_HYBRID_INSTANCED_PROP(_ComputeMeshIndex, float)) + vertexID];
positionOut = vertexData.Position;
normalOut = vertexData.Normal;
tangentOut = vertexData.Tangent;
}

// Custom interpolators pre vertex
/* WARNING: $splice Could not find named fragment 'CustomInterpolatorPreVertex' */

// Graph Vertex
struct VertexDescription
{
float3 Position;
};

VertexDescription VertexDescriptionFunction(VertexDescriptionInputs IN)
{
VertexDescription description = (VertexDescription)0;
#if defined(UNITY_DOTS_INSTANCING_ENABLED)
float3 _ComputeDeformation_1d37a9a23f19400eb3356a05ea2721bc_DeformedPosition_0_Vector3 = 0;
float3 _ComputeDeformation_1d37a9a23f19400eb3356a05ea2721bc_DeformedNormal_1_Vector3 = 0;
float3 _ComputeDeformation_1d37a9a23f19400eb3356a05ea2721bc_DeformedTangent_2_Vector3 = 0;
Unity_ComputeDeformedVertex_float(IN.VertexID, _ComputeDeformation_1d37a9a23f19400eb3356a05ea2721bc_DeformedPosition_0_Vector3, _ComputeDeformation_1d37a9a23f19400eb3356a05ea2721bc_DeformedNormal_1_Vector3, _ComputeDeformation_1d37a9a23f19400eb3356a05ea2721bc_DeformedTangent_2_Vector3);
#else
float3 _ComputeDeformation_1d37a9a23f19400eb3356a05ea2721bc_DeformedPosition_0_Vector3 = IN.ObjectSpacePosition;
float3 _ComputeDeformation_1d37a9a23f19400eb3356a05ea2721bc_DeformedNormal_1_Vector3 = IN.ObjectSpaceNormal;
float3 _ComputeDeformation_1d37a9a23f19400eb3356a05ea2721bc_DeformedTangent_2_Vector3 = IN.ObjectSpaceTangent;
#endif
description.Position = _ComputeDeformation_1d37a9a23f19400eb3356a05ea2721bc_DeformedPosition_0_Vector3;
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
    output.VertexID =                                   input.vertexID;
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
#define ATTRIBUTES_NEED_VERTEXID
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
 uint vertexID : VERTEXID_SEMANTIC;
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
 uint VertexID;
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
float _ComputeMeshIndex;
float4 _ColorMap_TexelSize;
float4 _SkinMaskTexture_TexelSize;
float4 _DarkMaskTexture_TexelSize;
float4 _DirtColor;
float4 _SkinColor;
float _DarkAmount;
float4 _DirtMaskTexture_TexelSize;
float _DirtAmount;
float4 _EyeEdgeMaskTexture_TexelSize;
float _EyelinerAmount;
float _SkinColorAmount;
float4 _BaseColor;
float _ExploderPulse;
UNITY_TEXTURE_STREAMING_DEBUG_VARS;
CBUFFER_END

#if defined(DOTS_INSTANCING_ON)
// DOTS instancing definitions
UNITY_DOTS_INSTANCING_START(MaterialPropertyMetadata)
    UNITY_DOTS_INSTANCED_PROP_OVERRIDE_SUPPORTED(float, _ComputeMeshIndex)
    UNITY_DOTS_INSTANCED_PROP_OVERRIDE_SUPPORTED(float4, _SkinColor)
    UNITY_DOTS_INSTANCED_PROP_OVERRIDE_SUPPORTED(float4, _BaseColor)
UNITY_DOTS_INSTANCING_END(MaterialPropertyMetadata)
// DOTS instancing usage macros
#define UNITY_ACCESS_HYBRID_INSTANCED_PROP(var, type) UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(type, var)
#elif defined(UNITY_INSTANCING_ENABLED)
// Unity instancing definitions
UNITY_INSTANCING_BUFFER_START(SGPerInstanceData)
    UNITY_DEFINE_INSTANCED_PROP(float, _ComputeMeshIndex)
    UNITY_DEFINE_INSTANCED_PROP(float4, _SkinColor)
    UNITY_DEFINE_INSTANCED_PROP(float4, _BaseColor)
UNITY_INSTANCING_BUFFER_END(SGPerInstanceData)
// Unity instancing usage macros
#define UNITY_ACCESS_HYBRID_INSTANCED_PROP(var, type) UNITY_ACCESS_INSTANCED_PROP(SGPerInstanceData, var)
#else
#define UNITY_ACCESS_HYBRID_INSTANCED_PROP(var, type) var
#endif

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
float _CutsAmount;
float4 _CutsColor;
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

struct DeformedVertexData
{
float3 Position;
float3 Normal;
float3 Tangent;
};
uniform StructuredBuffer<DeformedVertexData> _DeformedMeshData : register(t1);

void Unity_ComputeDeformedVertex_float(uint vertexID, out float3 positionOut, out float3 normalOut, out float3 tangentOut)
{
const DeformedVertexData vertexData = _DeformedMeshData[asuint(UNITY_ACCESS_HYBRID_INSTANCED_PROP(_ComputeMeshIndex, float)) + vertexID];
positionOut = vertexData.Position;
normalOut = vertexData.Normal;
tangentOut = vertexData.Tangent;
}

// Custom interpolators pre vertex
/* WARNING: $splice Could not find named fragment 'CustomInterpolatorPreVertex' */

// Graph Vertex
struct VertexDescription
{
float3 Position;
float3 Normal;
float3 Tangent;
};

VertexDescription VertexDescriptionFunction(VertexDescriptionInputs IN)
{
VertexDescription description = (VertexDescription)0;
#if defined(UNITY_DOTS_INSTANCING_ENABLED)
float3 _ComputeDeformation_1d37a9a23f19400eb3356a05ea2721bc_DeformedPosition_0_Vector3 = 0;
float3 _ComputeDeformation_1d37a9a23f19400eb3356a05ea2721bc_DeformedNormal_1_Vector3 = 0;
float3 _ComputeDeformation_1d37a9a23f19400eb3356a05ea2721bc_DeformedTangent_2_Vector3 = 0;
Unity_ComputeDeformedVertex_float(IN.VertexID, _ComputeDeformation_1d37a9a23f19400eb3356a05ea2721bc_DeformedPosition_0_Vector3, _ComputeDeformation_1d37a9a23f19400eb3356a05ea2721bc_DeformedNormal_1_Vector3, _ComputeDeformation_1d37a9a23f19400eb3356a05ea2721bc_DeformedTangent_2_Vector3);
#else
float3 _ComputeDeformation_1d37a9a23f19400eb3356a05ea2721bc_DeformedPosition_0_Vector3 = IN.ObjectSpacePosition;
float3 _ComputeDeformation_1d37a9a23f19400eb3356a05ea2721bc_DeformedNormal_1_Vector3 = IN.ObjectSpaceNormal;
float3 _ComputeDeformation_1d37a9a23f19400eb3356a05ea2721bc_DeformedTangent_2_Vector3 = IN.ObjectSpaceTangent;
#endif
description.Position = _ComputeDeformation_1d37a9a23f19400eb3356a05ea2721bc_DeformedPosition_0_Vector3;
description.Normal = _ComputeDeformation_1d37a9a23f19400eb3356a05ea2721bc_DeformedNormal_1_Vector3;
description.Tangent = _ComputeDeformation_1d37a9a23f19400eb3356a05ea2721bc_DeformedTangent_2_Vector3;
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
    output.VertexID =                                   input.vertexID;
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
#define ATTRIBUTES_NEED_VERTEXID
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
 uint vertexID : VERTEXID_SEMANTIC;
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
 uint VertexID;
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
float _ComputeMeshIndex;
float4 _ColorMap_TexelSize;
float4 _SkinMaskTexture_TexelSize;
float4 _DarkMaskTexture_TexelSize;
float4 _DirtColor;
float4 _SkinColor;
float _DarkAmount;
float4 _DirtMaskTexture_TexelSize;
float _DirtAmount;
float4 _EyeEdgeMaskTexture_TexelSize;
float _EyelinerAmount;
float _SkinColorAmount;
float4 _BaseColor;
float _ExploderPulse;
UNITY_TEXTURE_STREAMING_DEBUG_VARS;
CBUFFER_END

#if defined(DOTS_INSTANCING_ON)
// DOTS instancing definitions
UNITY_DOTS_INSTANCING_START(MaterialPropertyMetadata)
    UNITY_DOTS_INSTANCED_PROP_OVERRIDE_SUPPORTED(float, _ComputeMeshIndex)
    UNITY_DOTS_INSTANCED_PROP_OVERRIDE_SUPPORTED(float4, _SkinColor)
    UNITY_DOTS_INSTANCED_PROP_OVERRIDE_SUPPORTED(float4, _BaseColor)
UNITY_DOTS_INSTANCING_END(MaterialPropertyMetadata)
// DOTS instancing usage macros
#define UNITY_ACCESS_HYBRID_INSTANCED_PROP(var, type) UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(type, var)
#elif defined(UNITY_INSTANCING_ENABLED)
// Unity instancing definitions
UNITY_INSTANCING_BUFFER_START(SGPerInstanceData)
    UNITY_DEFINE_INSTANCED_PROP(float, _ComputeMeshIndex)
    UNITY_DEFINE_INSTANCED_PROP(float4, _SkinColor)
    UNITY_DEFINE_INSTANCED_PROP(float4, _BaseColor)
UNITY_INSTANCING_BUFFER_END(SGPerInstanceData)
// Unity instancing usage macros
#define UNITY_ACCESS_HYBRID_INSTANCED_PROP(var, type) UNITY_ACCESS_INSTANCED_PROP(SGPerInstanceData, var)
#else
#define UNITY_ACCESS_HYBRID_INSTANCED_PROP(var, type) var
#endif

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
float _CutsAmount;
float4 _CutsColor;
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

struct DeformedVertexData
{
float3 Position;
float3 Normal;
float3 Tangent;
};
uniform StructuredBuffer<DeformedVertexData> _DeformedMeshData : register(t1);

void Unity_ComputeDeformedVertex_float(uint vertexID, out float3 positionOut, out float3 normalOut, out float3 tangentOut)
{
const DeformedVertexData vertexData = _DeformedMeshData[asuint(UNITY_ACCESS_HYBRID_INSTANCED_PROP(_ComputeMeshIndex, float)) + vertexID];
positionOut = vertexData.Position;
normalOut = vertexData.Normal;
tangentOut = vertexData.Tangent;
}

// Custom interpolators pre vertex
/* WARNING: $splice Could not find named fragment 'CustomInterpolatorPreVertex' */

// Graph Vertex
struct VertexDescription
{
float3 Position;
float3 Normal;
float3 Tangent;
};

VertexDescription VertexDescriptionFunction(VertexDescriptionInputs IN)
{
VertexDescription description = (VertexDescription)0;
#if defined(UNITY_DOTS_INSTANCING_ENABLED)
float3 _ComputeDeformation_1d37a9a23f19400eb3356a05ea2721bc_DeformedPosition_0_Vector3 = 0;
float3 _ComputeDeformation_1d37a9a23f19400eb3356a05ea2721bc_DeformedNormal_1_Vector3 = 0;
float3 _ComputeDeformation_1d37a9a23f19400eb3356a05ea2721bc_DeformedTangent_2_Vector3 = 0;
Unity_ComputeDeformedVertex_float(IN.VertexID, _ComputeDeformation_1d37a9a23f19400eb3356a05ea2721bc_DeformedPosition_0_Vector3, _ComputeDeformation_1d37a9a23f19400eb3356a05ea2721bc_DeformedNormal_1_Vector3, _ComputeDeformation_1d37a9a23f19400eb3356a05ea2721bc_DeformedTangent_2_Vector3);
#else
float3 _ComputeDeformation_1d37a9a23f19400eb3356a05ea2721bc_DeformedPosition_0_Vector3 = IN.ObjectSpacePosition;
float3 _ComputeDeformation_1d37a9a23f19400eb3356a05ea2721bc_DeformedNormal_1_Vector3 = IN.ObjectSpaceNormal;
float3 _ComputeDeformation_1d37a9a23f19400eb3356a05ea2721bc_DeformedTangent_2_Vector3 = IN.ObjectSpaceTangent;
#endif
description.Position = _ComputeDeformation_1d37a9a23f19400eb3356a05ea2721bc_DeformedPosition_0_Vector3;
description.Normal = _ComputeDeformation_1d37a9a23f19400eb3356a05ea2721bc_DeformedNormal_1_Vector3;
description.Tangent = _ComputeDeformation_1d37a9a23f19400eb3356a05ea2721bc_DeformedTangent_2_Vector3;
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
float3 NormalTS;
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
    output.VertexID =                                   input.vertexID;
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
#define ATTRIBUTES_NEED_VERTEXID
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
 uint vertexID : VERTEXID_SEMANTIC;
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
 float3 TimeParameters;
};
struct VertexDescriptionInputs
{
 float3 ObjectSpaceNormal;
 float3 ObjectSpaceTangent;
 float3 ObjectSpacePosition;
 uint VertexID;
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
float _ComputeMeshIndex;
float4 _ColorMap_TexelSize;
float4 _SkinMaskTexture_TexelSize;
float4 _DarkMaskTexture_TexelSize;
float4 _DirtColor;
float4 _SkinColor;
float _DarkAmount;
float4 _DirtMaskTexture_TexelSize;
float _DirtAmount;
float4 _EyeEdgeMaskTexture_TexelSize;
float _EyelinerAmount;
float _SkinColorAmount;
float4 _BaseColor;
float _ExploderPulse;
UNITY_TEXTURE_STREAMING_DEBUG_VARS;
CBUFFER_END

#if defined(DOTS_INSTANCING_ON)
// DOTS instancing definitions
UNITY_DOTS_INSTANCING_START(MaterialPropertyMetadata)
    UNITY_DOTS_INSTANCED_PROP_OVERRIDE_SUPPORTED(float, _ComputeMeshIndex)
    UNITY_DOTS_INSTANCED_PROP_OVERRIDE_SUPPORTED(float4, _SkinColor)
    UNITY_DOTS_INSTANCED_PROP_OVERRIDE_SUPPORTED(float4, _BaseColor)
UNITY_DOTS_INSTANCING_END(MaterialPropertyMetadata)
// DOTS instancing usage macros
#define UNITY_ACCESS_HYBRID_INSTANCED_PROP(var, type) UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(type, var)
#elif defined(UNITY_INSTANCING_ENABLED)
// Unity instancing definitions
UNITY_INSTANCING_BUFFER_START(SGPerInstanceData)
    UNITY_DEFINE_INSTANCED_PROP(float, _ComputeMeshIndex)
    UNITY_DEFINE_INSTANCED_PROP(float4, _SkinColor)
    UNITY_DEFINE_INSTANCED_PROP(float4, _BaseColor)
UNITY_INSTANCING_BUFFER_END(SGPerInstanceData)
// Unity instancing usage macros
#define UNITY_ACCESS_HYBRID_INSTANCED_PROP(var, type) UNITY_ACCESS_INSTANCED_PROP(SGPerInstanceData, var)
#else
#define UNITY_ACCESS_HYBRID_INSTANCED_PROP(var, type) var
#endif

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
float _CutsAmount;
float4 _CutsColor;
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

struct DeformedVertexData
{
float3 Position;
float3 Normal;
float3 Tangent;
};
uniform StructuredBuffer<DeformedVertexData> _DeformedMeshData : register(t1);

void Unity_ComputeDeformedVertex_float(uint vertexID, out float3 positionOut, out float3 normalOut, out float3 tangentOut)
{
const DeformedVertexData vertexData = _DeformedMeshData[asuint(UNITY_ACCESS_HYBRID_INSTANCED_PROP(_ComputeMeshIndex, float)) + vertexID];
positionOut = vertexData.Position;
normalOut = vertexData.Normal;
tangentOut = vertexData.Tangent;
}

void Unity_Lerp_float4(float4 A, float4 B, float4 T, out float4 Out)
{
    Out = lerp(A, B, T);
}

void Unity_OneMinus_float(float In, out float Out)
{
    Out = 1 - In;
}

void Unity_Clamp_float(float In, float Min, float Max, out float Out)
{
    Out = clamp(In, Min, Max);
}

void Unity_Multiply_float4_float4(float4 A, float4 B, out float4 Out)
{
Out = A * B;
}

void Unity_Multiply_float_float(float A, float B, out float Out)
{
Out = A * B;
}

void Unity_Sine_float(float In, out float Out)
{
    Out = sin(In);
}

void Unity_Add_float(float A, float B, out float Out)
{
    Out = A + B;
}

// Custom interpolators pre vertex
/* WARNING: $splice Could not find named fragment 'CustomInterpolatorPreVertex' */

// Graph Vertex
struct VertexDescription
{
float3 Position;
float3 Normal;
float3 Tangent;
};

VertexDescription VertexDescriptionFunction(VertexDescriptionInputs IN)
{
VertexDescription description = (VertexDescription)0;
#if defined(UNITY_DOTS_INSTANCING_ENABLED)
float3 _ComputeDeformation_1d37a9a23f19400eb3356a05ea2721bc_DeformedPosition_0_Vector3 = 0;
float3 _ComputeDeformation_1d37a9a23f19400eb3356a05ea2721bc_DeformedNormal_1_Vector3 = 0;
float3 _ComputeDeformation_1d37a9a23f19400eb3356a05ea2721bc_DeformedTangent_2_Vector3 = 0;
Unity_ComputeDeformedVertex_float(IN.VertexID, _ComputeDeformation_1d37a9a23f19400eb3356a05ea2721bc_DeformedPosition_0_Vector3, _ComputeDeformation_1d37a9a23f19400eb3356a05ea2721bc_DeformedNormal_1_Vector3, _ComputeDeformation_1d37a9a23f19400eb3356a05ea2721bc_DeformedTangent_2_Vector3);
#else
float3 _ComputeDeformation_1d37a9a23f19400eb3356a05ea2721bc_DeformedPosition_0_Vector3 = IN.ObjectSpacePosition;
float3 _ComputeDeformation_1d37a9a23f19400eb3356a05ea2721bc_DeformedNormal_1_Vector3 = IN.ObjectSpaceNormal;
float3 _ComputeDeformation_1d37a9a23f19400eb3356a05ea2721bc_DeformedTangent_2_Vector3 = IN.ObjectSpaceTangent;
#endif
description.Position = _ComputeDeformation_1d37a9a23f19400eb3356a05ea2721bc_DeformedPosition_0_Vector3;
description.Normal = _ComputeDeformation_1d37a9a23f19400eb3356a05ea2721bc_DeformedNormal_1_Vector3;
description.Tangent = _ComputeDeformation_1d37a9a23f19400eb3356a05ea2721bc_DeformedTangent_2_Vector3;
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
float3 BaseColor;
float3 Emission;
};

SurfaceDescription SurfaceDescriptionFunction(SurfaceDescriptionInputs IN)
{
SurfaceDescription surface = (SurfaceDescription)0;
float4 Color_9e0f8909aabe4b33b699f1d9bf4443dd = IsGammaSpace() ? float4(0, 0, 0, 0) : float4(SRGBToLinear(float3(0, 0, 0)), 0);
float4 _Property_ecf2fe5309e54977b870684c8d900a70_Out_0_Vector4 = UNITY_ACCESS_HYBRID_INSTANCED_PROP(_SkinColor, float4);
float4 _Property_dee2f69732114c579841770d3a5f1ce9_Out_0_Vector4 = _CutsColor;
float4 _Property_2152e36dab12470385b94206ce043312_Out_0_Vector4 = _DirtColor;
float4 Color_1d5d22b78f8b4dc6b677715e3c97350d = IsGammaSpace() ? float4(0, 0, 0, 0) : float4(SRGBToLinear(float3(0, 0, 0)), 0);
UnityTexture2D _Property_29217945295d45b482fdf43563ceafe1_Out_0_Texture2D = UnityBuildTexture2DStructNoScale(_ColorMap);
float4 _SampleTexture2D_84a95f63ac39426f99979a9f9613953a_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_29217945295d45b482fdf43563ceafe1_Out_0_Texture2D.tex, _Property_29217945295d45b482fdf43563ceafe1_Out_0_Texture2D.samplerstate, _Property_29217945295d45b482fdf43563ceafe1_Out_0_Texture2D.GetTransformedUV(IN.uv0.xy) );
float _SampleTexture2D_84a95f63ac39426f99979a9f9613953a_R_4_Float = _SampleTexture2D_84a95f63ac39426f99979a9f9613953a_RGBA_0_Vector4.r;
float _SampleTexture2D_84a95f63ac39426f99979a9f9613953a_G_5_Float = _SampleTexture2D_84a95f63ac39426f99979a9f9613953a_RGBA_0_Vector4.g;
float _SampleTexture2D_84a95f63ac39426f99979a9f9613953a_B_6_Float = _SampleTexture2D_84a95f63ac39426f99979a9f9613953a_RGBA_0_Vector4.b;
float _SampleTexture2D_84a95f63ac39426f99979a9f9613953a_A_7_Float = _SampleTexture2D_84a95f63ac39426f99979a9f9613953a_RGBA_0_Vector4.a;
float4 Color_22af752f56df4b69a15a74bd003266a0 = IsGammaSpace() ? float4(1, 1, 1, 0) : float4(SRGBToLinear(float3(1, 1, 1)), 0);
UnityTexture2D _Property_c18bcc123f5545dda6a4416e976dce17_Out_0_Texture2D = UnityBuildTexture2DStructNoScale(_DarkMaskTexture);
float4 _SampleTexture2D_6ccf5a041d9a4379a08064554c835a43_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_c18bcc123f5545dda6a4416e976dce17_Out_0_Texture2D.tex, _Property_c18bcc123f5545dda6a4416e976dce17_Out_0_Texture2D.samplerstate, _Property_c18bcc123f5545dda6a4416e976dce17_Out_0_Texture2D.GetTransformedUV(IN.uv0.xy) );
float _SampleTexture2D_6ccf5a041d9a4379a08064554c835a43_R_4_Float = _SampleTexture2D_6ccf5a041d9a4379a08064554c835a43_RGBA_0_Vector4.r;
float _SampleTexture2D_6ccf5a041d9a4379a08064554c835a43_G_5_Float = _SampleTexture2D_6ccf5a041d9a4379a08064554c835a43_RGBA_0_Vector4.g;
float _SampleTexture2D_6ccf5a041d9a4379a08064554c835a43_B_6_Float = _SampleTexture2D_6ccf5a041d9a4379a08064554c835a43_RGBA_0_Vector4.b;
float _SampleTexture2D_6ccf5a041d9a4379a08064554c835a43_A_7_Float = _SampleTexture2D_6ccf5a041d9a4379a08064554c835a43_RGBA_0_Vector4.a;
float _Property_f460bfa9d43848bba59ba7460f20c9c2_Out_0_Float = _DarkAmount;
float4 _Lerp_b97b4bab53a04c6ea870c54dde30e56e_Out_3_Vector4;
Unity_Lerp_float4(Color_22af752f56df4b69a15a74bd003266a0, _SampleTexture2D_6ccf5a041d9a4379a08064554c835a43_RGBA_0_Vector4, (_Property_f460bfa9d43848bba59ba7460f20c9c2_Out_0_Float.xxxx), _Lerp_b97b4bab53a04c6ea870c54dde30e56e_Out_3_Vector4);
float4 _Lerp_5fb58a8630424f0aa299b82d3c6b7cf2_Out_3_Vector4;
Unity_Lerp_float4(Color_1d5d22b78f8b4dc6b677715e3c97350d, _SampleTexture2D_84a95f63ac39426f99979a9f9613953a_RGBA_0_Vector4, _Lerp_b97b4bab53a04c6ea870c54dde30e56e_Out_3_Vector4, _Lerp_5fb58a8630424f0aa299b82d3c6b7cf2_Out_3_Vector4);
UnityTexture2D _Property_f8239e9549eb4afdb04e0add697a9e63_Out_0_Texture2D = UnityBuildTexture2DStructNoScale(_DirtMaskTexture);
float4 _SampleTexture2D_7a735da5e71c436f82fc33c38b3cb722_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_f8239e9549eb4afdb04e0add697a9e63_Out_0_Texture2D.tex, _Property_f8239e9549eb4afdb04e0add697a9e63_Out_0_Texture2D.samplerstate, _Property_f8239e9549eb4afdb04e0add697a9e63_Out_0_Texture2D.GetTransformedUV(IN.uv0.xy) );
float _SampleTexture2D_7a735da5e71c436f82fc33c38b3cb722_R_4_Float = _SampleTexture2D_7a735da5e71c436f82fc33c38b3cb722_RGBA_0_Vector4.r;
float _SampleTexture2D_7a735da5e71c436f82fc33c38b3cb722_G_5_Float = _SampleTexture2D_7a735da5e71c436f82fc33c38b3cb722_RGBA_0_Vector4.g;
float _SampleTexture2D_7a735da5e71c436f82fc33c38b3cb722_B_6_Float = _SampleTexture2D_7a735da5e71c436f82fc33c38b3cb722_RGBA_0_Vector4.b;
float _SampleTexture2D_7a735da5e71c436f82fc33c38b3cb722_A_7_Float = _SampleTexture2D_7a735da5e71c436f82fc33c38b3cb722_RGBA_0_Vector4.a;
float _Property_ef41be1c8664497cbdfb806842079a89_Out_0_Float = _DirtAmount;
float4 _Lerp_aec65eee28314809a1410cf73b04a889_Out_3_Vector4;
Unity_Lerp_float4(Color_22af752f56df4b69a15a74bd003266a0, _SampleTexture2D_7a735da5e71c436f82fc33c38b3cb722_RGBA_0_Vector4, (_Property_ef41be1c8664497cbdfb806842079a89_Out_0_Float.xxxx), _Lerp_aec65eee28314809a1410cf73b04a889_Out_3_Vector4);
float4 _Lerp_1422a096bdfb4539b217cd9460b0b019_Out_3_Vector4;
Unity_Lerp_float4(_Property_2152e36dab12470385b94206ce043312_Out_0_Vector4, _Lerp_5fb58a8630424f0aa299b82d3c6b7cf2_Out_3_Vector4, _Lerp_aec65eee28314809a1410cf73b04a889_Out_3_Vector4, _Lerp_1422a096bdfb4539b217cd9460b0b019_Out_3_Vector4);
UnityTexture2D _Property_cad87ecf436a44458da862f7575dfeec_Out_0_Texture2D = UnityBuildTexture2DStructNoScale(_CutsMaskTexture);
float4 _SampleTexture2D_1937dc0552af4944a98689fd9d6fa9cc_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_cad87ecf436a44458da862f7575dfeec_Out_0_Texture2D.tex, _Property_cad87ecf436a44458da862f7575dfeec_Out_0_Texture2D.samplerstate, _Property_cad87ecf436a44458da862f7575dfeec_Out_0_Texture2D.GetTransformedUV(IN.uv0.xy) );
float _SampleTexture2D_1937dc0552af4944a98689fd9d6fa9cc_R_4_Float = _SampleTexture2D_1937dc0552af4944a98689fd9d6fa9cc_RGBA_0_Vector4.r;
float _SampleTexture2D_1937dc0552af4944a98689fd9d6fa9cc_G_5_Float = _SampleTexture2D_1937dc0552af4944a98689fd9d6fa9cc_RGBA_0_Vector4.g;
float _SampleTexture2D_1937dc0552af4944a98689fd9d6fa9cc_B_6_Float = _SampleTexture2D_1937dc0552af4944a98689fd9d6fa9cc_RGBA_0_Vector4.b;
float _SampleTexture2D_1937dc0552af4944a98689fd9d6fa9cc_A_7_Float = _SampleTexture2D_1937dc0552af4944a98689fd9d6fa9cc_RGBA_0_Vector4.a;
float _Property_092064d1a1804dcb88349a0dfa930d34_Out_0_Float = _CutsAmount;
float4 _Lerp_587eb00b83614171a84a065eb4475d69_Out_3_Vector4;
Unity_Lerp_float4(Color_22af752f56df4b69a15a74bd003266a0, _SampleTexture2D_1937dc0552af4944a98689fd9d6fa9cc_RGBA_0_Vector4, (_Property_092064d1a1804dcb88349a0dfa930d34_Out_0_Float.xxxx), _Lerp_587eb00b83614171a84a065eb4475d69_Out_3_Vector4);
float4 _Lerp_480d292819cd497f84da826a5234e813_Out_3_Vector4;
Unity_Lerp_float4(_Property_dee2f69732114c579841770d3a5f1ce9_Out_0_Vector4, _Lerp_1422a096bdfb4539b217cd9460b0b019_Out_3_Vector4, _Lerp_587eb00b83614171a84a065eb4475d69_Out_3_Vector4, _Lerp_480d292819cd497f84da826a5234e813_Out_3_Vector4);
UnityTexture2D _Property_b2cfc554a3654888b085ea4dadd11e99_Out_0_Texture2D = UnityBuildTexture2DStructNoScale(_SkinMaskTexture);
float4 _SampleTexture2D_864cfb091b7d4331bd7218fdeaf8d858_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_b2cfc554a3654888b085ea4dadd11e99_Out_0_Texture2D.tex, _Property_b2cfc554a3654888b085ea4dadd11e99_Out_0_Texture2D.samplerstate, _Property_b2cfc554a3654888b085ea4dadd11e99_Out_0_Texture2D.GetTransformedUV(IN.uv0.xy) );
float _SampleTexture2D_864cfb091b7d4331bd7218fdeaf8d858_R_4_Float = _SampleTexture2D_864cfb091b7d4331bd7218fdeaf8d858_RGBA_0_Vector4.r;
float _SampleTexture2D_864cfb091b7d4331bd7218fdeaf8d858_G_5_Float = _SampleTexture2D_864cfb091b7d4331bd7218fdeaf8d858_RGBA_0_Vector4.g;
float _SampleTexture2D_864cfb091b7d4331bd7218fdeaf8d858_B_6_Float = _SampleTexture2D_864cfb091b7d4331bd7218fdeaf8d858_RGBA_0_Vector4.b;
float _SampleTexture2D_864cfb091b7d4331bd7218fdeaf8d858_A_7_Float = _SampleTexture2D_864cfb091b7d4331bd7218fdeaf8d858_RGBA_0_Vector4.a;
float _Property_495de44ef11d40cb91ca34953cc899b1_Out_0_Float = _SkinColorAmount;
float4 _Lerp_82953a0631914e78ba370c51d143d891_Out_3_Vector4;
Unity_Lerp_float4(Color_22af752f56df4b69a15a74bd003266a0, _SampleTexture2D_864cfb091b7d4331bd7218fdeaf8d858_RGBA_0_Vector4, (_Property_495de44ef11d40cb91ca34953cc899b1_Out_0_Float.xxxx), _Lerp_82953a0631914e78ba370c51d143d891_Out_3_Vector4);
float4 _Lerp_56dd8066e70d4c85a8d72c30ef200bb7_Out_3_Vector4;
Unity_Lerp_float4(_Property_ecf2fe5309e54977b870684c8d900a70_Out_0_Vector4, _Lerp_480d292819cd497f84da826a5234e813_Out_3_Vector4, _Lerp_82953a0631914e78ba370c51d143d891_Out_3_Vector4, _Lerp_56dd8066e70d4c85a8d72c30ef200bb7_Out_3_Vector4);
float _Property_4208ddc25284429fba969ec5b8970ca4_Out_0_Float = _EyelinerAmount;
float _OneMinus_dfa88fc204374b35898331eae5eba835_Out_1_Float;
Unity_OneMinus_float(_Property_4208ddc25284429fba969ec5b8970ca4_Out_0_Float, _OneMinus_dfa88fc204374b35898331eae5eba835_Out_1_Float);
float _Clamp_a3567345de7b4ba6850279acb3e1bbda_Out_3_Float;
Unity_Clamp_float(_OneMinus_dfa88fc204374b35898331eae5eba835_Out_1_Float, float(0), float(0.5), _Clamp_a3567345de7b4ba6850279acb3e1bbda_Out_3_Float);
UnityTexture2D _Property_0dfa773f4d6f47bc85db4d5cfb627c23_Out_0_Texture2D = UnityBuildTexture2DStructNoScale(_EyeEdgeMaskTexture);
float4 _SampleTexture2D_1e5e254c29e84e2ebe790f4de0afeab7_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_0dfa773f4d6f47bc85db4d5cfb627c23_Out_0_Texture2D.tex, _Property_0dfa773f4d6f47bc85db4d5cfb627c23_Out_0_Texture2D.samplerstate, _Property_0dfa773f4d6f47bc85db4d5cfb627c23_Out_0_Texture2D.GetTransformedUV(IN.uv0.xy) );
float _SampleTexture2D_1e5e254c29e84e2ebe790f4de0afeab7_R_4_Float = _SampleTexture2D_1e5e254c29e84e2ebe790f4de0afeab7_RGBA_0_Vector4.r;
float _SampleTexture2D_1e5e254c29e84e2ebe790f4de0afeab7_G_5_Float = _SampleTexture2D_1e5e254c29e84e2ebe790f4de0afeab7_RGBA_0_Vector4.g;
float _SampleTexture2D_1e5e254c29e84e2ebe790f4de0afeab7_B_6_Float = _SampleTexture2D_1e5e254c29e84e2ebe790f4de0afeab7_RGBA_0_Vector4.b;
float _SampleTexture2D_1e5e254c29e84e2ebe790f4de0afeab7_A_7_Float = _SampleTexture2D_1e5e254c29e84e2ebe790f4de0afeab7_RGBA_0_Vector4.a;
float4 _Lerp_3fc533c198e74a1ebb2c7f7bad6d9762_Out_3_Vector4;
Unity_Lerp_float4(Color_22af752f56df4b69a15a74bd003266a0, (_Clamp_a3567345de7b4ba6850279acb3e1bbda_Out_3_Float.xxxx), _SampleTexture2D_1e5e254c29e84e2ebe790f4de0afeab7_RGBA_0_Vector4, _Lerp_3fc533c198e74a1ebb2c7f7bad6d9762_Out_3_Vector4);
float4 _Lerp_7b945e4c623b447f84028beaef139c78_Out_3_Vector4;
Unity_Lerp_float4(Color_9e0f8909aabe4b33b699f1d9bf4443dd, _Lerp_56dd8066e70d4c85a8d72c30ef200bb7_Out_3_Vector4, _Lerp_3fc533c198e74a1ebb2c7f7bad6d9762_Out_3_Vector4, _Lerp_7b945e4c623b447f84028beaef139c78_Out_3_Vector4);
float4 _EnemyBodyTint_7c6b1708145240cfb235df3b94d53325_Out_0_Vector4 = UNITY_ACCESS_HYBRID_INSTANCED_PROP(_BaseColor, float4);
float4 _BodyReadability_84a204bce97045fc9c6824c3328d4e49_Out_2_Vector4;
Unity_Multiply_float4_float4(_Lerp_7b945e4c623b447f84028beaef139c78_Out_3_Vector4, _EnemyBodyTint_7c6b1708145240cfb235df3b94d53325_Out_0_Vector4, _BodyReadability_84a204bce97045fc9c6824c3328d4e49_Out_2_Vector4);
float _Pulseperiod12seconds_8c95b2a85a75428ea0e1a96e4244cd75_Out_2_Float;
Unity_Multiply_float_float(IN.TimeParameters.x, 5.235988, _Pulseperiod12seconds_8c95b2a85a75428ea0e1a96e4244cd75_Out_2_Float);
float _Exploderrhythm_e111e0eb61ec43eab84b2040404dfcc1_Out_1_Float;
Unity_Sine_float(_Pulseperiod12seconds_8c95b2a85a75428ea0e1a96e4244cd75_Out_2_Float, _Exploderrhythm_e111e0eb61ec43eab84b2040404dfcc1_Out_1_Float);
float _Halfwave_e0c9105d5a604220acf6e06d34303944_Out_2_Float;
Unity_Multiply_float_float(_Exploderrhythm_e111e0eb61ec43eab84b2040404dfcc1_Out_1_Float, 0.5, _Halfwave_e0c9105d5a604220acf6e06d34303944_Out_2_Float);
float _Positivepulse_a9c4de5ed7454cf9b723fa518a26c658_Out_2_Float;
Unity_Add_float(_Halfwave_e0c9105d5a604220acf6e06d34303944_Out_2_Float, float(0.5), _Positivepulse_a9c4de5ed7454cf9b723fa518a26c658_Out_2_Float);
float _Softheartbeat_60e3ab0a1bd6410b8fccad5484f2bd45_Out_2_Float;
Unity_Multiply_float_float(_Positivepulse_a9c4de5ed7454cf9b723fa518a26c658_Out_2_Float, _Positivepulse_a9c4de5ed7454cf9b723fa518a26c658_Out_2_Float, _Softheartbeat_60e3ab0a1bd6410b8fccad5484f2bd45_Out_2_Float);
float _ExploderPulse_78b25de6c19f40b385c67b258dfbd5de_Out_0_Float = _ExploderPulse;
float _Exploderglowstrength_60532f0cb28f4812a2be39e3de2ee660_Out_2_Float;
Unity_Multiply_float_float(_Softheartbeat_60e3ab0a1bd6410b8fccad5484f2bd45_Out_2_Float, _ExploderPulse_78b25de6c19f40b385c67b258dfbd5de_Out_0_Float, _Exploderglowstrength_60532f0cb28f4812a2be39e3de2ee660_Out_2_Float);
float _Pulseandshadowfill_6d61d073f3f3403a9c853596880987e2_Out_2_Float;
Unity_Add_float(_Exploderglowstrength_60532f0cb28f4812a2be39e3de2ee660_Out_2_Float, float(0.075), _Pulseandshadowfill_6d61d073f3f3403a9c853596880987e2_Out_2_Float);
float4 _PaletteShadowFill_b77d58bc87fe4a2e98ca8c704eb8dafc_Out_2_Vector4;
Unity_Multiply_float4_float4(_BodyReadability_84a204bce97045fc9c6824c3328d4e49_Out_2_Vector4, (_Pulseandshadowfill_6d61d073f3f3403a9c853596880987e2_Out_2_Float.xxxx), _PaletteShadowFill_b77d58bc87fe4a2e98ca8c704eb8dafc_Out_2_Vector4);
surface.BaseColor = (_BodyReadability_84a204bce97045fc9c6824c3328d4e49_Out_2_Vector4.xyz);
surface.Emission = (_PaletteShadowFill_b77d58bc87fe4a2e98ca8c704eb8dafc_Out_2_Vector4.xyz);
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
    output.VertexID =                                   input.vertexID;
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
    output.TimeParameters = _TimeParameters.xyz; // This is mainly for LW as HD overwrite this value
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
#define ATTRIBUTES_NEED_VERTEXID
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
 uint vertexID : VERTEXID_SEMANTIC;
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
 uint VertexID;
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
float _ComputeMeshIndex;
float4 _ColorMap_TexelSize;
float4 _SkinMaskTexture_TexelSize;
float4 _DarkMaskTexture_TexelSize;
float4 _DirtColor;
float4 _SkinColor;
float _DarkAmount;
float4 _DirtMaskTexture_TexelSize;
float _DirtAmount;
float4 _EyeEdgeMaskTexture_TexelSize;
float _EyelinerAmount;
float _SkinColorAmount;
float4 _BaseColor;
float _ExploderPulse;
UNITY_TEXTURE_STREAMING_DEBUG_VARS;
CBUFFER_END

#if defined(DOTS_INSTANCING_ON)
// DOTS instancing definitions
UNITY_DOTS_INSTANCING_START(MaterialPropertyMetadata)
    UNITY_DOTS_INSTANCED_PROP_OVERRIDE_SUPPORTED(float, _ComputeMeshIndex)
    UNITY_DOTS_INSTANCED_PROP_OVERRIDE_SUPPORTED(float4, _SkinColor)
    UNITY_DOTS_INSTANCED_PROP_OVERRIDE_SUPPORTED(float4, _BaseColor)
UNITY_DOTS_INSTANCING_END(MaterialPropertyMetadata)
// DOTS instancing usage macros
#define UNITY_ACCESS_HYBRID_INSTANCED_PROP(var, type) UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(type, var)
#elif defined(UNITY_INSTANCING_ENABLED)
// Unity instancing definitions
UNITY_INSTANCING_BUFFER_START(SGPerInstanceData)
    UNITY_DEFINE_INSTANCED_PROP(float, _ComputeMeshIndex)
    UNITY_DEFINE_INSTANCED_PROP(float4, _SkinColor)
    UNITY_DEFINE_INSTANCED_PROP(float4, _BaseColor)
UNITY_INSTANCING_BUFFER_END(SGPerInstanceData)
// Unity instancing usage macros
#define UNITY_ACCESS_HYBRID_INSTANCED_PROP(var, type) UNITY_ACCESS_INSTANCED_PROP(SGPerInstanceData, var)
#else
#define UNITY_ACCESS_HYBRID_INSTANCED_PROP(var, type) var
#endif

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
float _CutsAmount;
float4 _CutsColor;
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

struct DeformedVertexData
{
float3 Position;
float3 Normal;
float3 Tangent;
};
uniform StructuredBuffer<DeformedVertexData> _DeformedMeshData : register(t1);

void Unity_ComputeDeformedVertex_float(uint vertexID, out float3 positionOut, out float3 normalOut, out float3 tangentOut)
{
const DeformedVertexData vertexData = _DeformedMeshData[asuint(UNITY_ACCESS_HYBRID_INSTANCED_PROP(_ComputeMeshIndex, float)) + vertexID];
positionOut = vertexData.Position;
normalOut = vertexData.Normal;
tangentOut = vertexData.Tangent;
}

// Custom interpolators pre vertex
/* WARNING: $splice Could not find named fragment 'CustomInterpolatorPreVertex' */

// Graph Vertex
struct VertexDescription
{
float3 Position;
float3 Normal;
float3 Tangent;
};

VertexDescription VertexDescriptionFunction(VertexDescriptionInputs IN)
{
VertexDescription description = (VertexDescription)0;
#if defined(UNITY_DOTS_INSTANCING_ENABLED)
float3 _ComputeDeformation_1d37a9a23f19400eb3356a05ea2721bc_DeformedPosition_0_Vector3 = 0;
float3 _ComputeDeformation_1d37a9a23f19400eb3356a05ea2721bc_DeformedNormal_1_Vector3 = 0;
float3 _ComputeDeformation_1d37a9a23f19400eb3356a05ea2721bc_DeformedTangent_2_Vector3 = 0;
Unity_ComputeDeformedVertex_float(IN.VertexID, _ComputeDeformation_1d37a9a23f19400eb3356a05ea2721bc_DeformedPosition_0_Vector3, _ComputeDeformation_1d37a9a23f19400eb3356a05ea2721bc_DeformedNormal_1_Vector3, _ComputeDeformation_1d37a9a23f19400eb3356a05ea2721bc_DeformedTangent_2_Vector3);
#else
float3 _ComputeDeformation_1d37a9a23f19400eb3356a05ea2721bc_DeformedPosition_0_Vector3 = IN.ObjectSpacePosition;
float3 _ComputeDeformation_1d37a9a23f19400eb3356a05ea2721bc_DeformedNormal_1_Vector3 = IN.ObjectSpaceNormal;
float3 _ComputeDeformation_1d37a9a23f19400eb3356a05ea2721bc_DeformedTangent_2_Vector3 = IN.ObjectSpaceTangent;
#endif
description.Position = _ComputeDeformation_1d37a9a23f19400eb3356a05ea2721bc_DeformedPosition_0_Vector3;
description.Normal = _ComputeDeformation_1d37a9a23f19400eb3356a05ea2721bc_DeformedNormal_1_Vector3;
description.Tangent = _ComputeDeformation_1d37a9a23f19400eb3356a05ea2721bc_DeformedTangent_2_Vector3;
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
    output.VertexID =                                   input.vertexID;
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
#define ATTRIBUTES_NEED_VERTEXID
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
 uint vertexID : VERTEXID_SEMANTIC;
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
 uint VertexID;
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
float _ComputeMeshIndex;
float4 _ColorMap_TexelSize;
float4 _SkinMaskTexture_TexelSize;
float4 _DarkMaskTexture_TexelSize;
float4 _DirtColor;
float4 _SkinColor;
float _DarkAmount;
float4 _DirtMaskTexture_TexelSize;
float _DirtAmount;
float4 _EyeEdgeMaskTexture_TexelSize;
float _EyelinerAmount;
float _SkinColorAmount;
float4 _BaseColor;
float _ExploderPulse;
UNITY_TEXTURE_STREAMING_DEBUG_VARS;
CBUFFER_END

#if defined(DOTS_INSTANCING_ON)
// DOTS instancing definitions
UNITY_DOTS_INSTANCING_START(MaterialPropertyMetadata)
    UNITY_DOTS_INSTANCED_PROP_OVERRIDE_SUPPORTED(float, _ComputeMeshIndex)
    UNITY_DOTS_INSTANCED_PROP_OVERRIDE_SUPPORTED(float4, _SkinColor)
    UNITY_DOTS_INSTANCED_PROP_OVERRIDE_SUPPORTED(float4, _BaseColor)
UNITY_DOTS_INSTANCING_END(MaterialPropertyMetadata)
// DOTS instancing usage macros
#define UNITY_ACCESS_HYBRID_INSTANCED_PROP(var, type) UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(type, var)
#elif defined(UNITY_INSTANCING_ENABLED)
// Unity instancing definitions
UNITY_INSTANCING_BUFFER_START(SGPerInstanceData)
    UNITY_DEFINE_INSTANCED_PROP(float, _ComputeMeshIndex)
    UNITY_DEFINE_INSTANCED_PROP(float4, _SkinColor)
    UNITY_DEFINE_INSTANCED_PROP(float4, _BaseColor)
UNITY_INSTANCING_BUFFER_END(SGPerInstanceData)
// Unity instancing usage macros
#define UNITY_ACCESS_HYBRID_INSTANCED_PROP(var, type) UNITY_ACCESS_INSTANCED_PROP(SGPerInstanceData, var)
#else
#define UNITY_ACCESS_HYBRID_INSTANCED_PROP(var, type) var
#endif

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
float _CutsAmount;
float4 _CutsColor;
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

struct DeformedVertexData
{
float3 Position;
float3 Normal;
float3 Tangent;
};
uniform StructuredBuffer<DeformedVertexData> _DeformedMeshData : register(t1);

void Unity_ComputeDeformedVertex_float(uint vertexID, out float3 positionOut, out float3 normalOut, out float3 tangentOut)
{
const DeformedVertexData vertexData = _DeformedMeshData[asuint(UNITY_ACCESS_HYBRID_INSTANCED_PROP(_ComputeMeshIndex, float)) + vertexID];
positionOut = vertexData.Position;
normalOut = vertexData.Normal;
tangentOut = vertexData.Tangent;
}

void Unity_Lerp_float4(float4 A, float4 B, float4 T, out float4 Out)
{
    Out = lerp(A, B, T);
}

void Unity_OneMinus_float(float In, out float Out)
{
    Out = 1 - In;
}

void Unity_Clamp_float(float In, float Min, float Max, out float Out)
{
    Out = clamp(In, Min, Max);
}

void Unity_Multiply_float4_float4(float4 A, float4 B, out float4 Out)
{
Out = A * B;
}

// Custom interpolators pre vertex
/* WARNING: $splice Could not find named fragment 'CustomInterpolatorPreVertex' */

// Graph Vertex
struct VertexDescription
{
float3 Position;
float3 Normal;
float3 Tangent;
};

VertexDescription VertexDescriptionFunction(VertexDescriptionInputs IN)
{
VertexDescription description = (VertexDescription)0;
#if defined(UNITY_DOTS_INSTANCING_ENABLED)
float3 _ComputeDeformation_1d37a9a23f19400eb3356a05ea2721bc_DeformedPosition_0_Vector3 = 0;
float3 _ComputeDeformation_1d37a9a23f19400eb3356a05ea2721bc_DeformedNormal_1_Vector3 = 0;
float3 _ComputeDeformation_1d37a9a23f19400eb3356a05ea2721bc_DeformedTangent_2_Vector3 = 0;
Unity_ComputeDeformedVertex_float(IN.VertexID, _ComputeDeformation_1d37a9a23f19400eb3356a05ea2721bc_DeformedPosition_0_Vector3, _ComputeDeformation_1d37a9a23f19400eb3356a05ea2721bc_DeformedNormal_1_Vector3, _ComputeDeformation_1d37a9a23f19400eb3356a05ea2721bc_DeformedTangent_2_Vector3);
#else
float3 _ComputeDeformation_1d37a9a23f19400eb3356a05ea2721bc_DeformedPosition_0_Vector3 = IN.ObjectSpacePosition;
float3 _ComputeDeformation_1d37a9a23f19400eb3356a05ea2721bc_DeformedNormal_1_Vector3 = IN.ObjectSpaceNormal;
float3 _ComputeDeformation_1d37a9a23f19400eb3356a05ea2721bc_DeformedTangent_2_Vector3 = IN.ObjectSpaceTangent;
#endif
description.Position = _ComputeDeformation_1d37a9a23f19400eb3356a05ea2721bc_DeformedPosition_0_Vector3;
description.Normal = _ComputeDeformation_1d37a9a23f19400eb3356a05ea2721bc_DeformedNormal_1_Vector3;
description.Tangent = _ComputeDeformation_1d37a9a23f19400eb3356a05ea2721bc_DeformedTangent_2_Vector3;
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
float3 BaseColor;
};

SurfaceDescription SurfaceDescriptionFunction(SurfaceDescriptionInputs IN)
{
SurfaceDescription surface = (SurfaceDescription)0;
float4 Color_9e0f8909aabe4b33b699f1d9bf4443dd = IsGammaSpace() ? float4(0, 0, 0, 0) : float4(SRGBToLinear(float3(0, 0, 0)), 0);
float4 _Property_ecf2fe5309e54977b870684c8d900a70_Out_0_Vector4 = UNITY_ACCESS_HYBRID_INSTANCED_PROP(_SkinColor, float4);
float4 _Property_dee2f69732114c579841770d3a5f1ce9_Out_0_Vector4 = _CutsColor;
float4 _Property_2152e36dab12470385b94206ce043312_Out_0_Vector4 = _DirtColor;
float4 Color_1d5d22b78f8b4dc6b677715e3c97350d = IsGammaSpace() ? float4(0, 0, 0, 0) : float4(SRGBToLinear(float3(0, 0, 0)), 0);
UnityTexture2D _Property_29217945295d45b482fdf43563ceafe1_Out_0_Texture2D = UnityBuildTexture2DStructNoScale(_ColorMap);
float4 _SampleTexture2D_84a95f63ac39426f99979a9f9613953a_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_29217945295d45b482fdf43563ceafe1_Out_0_Texture2D.tex, _Property_29217945295d45b482fdf43563ceafe1_Out_0_Texture2D.samplerstate, _Property_29217945295d45b482fdf43563ceafe1_Out_0_Texture2D.GetTransformedUV(IN.uv0.xy) );
float _SampleTexture2D_84a95f63ac39426f99979a9f9613953a_R_4_Float = _SampleTexture2D_84a95f63ac39426f99979a9f9613953a_RGBA_0_Vector4.r;
float _SampleTexture2D_84a95f63ac39426f99979a9f9613953a_G_5_Float = _SampleTexture2D_84a95f63ac39426f99979a9f9613953a_RGBA_0_Vector4.g;
float _SampleTexture2D_84a95f63ac39426f99979a9f9613953a_B_6_Float = _SampleTexture2D_84a95f63ac39426f99979a9f9613953a_RGBA_0_Vector4.b;
float _SampleTexture2D_84a95f63ac39426f99979a9f9613953a_A_7_Float = _SampleTexture2D_84a95f63ac39426f99979a9f9613953a_RGBA_0_Vector4.a;
float4 Color_22af752f56df4b69a15a74bd003266a0 = IsGammaSpace() ? float4(1, 1, 1, 0) : float4(SRGBToLinear(float3(1, 1, 1)), 0);
UnityTexture2D _Property_c18bcc123f5545dda6a4416e976dce17_Out_0_Texture2D = UnityBuildTexture2DStructNoScale(_DarkMaskTexture);
float4 _SampleTexture2D_6ccf5a041d9a4379a08064554c835a43_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_c18bcc123f5545dda6a4416e976dce17_Out_0_Texture2D.tex, _Property_c18bcc123f5545dda6a4416e976dce17_Out_0_Texture2D.samplerstate, _Property_c18bcc123f5545dda6a4416e976dce17_Out_0_Texture2D.GetTransformedUV(IN.uv0.xy) );
float _SampleTexture2D_6ccf5a041d9a4379a08064554c835a43_R_4_Float = _SampleTexture2D_6ccf5a041d9a4379a08064554c835a43_RGBA_0_Vector4.r;
float _SampleTexture2D_6ccf5a041d9a4379a08064554c835a43_G_5_Float = _SampleTexture2D_6ccf5a041d9a4379a08064554c835a43_RGBA_0_Vector4.g;
float _SampleTexture2D_6ccf5a041d9a4379a08064554c835a43_B_6_Float = _SampleTexture2D_6ccf5a041d9a4379a08064554c835a43_RGBA_0_Vector4.b;
float _SampleTexture2D_6ccf5a041d9a4379a08064554c835a43_A_7_Float = _SampleTexture2D_6ccf5a041d9a4379a08064554c835a43_RGBA_0_Vector4.a;
float _Property_f460bfa9d43848bba59ba7460f20c9c2_Out_0_Float = _DarkAmount;
float4 _Lerp_b97b4bab53a04c6ea870c54dde30e56e_Out_3_Vector4;
Unity_Lerp_float4(Color_22af752f56df4b69a15a74bd003266a0, _SampleTexture2D_6ccf5a041d9a4379a08064554c835a43_RGBA_0_Vector4, (_Property_f460bfa9d43848bba59ba7460f20c9c2_Out_0_Float.xxxx), _Lerp_b97b4bab53a04c6ea870c54dde30e56e_Out_3_Vector4);
float4 _Lerp_5fb58a8630424f0aa299b82d3c6b7cf2_Out_3_Vector4;
Unity_Lerp_float4(Color_1d5d22b78f8b4dc6b677715e3c97350d, _SampleTexture2D_84a95f63ac39426f99979a9f9613953a_RGBA_0_Vector4, _Lerp_b97b4bab53a04c6ea870c54dde30e56e_Out_3_Vector4, _Lerp_5fb58a8630424f0aa299b82d3c6b7cf2_Out_3_Vector4);
UnityTexture2D _Property_f8239e9549eb4afdb04e0add697a9e63_Out_0_Texture2D = UnityBuildTexture2DStructNoScale(_DirtMaskTexture);
float4 _SampleTexture2D_7a735da5e71c436f82fc33c38b3cb722_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_f8239e9549eb4afdb04e0add697a9e63_Out_0_Texture2D.tex, _Property_f8239e9549eb4afdb04e0add697a9e63_Out_0_Texture2D.samplerstate, _Property_f8239e9549eb4afdb04e0add697a9e63_Out_0_Texture2D.GetTransformedUV(IN.uv0.xy) );
float _SampleTexture2D_7a735da5e71c436f82fc33c38b3cb722_R_4_Float = _SampleTexture2D_7a735da5e71c436f82fc33c38b3cb722_RGBA_0_Vector4.r;
float _SampleTexture2D_7a735da5e71c436f82fc33c38b3cb722_G_5_Float = _SampleTexture2D_7a735da5e71c436f82fc33c38b3cb722_RGBA_0_Vector4.g;
float _SampleTexture2D_7a735da5e71c436f82fc33c38b3cb722_B_6_Float = _SampleTexture2D_7a735da5e71c436f82fc33c38b3cb722_RGBA_0_Vector4.b;
float _SampleTexture2D_7a735da5e71c436f82fc33c38b3cb722_A_7_Float = _SampleTexture2D_7a735da5e71c436f82fc33c38b3cb722_RGBA_0_Vector4.a;
float _Property_ef41be1c8664497cbdfb806842079a89_Out_0_Float = _DirtAmount;
float4 _Lerp_aec65eee28314809a1410cf73b04a889_Out_3_Vector4;
Unity_Lerp_float4(Color_22af752f56df4b69a15a74bd003266a0, _SampleTexture2D_7a735da5e71c436f82fc33c38b3cb722_RGBA_0_Vector4, (_Property_ef41be1c8664497cbdfb806842079a89_Out_0_Float.xxxx), _Lerp_aec65eee28314809a1410cf73b04a889_Out_3_Vector4);
float4 _Lerp_1422a096bdfb4539b217cd9460b0b019_Out_3_Vector4;
Unity_Lerp_float4(_Property_2152e36dab12470385b94206ce043312_Out_0_Vector4, _Lerp_5fb58a8630424f0aa299b82d3c6b7cf2_Out_3_Vector4, _Lerp_aec65eee28314809a1410cf73b04a889_Out_3_Vector4, _Lerp_1422a096bdfb4539b217cd9460b0b019_Out_3_Vector4);
UnityTexture2D _Property_cad87ecf436a44458da862f7575dfeec_Out_0_Texture2D = UnityBuildTexture2DStructNoScale(_CutsMaskTexture);
float4 _SampleTexture2D_1937dc0552af4944a98689fd9d6fa9cc_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_cad87ecf436a44458da862f7575dfeec_Out_0_Texture2D.tex, _Property_cad87ecf436a44458da862f7575dfeec_Out_0_Texture2D.samplerstate, _Property_cad87ecf436a44458da862f7575dfeec_Out_0_Texture2D.GetTransformedUV(IN.uv0.xy) );
float _SampleTexture2D_1937dc0552af4944a98689fd9d6fa9cc_R_4_Float = _SampleTexture2D_1937dc0552af4944a98689fd9d6fa9cc_RGBA_0_Vector4.r;
float _SampleTexture2D_1937dc0552af4944a98689fd9d6fa9cc_G_5_Float = _SampleTexture2D_1937dc0552af4944a98689fd9d6fa9cc_RGBA_0_Vector4.g;
float _SampleTexture2D_1937dc0552af4944a98689fd9d6fa9cc_B_6_Float = _SampleTexture2D_1937dc0552af4944a98689fd9d6fa9cc_RGBA_0_Vector4.b;
float _SampleTexture2D_1937dc0552af4944a98689fd9d6fa9cc_A_7_Float = _SampleTexture2D_1937dc0552af4944a98689fd9d6fa9cc_RGBA_0_Vector4.a;
float _Property_092064d1a1804dcb88349a0dfa930d34_Out_0_Float = _CutsAmount;
float4 _Lerp_587eb00b83614171a84a065eb4475d69_Out_3_Vector4;
Unity_Lerp_float4(Color_22af752f56df4b69a15a74bd003266a0, _SampleTexture2D_1937dc0552af4944a98689fd9d6fa9cc_RGBA_0_Vector4, (_Property_092064d1a1804dcb88349a0dfa930d34_Out_0_Float.xxxx), _Lerp_587eb00b83614171a84a065eb4475d69_Out_3_Vector4);
float4 _Lerp_480d292819cd497f84da826a5234e813_Out_3_Vector4;
Unity_Lerp_float4(_Property_dee2f69732114c579841770d3a5f1ce9_Out_0_Vector4, _Lerp_1422a096bdfb4539b217cd9460b0b019_Out_3_Vector4, _Lerp_587eb00b83614171a84a065eb4475d69_Out_3_Vector4, _Lerp_480d292819cd497f84da826a5234e813_Out_3_Vector4);
UnityTexture2D _Property_b2cfc554a3654888b085ea4dadd11e99_Out_0_Texture2D = UnityBuildTexture2DStructNoScale(_SkinMaskTexture);
float4 _SampleTexture2D_864cfb091b7d4331bd7218fdeaf8d858_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_b2cfc554a3654888b085ea4dadd11e99_Out_0_Texture2D.tex, _Property_b2cfc554a3654888b085ea4dadd11e99_Out_0_Texture2D.samplerstate, _Property_b2cfc554a3654888b085ea4dadd11e99_Out_0_Texture2D.GetTransformedUV(IN.uv0.xy) );
float _SampleTexture2D_864cfb091b7d4331bd7218fdeaf8d858_R_4_Float = _SampleTexture2D_864cfb091b7d4331bd7218fdeaf8d858_RGBA_0_Vector4.r;
float _SampleTexture2D_864cfb091b7d4331bd7218fdeaf8d858_G_5_Float = _SampleTexture2D_864cfb091b7d4331bd7218fdeaf8d858_RGBA_0_Vector4.g;
float _SampleTexture2D_864cfb091b7d4331bd7218fdeaf8d858_B_6_Float = _SampleTexture2D_864cfb091b7d4331bd7218fdeaf8d858_RGBA_0_Vector4.b;
float _SampleTexture2D_864cfb091b7d4331bd7218fdeaf8d858_A_7_Float = _SampleTexture2D_864cfb091b7d4331bd7218fdeaf8d858_RGBA_0_Vector4.a;
float _Property_495de44ef11d40cb91ca34953cc899b1_Out_0_Float = _SkinColorAmount;
float4 _Lerp_82953a0631914e78ba370c51d143d891_Out_3_Vector4;
Unity_Lerp_float4(Color_22af752f56df4b69a15a74bd003266a0, _SampleTexture2D_864cfb091b7d4331bd7218fdeaf8d858_RGBA_0_Vector4, (_Property_495de44ef11d40cb91ca34953cc899b1_Out_0_Float.xxxx), _Lerp_82953a0631914e78ba370c51d143d891_Out_3_Vector4);
float4 _Lerp_56dd8066e70d4c85a8d72c30ef200bb7_Out_3_Vector4;
Unity_Lerp_float4(_Property_ecf2fe5309e54977b870684c8d900a70_Out_0_Vector4, _Lerp_480d292819cd497f84da826a5234e813_Out_3_Vector4, _Lerp_82953a0631914e78ba370c51d143d891_Out_3_Vector4, _Lerp_56dd8066e70d4c85a8d72c30ef200bb7_Out_3_Vector4);
float _Property_4208ddc25284429fba969ec5b8970ca4_Out_0_Float = _EyelinerAmount;
float _OneMinus_dfa88fc204374b35898331eae5eba835_Out_1_Float;
Unity_OneMinus_float(_Property_4208ddc25284429fba969ec5b8970ca4_Out_0_Float, _OneMinus_dfa88fc204374b35898331eae5eba835_Out_1_Float);
float _Clamp_a3567345de7b4ba6850279acb3e1bbda_Out_3_Float;
Unity_Clamp_float(_OneMinus_dfa88fc204374b35898331eae5eba835_Out_1_Float, float(0), float(0.5), _Clamp_a3567345de7b4ba6850279acb3e1bbda_Out_3_Float);
UnityTexture2D _Property_0dfa773f4d6f47bc85db4d5cfb627c23_Out_0_Texture2D = UnityBuildTexture2DStructNoScale(_EyeEdgeMaskTexture);
float4 _SampleTexture2D_1e5e254c29e84e2ebe790f4de0afeab7_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_0dfa773f4d6f47bc85db4d5cfb627c23_Out_0_Texture2D.tex, _Property_0dfa773f4d6f47bc85db4d5cfb627c23_Out_0_Texture2D.samplerstate, _Property_0dfa773f4d6f47bc85db4d5cfb627c23_Out_0_Texture2D.GetTransformedUV(IN.uv0.xy) );
float _SampleTexture2D_1e5e254c29e84e2ebe790f4de0afeab7_R_4_Float = _SampleTexture2D_1e5e254c29e84e2ebe790f4de0afeab7_RGBA_0_Vector4.r;
float _SampleTexture2D_1e5e254c29e84e2ebe790f4de0afeab7_G_5_Float = _SampleTexture2D_1e5e254c29e84e2ebe790f4de0afeab7_RGBA_0_Vector4.g;
float _SampleTexture2D_1e5e254c29e84e2ebe790f4de0afeab7_B_6_Float = _SampleTexture2D_1e5e254c29e84e2ebe790f4de0afeab7_RGBA_0_Vector4.b;
float _SampleTexture2D_1e5e254c29e84e2ebe790f4de0afeab7_A_7_Float = _SampleTexture2D_1e5e254c29e84e2ebe790f4de0afeab7_RGBA_0_Vector4.a;
float4 _Lerp_3fc533c198e74a1ebb2c7f7bad6d9762_Out_3_Vector4;
Unity_Lerp_float4(Color_22af752f56df4b69a15a74bd003266a0, (_Clamp_a3567345de7b4ba6850279acb3e1bbda_Out_3_Float.xxxx), _SampleTexture2D_1e5e254c29e84e2ebe790f4de0afeab7_RGBA_0_Vector4, _Lerp_3fc533c198e74a1ebb2c7f7bad6d9762_Out_3_Vector4);
float4 _Lerp_7b945e4c623b447f84028beaef139c78_Out_3_Vector4;
Unity_Lerp_float4(Color_9e0f8909aabe4b33b699f1d9bf4443dd, _Lerp_56dd8066e70d4c85a8d72c30ef200bb7_Out_3_Vector4, _Lerp_3fc533c198e74a1ebb2c7f7bad6d9762_Out_3_Vector4, _Lerp_7b945e4c623b447f84028beaef139c78_Out_3_Vector4);
float4 _EnemyBodyTint_7c6b1708145240cfb235df3b94d53325_Out_0_Vector4 = UNITY_ACCESS_HYBRID_INSTANCED_PROP(_BaseColor, float4);
float4 _BodyReadability_84a204bce97045fc9c6824c3328d4e49_Out_2_Vector4;
Unity_Multiply_float4_float4(_Lerp_7b945e4c623b447f84028beaef139c78_Out_3_Vector4, _EnemyBodyTint_7c6b1708145240cfb235df3b94d53325_Out_0_Vector4, _BodyReadability_84a204bce97045fc9c6824c3328d4e49_Out_2_Vector4);
surface.BaseColor = (_BodyReadability_84a204bce97045fc9c6824c3328d4e49_Out_2_Vector4.xyz);
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
    output.VertexID =                                   input.vertexID;
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
#define ATTRIBUTES_NEED_VERTEXID
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
 uint vertexID : VERTEXID_SEMANTIC;
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
 uint VertexID;
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
float _ComputeMeshIndex;
float4 _ColorMap_TexelSize;
float4 _SkinMaskTexture_TexelSize;
float4 _DarkMaskTexture_TexelSize;
float4 _DirtColor;
float4 _SkinColor;
float _DarkAmount;
float4 _DirtMaskTexture_TexelSize;
float _DirtAmount;
float4 _EyeEdgeMaskTexture_TexelSize;
float _EyelinerAmount;
float _SkinColorAmount;
float4 _BaseColor;
float _ExploderPulse;
UNITY_TEXTURE_STREAMING_DEBUG_VARS;
CBUFFER_END

#if defined(DOTS_INSTANCING_ON)
// DOTS instancing definitions
UNITY_DOTS_INSTANCING_START(MaterialPropertyMetadata)
    UNITY_DOTS_INSTANCED_PROP_OVERRIDE_SUPPORTED(float, _ComputeMeshIndex)
    UNITY_DOTS_INSTANCED_PROP_OVERRIDE_SUPPORTED(float4, _SkinColor)
    UNITY_DOTS_INSTANCED_PROP_OVERRIDE_SUPPORTED(float4, _BaseColor)
UNITY_DOTS_INSTANCING_END(MaterialPropertyMetadata)
// DOTS instancing usage macros
#define UNITY_ACCESS_HYBRID_INSTANCED_PROP(var, type) UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(type, var)
#elif defined(UNITY_INSTANCING_ENABLED)
// Unity instancing definitions
UNITY_INSTANCING_BUFFER_START(SGPerInstanceData)
    UNITY_DEFINE_INSTANCED_PROP(float, _ComputeMeshIndex)
    UNITY_DEFINE_INSTANCED_PROP(float4, _SkinColor)
    UNITY_DEFINE_INSTANCED_PROP(float4, _BaseColor)
UNITY_INSTANCING_BUFFER_END(SGPerInstanceData)
// Unity instancing usage macros
#define UNITY_ACCESS_HYBRID_INSTANCED_PROP(var, type) UNITY_ACCESS_INSTANCED_PROP(SGPerInstanceData, var)
#else
#define UNITY_ACCESS_HYBRID_INSTANCED_PROP(var, type) var
#endif

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
float _CutsAmount;
float4 _CutsColor;
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

struct DeformedVertexData
{
float3 Position;
float3 Normal;
float3 Tangent;
};
uniform StructuredBuffer<DeformedVertexData> _DeformedMeshData : register(t1);

void Unity_ComputeDeformedVertex_float(uint vertexID, out float3 positionOut, out float3 normalOut, out float3 tangentOut)
{
const DeformedVertexData vertexData = _DeformedMeshData[asuint(UNITY_ACCESS_HYBRID_INSTANCED_PROP(_ComputeMeshIndex, float)) + vertexID];
positionOut = vertexData.Position;
normalOut = vertexData.Normal;
tangentOut = vertexData.Tangent;
}

void Unity_Lerp_float4(float4 A, float4 B, float4 T, out float4 Out)
{
    Out = lerp(A, B, T);
}

void Unity_OneMinus_float(float In, out float Out)
{
    Out = 1 - In;
}

void Unity_Clamp_float(float In, float Min, float Max, out float Out)
{
    Out = clamp(In, Min, Max);
}

void Unity_Multiply_float4_float4(float4 A, float4 B, out float4 Out)
{
Out = A * B;
}

// Custom interpolators pre vertex
/* WARNING: $splice Could not find named fragment 'CustomInterpolatorPreVertex' */

// Graph Vertex
struct VertexDescription
{
float3 Position;
float3 Normal;
float3 Tangent;
};

VertexDescription VertexDescriptionFunction(VertexDescriptionInputs IN)
{
VertexDescription description = (VertexDescription)0;
#if defined(UNITY_DOTS_INSTANCING_ENABLED)
float3 _ComputeDeformation_1d37a9a23f19400eb3356a05ea2721bc_DeformedPosition_0_Vector3 = 0;
float3 _ComputeDeformation_1d37a9a23f19400eb3356a05ea2721bc_DeformedNormal_1_Vector3 = 0;
float3 _ComputeDeformation_1d37a9a23f19400eb3356a05ea2721bc_DeformedTangent_2_Vector3 = 0;
Unity_ComputeDeformedVertex_float(IN.VertexID, _ComputeDeformation_1d37a9a23f19400eb3356a05ea2721bc_DeformedPosition_0_Vector3, _ComputeDeformation_1d37a9a23f19400eb3356a05ea2721bc_DeformedNormal_1_Vector3, _ComputeDeformation_1d37a9a23f19400eb3356a05ea2721bc_DeformedTangent_2_Vector3);
#else
float3 _ComputeDeformation_1d37a9a23f19400eb3356a05ea2721bc_DeformedPosition_0_Vector3 = IN.ObjectSpacePosition;
float3 _ComputeDeformation_1d37a9a23f19400eb3356a05ea2721bc_DeformedNormal_1_Vector3 = IN.ObjectSpaceNormal;
float3 _ComputeDeformation_1d37a9a23f19400eb3356a05ea2721bc_DeformedTangent_2_Vector3 = IN.ObjectSpaceTangent;
#endif
description.Position = _ComputeDeformation_1d37a9a23f19400eb3356a05ea2721bc_DeformedPosition_0_Vector3;
description.Normal = _ComputeDeformation_1d37a9a23f19400eb3356a05ea2721bc_DeformedNormal_1_Vector3;
description.Tangent = _ComputeDeformation_1d37a9a23f19400eb3356a05ea2721bc_DeformedTangent_2_Vector3;
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
float3 BaseColor;
};

SurfaceDescription SurfaceDescriptionFunction(SurfaceDescriptionInputs IN)
{
SurfaceDescription surface = (SurfaceDescription)0;
float4 Color_9e0f8909aabe4b33b699f1d9bf4443dd = IsGammaSpace() ? float4(0, 0, 0, 0) : float4(SRGBToLinear(float3(0, 0, 0)), 0);
float4 _Property_ecf2fe5309e54977b870684c8d900a70_Out_0_Vector4 = UNITY_ACCESS_HYBRID_INSTANCED_PROP(_SkinColor, float4);
float4 _Property_dee2f69732114c579841770d3a5f1ce9_Out_0_Vector4 = _CutsColor;
float4 _Property_2152e36dab12470385b94206ce043312_Out_0_Vector4 = _DirtColor;
float4 Color_1d5d22b78f8b4dc6b677715e3c97350d = IsGammaSpace() ? float4(0, 0, 0, 0) : float4(SRGBToLinear(float3(0, 0, 0)), 0);
UnityTexture2D _Property_29217945295d45b482fdf43563ceafe1_Out_0_Texture2D = UnityBuildTexture2DStructNoScale(_ColorMap);
float4 _SampleTexture2D_84a95f63ac39426f99979a9f9613953a_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_29217945295d45b482fdf43563ceafe1_Out_0_Texture2D.tex, _Property_29217945295d45b482fdf43563ceafe1_Out_0_Texture2D.samplerstate, _Property_29217945295d45b482fdf43563ceafe1_Out_0_Texture2D.GetTransformedUV(IN.uv0.xy) );
float _SampleTexture2D_84a95f63ac39426f99979a9f9613953a_R_4_Float = _SampleTexture2D_84a95f63ac39426f99979a9f9613953a_RGBA_0_Vector4.r;
float _SampleTexture2D_84a95f63ac39426f99979a9f9613953a_G_5_Float = _SampleTexture2D_84a95f63ac39426f99979a9f9613953a_RGBA_0_Vector4.g;
float _SampleTexture2D_84a95f63ac39426f99979a9f9613953a_B_6_Float = _SampleTexture2D_84a95f63ac39426f99979a9f9613953a_RGBA_0_Vector4.b;
float _SampleTexture2D_84a95f63ac39426f99979a9f9613953a_A_7_Float = _SampleTexture2D_84a95f63ac39426f99979a9f9613953a_RGBA_0_Vector4.a;
float4 Color_22af752f56df4b69a15a74bd003266a0 = IsGammaSpace() ? float4(1, 1, 1, 0) : float4(SRGBToLinear(float3(1, 1, 1)), 0);
UnityTexture2D _Property_c18bcc123f5545dda6a4416e976dce17_Out_0_Texture2D = UnityBuildTexture2DStructNoScale(_DarkMaskTexture);
float4 _SampleTexture2D_6ccf5a041d9a4379a08064554c835a43_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_c18bcc123f5545dda6a4416e976dce17_Out_0_Texture2D.tex, _Property_c18bcc123f5545dda6a4416e976dce17_Out_0_Texture2D.samplerstate, _Property_c18bcc123f5545dda6a4416e976dce17_Out_0_Texture2D.GetTransformedUV(IN.uv0.xy) );
float _SampleTexture2D_6ccf5a041d9a4379a08064554c835a43_R_4_Float = _SampleTexture2D_6ccf5a041d9a4379a08064554c835a43_RGBA_0_Vector4.r;
float _SampleTexture2D_6ccf5a041d9a4379a08064554c835a43_G_5_Float = _SampleTexture2D_6ccf5a041d9a4379a08064554c835a43_RGBA_0_Vector4.g;
float _SampleTexture2D_6ccf5a041d9a4379a08064554c835a43_B_6_Float = _SampleTexture2D_6ccf5a041d9a4379a08064554c835a43_RGBA_0_Vector4.b;
float _SampleTexture2D_6ccf5a041d9a4379a08064554c835a43_A_7_Float = _SampleTexture2D_6ccf5a041d9a4379a08064554c835a43_RGBA_0_Vector4.a;
float _Property_f460bfa9d43848bba59ba7460f20c9c2_Out_0_Float = _DarkAmount;
float4 _Lerp_b97b4bab53a04c6ea870c54dde30e56e_Out_3_Vector4;
Unity_Lerp_float4(Color_22af752f56df4b69a15a74bd003266a0, _SampleTexture2D_6ccf5a041d9a4379a08064554c835a43_RGBA_0_Vector4, (_Property_f460bfa9d43848bba59ba7460f20c9c2_Out_0_Float.xxxx), _Lerp_b97b4bab53a04c6ea870c54dde30e56e_Out_3_Vector4);
float4 _Lerp_5fb58a8630424f0aa299b82d3c6b7cf2_Out_3_Vector4;
Unity_Lerp_float4(Color_1d5d22b78f8b4dc6b677715e3c97350d, _SampleTexture2D_84a95f63ac39426f99979a9f9613953a_RGBA_0_Vector4, _Lerp_b97b4bab53a04c6ea870c54dde30e56e_Out_3_Vector4, _Lerp_5fb58a8630424f0aa299b82d3c6b7cf2_Out_3_Vector4);
UnityTexture2D _Property_f8239e9549eb4afdb04e0add697a9e63_Out_0_Texture2D = UnityBuildTexture2DStructNoScale(_DirtMaskTexture);
float4 _SampleTexture2D_7a735da5e71c436f82fc33c38b3cb722_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_f8239e9549eb4afdb04e0add697a9e63_Out_0_Texture2D.tex, _Property_f8239e9549eb4afdb04e0add697a9e63_Out_0_Texture2D.samplerstate, _Property_f8239e9549eb4afdb04e0add697a9e63_Out_0_Texture2D.GetTransformedUV(IN.uv0.xy) );
float _SampleTexture2D_7a735da5e71c436f82fc33c38b3cb722_R_4_Float = _SampleTexture2D_7a735da5e71c436f82fc33c38b3cb722_RGBA_0_Vector4.r;
float _SampleTexture2D_7a735da5e71c436f82fc33c38b3cb722_G_5_Float = _SampleTexture2D_7a735da5e71c436f82fc33c38b3cb722_RGBA_0_Vector4.g;
float _SampleTexture2D_7a735da5e71c436f82fc33c38b3cb722_B_6_Float = _SampleTexture2D_7a735da5e71c436f82fc33c38b3cb722_RGBA_0_Vector4.b;
float _SampleTexture2D_7a735da5e71c436f82fc33c38b3cb722_A_7_Float = _SampleTexture2D_7a735da5e71c436f82fc33c38b3cb722_RGBA_0_Vector4.a;
float _Property_ef41be1c8664497cbdfb806842079a89_Out_0_Float = _DirtAmount;
float4 _Lerp_aec65eee28314809a1410cf73b04a889_Out_3_Vector4;
Unity_Lerp_float4(Color_22af752f56df4b69a15a74bd003266a0, _SampleTexture2D_7a735da5e71c436f82fc33c38b3cb722_RGBA_0_Vector4, (_Property_ef41be1c8664497cbdfb806842079a89_Out_0_Float.xxxx), _Lerp_aec65eee28314809a1410cf73b04a889_Out_3_Vector4);
float4 _Lerp_1422a096bdfb4539b217cd9460b0b019_Out_3_Vector4;
Unity_Lerp_float4(_Property_2152e36dab12470385b94206ce043312_Out_0_Vector4, _Lerp_5fb58a8630424f0aa299b82d3c6b7cf2_Out_3_Vector4, _Lerp_aec65eee28314809a1410cf73b04a889_Out_3_Vector4, _Lerp_1422a096bdfb4539b217cd9460b0b019_Out_3_Vector4);
UnityTexture2D _Property_cad87ecf436a44458da862f7575dfeec_Out_0_Texture2D = UnityBuildTexture2DStructNoScale(_CutsMaskTexture);
float4 _SampleTexture2D_1937dc0552af4944a98689fd9d6fa9cc_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_cad87ecf436a44458da862f7575dfeec_Out_0_Texture2D.tex, _Property_cad87ecf436a44458da862f7575dfeec_Out_0_Texture2D.samplerstate, _Property_cad87ecf436a44458da862f7575dfeec_Out_0_Texture2D.GetTransformedUV(IN.uv0.xy) );
float _SampleTexture2D_1937dc0552af4944a98689fd9d6fa9cc_R_4_Float = _SampleTexture2D_1937dc0552af4944a98689fd9d6fa9cc_RGBA_0_Vector4.r;
float _SampleTexture2D_1937dc0552af4944a98689fd9d6fa9cc_G_5_Float = _SampleTexture2D_1937dc0552af4944a98689fd9d6fa9cc_RGBA_0_Vector4.g;
float _SampleTexture2D_1937dc0552af4944a98689fd9d6fa9cc_B_6_Float = _SampleTexture2D_1937dc0552af4944a98689fd9d6fa9cc_RGBA_0_Vector4.b;
float _SampleTexture2D_1937dc0552af4944a98689fd9d6fa9cc_A_7_Float = _SampleTexture2D_1937dc0552af4944a98689fd9d6fa9cc_RGBA_0_Vector4.a;
float _Property_092064d1a1804dcb88349a0dfa930d34_Out_0_Float = _CutsAmount;
float4 _Lerp_587eb00b83614171a84a065eb4475d69_Out_3_Vector4;
Unity_Lerp_float4(Color_22af752f56df4b69a15a74bd003266a0, _SampleTexture2D_1937dc0552af4944a98689fd9d6fa9cc_RGBA_0_Vector4, (_Property_092064d1a1804dcb88349a0dfa930d34_Out_0_Float.xxxx), _Lerp_587eb00b83614171a84a065eb4475d69_Out_3_Vector4);
float4 _Lerp_480d292819cd497f84da826a5234e813_Out_3_Vector4;
Unity_Lerp_float4(_Property_dee2f69732114c579841770d3a5f1ce9_Out_0_Vector4, _Lerp_1422a096bdfb4539b217cd9460b0b019_Out_3_Vector4, _Lerp_587eb00b83614171a84a065eb4475d69_Out_3_Vector4, _Lerp_480d292819cd497f84da826a5234e813_Out_3_Vector4);
UnityTexture2D _Property_b2cfc554a3654888b085ea4dadd11e99_Out_0_Texture2D = UnityBuildTexture2DStructNoScale(_SkinMaskTexture);
float4 _SampleTexture2D_864cfb091b7d4331bd7218fdeaf8d858_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_b2cfc554a3654888b085ea4dadd11e99_Out_0_Texture2D.tex, _Property_b2cfc554a3654888b085ea4dadd11e99_Out_0_Texture2D.samplerstate, _Property_b2cfc554a3654888b085ea4dadd11e99_Out_0_Texture2D.GetTransformedUV(IN.uv0.xy) );
float _SampleTexture2D_864cfb091b7d4331bd7218fdeaf8d858_R_4_Float = _SampleTexture2D_864cfb091b7d4331bd7218fdeaf8d858_RGBA_0_Vector4.r;
float _SampleTexture2D_864cfb091b7d4331bd7218fdeaf8d858_G_5_Float = _SampleTexture2D_864cfb091b7d4331bd7218fdeaf8d858_RGBA_0_Vector4.g;
float _SampleTexture2D_864cfb091b7d4331bd7218fdeaf8d858_B_6_Float = _SampleTexture2D_864cfb091b7d4331bd7218fdeaf8d858_RGBA_0_Vector4.b;
float _SampleTexture2D_864cfb091b7d4331bd7218fdeaf8d858_A_7_Float = _SampleTexture2D_864cfb091b7d4331bd7218fdeaf8d858_RGBA_0_Vector4.a;
float _Property_495de44ef11d40cb91ca34953cc899b1_Out_0_Float = _SkinColorAmount;
float4 _Lerp_82953a0631914e78ba370c51d143d891_Out_3_Vector4;
Unity_Lerp_float4(Color_22af752f56df4b69a15a74bd003266a0, _SampleTexture2D_864cfb091b7d4331bd7218fdeaf8d858_RGBA_0_Vector4, (_Property_495de44ef11d40cb91ca34953cc899b1_Out_0_Float.xxxx), _Lerp_82953a0631914e78ba370c51d143d891_Out_3_Vector4);
float4 _Lerp_56dd8066e70d4c85a8d72c30ef200bb7_Out_3_Vector4;
Unity_Lerp_float4(_Property_ecf2fe5309e54977b870684c8d900a70_Out_0_Vector4, _Lerp_480d292819cd497f84da826a5234e813_Out_3_Vector4, _Lerp_82953a0631914e78ba370c51d143d891_Out_3_Vector4, _Lerp_56dd8066e70d4c85a8d72c30ef200bb7_Out_3_Vector4);
float _Property_4208ddc25284429fba969ec5b8970ca4_Out_0_Float = _EyelinerAmount;
float _OneMinus_dfa88fc204374b35898331eae5eba835_Out_1_Float;
Unity_OneMinus_float(_Property_4208ddc25284429fba969ec5b8970ca4_Out_0_Float, _OneMinus_dfa88fc204374b35898331eae5eba835_Out_1_Float);
float _Clamp_a3567345de7b4ba6850279acb3e1bbda_Out_3_Float;
Unity_Clamp_float(_OneMinus_dfa88fc204374b35898331eae5eba835_Out_1_Float, float(0), float(0.5), _Clamp_a3567345de7b4ba6850279acb3e1bbda_Out_3_Float);
UnityTexture2D _Property_0dfa773f4d6f47bc85db4d5cfb627c23_Out_0_Texture2D = UnityBuildTexture2DStructNoScale(_EyeEdgeMaskTexture);
float4 _SampleTexture2D_1e5e254c29e84e2ebe790f4de0afeab7_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_0dfa773f4d6f47bc85db4d5cfb627c23_Out_0_Texture2D.tex, _Property_0dfa773f4d6f47bc85db4d5cfb627c23_Out_0_Texture2D.samplerstate, _Property_0dfa773f4d6f47bc85db4d5cfb627c23_Out_0_Texture2D.GetTransformedUV(IN.uv0.xy) );
float _SampleTexture2D_1e5e254c29e84e2ebe790f4de0afeab7_R_4_Float = _SampleTexture2D_1e5e254c29e84e2ebe790f4de0afeab7_RGBA_0_Vector4.r;
float _SampleTexture2D_1e5e254c29e84e2ebe790f4de0afeab7_G_5_Float = _SampleTexture2D_1e5e254c29e84e2ebe790f4de0afeab7_RGBA_0_Vector4.g;
float _SampleTexture2D_1e5e254c29e84e2ebe790f4de0afeab7_B_6_Float = _SampleTexture2D_1e5e254c29e84e2ebe790f4de0afeab7_RGBA_0_Vector4.b;
float _SampleTexture2D_1e5e254c29e84e2ebe790f4de0afeab7_A_7_Float = _SampleTexture2D_1e5e254c29e84e2ebe790f4de0afeab7_RGBA_0_Vector4.a;
float4 _Lerp_3fc533c198e74a1ebb2c7f7bad6d9762_Out_3_Vector4;
Unity_Lerp_float4(Color_22af752f56df4b69a15a74bd003266a0, (_Clamp_a3567345de7b4ba6850279acb3e1bbda_Out_3_Float.xxxx), _SampleTexture2D_1e5e254c29e84e2ebe790f4de0afeab7_RGBA_0_Vector4, _Lerp_3fc533c198e74a1ebb2c7f7bad6d9762_Out_3_Vector4);
float4 _Lerp_7b945e4c623b447f84028beaef139c78_Out_3_Vector4;
Unity_Lerp_float4(Color_9e0f8909aabe4b33b699f1d9bf4443dd, _Lerp_56dd8066e70d4c85a8d72c30ef200bb7_Out_3_Vector4, _Lerp_3fc533c198e74a1ebb2c7f7bad6d9762_Out_3_Vector4, _Lerp_7b945e4c623b447f84028beaef139c78_Out_3_Vector4);
float4 _EnemyBodyTint_7c6b1708145240cfb235df3b94d53325_Out_0_Vector4 = UNITY_ACCESS_HYBRID_INSTANCED_PROP(_BaseColor, float4);
float4 _BodyReadability_84a204bce97045fc9c6824c3328d4e49_Out_2_Vector4;
Unity_Multiply_float4_float4(_Lerp_7b945e4c623b447f84028beaef139c78_Out_3_Vector4, _EnemyBodyTint_7c6b1708145240cfb235df3b94d53325_Out_0_Vector4, _BodyReadability_84a204bce97045fc9c6824c3328d4e49_Out_2_Vector4);
surface.BaseColor = (_BodyReadability_84a204bce97045fc9c6824c3328d4e49_Out_2_Vector4.xyz);
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
    output.VertexID =                                   input.vertexID;
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