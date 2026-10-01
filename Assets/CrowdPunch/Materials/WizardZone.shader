Shader "CrowdPunch/WizardZone"
{
    Properties { _Color ("Violet", Color) = (.55,.08,1,.4) _Ring ("Ring intensity", Float) = .85 }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Blend SrcAlpha One
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _Color;
            half _Ring;
            CBUFFER_END
            struct Input { float4 vertex: POSITION; float2 uv: TEXCOORD0; };
            struct Output { float4 position: SV_POSITION; float2 uv: TEXCOORD0; };
            Output vert(Input i) { Output o; o.position=TransformObjectToHClip(i.vertex.xyz); o.uv=i.uv; return o; }
            half4 frag(Output i): SV_Target
            {
                float radius=length(i.uv*2-1);
                half ring=smoothstep(.94,.96,radius);
                return half4(_Color.rgb, _Color.a + ring * _Ring);
            }
            ENDHLSL
        }
    }
}
