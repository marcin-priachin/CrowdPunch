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
            struct Output { float4 position : SV_POSITION; half4 color : COLOR; };
            Output vert(Input i) { Output o; o.position = TransformObjectToHClip(i.vertex.xyz); o.color = i.color; return o; }
            half4 frag(Output i) : SV_Target { return i.color; }
            ENDHLSL
        }
    }
}
