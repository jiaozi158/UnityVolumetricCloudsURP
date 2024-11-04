using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

#if UNITY_2023_1_OR_NEWER
[Serializable, VolumeComponentMenu("Sky/Volumetric Clouds (URP)"), SupportedOnRenderPipeline(typeof(UniversalRenderPipelineAsset))]
#else
[Serializable, VolumeComponentMenuForRenderPipeline("Sky/Volumetric Clouds (URP)", typeof(UniversalRenderPipeline))]
#endif
[HelpURL("https://github.com/jiaozi158/UnityVolumetricCloudsURP/tree/main")]
public class VolumetricClouds : VolumeComponent, IPostProcessComponent
{
    /// <summary>
    /// Enable/Disable the volumetric clouds effect.
    /// </summary>
    [Header("General"), Tooltip("Enable/Disable the volumetric clouds effect.")]
    public BoolParameter state = new(false, BoolParameter.DisplayType.EnumPopup, overrideState: true);

    [Tooltip("Indicates whether the clouds are part of the scene or rendered into the skybox.")]
    public BoolParameter localClouds = new(false, BoolParameter.DisplayType.Checkbox, overrideState: false);

    /// <summary>
    /// Specifies the weather preset in Simple mode.
    /// </summary>
    public CloudPresets cloudPreset
    {
        get { return m_CloudPreset.value; }
        set { m_CloudPreset.value = value; ApplyCurrentCloudPreset(); }
    }

    [Header("Shape"), InspectorName("Cloud Preset"), SerializeField, Tooltip("Specifies the weather preset in Simple mode.")]
    private CloudPresetsParameter m_CloudPreset = new(CloudPresets.Cloudy, overrideState: false);

    /// <summary>
    /// Controls the global density of the cloud volume.
    /// </summary>
    [Tooltip("Controls the global density of the cloud volume.")]
    public ClampedFloatParameter densityMultiplier = new(0.4f, 0.0f, 1.0f);

    /// <summary>
    /// Controls the density (Y axis) of the volumetric clouds as a function of the height (X Axis) inside the cloud volume.
    /// </summary>
    [Tooltip("Controls the density (Y axis) of the volumetric clouds as a function of the height (X Axis) inside the cloud volume.")]
    public AnimationCurveParameter densityCurve = new(new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.15f, 1.0f), new Keyframe(1.0f, 0.1f)), false);

    /// <summary>
    /// Controls the larger noise passing through the cloud coverage. A higher value will yield less cloud coverage and smaller clouds.
    /// </summary>
    [Tooltip("Controls the larger noise passing through the cloud coverage. A higher value will yield less cloud coverage and smaller clouds.")]
    public ClampedFloatParameter shapeFactor = new(0.9f, 0.0f, 1.0f);

    /// <summary>
    /// Controls the size of the larger noise passing through the cloud coverage.
    /// </summary>
    [Tooltip("Controls the size of the larger noise passing through the cloud coverage.")]
    public MinFloatParameter shapeScale = new(5.0f, 0.1f);

    /// <summary>
    /// Controls the smaller noise on the edge of the clouds. A higher value will erode clouds more significantly.
    /// </summary>
    [Tooltip("Controls the smaller noise on the edge of the clouds. A higher value will erode clouds more significantly.")]
    public ClampedFloatParameter erosionFactor = new(0.8f, 0.0f, 1.0f);

    /// <summary>
    /// Controls the size of the smaller noise passing through the cloud coverage.
    /// </summary>
    [Tooltip("Controls the size of the smaller noise passing through the cloud coverage.")]
    public MinFloatParameter erosionScale = new(107.0f, 1.0f);

    /// <summary>
    /// Controls the erosion (Y axis) of the volumetric clouds as a function of the height (X Axis) inside the cloud volume.
    /// </summary>
    [Tooltip("Controls the erosion (Y axis) of the volumetric clouds as a function of the height (X Axis) inside the cloud volume.")]
    public AnimationCurveParameter erosionCurve = new(new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.1f, 0.9f), new Keyframe(1.0f, 1.0f)), false);

    /// <summary>
    /// Controls the ambient occlusion (Y axis) of the volumetric clouds as a function of the height (X Axis) inside the cloud volume.
    /// </summary>
    [Tooltip("Controls the ambient occlusion (Y axis) of the volumetric clouds as a function of the height (X Axis) inside the cloud volume.")]
    public AnimationCurveParameter ambientOcclusionCurve = new(new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.25f, 0.4f), new Keyframe(1.0f, 0.0f)), false);

    /// <summary>
    /// When enabled, an additional noise should be evaluated for the clouds in the advanced and manual modes. This increases signficantly the cost of the volumetric clouds.
    /// </summary>
    [Tooltip("When enabled, an additional noise should be evaluated for the clouds in the advanced and manual modes. This increases signficantly the cost of the volumetric clouds.")]
    public BoolParameter microErosion = new(false, BoolParameter.DisplayType.Checkbox, overrideState: false);

    /// <summary>
    /// Controls the smallest noise on the edge of the clouds. A higher value will erode clouds more.
    /// </summary>
    [Tooltip("Controls the smallest noise on the edge of the clouds. A higher value will erode clouds more.")]
    public ClampedFloatParameter microErosionFactor = new(0.5f, 0.0f, 1.0f);

    /// <summary>
    /// Controls the size of the smaller noise passing through the cloud coverage.
    /// </summary>
    [Tooltip("Controls the size of the smaller noise passing through the cloud coverage.")]
    public MinFloatParameter microErosionScale = new(200.0f, 0.1f);

    /// <summary>
    /// Controls the altitude of the bottom of the volumetric clouds volume in meters.
    /// </summary>
    [Tooltip("Controls the altitude of the bottom of the volumetric clouds volume in meters.")]
    public MinFloatParameter bottomAltitude = new(1200.0f, 0.01f);

    /// <summary>
    /// Controls the size of the volumetric clouds volume in meters.
    /// </summary>
    [Tooltip("Controls the size of the volumetric clouds volume in meters.")]
    public MinFloatParameter altitudeRange = new(2000.0f, 100.0f);

    /// <summary>
    /// Controls the world space offset applied when evaluating the larger noise passing through the cloud coverage.
    /// </summary>
    [Tooltip("Controls the world space offset applied when evaluating the larger noise passing through the cloud coverage.")]
    public Vector3Parameter shapeOffset = new(Vector3.zero);

    /// <summary>
    /// Controls the curvature of the cloud volume which defines the distance at which the clouds intersect with the horizon.
    /// </summary>
    [Tooltip("Controls the curvature of the cloud volume which defines the distance at which the clouds intersect with the horizon.")]
    public ClampedFloatParameter earthCurvature = new(0.0f, 0.0f, 1.0f);

    /// <summary>
    /// Sets the global horizontal wind speed in kilometers per hour.
    /// </summary>
    [Header("Wind"), Tooltip("Sets the global horizontal wind speed in kilometers per hour.")]
    public FloatParameter globalSpeed = new(0.0f);

    /// <summary>
    /// Controls the global orientation of the wind relative to the X world vector.
    /// </summary>
    [Tooltip("Controls the global orientation of the wind relative to the X world vector.")]
    public ClampedFloatParameter globalOrientation = new(0.0f, 0.0f, 360.0f);

    /// <summary>
    /// Controls the multiplier to the speed of the larger cloud shapes.
    /// </summary>
    [AdditionalProperty]
    [Tooltip("Controls the multiplier to the speed of the larger cloud shapes.")]
    public ClampedFloatParameter shapeSpeedMultiplier = new(1.0f, 0.0f, 1.0f);

    /// <summary>
    /// Controls the multiplier to the speed of the erosion cloud shapes.
    /// </summary>
    [AdditionalProperty]
    [Tooltip("Controls the multiplier to the speed of the erosion cloud shapes.")]
    public ClampedFloatParameter erosionSpeedMultiplier = new(0.25f, 0.0f, 1.0f);

    /// <summary>
    /// Controls the intensity of the wind-based altitude distortion of the clouds.
    /// </summary>
    [AdditionalProperty]
    [Tooltip("Controls the intensity of the wind-based altitude distortion of the clouds.")]
    public ClampedFloatParameter altitudeDistortion = new(0.25f, -1.0f, 1.0f);
    
    /// <summary>
    /// Controls the vertical wind speed of the larger cloud shapes.
    /// </summary>
    [AdditionalProperty]
    [Tooltip("Controls the vertical wind speed of the larger cloud shapes.")]
    public FloatParameter verticalShapeWindSpeed = new(0.0f);

    /// <summary>
    /// Controls the vertical wind speed of the erosion cloud shapes.
    /// </summary>
    [AdditionalProperty]
    [Tooltip("Controls the vertical wind speed of the erosion cloud shapes.")]
    public FloatParameter verticalErosionWindSpeed = new(0.0f);

    /*
    /// <summary>
    /// Controls the multiplier to the speed of the cloud map.
    /// </summary>
    [AdditionalProperty]
    [Tooltip("Controls the multiplier to the speed of the cloud map.")]
    public ClampedFloatParameter cloudMapSpeedMultiplier = new(0.5f, 0.0f, 1.0f); 
    */

    /// <summary>
    /// Controls the influence of the light probes on the cloud volume. A lower value will suppress the ambient light and produce darker clouds overall.
    /// </summary>
    [Header("Lighting"), Tooltip("Controls the influence of the light probes on the cloud volume. A lower value will suppress the ambient light and produce darker clouds overall.")]
    public ClampedFloatParameter ambientLightProbeDimmer = new(1.0f, 0.0f, 2.0f);

    /// <summary>
    /// Controls the influence of the sun light on the cloud volume. A lower value will suppress the sun light and produce darker clouds overall.
    /// </summary>
    [Tooltip("Controls the influence of the sun light on the cloud volume. A lower value will suppress the sun light and produce darker clouds overall.")]
    public ClampedFloatParameter sunLightDimmer = new(1.0f, 0.0f, 2.0f);

    /// <summary>
    /// Controls how much Erosion Factor is taken into account when computing ambient occlusion. The Erosion Factor parameter is editable in the custom preset, Advanced and Manual Modes.
    /// </summary>
    [AdditionalProperty, Tooltip("Controls how much Erosion Factor is taken into account when computing ambient occlusion. The Erosion Factor parameter is editable in the custom preset, Advanced and Manual Modes.")]
    public ClampedFloatParameter erosionOcclusion = new(0.1f, 0.0f, 1.0f);

    /// <summary>
    /// Specifies the tint of the cloud scattering color.
    /// </summary>
    [Tooltip("Specifies the tint of the cloud scattering color.")]
    public ColorParameter scatteringTint = new(new Color(0.0f, 0.0f, 0.0f, 1.0f));

    /// <summary>
    /// Controls the amount of local scattering in the clouds. A higher value may produce a more powdery or diffused aspect.
    /// </summary>
    [AdditionalProperty, Tooltip("Controls the amount of local scattering in the clouds. A higher value may produce a more powdery or diffused aspect.")]
    public ClampedFloatParameter powderEffectIntensity = new(0.25f, 0.0f, 1.0f);

    /// <summary>
    /// Controls the amount of multi-scattering inside the cloud.
    /// </summary>
    [AdditionalProperty, Tooltip("Controls the amount of multi-scattering inside the cloud.")]
    public ClampedFloatParameter multiScattering = new(0.5f, 0.0f, 1.0f);

    /// <summary>
    /// When enabled, URP evaluates the Volumetric Clouds' shadows. To render the shadows, this property overrides the cookie in the main directional light.
    /// </summary>
    [Header("Shadows"), Tooltip("When enabled, URP evaluates the Volumetric Clouds' shadows. To render the shadows, this property overrides the cookie in the main directional light.")]
    public BoolParameter shadows = new BoolParameter(false);

    /// <summary>
    /// Resolution of the volumetric clouds shadow.
    /// </summary>
    public enum CloudShadowResolution
    {
        /// <summary>The volumetric clouds shadow will be 64x64.</summary>
        VeryLow64 = 64,
        /// <summary>The volumetric clouds shadow will be 128x128.</summary>
        Low128 = 128,
        /// <summary>The volumetric clouds shadow will be 256x256.</summary>
        Medium256 = 256,
        /// <summary>The volumetric clouds shadow will be 512x512.</summary>
        High512 = 512,
        /// <summary>The volumetric clouds shadow will be 1024x1024.</summary>
        Ultra1024 = 1024,
    }

    /// <summary>
    /// Specifies the resolution of the volumetric clouds shadow map.
    /// </summary>
    [Tooltip("Specifies the resolution of the volumetric clouds shadow map.")]
    public CloudShadowResolutionParameter shadowResolution = new CloudShadowResolutionParameter(CloudShadowResolution.Medium256);

    /// <summary>
    /// Sets the size of the area covered by shadow around the camera.
    /// </summary>
    [Tooltip("Sets the size of the area covered by shadow around the camera.")]
    [AdditionalProperty]
    public MinFloatParameter shadowDistance = new MinFloatParameter(8000.0f, 1000.0f);

    /// <summary>
    /// Controls the vertical offset applied to compute the volumetric clouds shadow in meters. To have accurate results, enter the average height at which the volumetric clouds shadow is received.
    /// </summary>
    //[Tooltip("Controls the vertical offset applied to compute the volumetric clouds shadow in meters. To have accurate results, enter the average height at which the volumetric clouds shadow is received.")]
    //public FloatParameter shadowPlaneHeightOffset = new FloatParameter(0.0f);

    /// <summary>
    /// Controls the opacity of the volumetric clouds shadow.
    /// </summary>
    [Tooltip("Controls the opacity of the volumetric clouds shadow.")]
    [AdditionalProperty]
    public ClampedFloatParameter shadowOpacity = new ClampedFloatParameter(1.0f, 0.0f, 1.0f);

    /// <summary>
    /// Controls the shadow opacity when outside the area covered by the volumetric clouds shadow.
    /// </summary>
    [Tooltip("Controls the shadow opacity when outside the area covered by the volumetric clouds shadow.")]
    [AdditionalProperty]
    public ClampedFloatParameter shadowOpacityFallback = new ClampedFloatParameter(0.0f, 0.0f, 1.0f);

    /// <summary>
    /// Temporal accumulation increases the visual quality of clouds by decreasing the noise. A higher value will give you better quality but can create ghosting.
    /// </summary>
    [Header("Quality"), Tooltip("Temporal accumulation increases the visual quality of clouds by decreasing the noise. A higher value will give you better quality but can create ghosting.")]
    public ClampedFloatParameter temporalAccumulationFactor = new(0.95f, 0.0f, 1.0f);

    /// <summary>
    /// Specifies the strength of the perceptual blending for the volumetric clouds. This value should be treated as flag and only be set to 0.0 or 1.0.
    /// </summary>
    [Tooltip("Specifies the strength of the perceptual blending for the volumetric clouds. This value should be treated as flag and only be set to 0.0 or 1.0.")]
    public ClampedFloatParameter perceptualBlending = new(1.0f, 0.0f, 1.0f);

    /// <summary>
    /// Controls the number of steps when evaluating the clouds' transmittance. A higher value may lead to a lower noise level and longer view distance, but at a higher cost.
    /// </summary>
    [Tooltip("Controls the number of steps when evaluating the clouds' transmittance. A higher value may lead to a lower noise level and longer view distance, but at a higher cost.")]
    public ClampedIntParameter numPrimarySteps = new(32, 24, 256);

    /// <summary>
    /// Controls the number of steps when evaluating the clouds' lighting. A higher value will lead to smoother lighting and improved self-shadowing, but at a higher cost.
    /// </summary>
    [Tooltip("Controls the number of steps when evaluating the clouds' lighting. A higher value will lead to smoother lighting and improved self-shadowing, but at a higher cost.")]
    public ClampedIntParameter numLightSteps = new(2, 1, 16);

    /// <summary>
    /// Controls the mode in which the clouds fade in when close to the camera's near plane.
    /// </summary>
    [Tooltip("Controls the mode in which the clouds fade in when close to the camera's near plane.")]
    public CloudFadeInParameter fadeInMode = new(CloudFadeInMode.Automatic);

    /// <summary>
    /// Controls the minimal distance at which clouds start appearing.
    /// </summary>
    [Tooltip("Controls the minimal distance at which clouds start appearing.")]
    public MinFloatParameter fadeInStart = new(0.0f, 0.0f);

    /// <summary>
    /// Controls the distance that it takes for the clouds to reach their complete density.
    /// </summary>
    [Tooltip("Controls the distance that it takes for the clouds to reach their complete density.")]
    public MinFloatParameter fadeInDistance = new(5000.0f, 0.01f);

    public bool IsActive()
    {
        return state.value;
    }

    // This is unused since 2023.1
    public bool IsTileCompatible() => false;

    /// <summary>
    /// The set of available presets for the simple cloud control mode.
    /// </summary>
    public enum CloudPresets
    {
        /// <summary>Smaller clouds that are spread apart.</summary>
        Sparse,
        /// <summary>Medium-sized clouds that partially cover the sky.</summary>
        Cloudy,
        /// <summary>A light layer of cloud that covers the entire sky. Some areas are less dense and let more light through, whereas other areas are more dense and appear darker.</summary>
        Overcast,
        /// <summary>Large dark clouds that cover most of the sky.</summary>
        Stormy,
        /// <summary>Exposes properties that control the shape of the clouds.</summary>
        Custom
    }

    void ApplyCurrentCloudPreset()
    {
        // Apply the currently set preset
        bool microDetails = microErosion.value;
        switch (cloudPreset)
        {
            case CloudPresets.Sparse:
            {
                densityMultiplier.value = 0.4f;
                if (microDetails)
                {
                    shapeFactor.value = 0.925f;
                    shapeScale.value = 5.0f;
                    erosionFactor.value = 0.85f;
                    erosionScale.value = 75.0f;
                    microErosionFactor.value = 0.65f;
                    microErosionScale.value = 300.0f;
                }
                else
                {
                    shapeFactor.value = 0.95f;
                    shapeScale.value = 5.0f;
                    erosionFactor.value = 0.8f;
                    erosionScale.value = 107.0f;
                }

                // Curves
                densityCurve.value = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.05f, 1.0f), new Keyframe(0.75f, 1.0f), new Keyframe(1.0f, 0.0f));
                erosionCurve.value = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.1f, 0.9f), new Keyframe(1.0f, 1.0f));
                ambientOcclusionCurve.value = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.25f, 0.5f), new Keyframe(1.0f, 0.0f));

                // Layer properties
                bottomAltitude.value = 3000.0f;
                altitudeRange.value = 1000.0f;
            }
            break;
            case CloudPresets.Cloudy:
            {
                densityMultiplier.value = 0.4f;

                if (microDetails)
                {
                    shapeFactor.value = 0.875f;
                    shapeScale.value = 5.0f;
                    erosionFactor.value = 0.9f;
                    erosionScale.value = 75.0f;
                    microErosionFactor.value = 0.65f;
                    microErosionScale.value = 300.0f;
                }
                else
                {
                    shapeFactor.value = 0.9f;
                    shapeScale.value = 5.0f;
                    erosionFactor.value = 0.8f;
                    erosionScale.value = 107.0f;
                }

                // Curves
                densityCurve.value = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.15f, 1.0f), new Keyframe(1.0f, 0.1f));
                erosionCurve.value = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.1f, 0.9f), new Keyframe(1.0f, 1.0f));
                ambientOcclusionCurve.value = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.25f, 0.4f), new Keyframe(1.0f, 0.0f));

                // Layer properties
                bottomAltitude.value = 1200.0f;
                altitudeRange.value = 2000.0f;
            }
            break;
            case CloudPresets.Overcast:
            {
                densityMultiplier.value = 0.3f;

                if (microDetails)
                {
                    shapeFactor.value = 0.45f;
                    shapeScale.value = 5.0f;
                    erosionFactor.value = 0.7f;
                    erosionScale.value = 75.0f;
                    microErosionFactor.value = 0.5f;
                    microErosionScale.value = 300.0f;
                }
                else
                {
                    shapeFactor.value = 0.5f;
                    shapeScale.value = 5.0f;
                    erosionFactor.value = 0.5f;
                    erosionScale.value = 107.0f;
                }

                // Curves
                densityCurve.value = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.05f, 1.0f), new Keyframe(0.9f, 0.0f), new Keyframe(1.0f, 0.0f));
                erosionCurve.value = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.1f, 0.9f), new Keyframe(1.0f, 1.0f));
                ambientOcclusionCurve.value = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(1.0f, 0.0f));

                // Layer properties
                bottomAltitude.value = 1500.0f;
                altitudeRange.value = 2500.0f;
            }
            break;
            case CloudPresets.Stormy:
            {
                densityMultiplier.value = 0.35f;

                if (microDetails)
                {
                    shapeFactor.value = 0.825f;
                    shapeScale.value = 5.0f;
                    erosionFactor.value = 0.9f;
                    erosionScale.value = 75.0f;
                    microErosionFactor.value = 0.6f;
                    microErosionScale.value = 300.0f;
                }
                else
                {
                    shapeFactor.value = 0.85f;
                    shapeScale.value = 5.0f;
                    erosionFactor.value = 0.75f;
                    erosionScale.value = 107.0f;
                }

                // Curves
                densityCurve.value = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.037f, 1.0f), new Keyframe(0.6f, 1.0f), new Keyframe(1.0f, 0.0f));
                erosionCurve.value = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.05f, 0.8f), new Keyframe(0.2438f, 0.9498f), new Keyframe(0.5f, 1.0f), new Keyframe(0.93f, 0.9268f), new Keyframe(1.0f, 1.0f));
                ambientOcclusionCurve.value = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.1f, 0.4f), new Keyframe(1.0f, 0.0f));

                // Layer properties
                bottomAltitude.value = 1000.0f;
                altitudeRange.value = 5000.0f;
            }
            break;
            default:
                break;
        }
    }

    /// <summary>
    /// A <see cref="VolumeParameter"/> that holds a <see cref="CloudPresets"/> value.
    /// </summary>
    [Serializable]
    public sealed class CloudPresetsParameter : VolumeParameter<CloudPresets>
    {
        /// <summary>
        /// Creates a new <see cref="CloudPresetsParameter"/> instance.
        /// </summary>
        /// <param name="value">The initial value to store in the parameter.</param>
        /// <param name="overrideState">The initial override state for the parameter.</param>
        public CloudPresetsParameter(CloudPresets value, bool overrideState = false) : base(value, overrideState) { }
    }

    /// <summary>
    /// The set mode in which the clouds fade in when close to the camera
    /// </summary>
    public enum CloudFadeInMode
    {
        /// <summary>The fade in parameters are automatically evaluated.</summary>
        Automatic,
        /// <summary>The fade in parameters are to be defined by the user.</summary>
        Manual
    }

    /// <summary>
    /// A <see cref="VolumeParameter"/> that holds a <see cref="CloudFadeInMode"/> value.
    /// </summary>
    [Serializable]
    public sealed class CloudFadeInParameter : VolumeParameter<CloudFadeInMode>
    {
        /// <summary>
        /// Creates a new <see cref="CloudFadeInParameter"/> instance.
        /// </summary>
        /// <param name="value">The initial value to store in the parameter.</param>
        /// <param name="overrideState">The initial override state for the parameter.</param>
        public CloudFadeInParameter(CloudFadeInMode value, bool overrideState = false) : base(value, overrideState) { }
    }

    /// <summary>
    /// A <see cref="VolumeParameter"/> that holds a <see cref="CloudShadowResolution"/> value.
    /// </summary>
    [Serializable]
    public sealed class CloudShadowResolutionParameter : VolumeParameter<CloudShadowResolution>
    {
        /// <summary>
        /// Creates a new <see cref="CloudShadowResolutionParameter"/> instance.
        /// </summary>
        /// <param name="value">The initial value to store in the parameter.</param>
        /// <param name="overrideState">The initial override state for the parameter.</param>
        public CloudShadowResolutionParameter(CloudShadowResolution value, bool overrideState = false) : base(value, overrideState) { }
    }
}