#ifndef URP_VOLUMETRIC_CLOUDS_TRANSPARENT_UTILITIES_HLSL
#define URP_VOLUMETRIC_CLOUDS_TRANSPARENT_UTILITIES_HLSL

#ifndef SHADERGRAPH_PREVIEW
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

TEXTURE2D_X(_VolumetricCloudsLightingTexture);
TEXTURE2D_X_FLOAT(_VolumetricCloudsDepthTexture);

SAMPLER(s_linear_clamp_sampler);
SAMPLER(s_point_clamp_sampler);
#endif

void ApplyTransparentVolumetricClouds_half(float4 ScreenPositionRaw, half Alpha, half AmbientOcclusion, out half FinalAlpha, out half FinalAO)
{
#ifndef SHADERGRAPH_PREVIEW

	// Calculate screenUV and object depth
	ScreenPositionRaw.xyz /= ScreenPositionRaw.w;
	float2 screenUV = ScreenPositionRaw.xy;
	float depth = ScreenPositionRaw.z;

	half cloudTransmittance = SAMPLE_TEXTURE2D_X_LOD(_VolumetricCloudsLightingTexture, s_linear_clamp_sampler, screenUV, 0).w; // Transmittance 0 means the volumetric cloud alpha is 1
	float cloudDepth = SAMPLE_TEXTURE2D_X_LOD(_VolumetricCloudsDepthTexture, s_point_clamp_sampler, screenUV, 0).x;

    #if UNITY_REVERSED_Z
	bool isCoveredByCloud = depth <= cloudDepth && cloudTransmittance <= 0.8;
    #else
	bool isCoveredByCloud = depth >= cloudDepth && cloudTransmittance <= 0.8;
    #endif

	FinalAlpha = isCoveredByCloud ? Alpha * cloudTransmittance : Alpha;
	FinalAO = isCoveredByCloud ? 0.0 : AmbientOcclusion;

#else // For Shader Graph Preview

	FinalAlpha = 0.0145; // Background UI Color
	FinalAO = AmbientOcclusion;

#endif
}

void ApplyTransparentVolumetricClouds_float(float4 ScreenPositionRaw, half Alpha, half AmbientOcclusion, out half FinalAlpha, out half FinalAO)
{
	ApplyTransparentVolumetricClouds_half(ScreenPositionRaw, Alpha, AmbientOcclusion, FinalAlpha, FinalAO);
}

#endif // URP_VOLUMETRIC_CLOUDS_TRANSPARENT_UTILITIES_HLSL