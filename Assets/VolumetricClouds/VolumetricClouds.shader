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
        [HideInInspector] _CloudNearPlane("Cloud Near Plane", Float) = 0.3
    }

    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        // Pass 0: Volumetric Clouds
        // Pass 1: Upscale + Combine
        // Pass 2: Prepare Denoising
        // Pass 3: Temporal Denoising
        // Pass 4: Volumetric Clouds Shadows
        // Pass 5: Shadows Filtering
        // Pass 6: Testing (output to scene depth)
        // Pass 7: Upscale + Combine (Physically Based Sky)

        Pass
        {
            Name "Volumetric Clouds"
            // Disable material preview to avoid render graph warning of accessing textures outside the render pass
            Tags { "LightMode" = "Volumetric Clouds" "PreviewType" = "None" }

            Blend One Zero
			
            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            // The Blit.hlsl file provides the vertex shader (Vert),
            // input structure (Attributes) and output structure (Varyings)
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
			
            #pragma vertex Vert
            #pragma fragment frag

            #pragma target 3.5
            
            TEXTURE2D(_CloudLutTexture);
            TEXTURE2D(_CloudCurveTexture);
            TEXTURE3D(_Worley128RGBA);
            TEXTURE3D(_ErosionNoise);
            TEXTURECUBE(_VolumetricCloudsAmbientProbe);

            SAMPLER(s_point_clamp_sampler);
            SAMPLER(s_linear_repeat_sampler);
            SAMPLER(s_trilinear_repeat_sampler);
            SAMPLER(sampler_VolumetricCloudsAmbientProbe);

            #pragma multi_compile_local_fragment _ _CLOUDS_MICRO_EROSION
            #pragma multi_compile_local_fragment _ _CLOUDS_AMBIENT_PROBE
            #pragma multi_compile_local_fragment _ _LOCAL_VOLUMETRIC_CLOUDS
            #pragma multi_compile_local_fragment _ _OUTPUT_CLOUDS_DEPTH
            #pragma multi_compile_local_fragment _ _PHYSICALLY_BASED_SUN
            #pragma multi_compile_local_fragment _ _PERCEPTUAL_BLENDING

            #include "./VolumetricClouds.hlsl"

            #define RAW_FAR_CLIP_THRESHOLD 1e-7

            #ifndef sampler_CameraDepthTexture
            #define sampler_CameraDepthTexture sampler_PointClamp
            #endif
            
        #if defined(_OUTPUT_CLOUDS_DEPTH)
            void frag(Varyings input, out half4 cloudsColor : SV_Target0, out float cloudsDepth : SV_Target1)
        #else
            void frag(Varyings input, out half4 cloudsColor : SV_Target)
        #endif
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 screenUV = input.texcoord;

                cloudsColor = half4(0.0, 0.0, 0.0, 1.0);
            #if defined(_OUTPUT_CLOUDS_DEPTH)
                cloudsDepth = UNITY_RAW_FAR_CLIP_VALUE;
            #endif

                // If the current pixel is sky
                float depth = SAMPLE_TEXTURE2D_X_LOD(_CameraDepthTexture, sampler_CameraDepthTexture, screenUV, 0).r;

                // It seems that some developers use shader graph to create the skybox, but cannot disable depth write due to Unity (shader graph) issue
                // For better compatibility with different skybox shaders, we add a depth comparision threshold
                bool isOccluded = abs(depth - UNITY_RAW_FAR_CLIP_VALUE) > RAW_FAR_CLIP_THRESHOLD;
                //bool isOccluded = depth != UNITY_RAW_FAR_CLIP_VALUE;

            #ifndef _LOCAL_VOLUMETRIC_CLOUDS
                // Exit if object is in front of the global cloud.
                if (isOccluded)
                    return;
            #endif

            #if !UNITY_REVERSED_Z
                depth = lerp(UNITY_NEAR_CLIP_VALUE, 1, depth);
            #endif

                // Calculate the virtual position of skybox for view direction calculation
                float3 positionWS = ComputeWorldSpacePosition(screenUV, UNITY_RAW_FAR_CLIP_VALUE, UNITY_MATRIX_I_VP);
                half3 invViewDirWS = normalize(positionWS - GetCameraPositionWS());

                Ray ray = BuildCloudsRay(screenUV, depth, invViewDirWS, isOccluded);

                // Evaluate the cloud transmittance
                RayHit rayHit = TraceCloudsRay(ray);

                if (rayHit.invalidRay)
                    return;

                rayHit.meanDistance = min(rayHit.meanDistance, ray.maxRayLength);

                cloudsColor = half4(rayHit.inScattering.xyz, rayHit.transmittance);

                // Perceptual Blending
            #if defined(_PERCEPTUAL_BLENDING)
                half3 sceneColor = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, s_point_clamp_sampler, screenUV, 0).rgb;
                cloudsColor.a = EvaluateFinalTransmittance(sceneColor.rgb, cloudsColor.a);
            #endif

            #if defined(_OUTPUT_CLOUDS_DEPTH)
                float3 cloudPosWS = GetCameraPositionWS() + rayHit.meanDistance * invViewDirWS;
                float4 cloudPosCS = TransformWorldToHClip(cloudPosWS);
                cloudPosCS.z /= cloudPosCS.w;
                cloudsDepth = rayHit.invalidRay ? UNITY_RAW_FAR_CLIP_VALUE : cloudPosCS.z;

                float cloudDepth = rayHit.meanDistance * dot(ray.direction, -UNITY_MATRIX_V[2].xyz); // Distance to depth
                //cloudsDepth = rayHit.invalidRay ? UNITY_RAW_FAR_CLIP_VALUE : EncodeInfiniteDepth(cloudDepth, _CloudNearPlane);
            #endif

                return;
            }
            ENDHLSL
        }

        Pass
        {
            Name "Volumetric Clouds Combine"
			Tags { "LightMode" = "Volumetric Clouds" }

            Blend One SrcAlpha, Zero One
			
            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            // The Blit.hlsl file provides the vertex shader (Vert),
            // input structure (Attributes) and output structure (Varyings)
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
			
            #pragma vertex Vert
            #pragma fragment frag

            #pragma target 3.5

            TEXTURE2D_X(_VolumetricCloudsLightingTexture);
            float4 _VolumetricCloudsLightingTexture_TexelSize;

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
                half4 cloudsColor = SAMPLE_TEXTURE2D_X_LOD(_VolumetricCloudsLightingTexture, s_linear_clamp_sampler, screenUV, 0).rgba;
            #endif

                return half4(cloudsColor.xyz, cloudsColor.w);
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
            // input structure (Attributes) and output structure (Varyings)
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
			
            #pragma vertex Vert
            #pragma fragment frag

            #pragma target 3.5
            
            TEXTURE2D_X(_VolumetricCloudsLightingTexture);
            float4 _VolumetricCloudsLightingTexture_TexelSize;

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
                half transmittance = SAMPLE_TEXTURE2D_X_LOD(_VolumetricCloudsLightingTexture, s_linear_clamp_sampler, screenUV, 0).a;
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
            // input structure (Attributes) and output structure (Varyings)
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
			
            #pragma vertex Vert
            #pragma fragment frag

            #pragma target 3.5

            #include "./VolumetricCloudsDefs.hlsl"

            #pragma multi_compile_local_fragment _ _LOCAL_VOLUMETRIC_CLOUDS

            TEXTURE2D_X(_VolumetricCloudsLightingTexture);
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

                if (cloudsColor.a == 1.0)
                    return half4(0.0, 0.0, 0.0, 0.0);

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

                if (prevUV.x > 1.0 || prevUV.x < 0.0 || prevUV.y > 1.0 || prevUV.y < 0.0)
                {
                    // return 0 alpha to keep the color in render target.
                    return half4(0.0, 0.0, 0.0, 0.0);
                }

                // Re-projected color from last frame.
                half3 prevColor = SAMPLE_TEXTURE2D_X_LOD(_VolumetricCloudsHistoryTexture, s_point_clamp_sampler, prevUV, 0).rgb;

                // Can be replace by clamp() to reduce performance cost.
                //prevColor.rgb = ClipToAABBCenter(prevColor.rgb, boxMin, boxMax);
                prevColor.rgb = clamp(prevColor.rgb, boxMin, boxMax);

                half intensity = saturate(min(_AccumulationFactor - (abs(velocity.x)) * _AccumulationFactor, _AccumulationFactor - (abs(velocity.y)) * _AccumulationFactor));

                return half4(prevColor.rgb, intensity);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Volumetric Clouds Shadows"
            Tags { "LightMode" = "Volumetric Clouds" }

            Blend One Zero

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            // The Blit.hlsl file provides the vertex shader (Vert),
            // input structure (Attributes) and output structure (Varyings)
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            #pragma vertex Vert
            #pragma fragment TraceVolumetricCloudsShadows

            #pragma target 3.5

            TEXTURE2D(_CloudLutTexture);
            TEXTURE2D(_CloudCurveTexture);
            TEXTURE3D(_Worley128RGBA);
            TEXTURE3D(_ErosionNoise);
            TEXTURECUBE(_VolumetricCloudsAmbientProbe);

            SAMPLER(s_linear_repeat_sampler);
            SAMPLER(s_trilinear_repeat_sampler);
            SAMPLER(sampler_VolumetricCloudsAmbientProbe);

            TEXTURE2D_X(_VolumetricCloudsLightingTexture);
            float4 _VolumetricCloudsLightingTexture_TexelSize;

            SAMPLER(s_linear_clamp_sampler);

            // URP pre-defined the following variable on 2023.2+.
        #if UNITY_VERSION < 202320
            float4 _BlitTexture_TexelSize;
        #endif

            #include "./VolumetricCloudsShadows.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "Volumetric Clouds Shadows Filtering"
            Tags { "LightMode" = "Volumetric Clouds" }

            Blend One Zero
			
            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            // The Blit.hlsl file provides the vertex shader (Vert),
            // input structure (Attributes) and output structure (Varyings)
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
			
            #pragma vertex Vert
            #pragma fragment FilterVolumetricCloudsShadow

            #pragma target 3.5

            TEXTURE2D(_CloudLutTexture);
            TEXTURE2D(_CloudCurveTexture);
            TEXTURE3D(_Worley128RGBA);
            TEXTURE3D(_ErosionNoise);
            TEXTURECUBE(_VolumetricCloudsAmbientProbe);

            SAMPLER(s_linear_repeat_sampler);
            SAMPLER(s_trilinear_repeat_sampler);
            SAMPLER(sampler_VolumetricCloudsAmbientProbe);
            
            TEXTURE2D_X(_VolumetricCloudsLightingTexture);
            float4 _VolumetricCloudsLightingTexture_TexelSize;

            SAMPLER(s_linear_clamp_sampler);

            // URP pre-defined the following variable on 2023.2+.
        #if UNITY_VERSION < 202320
            float4 _BlitTexture_TexelSize;
        #endif

            #include "./VolumetricCloudsShadows.hlsl"
            ENDHLSL
        }

        // TODO: outputs volumetric clouds depth to the "_CameraDepthTexture"
        Pass
        {
            Name "Volumetric Clouds Update Depth"
            Tags { "LightMode" = "Volumetric Clouds" }

            ZWrite On
            ColorMask R
            Blend One Zero
			
            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            // The Blit.hlsl file provides the vertex shader (Vert),
            // input structure (Attributes) and output structure (Varyings)
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
			
            #pragma vertex Vert
            #pragma fragment frag

            #pragma target 3.5

            TEXTURE2D_X(_VolumetricCloudsDepthTexture);
            float4 _VolumetricCloudsDepthTexture_TexelSize;

            // URP pre-defined the following variable on 2023.2+.
        #if UNITY_VERSION < 202320
            float4 _BlitTexture_TexelSize;
        #endif

            SAMPLER(s_point_clamp_sampler);

            //#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            
            float frag(Varyings input, out float depth : SV_Depth) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 screenUV = input.texcoord;

                float sceneDepth = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, s_point_clamp_sampler, screenUV, 0).r;

                float cloudsDepth = SAMPLE_TEXTURE2D_X_LOD(_VolumetricCloudsDepthTexture, s_point_clamp_sampler, screenUV, 0).r;

            #if !UNITY_REVERSED_Z
                depth = min(cloudsDepth, sceneDepth);
            #else
                depth = max(cloudsDepth, sceneDepth);
            #endif
                return depth;
            }
            ENDHLSL
        }

        Pass
        {
            // Skip compiling this pass if PBSky is not installed
            PackageRequirements { "com.jiaozi158.unity-physically-based-sky-urp": "1.0.0" }

            Name "Volumetric Clouds Combine"
            Tags { "LightMode" = "Volumetric Clouds" }

            Blend One SrcAlpha, Zero One

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            // The Blit.hlsl file provides the vertex shader (Vert),
            // input structure (Attributes) and output structure (Varyings)
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            #pragma vertex Vert
            #pragma fragment frag

            #pragma target 3.5

            TEXTURE2D_X(_VolumetricCloudsLightingTexture);
            float4 _VolumetricCloudsLightingTexture_TexelSize;

            // URP pre-defined the following variable on 2023.2+.
        #if UNITY_VERSION < 202320
            float4 _BlitTexture_TexelSize;
        #endif

            half _EnableAtmosphericScattering;

            SAMPLER(s_point_clamp_sampler);

        #ifndef PHYSICALLY_BASED_SKY
            SAMPLER(s_linear_clamp_sampler);
        #endif

            TEXTURE2D_X_FLOAT(_VolumetricCloudsDepthTexture);

            #pragma multi_compile_local_fragment _ _LOW_RESOLUTION_CLOUDS
            #pragma multi_compile_local_fragment _ _OUTPUT_CLOUDS_DEPTH

            #pragma multi_compile_fragment _ PHYSICALLY_BASED_SKY

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

        #if defined(PHYSICALLY_BASED_SKY)
            #include "Packages/com.jiaozi158.unity-physically-based-sky-urp/Shaders/PhysicallyBasedSkyRendering.hlsl"
            #include "Packages/com.jiaozi158.unity-physically-based-sky-urp/Shaders/PhysicallyBasedSkyEvaluation.hlsl"
            #include "Packages/com.jiaozi158.unity-physically-based-sky-urp/Shaders/AtmosphericScattering.hlsl"
        #endif

            #include "./VolumetricCloudsDefs.hlsl"
            #include "./VolumetricCloudsUpscale.hlsl"

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 screenUV = input.texcoord;

            #ifdef _LOW_RESOLUTION_CLOUDS
                half4 cloudsColor = BilateralUpscale(screenUV);
            #else
                half4 cloudsColor = SAMPLE_TEXTURE2D_X_LOD(_VolumetricCloudsLightingTexture, s_linear_clamp_sampler, screenUV, 0).rgba;
            #endif

            #ifdef PHYSICALLY_BASED_SKY
                if (_EnableAtmosphericScattering)
                {
                    // We don't force enabling clouds depth, but it's required for physically "correct" results
                #ifdef _OUTPUT_CLOUDS_DEPTH
                    float depth = SAMPLE_TEXTURE2D_X_LOD(_VolumetricCloudsDepthTexture, s_point_clamp_sampler, screenUV, 0).r;
                #else
                    float depth = UNITY_RAW_FAR_CLIP_VALUE;
                #endif

                    PositionInputs posInput = GetPositionInput(input.positionCS.xy, _ScreenSize.zw, depth, UNITY_MATRIX_I_VP, UNITY_MATRIX_V);

                    half3 V = normalize(GetCameraPositionWS() - posInput.positionWS);

                    half3 volColor, volOpacity = 0.0;

                    EvaluateAtmosphericScattering(posInput, V, volColor, volOpacity);

                    cloudsColor.xyz = volColor * (1.0 - cloudsColor.w) + (1.0 - volOpacity) * cloudsColor.xyz;
                }
            #endif

                return half4(cloudsColor.xyz, cloudsColor.w);
            }
            ENDHLSL
        }
    }
}
