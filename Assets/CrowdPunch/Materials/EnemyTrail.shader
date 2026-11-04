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
            struct Input
            {
                float4 vertex : POSITION;
                half4 color : COLOR;
                float4 capsule : TEXCOORD0;
            };

            struct Output
            {
                float4 position : SV_POSITION;
                half4 color : COLOR;
                float4 capsule : TEXCOORD0;
                float2 world : TEXCOORD1;
            };

            Output vert(Input i)
            {
                Output o;
                o.position = TransformObjectToHClip(i.vertex.xyz);
                o.color = i.color;
                o.capsule = i.capsule;
                o.world = TransformObjectToWorld(i.vertex.xyz).xz;
                return o;
            }

            half4 frag(Output i) : SV_Target
            {
                float2 local = i.capsule.xy;
                float segmentLength = i.capsule.z;
                float radius = i.capsule.w;
                // Match TrailGeometry.Closest, including very short or stationary sections.
                float along = saturate(local.x * segmentLength / max(.000001, segmentLength * segmentLength));
                float distanceToSpine = length(local - float2(along * segmentLength, 0));
                clip(radius - distanceToSpine);

                float time = _Time.y;
                float2 flow = float2(dot(i.world, float2(.8, .6)), dot(i.world, float2(-.6, .8)));
                float swirl = sin(flow.x * 5.2 + sin(flow.y * 3.1 - time * 1.4) + time * .9);
                float edgeDepth = 1.0 - distanceToSpine / radius;
                half rim = 1.0 - smoothstep(.035, .11 + .035 * swirl, edgeDepth);
                half wetEdge = smoothstep(.07, .14, edgeDepth) * (1.0 - smoothstep(.14, .25, edgeDepth));
                half sheen = smoothstep(.86, .98, swirl) *
                    smoothstep(.15, .65, sin(flow.y * 6.0 + flow.x * 1.5 - time * .75));

                // Ground-space bubbles and highlights continue across overlapping sections.
                float2 cell = flow * 2.6;
                float seed = frac(dot(floor(cell), float2(.75487766, .56984029)));
                float2 bubbleUV = (frac(cell) - float2(.34 + .32 * seed, .5)) * float2(.85, 1.1);
                float bubbleRadius = .10 + .04 * sin(time * 2.0 + seed * 6.283);
                float bubbleDistance = length(bubbleUV);
                half bubble = (1.0 - smoothstep(bubbleRadius, bubbleRadius + .03, bubbleDistance)) *
                    smoothstep(bubbleRadius - .06, bubbleRadius - .02, bubbleDistance);

                half3 body = lerp(i.color.rgb * .76, i.color.rgb, smoothstep(-.45, -.2, swirl));
                body = lerp(body, i.color.rgb * .52, rim * .65);
                half highlight = saturate(wetEdge * .22 + sheen * .48 + bubble * .64);
                body = lerp(body, lerp(i.color.rgb, half3(1.0, .97, .84), .75), highlight);
                return half4(body, i.color.a);
            }
            ENDHLSL
        }
    }
}
