Shader "CrowdPunch/VFX/Combat Sprite"
{
    Properties
    {
        [MainTexture] _BaseMap("Shape", 2D) = "white" {}
        _NoiseMap("Erosion", 2D) = "gray" {}
        [MainColor] _BaseColor("Tint", Color) = (1,1,1,1)
        _MaskChannel("Mask channel", Vector) = (0,0,0,1)
        _Scroll("Shape scroll", Vector) = (0,0,0,0)
        _Erosion("Erosion amount", Range(0,1)) = 0
        _Glow("Brightness", Range(0,2)) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_NoiseMap); SAMPLER(sampler_NoiseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST, _BaseColor, _MaskChannel, _Scroll;
                float _Erosion, _Glow;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; float4 circle : TEXCOORD1; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 world : TEXCOORD0; float2 uv : TEXCOORD1; half4 color : COLOR; float4 circle : TEXCOORD2; };
            Varyings Vert(Attributes i)
            {
                Varyings o;
                o.world = TransformObjectToWorld(i.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.world);
                o.uv = i.uv; o.color = i.color * _BaseColor; o.circle = i.circle;
                return o;
            }
            half4 Frag(Varyings i) : SV_Target
            {
                float2 uv = i.uv * _BaseMap_ST.xy + _BaseMap_ST.zw + _Time.y * _Scroll.xy;
                half mask = dot(SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,uv),_MaskChannel);
                half noise = SAMPLE_TEXTURE2D(_NoiseMap,sampler_NoiseMap,i.uv * 2 + _Time.y * float2(.13,-.21)).r;
                half alpha = saturate((mask - noise * _Erosion) / max(.12,1-_Erosion));
                // Clip cloud decoration to the authoritative circular footprint, including tilted billboards.
                // Missing particle TEXCOORD1 defaults to (0,0,0,1) on some backends.
                if (i.circle.w > .5 && i.circle.z > .001)
                {
                    float edge = i.circle.z - distance(i.world.xz,i.circle.xy);
                    alpha *= saturate(edge / .12);
                }
                return half4(i.color.rgb * _Glow, i.color.a * alpha);
            }
            ENDHLSL
        }
    }
}
