Shader "CrowdPunch/Alien Spike Projectile"
{
    Properties
    {
        _BaseColor ("Ownership Color", Color) = (1, 1, 1, 1)
        [NoScaleOffset] _Gradient ("Tip to Base", 2D) = "white" {}
        [HideInInspector] _PlayerOwned ("Player Owned", Float) = 0
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" }
        Pass
        {
            Tags { "LightMode" = "SRPDefaultUnlit" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_Gradient);
            SAMPLER(sampler_Gradient);
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _PlayerOwned;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float gradientPosition : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.gradientPosition = input.uv.x;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half3 tint = SAMPLE_TEXTURE2D(_Gradient, sampler_Gradient, float2(input.gradientPosition, 0.5)).rgb;
                // Preserve the gradient's value while letting the player accent supply the hue.
                half value = max(tint.r, max(tint.g, tint.b));
                tint = lerp(tint, value.xxx, _PlayerOwned);
                return half4(tint * _BaseColor.rgb, 1);
            }
            ENDHLSL
        }
    }
}
