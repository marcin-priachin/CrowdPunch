Shader "CrowdPunch/WizardZone"
{
    Properties
    {
        _Color ("Fill color and opacity", Color) = (.58,.7,.12,.32)
        _RingColor ("Ring color", Color) = (.58,.7,.12,1)
        _Ring ("Ring opacity", Range(0,1)) = .5525
        _RingThickness ("Ring thickness (fraction of radius)", Range(0,1)) = .06
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Blend One One, SrcAlpha One
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _Color, _RingColor;
            half _Ring;
            float _RingThickness;
            CBUFFER_END
            struct Input { float4 vertex: POSITION; float2 uv: TEXCOORD0; };
            struct Output { float4 position: SV_POSITION; float2 uv: TEXCOORD0; };
            Output vert(Input i) { Output o; o.position=TransformObjectToHClip(i.vertex.xyz); o.uv=i.uv; return o; }
            half4 frag(Output i): SV_Target
            {
                float radius=length(i.uv*2-1);
                float inner = 1 - _RingThickness;
                float feather = max(.00001, _RingThickness / 3);
                half ring = _RingThickness > 0 ? smoothstep(inner, inner + feather, radius) : 0;
                float2 p=i.uv*2-1;
                float drifting=sin(p.x*17+_Time.y*.8)*sin(p.y*14-_Time.y*.6);
                half mottled=.65+.35*smoothstep(-.4,.7,drifting);
                half fillOpacity = _Color.a * mottled;
                half ringOpacity = ring * _Ring;
                half3 color = _Color.rgb * fillOpacity + _RingColor.rgb * ringOpacity;
                return half4(color, fillOpacity + ringOpacity);
            }
            ENDHLSL
        }
    }
}
