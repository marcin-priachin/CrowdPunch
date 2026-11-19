Shader "CrowdPunch/CharacterGroundShadow"
{
    Properties
    {
        _ShadowTint("Shadow tint", Vector) = (.39, .39, .39, 1)
        _ShadowStrength("Shadow strength", Float) = .65
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent-100" }
        Pass
        {
            Name "Character ground shadows"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend DstColor Zero
            ColorMask RGB
            ZWrite Off
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma instancing_options forcemaxcount:128
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _ShadowTint;
            CBUFFER_END
            UNITY_INSTANCING_BUFFER_START(GroundShadow)
                UNITY_DEFINE_INSTANCED_PROP(float, _ShadowStrength)
            UNITY_INSTANCING_BUFFER_END(GroundShadow)

            struct Attributes
            {
                float3 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half3 multiplier : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS);
                half strength = UNITY_ACCESS_INSTANCED_PROP(GroundShadow, _ShadowStrength);
                half3 tint = lerp(1.0h, _ShadowTint.rgb, strength);
                output.multiplier = MixFogColor(tint, half3(1, 1, 1), ComputeFogFactor(output.positionCS.z));
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                return half4(input.multiplier, 1.0h);
            }
            ENDHLSL
        }
    }
}
