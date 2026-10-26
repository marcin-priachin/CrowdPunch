Shader "CrowdPunch/EnemyTrail"
{
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Input { float4 vertex : POSITION; half4 color : COLOR; };
            struct Output { float4 position : SV_POSITION; half4 color : COLOR; float2 world : TEXCOORD0; };
            Output vert(Input i) { Output o; o.position = TransformObjectToHClip(i.vertex.xyz); o.color = i.color; o.world=TransformObjectToWorld(i.vertex.xyz).xz; return o; }
            half4 frag(Output i) : SV_Target
            {
                float ripple=sin(i.world.x*8+_Time.y*2)*sin(i.world.y*7-_Time.y*1.4);
                half pigment=smoothstep(.45,.8,ripple);
                return half4(i.color.rgb*(.85+pigment*.35),i.color.a*(.75+pigment*.25));
            }
            ENDHLSL
        }
    }
}
