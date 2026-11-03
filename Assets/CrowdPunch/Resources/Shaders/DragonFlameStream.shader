Shader "CrowdPunch/VFX/Dragon Flame Stream"
{
    Properties
    {
        _NoiseMap("Vefects flow noise", 2D) = "gray" {}
        _SwirlMap("Vefects flame swirls", 2D) = "gray" {}
        _OuterColor("Orange edge", Color) = (1,.16,.015,1)
        _MiddleColor("Yellow flame", Color) = (1,.56,.035,1)
        _CoreColor("Hot cream core", Color) = (1,.91,.43,1)
        _FlowSpeed("Outward flow", Float) = 1.6
        _Brightness("Brightness", Range(0,2)) = 1.15
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite On
            ZTest LEqual
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_NoiseMap); SAMPLER(sampler_NoiseMap);
            TEXTURE2D(_SwirlMap); SAMPLER(sampler_SwirlMap);
            CBUFFER_START(UnityPerMaterial)
                half4 _OuterColor, _MiddleColor, _CoreColor;
                float _FlowSpeed, _Brightness;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float3 world : TEXCOORD1; };
            Varyings Vert(Attributes i)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(i.positionOS.xyz);
                o.world = TransformObjectToWorld(i.positionOS.xyz);
                o.uv = i.uv;
                return o;
            }
            half4 Frag(Varyings i) : SV_Target
            {
                // Continuous UVs carry broad shapes away from the mouth. Noise changes the
                // silhouette and colour bands, never punches scattered holes through the core.
                float time = _Time.y * _FlowSpeed;
                float2 flow = float2(i.uv.x * 2.1, i.uv.y * 1.1 - time);
                half noise = SAMPLE_TEXTURE2D(_NoiseMap, sampler_NoiseMap, flow).r;
                half swirl = SAMPLE_TEXTURE2D(_SwirlMap, sampler_SwirlMap,
                    float2(i.uv.x * 1.4 + (noise - .5) * .2, i.uv.y * .9 - time * .7)).r;
                float lateral = abs(i.uv.x * 2 - 1);
                float edgeNoise = SAMPLE_TEXTURE2D(_NoiseMap, sampler_NoiseMap,
                    float2(i.uv.x < .5 ? .15 : .85, i.uv.y * 1.1 - time)).r;
                float edge = 1 - lateral - (.035 + edgeNoise * .16) * smoothstep(.02, .18, i.uv.y);
                float outline = smoothstep(0, .045, edge);
                float tipNoise = SAMPLE_TEXTURE2D(_SwirlMap, sampler_SwirlMap,
                    float2(i.uv.x * 1.4, .9 - time * .7)).r;
                // One cutoff per angular column keeps the tip from disappearing and reappearing.
                float tip = .88 + .12 * tipNoise;
                float end = 1 - smoothstep(tip - .045, tip, i.uv.y);
                float heat = 1 - lateral * .57 - i.uv.y * .36 + (noise - .5) * .13 + (swirl - .5) * .06;
                half3 color = lerp(_OuterColor.rgb, _MiddleColor.rgb, smoothstep(.47, .51, heat));
                color = lerp(color, _CoreColor.rgb, smoothstep(.77, .81, heat));
                float alpha = outline * end * .94;
                clip(alpha - .02);
                // Depth-writing visible surfaces keep the back shell from painting over the front.
                // A small cel shade makes the rolling volume readable without realistic lighting.
                float3 normal = normalize(cross(ddy(i.world), ddx(i.world)));
                float shade = lerp(.86, 1, smoothstep(.45, .55, abs(normal.y)));
                return half4(color * _Brightness * shade, alpha);
            }
            ENDHLSL
        }
    }
}
