Shader "CrowdPunch/Chapter Six Cartoon Dust"
{
    Properties
    {
        _Tint("Sunlit sand", Color) = (.9,.73,.46,1)
        _Shade("Ochre underside", Color) = (.7,.51,.29,1)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Tint, _Shade;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; half4 color:COLOR; float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionCS:SV_POSITION; half4 color:COLOR; float2 uv:TEXCOORD0; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS=TransformObjectToHClip(input.positionOS.xyz);
                output.color=input.color;
                output.uv=input.uv;
                return output;
            }
            half4 Frag(Varyings input):SV_Target
            {
                float2 p=input.uv*2-1;
                // Three rounded lobes and a broad base form a graphic puff, without a fog sheet.
                float d=min(length(p-float2(-.38,-.1))-.42,length(p-float2(.35,-.12))-.44);
                d=min(d,length(p-float2(-.05,.2))-.52);
                d=min(d,length(p*float2(1,1.8)-float2(0,-.28))-.64);
                half edge=1-smoothstep(-.025,.035,d);
                half highlight=step(.02,p.y+.08*sin(p.x*8));
                return half4(lerp(_Shade.rgb,_Tint.rgb,highlight)*input.color.rgb,input.color.a*edge);
            }
            ENDHLSL
        }
    }
}
