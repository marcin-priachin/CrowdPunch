Shader "CrowdPunch/GroundHazard"
{
    Properties
    {
        _Color ("Color", Color) = (1,.2,.1,.65)
        _Circle ("Circle", Float) = 0
        _Quarry ("Quarry surface", Float) = 0
        _Observatory ("Observatory heated tiles", Float) = 0
        _Necropolis ("Necropolis scorched paving", Float) = 0
        _Canyon ("Canyon scorched sandstone", Float) = 0
        _Vault ("Vault heated frost stone", Float) = 0
        _Phase ("Hazard phase", Float) = 0
        _FootprintSize ("Footprint half size", Vector) = (1,1,0,0)
    }
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
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float fog : TEXCOORD1; };
            CBUFFER_START(UnityPerMaterial)
            float4 _Color;
            float _Circle;
            float _Quarry, _Phase, _Observatory, _Necropolis, _Canyon, _Vault;
            float4 _FootprintSize;
            CBUFFER_END
            Varyings Vert(Attributes v) { Varyings o; o.positionCS=TransformObjectToHClip(v.positionOS.xyz); o.uv=v.uv; o.fog=ComputeFogFactor(o.positionCS.z); return o; }
            float2 CellHash(float2 p)
            {
                return frac(sin(float2(dot(p,float2(127.1,311.7)),dot(p,float2(269.5,183.3))))*43758.5453);
            }
            half4 QuarrySurface(Varyings v,float edge)
            {
                // Metre-based cells keep fissure scale consistent on circles and long strips.
                float2 p=(v.uv*2-1)*_FootprintSize.xy*.95;
                float2 cell=floor(p), local=frac(p), nearestCell=0;
                float first=100, second=100;
                [unroll] for(int y=-1;y<=1;y++) [unroll] for(int x=-1;x<=1;x++)
                {
                    float2 neighbor=float2(x,y);
                    float2 delta=neighbor+.15+CellHash(cell+neighbor)*.7-local;
                    float d=dot(delta,delta);
                    if(d<first){second=first;first=d;nearestCell=cell+neighbor;}else second=min(second,d);
                }
                float seam=sqrt(second)-sqrt(first);
                if(_Observatory>.5)
                {
                    // Broken geometric ceramic tiles, with the same exact footprint and clock.
                    float2 tile=abs(frac(p*.8)-.5);
                    seam=min(.5-tile.x,.5-tile.y);
                    nearestCell=floor(p*.8);
                }
                if(_Necropolis>.5 || _Vault>.5)
                {
                    // Staggered worn paving stays fixed to the actual metre-sized footprint.
                    float2 paving=p*float2(.65,1.25);
                    paving.x+=fmod(floor(paving.y),2)*.5;
                    float2 tile=abs(frac(paving)-.5);
                    seam=min(.5-tile.x,.5-tile.y);
                    nearestCell=floor(paving);
                }
                float aa=max(fwidth(seam),.006);
                float thickness=_Phase>1.5?.045:.024;
                float fissure=1-smoothstep(thickness,thickness+aa,seam);
                float variation=CellHash(nearestCell).x;
                float shade=variation>.65?1.04:variation>.3?.93:.82;
                float distanceInside=_Circle>.5?(1-edge)*_FootprintSize.x:min((1-abs(v.uv.x*2-1))*_FootprintSize.x,(1-abs(v.uv.y*2-1))*_FootprintSize.y);
                float rim=1-smoothstep(.06,.13+fwidth(distanceInside),distanceInside);
                half3 baseColor=half3(.15,.19,.18)*shade;
                half3 crackColor=half3(.065,.085,.08);
                if(_Observatory>.5)baseColor=half3(.21,.29,.25)*shade;
                if(_Necropolis>.5)baseColor=half3(.19,.24,.25)*shade;
                if(_Canyon>.5)baseColor=half3(.27,.22,.17)*shade;
                if(_Vault>.5)baseColor=half3(.25,.33,.41)*shade;
                half3 rimColor=half3(.32,.36,.32);
                if(_Phase>.5 && _Phase<1.5)
                {
                    float pulse=saturate((_Color.a-.35)/.25);
                    // The whole true footprint pulses; cracks never imply a smaller danger area.
                    baseColor=lerp(baseColor,half3(.56,.32,.065)*shade,.48+.22*pulse);
                    crackColor=half3(.78,.43,.085)*(.85+.15*pulse);
                    rimColor=half3(.9,.56,.12)*(.8+.2*pulse);
                }
                else if(_Phase>1.5)
                {
                    baseColor=half3(.64,.16,.045)*shade;
                    crackColor=half3(1,.52,.09);
                    rimColor=half3(.95,.30,.055);
                }
                half3 color=lerp(baseColor,crackColor,fissure);
                color=lerp(color,rimColor,rim*.85);
                return half4(MixFog(color,v.fog),.94);
            }
            half4 Frag(Varyings v) : SV_Target
            {
                float2 q=abs(v.uv*2-1);
                float edge=lerp(max(q.x,q.y),length(q),_Circle);
                clip(1-edge);
                if(_Quarry>.5 || _Observatory>.5 || _Necropolis>.5 || _Canyon>.5 || _Vault>.5)return QuarrySurface(v,edge);
                float border=smoothstep(.88,.94,edge);
                float stripe=step(.65,frac((v.uv.x+v.uv.y)*12));
                return half4(_Color.rgb*(.8+.2*stripe+.25*border),_Color.a);
            }
            ENDHLSL
        }
    }
}
