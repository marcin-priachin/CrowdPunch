Shader "CrowdPunch/EnemySpawnSoil"
{
    Properties { _Digging("Digging age and phase", Vector) = (0.5, 0, 0, 0) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry+10" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            ZWrite On
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            UNITY_INSTANCING_BUFFER_START(Burrow)
                UNITY_DEFINE_INSTANCED_PROP(float4, _Digging)
            UNITY_INSTANCING_BUFFER_END(Burrow)
            struct Attributes { float4 positionOS : POSITION; half4 color : COLOR; float2 animation : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; half4 color : COLOR; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                float2 digging = UNITY_ACCESS_INSTANCED_PROP(Burrow, _Digging).xy;
                float age = digging.x, phase = digging.y, progress = saturate(age);
                float3 position = input.positionOS.xyz;
                if (input.animation.x == 1)
                {
                    float lane = input.animation.y;
                    float flight = frac(age * (2.4 + progress * .6) + lane * .173);
                    float lift = sin(flight * PI);
                    float size = (.085 + .035 * fmod(lane, 3)) * smoothstep(0, 1, saturate(lift * 4));
                    float spin = lane * PI / 3 + flight * 5;
                    float s, c; sincos(spin, s, c);
                    position.xz = float2(position.x * c - position.z * s, position.x * s + position.z * c);
                    position *= size;
                    float angle = lane * PI / 3;
                    position += float3(cos(angle) * (.55 + flight * .65), .08 + lift * (.35 + .25 * progress), sin(angle) * (.55 + flight * .65));
                }
                else
                {
                    float heave = .5 + .5 * sin(age * 22 + phase);
                    float height = .22 + .32 * progress + .12 * heave;
                    position.y *= height;
                    float sway = saturate(1 - length(position.xz) / .43);
                    position.xz += float2(sin(age * 19 + phase), cos(age * 23 + phase)) * .045 * sway;
                    if (input.animation.x == 2) position.y += .012;
                }
                output.positionCS = TransformObjectToHClip(position);
                output.color = input.color;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target { return input.color; }
            ENDHLSL
        }
    }
}
