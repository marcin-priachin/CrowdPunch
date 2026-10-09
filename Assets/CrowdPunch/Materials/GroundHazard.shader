Shader "CrowdPunch/GroundHazard"
{
    Properties { _Color ("Color", Color) = (1,.2,.1,.65) _Circle ("Circle", Float) = 0 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
            CBUFFER_START(UnityPerMaterial)
            float4 _Color;
            float _Circle;
            CBUFFER_END
            Varyings Vert(Attributes v) { Varyings o; o.positionCS=TransformObjectToHClip(v.positionOS.xyz); o.uv=v.uv; return o; }
            half4 Frag(Varyings v) : SV_Target
            {
                float2 q=abs(v.uv*2-1);
                float edge=lerp(max(q.x,q.y),length(q),_Circle);
                clip(1-edge);
                float border=smoothstep(.88,.94,edge);
                float stripe=step(.65,frac((v.uv.x+v.uv.y)*12));
                return half4(_Color.rgb*(.8+.2*stripe+.25*border),_Color.a);
            }
            ENDHLSL
        }
    }
}
