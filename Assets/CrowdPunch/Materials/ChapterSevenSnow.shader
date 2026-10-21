Shader "CrowdPunch/Chapter Seven Cartoon Snow"
{
    Properties
    {
        _Tint("Snow face", Color) = (1,1,1,1)
        _Shade("Ice blue bevel", Color) = (.65,.82,.93,1)
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
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Tint, _Shade;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; half4 color:COLOR; float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionCS:SV_POSITION; half4 color:COLOR; float2 uv:TEXCOORD0; half fog:TEXCOORD1; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 positionWS=TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS=TransformWorldToHClip(positionWS);
                output.color=input.color;
                // Foreground flakes fade before they can fill the view or cover a threat.
                output.color.a*=smoothstep(1.5,4,distance(positionWS,GetCameraPositionWS()));
                output.uv=input.uv;
                output.fog=ComputeFogFactor(output.positionCS.z);
                return output;
            }
            half4 Frag(Varyings input):SV_Target
            {
                float2 p=input.uv*2-1;
                float2 q=abs(p);
                float d=max(dot(q,float2(.8660254,.5)),q.y)-.76;
                float aa=max(fwidth(d),.012);
                half edge=1-smoothstep(-aa,aa,d);
                half face=step(-.15,d)*step(p.y,-.1);
                half3 snow=lerp(_Tint.rgb,_Shade.rgb,face)*input.color.rgb;
                return half4(MixFog(snow,input.fog),input.color.a*edge);
            }
            ENDHLSL
        }
    }
}
