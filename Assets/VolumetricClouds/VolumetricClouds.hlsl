#ifndef URP_VOLUMETRIC_CLOUDS_HLSL
#define URP_VOLUMETRIC_CLOUDS_HLSL

#include "./VolumetricCloudsUtilities.hlsl"

Ray BuildCloudsRay(float2 screenUV, float3 positionWS, half3 invViewDirWS, bool isOccluded)
{
    Ray ray;

    ray.depth = UNITY_RAW_FAR_CLIP_VALUE;

#ifdef _LOCAL_VOLUMETRIC_CLOUDS
    ray.originWS = GetCameraPositionWS();
#else
    ray.originWS = float3(0.0, 0.0, 0.0);
#endif

    ray.direction = invViewDirWS;

    // Compute the max cloud ray length
#ifdef _LOCAL_VOLUMETRIC_CLOUDS
    ray.maxRayLength = lerp(MAX_SKYBOX_VOLUMETRIC_CLOUDS_DISTANCE, length(positionWS), isOccluded);
#else
    ray.maxRayLength = MAX_SKYBOX_VOLUMETRIC_CLOUDS_DISTANCE;
#endif

    ray.integrationNoise = GenerateRandomFloat(screenUV);

    return ray;
}

RayHit TraceCloudsRay(Ray ray)
{
    RayHit rayHit;
    rayHit.inScattering = half3(0.0, 0.0, 0.0);
    rayHit.transmittance = 1.0;
    rayHit.meanDistance = _MaxCloudDistance;
    rayHit.invalidRay = true;

    // Determine if ray intersects bounding volume, if the ray does not intersect the cloud volume AABB, skip right away
    RayMarchRange rayMarchRange;
    if (GetCloudVolumeIntersection(ray.originWS, ray.direction, rayMarchRange))
    {
        if (ray.maxRayLength >= rayMarchRange.start)
        {
            // Initialize the depth for accumulation
            rayHit.meanDistance = 0.0;

            // Total distance that the ray must travel including empty spaces
            // Clamp the travel distance to whatever is closer
            // - Sky Occluder
            // - Volume end
            // - Far plane
            float totalDistance = min(rayMarchRange.end, ray.maxRayLength) - rayMarchRange.start;

            // Compute the environment lighting that is going to be used for the cloud evaluation
            float3 rayMarchStartPS = ConvertToPS(ray.originWS) + rayMarchRange.start * ray.direction;
            float3 rayMarchEndPS = rayMarchStartPS + totalDistance * ray.direction;

            // Evaluate our integration step
            float stepS = totalDistance / (float)_NumPrimarySteps;

            // Tracking the number of steps that have been made
            int currentIndex = 0;

            // Normalization value of the depth
            float meanDistanceDivider = 0.0;

            // Current position for the evaluation
            float3 currentPositionWS = ray.originWS + rayMarchRange.start * ray.direction;

            // Current Distance that has been marched
            float currentDistance = 0;

            // Initialize the values for the optimized ray marching
            bool activeSampling = true;
            int sequentialEmptySamples = 0;

            // Do the ray march for every step that we can.
            while (currentIndex < (int)_NumPrimarySteps && currentDistance < totalDistance)
            {
                // Compute the camera-distance based attenuation
                float densityAttenuationValue = DensityFadeValue(rayMarchRange.start + currentDistance);
                // Compute the mip offset for the erosion texture
                float erosionMipOffset = ErosionMipOffset(rayMarchRange.start + currentDistance);

                // Should we be evaluating the clouds or just doing the large ray marching
                if (activeSampling)
                {
                    // Convert to planet space
                    //float3 positionPS = currentPositionWS + float3(0, _EarthRadius, 0);
                    float3 positionPS = ConvertToPS(currentPositionWS);

                    // If the density is null, we can skip as there will be no contribution
                    CloudProperties properties;
                    EvaluateCloudProperties(positionPS, 0.0, erosionMipOffset, false, false, properties);

                    // Apply the fade in function to the density
                    properties.density *= densityAttenuationValue;
                    //rayHit.inScattering = properties.density.xxx;

                    if (properties.density > CLOUD_DENSITY_TRESHOLD)
                    {
                        // Contribute to the average depth (must be done first in case we end up inside a cloud at the next step)
                        half transmitanceXdensity = rayHit.transmittance * properties.density;
                        rayHit.meanDistance += (rayMarchRange.start + currentDistance) * transmitanceXdensity;
                        meanDistanceDivider += transmitanceXdensity;

                        // Evaluate the cloud at the position
                        EvaluateCloud(properties, ray.direction, currentPositionWS, rayMarchStartPS, rayMarchEndPS, stepS, currentDistance / totalDistance, rayHit);

                        // if most of the energy is absorbed, just leave.
                        if (rayHit.transmittance < 0.003)
                        {
                            rayHit.transmittance = 0.0;
                            break;
                        }

                        // Reset the empty sample counter
                        sequentialEmptySamples = 0;
                    }
                    else
                        sequentialEmptySamples++;

                    // If it has been more than EMPTY_STEPS_BEFORE_LARGE_STEPS, disable active sampling and start large steps
                    if (sequentialEmptySamples == EMPTY_STEPS_BEFORE_LARGE_STEPS)
                        activeSampling = false;

                    // Do the next step
                    float relativeStepSize = lerp(ray.integrationNoise, 1.0, saturate(currentIndex));
                    currentPositionWS += ray.direction * stepS * relativeStepSize;
                    currentDistance += stepS * relativeStepSize;

                }
                else
                {
                    // Convert to planet space
                    float3 positionPS = ConvertToPS(currentPositionWS);

                    CloudProperties properties;
                    EvaluateCloudProperties(positionPS, 1.0, 0.0, true, false, properties);

                    // Apply the fade in function to the density
                    properties.density *= densityAttenuationValue;

                    // If the density is lower than our tolerance,
                    if (properties.density < CLOUD_DENSITY_TRESHOLD)
                    {
                        currentPositionWS += ray.direction * stepS * 2.0;
                        currentDistance += stepS * 2.0;
                    }
                    else
                    {
                        // Somewhere between this step and the previous clouds started
                        // We reset all the counters and enable active sampling
                        currentPositionWS -= ray.direction * stepS;
                        currentDistance -= stepS;
                        activeSampling = true;
                        sequentialEmptySamples = 0;
                    }
                }
                currentIndex++;
            }

            // Normalized the depth we computed
            if (rayHit.meanDistance == 0.0)
                rayHit.invalidRay = true;
            else
            {
                rayHit.meanDistance /= meanDistanceDivider;
                rayHit.invalidRay = false;
            }
        }
    }
    return rayHit;
}

#endif