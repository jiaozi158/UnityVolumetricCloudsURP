#ifndef URP_VOLUMETRIC_CLOUDS_UPSCALE_HLSL
#define URP_VOLUMETRIC_CLOUDS_UPSCALE_HLSL

// Bilateral kernel size
#define KERNEL_SIZE 3

// Avoid division by zero
#define _UpsampleTolerance 1e-5
//#define _NoiseFilterStrength 0.99999999

half Weight(half distance)
{
    return exp(-distance * distance);
}

half4 BilateralUpscale(float2 screenUV)
{
    // Calculate the current screenUV in a low resolution texture.
    float2 offsetUV = (floor(screenUV * _VolumetricCloudsColorTexture_TexelSize.zw) + 0.5) * _VolumetricCloudsColorTexture_TexelSize.xy;
    half4 centerColor = SAMPLE_TEXTURE2D_X_LOD(_VolumetricCloudsColorTexture, s_linear_clamp_sampler, screenUV, 0).rgba;

    half4 resultColor = half4(0.0, 0.0, 0.0, 0.0);
    half normalization = 0.0;

    for (int i = -KERNEL_SIZE; i <= KERNEL_SIZE; i++)
    {
        for (int j = -KERNEL_SIZE; j <= KERNEL_SIZE; j++)
        {
            half4 neighborColor = SAMPLE_TEXTURE2D_X_LOD(_VolumetricCloudsColorTexture, s_linear_clamp_sampler, offsetUV + float2(i, j) * _VolumetricCloudsColorTexture_TexelSize.xy, 0).rgba;

            half2 distance = (screenUV - offsetUV) * _ScreenParams.xy;

            half colorDiff = length(centerColor.rgba - neighborColor.rgba);

            half weight = Weight(length(distance)) * rcp(colorDiff + _UpsampleTolerance);

            resultColor += neighborColor * weight;
            normalization += weight;
        }
    }
    
    resultColor *= rcp(normalization);

    return resultColor;
}

half BilateralUpscaleTransmittance(float2 screenUV)
{
    // Calculate the current screenUV in a low resolution texture.
    float2 offsetUV = (floor(screenUV * _VolumetricCloudsColorTexture_TexelSize.zw) + 0.5) * _VolumetricCloudsColorTexture_TexelSize.xy;
    half centerTransmittance = SAMPLE_TEXTURE2D_X_LOD(_VolumetricCloudsColorTexture, s_linear_clamp_sampler, screenUV, 0).a;

    half resultTransmittance = 0.0;
    half normalization = 0.0;

    for (int i = -KERNEL_SIZE; i <= KERNEL_SIZE; i++)
    {
        for (int j = -KERNEL_SIZE; j <= KERNEL_SIZE; j++)
        {
            half neighborTransmittance = SAMPLE_TEXTURE2D_X_LOD(_VolumetricCloudsColorTexture, s_linear_clamp_sampler, offsetUV + float2(i, j) * _VolumetricCloudsColorTexture_TexelSize.xy, 0).a;

            half2 distance = (screenUV - offsetUV) * _ScreenParams.xy;

            half transmittanceDiff = abs(centerTransmittance - neighborTransmittance);

            half weight = Weight(length(distance)) * rcp(transmittanceDiff + _UpsampleTolerance);

            resultTransmittance += neighborTransmittance * weight;
            normalization += weight;
        }
    }

    resultTransmittance *= rcp(normalization);

    return resultTransmittance;
}

#endif