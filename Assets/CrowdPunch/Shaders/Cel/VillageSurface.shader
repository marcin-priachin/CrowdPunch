Shader "CrowdPunch/Cel/Village Surface"
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
        [ToggleUI] _ReceiveShadows("Receive cast shadows", Float) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "UniversalMaterialType"="Lit" }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor, _PathColor;
            float4 _Bounds;
            float _TextureScale, _TextureStrength, _Meadow, _Variation, _ReceiveShadows, _PlasterGrain;
        CBUFFER_END
        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        TEXTURE2D(_DirtMap); SAMPLER(sampler_DirtMap);
        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };
        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float3 positionWS : TEXCOORD0;
            half3 normalWS : TEXCOORD1;
            half fog : TEXCOORD2;
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
            return lerp(1, .45+dot(sampleColor,half3(.2126,.7152,.0722))*2.4, _TextureStrength);
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
            half4 Fragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half3 n=normalize(input.normalWS);
                float3 p=input.positionWS;
                half3 weights=pow(abs(n),4); weights/=max(dot(weights,half3(1,1,1)),.001);
                // World projection also works on imported atlas models and UV-less ECS floors.
                half3 tex=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,p.zy*_TextureScale).rgb*weights.x
                    + SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,p.xz*_TextureScale).rgb*weights.y
                    + SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,p.xy*_TextureScale).rgb*weights.z;
                half macro=Noise(p.xz*.045);
                half3 albedo=_BaseColor.rgb*Detail(tex)*(1+(macro-.5)*_Variation*2);
                [branch] if(_PlasterGrain>0)
                {
                    half plaster=Noise(p.xy*.7+p.z*.31)*.75+Noise(p.zy*5+p.x*.47)*.25;
                    albedo*=1+(plaster-.5)*_PlasterGrain;
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
                Light sun=GetMainLight(shadowCoord);
                half ndl=saturate(dot(n,sun.direction));
                half feather=max(fwidth(ndl)*1.5,.018);
                half band=.20+.42*smoothstep(.23-feather,.23+feather,ndl)+.38*smoothstep(.68-feather,.68+feather,ndl);
                AmbientOcclusionFactor ao=GetScreenSpaceAmbientOcclusion(GetNormalizedScreenSpaceUV(input.positionCS));
                half visibility=lerp(.18,1,lerp(1,sun.shadowAttenuation,_ReceiveShadows))*ao.directAmbientOcclusion;
                half3 fill=half3(.17,.21,.28)+SampleSH(n)*.24;
                half3 color=albedo*(fill+band*visibility*min(sun.color,half3(1.35,1.35,1.35)))*ao.indirectAmbientOcclusion;
                return half4(MixFog(color,input.fog),1);
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
            half DepthFragment(Varyings input):SV_Target { return input.positionCS.z; }
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
