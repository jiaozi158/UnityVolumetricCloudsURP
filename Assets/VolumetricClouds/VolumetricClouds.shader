Shader "Hidden/Sky/VolumetricClouds"
{
    Properties
    {
        [HideInInspector][NoScaleOffset] _CloudLutTexture("Cloud LUT Texture", 2D) = "white" {}
        [HideInInspector][NoScaleOffset] _CloudCurveTexture("Cloud LUT Curve Texture", 2D) = "white" {}
        [NoScaleOffset] _ErosionNoise("Erosion Noise Texture", 3D) = "white" {}
        [NoScaleOffset] _Worley128RGBA("Worley Noise Texture", 3D) = "white" {}
        [HideInInspector] _Seed("Private: Random Seed", Float) = 0.0
        [HideInInspector] _VolumetricCloudsAmbientProbe("Ambient Probe", CUBE) = "grey" {}
        [HideInInspector] _NumPrimarySteps("Ray Steps", Float) = 32.0
        [HideInInspector] _NumLightSteps("Light Steps", Float) = 1.0
        [HideInInspector] _MaxStepSize("Maximum Step Size", Float) = 250.0
        [HideInInspector] _HighestCloudAltitude("Highest Cloud Altitude", Float) = 3200.0
        [HideInInspector] _LowestCloudAltitude("Lowest Cloud Altitude", Float) = 1200.0
        [HideInInspector] _ShapeNoiseOffset("Shape Offset", Vector) = (0.0, 0.0, 0.0, 0.0)
        [HideInInspector] _VerticalShapeNoiseOffset("Vertical Shape Offset", Float) = 0.0
        [HideInInspector] _WindDirection("Wind Direction", Vector) = (0.0, 0.0, 0.0, 0.0)
        [HideInInspector] _WindVector("Wind Vector", Vector) = (0.0, 0.0, 0.0, 0.0)
        [HideInInspector] _VerticalShapeWindDisplacement("Vertical Shape Wind Speed", Float) = 0.0
        [HideInInspector] _VerticalErosionWindDisplacement("Vertical Erosion Wind Speed", Float) = 0.0
        [HideInInspector] _MediumWindSpeed("Shape Speed Multiplier", Float) = 0.0
        [HideInInspector] _SmallWindSpeed("Erosion Speed Multiplier", Float) = 0.0
        [HideInInspector] _AltitudeDistortion("Altitude Distortion", Float) = 0.0
        [HideInInspector] _DensityMultiplier("Density Multiplier", Float) = 0.4
        [HideInInspector] _PowderEffectIntensity("Powder Effect Intensity", Float) = 0.25
        [HideInInspector] _ShapeScale("Shape Scale", Float) = 5.0
        [HideInInspector] _ShapeFactor("Shape Factor", Float) = 0.7
        [HideInInspector] _ErosionScale("Erosion Scale", Float) = 57.0
        [HideInInspector] _ErosionFactor("Erosion Factor", Float) = 0.8
        [HideInInspector] _ErosionOcclusion("Erosion Occlusion", Float) = 0.1
        [HideInInspector] _MicroErosionScale("Micro Erosion Scale", Float) = 122.0
        [HideInInspector] _MicroErosionFactor("Erosion Factor", Float) = 0.7
        [HideInInspector] _FadeInStart("Fade In Start", Float) = 0.0
        [HideInInspector] _FadeInDistance("Fade In Distance", Float) = 5000.0
        [HideInInspector] _MultiScattering("Multi Scattering", Float) = 0.5
        [HideInInspector] _ScatteringTint("Scattering Tint", Color) = (0.0, 0.0, 0.0, 1.0)
        [HideInInspector] _AmbientProbeDimmer("Ambient Light Probe Dimmer", Float) = 1.0
        [HideInInspector] _SunLightDimmer("Sun Light Dimmer", Float) = 1.0
        [HideInInspector] _EarthRadius("Earth Radius", Float) = 6378100.0
        [HideInInspector] _NormalizationFactor("Normalization Factor", Float) = 0.7854
        [HideInInspector] _AccumulationFactor("Accumulation Factor", Float) = 0.95
    }

    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            Name "Volumetric Clouds"
			Tags { "LightMode" = "Volumetric Clouds" }

            Blend One Zero
			
			HLSLPROGRAM
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			// The Blit.hlsl file provides the vertex shader (Vert),
			// input structure (Attributes) and output strucutre (Varyings)
			#include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
			
			#pragma vertex Vert
			#pragma fragment frag

            #pragma target 3.5
            
            TEXTURE2D(_CloudLutTexture);
            TEXTURE2D(_CloudCurveTexture);
            TEXTURE3D(_Worley128RGBA);
            TEXTURE3D(_ErosionNoise);
            TEXTURECUBE(_VolumetricCloudsAmbientProbe);

            SAMPLER(s_linear_repeat_sampler);
            SAMPLER(s_trilinear_repeat_sampler);
            SAMPLER(sampler_VolumetricCloudsAmbientProbe);

            #pragma multi_compile_local_fragment _ _CLOUDS_MICRO_EROSION
            #pragma multi_compile_local_fragment _ _CLOUDS_AMBIENT_PROBE
            #pragma multi_compile_local_fragment _ _LOCAL_VOLUMETRIC_CLOUDS

            #include "./VolumetricClouds.hlsl"
            
            //half4 frag(Varyings input, out float depth : SV_Depth) : SV_Target
            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 screenUV = input.texcoord;

                float depth = SAMPLE_TEXTURE2D_X_LOD(_CameraDepthTexture, sampler_CameraDepthTexture, screenUV, 0).r;
                // If the current pixel is sky
                bool isOccluded = depth != UNITY_RAW_FAR_CLIP_VALUE;

            #if !UNITY_REVERSED_Z
                depth = lerp(UNITY_NEAR_CLIP_VALUE, 1, depth);
            #endif

                // Calculate the virtual position of skybox for view direction calculation
                float3 positionWS = ComputeWorldSpacePosition(screenUV, UNITY_RAW_FAR_CLIP_VALUE, UNITY_MATRIX_I_VP);
                half3 invViewDirWS = normalize(positionWS - GetCameraPositionWS());
                
            #ifndef _LOCAL_VOLUMETRIC_CLOUDS
                // Exit if object is in front of the global cloud.
                if (isOccluded)
                    return half4(0.0, 0.0, 0.0, 1.0);
            #endif

                Ray ray = BuildCloudsRay(screenUV, depth, invViewDirWS, isOccluded);

                // Evaluate the cloud transmittance
                RayHit rayHit = TraceCloudsRay(ray);

                if (rayHit.invalidRay)
                    return half4(0.0, 0.0, 0.0, 1.0);

                //rayHit.meanDistance = min(rayHit.meanDistance, ray.maxRayLength);

            // [Deprecated] Old clouds blending, keep it in case we need to output clouds depth in the future.
            /*
            #ifdef _LOCAL_VOLUMETRIC_CLOUDS
                float3 cloudPosWS = GetCameraPositionWS() + rayHit.meanDistance * invViewDirWS;
                float cloudDepth = ConvertCloudDepth(cloudPosWS);
                // Apply a simple depth test.
            #if !UNITY_REVERSED_Z
                if (cloudDepth >= depth)
                    return half4(0.0, 0.0, 0.0, 1.0);
            #else
                if (cloudDepth <= depth)
                    return half4(0.0, 0.0, 0.0, 1.0);
            #endif
            #endif
            */
                return half4(rayHit.inScattering.xyz, rayHit.transmittance);

            }
            ENDHLSL
        }

        Pass
        {
            Name "Volumetric Clouds Combine"
			Tags { "LightMode" = "Volumetric Clouds Combine" }

            Blend One OneMinusSrcAlpha, Zero One
			
			HLSLPROGRAM
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			// The Blit.hlsl file provides the vertex shader (Vert),
			// input structure (Attributes) and output strucutre (Varyings)
			#include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
			
			#pragma vertex Vert
			#pragma fragment frag

            #pragma target 3.5

            TEXTURE2D_X(_VolumetricCloudsColorTexture);
            float4 _VolumetricCloudsColorTexture_TexelSize;

            // URP pre-defined the following variable on 2023.2+.
        #if UNITY_VERSION < 202320
            float4 _BlitTexture_TexelSize;
        #endif

            SAMPLER(s_linear_clamp_sampler);

            #pragma multi_compile_local_fragment _ _LOW_RESOLUTION_CLOUDS

            #include "./VolumetricCloudsUpscale.hlsl"
            
            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 screenUV = input.texcoord;

            #ifdef _LOW_RESOLUTION_CLOUDS
                half4 cloudsColor = BilateralUpscale(screenUV);
            #else
                half4 cloudsColor = SAMPLE_TEXTURE2D_X_LOD(_VolumetricCloudsColorTexture, s_linear_clamp_sampler, screenUV, 0).rgba;
            #endif

                return half4(cloudsColor.xyz, 1.0 - cloudsColor.w);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Volumetric Clouds Blit"
			Tags { "LightMode" = "Volumetric Clouds" }

            Blend One Zero
			
			HLSLPROGRAM
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			// The Blit.hlsl file provides the vertex shader (Vert),
			// input structure (Attributes) and output strucutre (Varyings)
			#include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
			
			#pragma vertex Vert
			#pragma fragment frag

            #pragma target 3.5
            
            TEXTURE2D_X(_VolumetricCloudsColorTexture);
            float4 _VolumetricCloudsColorTexture_TexelSize;

            SAMPLER(s_linear_clamp_sampler);

            #pragma multi_compile_local_fragment _ _LOW_RESOLUTION_CLOUDS

            #include "./VolumetricCloudsUpscale.hlsl"

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 screenUV = input.texcoord;

                half3 sceneColor = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, s_linear_clamp_sampler, screenUV, 0).rgb;

            #ifdef _LOW_RESOLUTION_CLOUDS
                half transmittance = BilateralUpscaleTransmittance(screenUV);
            #else
                half transmittance = SAMPLE_TEXTURE2D_X_LOD(_VolumetricCloudsColorTexture, s_linear_clamp_sampler, screenUV, 0).a;
            #endif

                // The camera color buffer (_BlitTexture) may not have an alpha channel (32 Bits)
                // We use a custom blit shader instead
                return half4(sceneColor.rgb, transmittance);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Volumetric Clouds Denoise"
			Tags { "LightMode" = "Volumetric Clouds" }

            Blend SrcAlpha OneMinusSrcAlpha, Zero One
			
			HLSLPROGRAM
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
			// The Blit.hlsl file provides the vertex shader (Vert),
			// input structure (Attributes) and output strucutre (Varyings)
			#include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
			
			#pragma vertex Vert
			#pragma fragment frag

            #pragma target 3.5

            #include "./VolumetricCloudsDefs.hlsl"

            #pragma multi_compile_local_fragment _ _LOCAL_VOLUMETRIC_CLOUDS

            TEXTURE2D_X(_VolumetricCloudsColorTexture);
            TEXTURE2D_X(_VolumetricCloudsHistoryTexture);

            SAMPLER(s_point_clamp_sampler);

            // URP pre-defined the following variable on 2023.2+.
        #if UNITY_VERSION < 202320
            float4 _BlitTexture_TexelSize;
        #endif

            half3 SampleColorPoint(float2 uv, float2 texelOffset)
            {
                return SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, s_point_clamp_sampler, uv + _BlitTexture_TexelSize.xy * texelOffset, 0).xyz;
            }
            
            void AdjustColorBox(inout half3 boxMin, inout half3 boxMax, inout half3 moment1, inout half3 moment2, float2 uv, half currX, half currY)
            {
                half3 color = SampleColorPoint(uv, float2(currX, currY));
                boxMin = min(color, boxMin);
                boxMax = max(color, boxMax);
                moment1 += color;
                moment2 += color * color;
            }

            // From Playdead's TAA
            // (half version of HDRP impl)
            half3 ClipToAABBCenter(half3 history, half3 minimum, half3 maximum)
            {
                // note: only clips towards aabb center (but fast!)
                half3 center = 0.5 * (maximum + minimum);
                half3 extents = 0.5 * (maximum - minimum);

                // This is actually `distance`, however the keyword is reserved
                half3 offset = history - center;
                half3 v_unit = offset.xyz / extents.xyz;
                half3 absUnit = abs(v_unit);
                half maxUnit = Max3(absUnit.x, absUnit.y, absUnit.z);
                if (maxUnit > 1.0)
                    return center + (offset / maxUnit);
                else
                    return history;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 screenUV = input.texcoord;

            #ifdef _LOCAL_VOLUMETRIC_CLOUDS
                float depth = SAMPLE_TEXTURE2D_X_LOD(_CameraDepthTexture, sampler_CameraDepthTexture, screenUV, 0).r;

                #if !UNITY_REVERSED_Z
                depth = lerp(UNITY_NEAR_CLIP_VALUE, 1, depth);
                #endif
            #else
                float depth = UNITY_RAW_FAR_CLIP_VALUE;
            #endif

                half4 cloudsColor = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, s_point_clamp_sampler, screenUV, 0).rgba;

                // Color Variance
                half3 colorCenter = cloudsColor.xyz;

                half3 boxMax = colorCenter;
                half3 boxMin = colorCenter;
                half3 moment1 = colorCenter;
                half3 moment2 = colorCenter * colorCenter;

                // adjacent pixels
                AdjustColorBox(boxMin, boxMax, moment1, moment2, screenUV, 0.0, -1.0);
                AdjustColorBox(boxMin, boxMax, moment1, moment2, screenUV, -1.0, 0.0);
                AdjustColorBox(boxMin, boxMax, moment1, moment2, screenUV, 1.0, 0.0);
                AdjustColorBox(boxMin, boxMax, moment1, moment2, screenUV, 0.0, 1.0);

                // Reconstruct world position
                float4 posWS = float4(ComputeWorldSpacePosition(screenUV, depth, UNITY_MATRIX_I_VP), 1.0);

                float4 prevClipPos = mul(_PrevViewProjMatrix, posWS);
                float4 curClipPos = mul(_NonJitteredViewProjMatrix, posWS);

                half2 prevPosCS = prevClipPos.xy / prevClipPos.w;
                half2 curPosCS = curClipPos.xy / curClipPos.w;

                // Backwards camera motion vectors
                half2 velocity = (prevPosCS - curPosCS) * 0.5h;
            #if UNITY_UV_STARTS_AT_TOP
                velocity.y = -velocity.y;
            #endif

                float2 prevUV = screenUV + velocity;
                
                // Re-projected color from last frame.
                half3 prevColor = SAMPLE_TEXTURE2D_X_LOD(_VolumetricCloudsHistoryTexture, s_point_clamp_sampler, prevUV, 0).rgb;

                if (prevUV.x > 1.0 || prevUV.x < 0.0 || prevUV.y > 1.0 || prevUV.y < 0.0 || cloudsColor.a == 1.0)
                {
                    // return 0 alpha to keep the color in render target.
                    return half4(0.0, 0.0, 0.0, 0.0);
                }

                // Can be replace by clamp() to reduce performance cost.
                //prevColor.rgb = ClipToAABBCenter(prevColor.rgb, boxMin, boxMax);
                prevColor.rgb = clamp(prevColor.rgb, boxMin, boxMax);

                half intensity = saturate(min(_AccumulationFactor - (abs(velocity.x)) * _AccumulationFactor, _AccumulationFactor - (abs(velocity.y)) * _AccumulationFactor));

                return half4(prevColor.rgb, intensity);
            }
            ENDHLSL
        }
    }
}
