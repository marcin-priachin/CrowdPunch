Shader "CrowdPunch/Cel/Painted Foundry"
{
    Properties
    {
        [MainColor] _BaseColor("Surface tint", Color) = (.6,.6,.6,1)
        [MainTexture] _BaseMap("Surface texture", 2D) = "white" {}
        _DirtMap("Path texture", 2D) = "white" {}
        _TextureScale("Repeats per metre", Float) = .2
        _TextureStrength("Painted detail", Range(0,1)) = .6
        _Meadow("Blend meadow and paths", Float) = 0
        _Bounds("Arena centre XZ / half size XZ", Vector) = (0,0,20,20)
        _PathColor("Path tint", Color) = (.56,.43,.25,1)
        _Variation("Large colour variation", Range(0,.5)) = .12
        _PlasterGrain("Plaster wash", Range(0,.2)) = 0
        _ShadowColor("Painted shadow pigment", Color) = (.43,.48,.65,1)
        _LightColor("Painted light pigment", Color) = (1,.92,.79,1)
        _WashColor("Undertone wash", Color) = (.6,.4,.3,1)
        _WashStrength("Pigment wash strength", Range(0,1)) = .18
        _GrainStrength("Dry brush grain", Range(0,.3)) = .07
        _SeamStrength("Drawn roof seams", Range(0,1)) = 0
        _Window("Painted glazing", Float) = 0
        _Contrast("Lighting contrast", Range(0,1)) = .8
        [ToggleUI] _CameraCutaway("Camera-side decoration cutaway", Float) = 0
        [ToggleUI] _ObjectSpacePaint("Paint follows moving parts", Float) = 0
        [ToggleUI] _UseEntityColor("Animated entity tint", Float) = 0
        [ToggleUI] _ReceiveShadows("Receive cast shadows", Float) = 1
        [HideInInspector] _BakedShadowMask("Baked receiver visibility",2D)="white" {}
        [HideInInspector] _BakedShadowST("Baked receiver UV transform",Vector)=(1,1,0,0)
        [HideInInspector] _UseBakedShadowMask("Explicit baked receiver",Float)=0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "UniversalMaterialType"="Lit" }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor, _PathColor;
            half4 _ShadowColor, _LightColor, _WashColor;
            float _WashStrength, _GrainStrength, _SeamStrength, _Window, _Contrast;
            float4 _BakedShadowST;
            float4 _Bounds;
            float _TextureScale, _TextureStrength, _Meadow, _Variation, _ReceiveShadows, _PlasterGrain;
            float _UseBakedShadowMask;
            float _CameraCutaway;
            float _ObjectSpacePaint;
            float _UseEntityColor;
        CBUFFER_END
        #ifdef UNITY_DOTS_INSTANCING_ENABLED
            UNITY_DOTS_INSTANCING_START(MaterialPropertyMetadata)
                UNITY_DOTS_INSTANCED_PROP(float4, _BaseColor)
            UNITY_DOTS_INSTANCING_END(MaterialPropertyMetadata)
        #endif
        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        TEXTURE2D(_DirtMap); SAMPLER(sampler_DirtMap);
        TEXTURE2D(_BakedShadowMask); SAMPLER(sampler_BakedShadowMask);
        float _CrowdPunchBakedShadowsOnly;
        #include "SceneryCutaway.hlsl"
        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float2 uv : TEXCOORD0;
            float2 lightmapUV : TEXCOORD1;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };
        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float3 positionWS : TEXCOORD0;
            half3 normalWS : TEXCOORD1;
            half fog : TEXCOORD2;
            float2 lightmapUV : TEXCOORD3;
            float2 receiverUV : TEXCOORD4;
            float2 uv : TEXCOORD5;
            float3 paintPosition : TEXCOORD6;
            half3 paintNormal : TEXCOORD7;
            UNITY_VERTEX_INPUT_INSTANCE_ID
            UNITY_VERTEX_OUTPUT_STEREO
        };
        Varyings Vertex(Attributes input)
        {
            Varyings output = (Varyings)0;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_TRANSFER_INSTANCE_ID(input, output);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
            VertexPositionInputs pos = GetVertexPositionInputs(input.positionOS.xyz);
            output.positionCS = pos.positionCS;
            output.positionWS = pos.positionWS;
            output.normalWS = TransformObjectToWorldNormal(input.normalOS);
            output.uv=input.uv;
            output.paintPosition=input.positionOS.xyz*3;
            output.paintNormal=input.normalOS;
            output.lightmapUV=input.lightmapUV*unity_LightmapST.xy+unity_LightmapST.zw;
            output.receiverUV=input.lightmapUV*_BakedShadowST.xy+_BakedShadowST.zw;
            output.fog = ComputeFogFactor(pos.positionCS.z);
            return output;
        }
        float Hash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
        float Noise(float2 p)
        {
            float2 i=floor(p), f=frac(p); f=f*f*(3-2*f);
            return lerp(lerp(Hash(i),Hash(i+float2(1,0)),f.x),lerp(Hash(i+float2(0,1)),Hash(i+1),f.x),f.y);
        }
        half Detail(half3 sampleColor)
        {
            // Use the painted value structure while keeping a coherent chapter palette.
            return lerp(1, clamp(.62+dot(sampleColor,half3(.2126,.7152,.0722))*1.4,.65,1.25), _TextureStrength);
        }
        ENDHLSL
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForwardOnly" }
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fog
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile _ USE_LEGACY_LIGHTMAPS
            half4 Fragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                ClipCameraSideScenery(input.positionWS,input.positionCS.xy,_CameraCutaway);
                half4 baseColor=_BaseColor;
                #ifdef UNITY_DOTS_INSTANCING_ENABLED
                    baseColor=lerp(baseColor,UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4,_BaseColor),_UseEntityColor);
                #endif
                half3 n=normalize(input.normalWS);
                float3 p=input.positionWS;
                float3 painted=lerp(p,input.paintPosition,_ObjectSpacePaint);
                half3 paintedNormal=normalize(lerp(n,input.paintNormal,_ObjectSpacePaint));
                half3 weights=pow(abs(paintedNormal),4); weights/=max(dot(weights,half3(1,1,1)),.001);
                // World projection also works on imported atlas models and UV-less ECS floors.
                half3 tex=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,painted.zy*_TextureScale).rgb*weights.x
                    + SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,painted.xz*_TextureScale).rgb*weights.y
                    + SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,painted.xy*_TextureScale).rgb*weights.z;
                // Stable world projection: pigment does not swim with the orbit camera.
                float2 paintUV=painted.zy*weights.x+painted.xz*weights.y+painted.xy*weights.z;
                half macro=Noise(paintUV*.27);
                half brush=Noise(paintUV*float2(.65,2.8)+macro*1.4);
                half3 albedo=lerp(baseColor.rgb,_WashColor.rgb,saturate(macro*.8+brush*.4)*_WashStrength);
                albedo*=Detail(tex)*(1+(brush-.5)*_Variation*2);
                // Fade subpixel pigment to prevent shimmer at the far skyline.
                half grainFade=1-saturate(max(length(ddx(paintUV)),length(ddy(paintUV)))*12);
                albedo*=1+(Noise(paintUV*19)-.5)*_GrainStrength*grainFade;
                if(_SeamStrength>.001)
                {
                    float axis=abs(n.x)>abs(n.z)?p.z:p.x;
                    float seamUV=axis*.72+(Noise(paintUV*.7)-.5)*.035;
                    float d=abs(frac(seamUV)-.5);
                    float aa=max(fwidth(seamUV),.002);
                    half seam=1-smoothstep(.014,.014+aa,d);
                    albedo=lerp(albedo,albedo*_ShadowColor.rgb,seam*_SeamStrength);
                }
                if(_Window>.5)
                {
                    float2 uv=input.uv;
                    float edge=min(min(uv.x,1-uv.x),min(uv.y,1-uv.y));
                    float inset=smoothstep(.045,.06,edge+(Noise(uv*9)-.5)*.012);
                    half3 glass=lerp(baseColor.rgb,_WashColor.rgb,smoothstep(.15,.85,uv.y));
                    albedo=lerp(_ShadowColor.rgb*.35,glass,inset);
                    // A single painted reflection, with no view-dependent specular.
                    albedo+=pow(saturate(1-abs(uv.x*.7+uv.y-.85)*13),2)*.035*inset;
                }
                if(_Meadow>.5)
                {
                    float2 q=abs(p.xz-_Bounds.xy);
                    float edgeNoise=(Noise(p.xz*.35)-.5)*1.6+(Noise(p.xz*1.2)-.5)*.45;
                    float side=abs(q.x-(_Bounds.z+5));
                    float north=q.x;
                    float sideMask=(1-smoothstep(2,3.2,side+edgeNoise))*(1-smoothstep(_Bounds.w+10,_Bounds.w+18,q.y));
                    float northMask=(1-smoothstep(2.8,4,north+edgeNoise))*(1-smoothstep(_Bounds.w+31,_Bounds.w+40,q.y));
                    float outside=smoothstep(-.5,1,max(q.x-_Bounds.z,q.y-_Bounds.w));
                    half path=max(sideMask,northMask)*outside;
                    half dirt=Detail(SAMPLE_TEXTURE2D(_DirtMap,sampler_DirtMap,p.xz*.22).rgb);
                    albedo=lerp(albedo,_PathColor.rgb*dirt,path);
                }
                float4 shadowCoord=TransformWorldToShadowCoord(p);
                #if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
                    shadowCoord=ComputeScreenPos(TransformWorldToHClip(p));
                #endif
                half4 shadowMask=SAMPLE_SHADOWMASK(input.lightmapUV);
                if(_UseBakedShadowMask>.5)
                    shadowMask=SAMPLE_TEXTURE2D(_BakedShadowMask,sampler_BakedShadowMask,input.receiverUV);
                Light sun=GetMainLight(shadowCoord,p,shadowMask);
                if(_CrowdPunchBakedShadowsOnly>.5)
                    sun.shadowAttenuation=step(.5,BakedShadow(shadowMask,_MainLightOcclusionProbes,half4(1,0,0,0)));
                half ndl=saturate(dot(n,sun.direction));
                half feather=max(fwidth(ndl)*1.5,.018);
                half band=.20+.42*smoothstep(.23-feather,.23+feather,ndl)+.38*smoothstep(.68-feather,.68+feather,ndl);
                AmbientOcclusionFactor ao=GetScreenSpaceAmbientOcclusion(GetNormalizedScreenSpaceUV(input.positionCS));
                half visibility=lerp(1,sun.shadowAttenuation,_ReceiveShadows);
                half lightAmount=lerp(.65,band*visibility,_Contrast);
                half3 pigment=lerp(_ShadowColor.rgb,_LightColor.rgb,lightAmount);
                half3 color=albedo*(pigment*1.25+SampleSH(n)*.12);
                color*=lerp(1,ao.indirectAmbientOcclusion,.28);
                return half4(MixFog(color,input.fog),1);
            }
            ENDHLSL
        }
        Pass
        {
            Name "Meta"
            Tags { "LightMode"="Meta" }
            Cull Off
            HLSLPROGRAM
            #pragma vertex VillageMetaVertex
            #pragma fragment VillageMetaFragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/MetaInput.hlsl"
            struct MetaAttributes { float4 positionOS:POSITION; float2 uv1:TEXCOORD1; float2 uv2:TEXCOORD2; };
            float4 VillageMetaVertex(MetaAttributes input):SV_POSITION
            {
                return UnityMetaVertexPosition(input.positionOS.xyz,input.uv1,input.uv2);
            }
            half4 VillageMetaFragment():SV_Target
            {
                MetaInput meta=(MetaInput)0; meta.Albedo=_BaseColor.rgb;
                return MetaFragment(meta);
            }
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex ShadowVertex
            #pragma fragment DepthFragment
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            float3 _LightDirection, _LightPosition;
            Varyings ShadowVertex(Attributes input)
            {
                Varyings output=Vertex(input);
                float3 direction=_LightDirection;
                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                    direction=normalize(_LightPosition-output.positionWS);
                #endif
                output.positionCS=TransformWorldToHClip(ApplyShadowBias(output.positionWS,output.normalWS,direction));
                #if UNITY_REVERSED_Z
                    output.positionCS.z=min(output.positionCS.z,UNITY_NEAR_CLIP_VALUE*output.positionCS.w);
                #else
                    output.positionCS.z=max(output.positionCS.z,UNITY_NEAR_CLIP_VALUE*output.positionCS.w);
                #endif
                return output;
            }
            half4 DepthFragment(Varyings input):SV_Target { return 0; }
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On ColorMask R
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vertex
            #pragma fragment DepthFragment
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
            half DepthFragment(Varyings input):SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                ClipCameraSideScenery(input.positionWS,input.positionCS.xy,_CameraCutaway);
                return input.positionCS.z;
            }
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormalsOnly" }
            ZWrite On
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vertex
            #pragma fragment NormalFragment
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            half4 NormalFragment(Varyings input):SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                ClipCameraSideScenery(input.positionWS,input.positionCS.xy,_CameraCutaway);
                half3 n=normalize(input.normalWS);
                #if defined(_GBUFFER_NORMALS_OCT)
                    float2 oct=PackNormalOctQuadEncode(n);
                    return half4(PackFloat2To888(saturate(oct*.5+.5)),0);
                #else
                    return half4(n,0);
                #endif
            }
            ENDHLSL
        }
    }
}
