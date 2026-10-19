Shader "CrowdPunch/Cel/Painted Haze Sky"
{
    Properties
    {
        _Horizon("Opaque haze",Color)=(.57,.68,.63,1)
        _Zenith("Upper sky wash",Color)=(.65,.74,.72,1)
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" "RenderPipeline"="UniversalPipeline" }
        Cull Off ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Horizon, _Zenith;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS:SV_POSITION; float3 direction:TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
            Varyings Vertex(Attributes input)
            {
                Varyings output=(Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS=TransformObjectToHClip(input.positionOS.xyz);
                // Sky must remain behind every arena and scenery depth sample.
                output.positionCS.z=UNITY_RAW_FAR_CLIP_VALUE*output.positionCS.w;
                output.direction=input.positionOS.xyz;
                return output;
            }
            half4 Fragment(Varyings input):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float upper=smoothstep(.55,1,normalize(input.direction).y);
                return half4(lerp(_Horizon.rgb,_Zenith.rgb,upper),1);
            }
            ENDHLSL
        }
    }
}
