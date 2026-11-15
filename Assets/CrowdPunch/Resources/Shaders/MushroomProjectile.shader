Shader "CrowdPunch/MushroomProjectile"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.8, 0.19, 0.10, 1)
        _HighlightColor ("Highlight Color", Color) = (1, 0.85, 0.60, 1)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _HighlightColor;
            CBUFFER_END

            struct Attributes { float3 positionOS : POSITION; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half gradient : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS);
                // Project across the unit sphere toward the upper front of the shot.
                output.gradient = dot(input.positionOS, float3(0, 0.8, 0.6)) + 0.5;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half depth = lerp(0.55h, 1.0h, smoothstep(0.0h, 0.55h, input.gradient));
                half highlight = smoothstep(0.58h, 0.96h, input.gradient);
                return half4(lerp(_BaseColor.rgb * depth, _HighlightColor.rgb, highlight), 1.0h);
            }
            ENDHLSL
        }
    }
}
