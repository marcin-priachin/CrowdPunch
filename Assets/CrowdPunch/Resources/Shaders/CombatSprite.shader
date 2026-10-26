Shader "CrowdPunch/VFX/Combat Sprite"
{
    Properties
    {
        [MainTexture] _BaseMap("Shape", 2D) = "white" {}
        _NoiseMap("Erosion", 2D) = "gray" {}
        [MainColor] _BaseColor("Tint", Color) = (1,1,1,1)
        _MaskChannel("Mask channel", Vector) = (0,0,0,1)
        _Scroll("Shape scroll", Vector) = (0,0,0,0)
        _Erosion("Erosion amount", Range(0,1)) = 0
        _Glow("Brightness", Range(0,2)) = 1
        [Toggle] _Ribbon("Parallel cartoon speed lines", Float) = 0
        _LineProfiles("Length profiles", 2D) = "white" {}
        _LineParameters("Count / thickness / blur / spacing", Vector) = (5,.0424,1,.92)
        _LineOpacity("Line opacity", Range(0,1)) = 1
        _LineBrightness("Line brightness", Range(0,2)) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_NoiseMap); SAMPLER(sampler_NoiseMap);
            TEXTURE2D(_LineProfiles); SAMPLER(sampler_LineProfiles);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST, _BaseColor, _MaskChannel, _Scroll;
                float4 _LineParameters;
                float _Erosion, _Glow, _Ribbon, _LineOpacity, _LineBrightness;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; float4 circle : TEXCOORD1; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 world : TEXCOORD0; float2 uv : TEXCOORD1; half4 color : COLOR; float4 circle : TEXCOORD2; };
            Varyings Vert(Attributes i)
            {
                Varyings o;
                o.world = TransformObjectToWorld(i.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.world);
                o.uv = i.uv; o.color = i.color * _BaseColor; o.circle = i.circle;
                return o;
            }
            half4 Frag(Varyings i) : SV_Target
            {
                if (_Ribbon > .5)
                {
                    // Unity's UV.x is zero at the enemy. Expose profiles in tail-to-enemy order.
                    float alongLength = saturate(1-i.uv.x);
                    float4 profile = SAMPLE_TEXTURE2D(_LineProfiles,sampler_LineProfiles,float2(alongLength,.25));
                    half4 tint = SAMPLE_TEXTURE2D(_LineProfiles,sampler_LineProfiles,float2(alongLength,.75));
                    float count = clamp(round(_LineParameters.x),1,12);
                    float spread = saturate(_LineParameters.w*profile.a);
                    float spacing = count > 1 ? max(.00001,spread/(count-1)) : 1;
                    float first = count > 1 ? .5-spread*.5 : .5;
                    float lane = clamp(round((i.uv.y-first)/spacing),0,count-1);
                    float distance = abs(i.uv.y-(first+lane*spacing));
                    float width = max(0,_LineParameters.y*profile.r);
                    float blur = saturate(_LineParameters.z*profile.b);
                    float pixelWidth = fwidth(i.uv.y);
                    float sigma = max(.00001,sqrt(pow(width*.4246609*blur,2) + pixelWidth*pixelWidth/12));
                    float edge = max(0,distance-width*.5*(1-blur));
                    float alpha = width > .00001 ? exp2(-.7213475*edge*edge/(sigma*sigma)) : 0;
                    return half4(i.color.rgb * _Glow * _LineBrightness * tint.rgb,
                        i.color.a * alpha * _LineOpacity * profile.g * tint.a);
                }
                float2 uv = i.uv * _BaseMap_ST.xy + _BaseMap_ST.zw + _Time.y * _Scroll.xy;
                half mask = dot(SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,uv),_MaskChannel);
                half noise = SAMPLE_TEXTURE2D(_NoiseMap,sampler_NoiseMap,i.uv * 2 + _Time.y * float2(.13,-.21)).r;
                half alpha = saturate((mask - noise * _Erosion) / max(.12,1-_Erosion));
                // Clip cloud decoration to the authoritative circular footprint, including tilted billboards.
                // Missing particle TEXCOORD1 defaults to (0,0,0,1) on some backends.
                if (i.circle.w > .5 && i.circle.z > .001)
                {
                    float edge = i.circle.z - distance(i.world.xz,i.circle.xy);
                    alpha *= saturate(edge / .12);
                }
                return half4(i.color.rgb * _Glow, i.color.a * alpha);
            }
            ENDHLSL
        }
    }
}
