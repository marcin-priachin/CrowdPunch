#ifndef CROWD_PUNCH_ART_LAB_LIGHTING
#define CROWD_PUNCH_ART_LAB_LIGHTING
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
float _ArtLabEnvironmentStyle;
float _ArtLabCharacterStyle;
float _CrowdPunchBakedShadowsOnly;

half4 ArtLabFragment(InputData inputData, SurfaceData surface)
{
    #if defined(CROWD_PUNCH_FULL_CEL)
        const float style = 2;
    #elif defined(ART_LAB_CHARACTER)
        float style = _ArtLabCharacterStyle;
    #else
        float style = _ArtLabEnvironmentStyle;
    #endif
    if(style > 3.5) {surface.smoothness=max(surface.smoothness,.55);surface.metallic*=.25;}
    half4 pbr = UniversalFragmentPBR(inputData, surface);
    if (style < .5) return pbr;
    half3 n = normalize(inputData.normalWS);
    half3 v = normalize(inputData.viewDirectionWS);
    half rim = pow(1-saturate(dot(n,v)), 3);
    if (style > 3.5)
    {
        pbr.rgb += rim * surface.albedo * half3(.15,.12,.08);
        return pbr;
    }
    half4 shadowMask = CalculateShadowMask(inputData);
    Light key = GetMainLight(inputData.shadowCoord, inputData.positionWS, shadowMask);
    if(_CrowdPunchBakedShadowsOnly>.5)
        key.shadowAttenuation=BakedShadow(shadowMask,_MainLightOcclusionProbes,half4(1,0,0,0));
    half diffuse = saturate(dot(n,key.direction));
    half feather = style < 1.5 ? .12 : .015;
    half band = .24 + .42*smoothstep(.23-feather,.23+feather,diffuse)
        + .34*smoothstep(.68-feather,.68+feather,diffuse);
    band *= lerp(.55,1,key.shadowAttenuation);
    half3 shadowTint = half3(.68,.76,.90);
    half3 lightTint = lerp(shadowTint,half3(1.05,1.03,.97),band);
    half3 result = surface.albedo * (lightTint * (.32 + .65*band) * min(key.color,half3(1.5,1.5,1.5))
        + inputData.bakedGI*.45) * surface.occlusion;
    uint count = GetAdditionalLightsCount();
    LIGHT_LOOP_BEGIN(count)
        Light light = GetAdditionalLight(lightIndex,inputData.positionWS,shadowMask);
        result += surface.albedo * light.color * saturate(dot(n,light.direction))
            * light.distanceAttenuation * light.shadowAttenuation * .5;
    LIGHT_LOOP_END
    result += surface.emission;
    if (style < 1.5)
    {
        #if defined(ART_LAB_CHARACTER)
            result = lerp(pbr.rgb,result,.8) + rim*surface.albedo*.18;
        #else
            result = lerp(pbr.rgb,result,.3);
        #endif
    }
    else if (style > 2.5)
    {
        // Interior silhouette ink preserves the original deformed surface and depth passes.
        half ink = smoothstep(.46,.62,rim);
        result *= lerp(1,.15,ink);
        float2 dots = frac(inputData.normalizedScreenSpaceUV * _ScreenParams.xy / 7)-.5;
        half dotMask = 1-smoothstep(.13,.23,length(dots));
        result *= 1-dotMask*(1-band)*.22;
    }
    return half4(result,surface.alpha);
}
#endif
