using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Experimental.Rendering;
using Unity.Mathematics;
using static Unity.Mathematics.math;

#if UNITY_6000_0_OR_NEWER
using UnityEngine.Rendering.RenderGraphModule;
#endif

/// <summary>
/// A renderer feature that adds volumetric clouds support to the URP volume.
/// </summary>
[DisallowMultipleRendererFeature("Volumetric Clouds URP")]
[Tooltip("Add this Renderer Feature to support volumetric clouds in URP Volume.")]
[HelpURL("https://github.com/jiaozi158/UnityVolumetricCloudsURP/tree/main")]
public class VolumetricCloudsURP : ScriptableRendererFeature
{
    [Header("Setup")]
    [Tooltip("The material of volumetric clouds shader.")]
    [SerializeField] private Material material;
    [Tooltip("Enable this to render volumetric clouds in Rendering Debugger view. \nThis is disabled by default to avoid affecting the individual lighting previews.")]
    [SerializeField] private bool renderingDebugger = false;

    [Header("Performance")]
    [Tooltip("Specifies if URP renders volumetric clouds in both real-time and baked reflection probes. \nVolumetric clouds in real-time reflection probes may reduce performance.")]
    [SerializeField] private bool reflectionProbe = false;
    [Range(0.25f, 1.0f), Tooltip("The resolution scale for volumetric clouds rendering.")]
    [SerializeField] private float resolutionScale = 0.5f;
    [Tooltip("Select the method to use for upscaling volumetric clouds.")]
    [SerializeField] private CloudsUpscaleMode upscaleMode = CloudsUpscaleMode.Bilinear;
    [Tooltip("Specifies the preferred texture render mode for volumetric clouds. \nThe Copy Texture mode should be more performant.")]
    [SerializeField] private CloudsRenderMode preferredRenderMode = CloudsRenderMode.CopyTexture;

    [Header("Lighting")]
    [Tooltip("Specifies the volumetric clouds ambient probe update frequency.")]
    [SerializeField] private CloudsAmbientMode ambientProbe = CloudsAmbientMode.Dynamic;
    [Tooltip("Specifies if URP calculates physically based sun attenuation for volumetric clouds.")]
    [SerializeField] private bool sunAttenuation = false;

    [Header("Wind")]
    [Tooltip("Enable to reset the wind offsets to their initial states when start playing.")]
    [SerializeField] private bool resetOnStart = true;

    [Header("Depth")]
    [Tooltip("Specifies if URP outputs volumetric clouds average depth to a global shader texture named \"_VolumetricCloudsDepthTexture\".")]
    [SerializeField] private bool outputDepth = true;

    [Header("Experimental"), Tooltip("Specifies if URP also outputs volumetric clouds average depth to \"_CameraDepthTexture\".")]
    [SerializeField] private bool depthTexture = false;

    private const string shaderName = "Hidden/Sky/VolumetricClouds";
    private const string VOLUMETRIC_CLOUDS = "VOLUMETRIC_CLOUDS";
    private const string VISUAL_ENVIRONMENT_DYNAMIC_SKY = "VISUAL_ENVIRONMENT_DYNAMIC_SKY";
    private VolumetricCloudsPass volumetricCloudsPass;
    private VolumetricCloudsAmbientPass volumetricCloudsAmbientPass;
    private VolumetricCloudsShadowsPass volumetricCloudsShadowsPass;

    // Pirnt message only once.
    private bool isLogPrinted = false;
    private bool isCookiePrinted = false;

    /// <summary>
    /// Gets or sets the material of volumetric clouds shader.
    /// </summary>
    /// <value>
    /// The material of volumetric clouds shader.
    /// </value>
    public Material CloudsMaterial
    {
        get { return material; }
        set { material = (value.shader == Shader.Find(shaderName)) ? value : material; }
    }

    /// <summary>
    /// Gets or sets a value indicating whether to render volumetric clouds in Rendering Debugger view.
    /// </summary>
    /// <value>
    /// <c>true</c> if rendering volumetric clouds in Rendering Debugger view; otherwise, <c>false</c>.
    /// </value>
    /// <remarks>
    /// This is disabled by default to avoid affecting the individual lighting previews.
    /// </remarks>
    public bool RenderingDebugger
    {
        get { return renderingDebugger; }
        set { renderingDebugger = value; }
    }

    /// <summary>
    /// Gets or sets the resolution scale for volumetric clouds rendering.
    /// </summary>
    /// <value>
    /// The resolution scale for volumetric clouds rendering, ranging from 0.25 to 1.0.
    /// </value>
    public float ResolutionScale
    {
        get { return resolutionScale; }
        set { resolutionScale = Mathf.Clamp(value, 0.25f, 1.0f); }
    }

    /// <summary>
    /// Gets or sets the preferred texture render mode for volumetric clouds.
    /// </summary>
    /// <value>
    /// The preferred texture render mode for volumetric clouds, either CopyTexture or BlitTexture.
    /// </value>
    /// <remarks>
    /// The CopyTexture mode should be more performant.
    /// </remarks>
    public CloudsRenderMode PreferredRenderMode
    {
        get { return preferredRenderMode; }
        set { preferredRenderMode = value; }
    }

    /// <summary>
    /// Gets or sets the ambient probe update frequency for volumetric clouds.
    /// </summary>
    /// <value>
    /// The ambient probe update frequency for volumetric clouds, either Static or Dynamic.
    /// </value>
    public CloudsAmbientMode AmbientUpdateMode
    {
        get { return ambientProbe; }
        set { ambientProbe = value; }
    }

    /// <summary>
    /// Gets or sets the method used for upscaling volumetric clouds.
    /// </summary>
    /// <value>
    /// The method to use for upscaling volumetric clouds.
    /// </value>
    public CloudsUpscaleMode UpscaleMode
    {
        get { return upscaleMode; }
        set { upscaleMode = value; }
    }

    /// <summary>
    /// Gets or sets a value indicating whether to reset wind offsets for volumetric clouds when entering playmode.
    /// </summary>
    /// <value>
    /// <c>true</c> if resetting wind offsets when entering playmode; otherwise, <c>false</c>.
    /// </value>
    public bool ResetWindOnStart
    {
        get { return resetOnStart; }
        set { resetOnStart = value; }
    }

    /// <summary>
    /// Gets or sets a value indicating whether URP calculates physically based sun attenuation for volumetric clouds.
    /// </summary>
    /// <value>
    /// <c>true</c> if URP calculates physically based sun attenuation for volumetric clouds; otherwise, <c>false</c>.
    /// </value>
    public bool SunAttenuation
    {
        get { return sunAttenuation; }
        set { sunAttenuation = value; }
    }

    /// <summary>
    /// Gets or sets a value indicating whether URP outputs volumetric clouds average depth to a global shader texture named "_VolumetricCloudsDepthTexture".
    /// </summary>
    /// <value>
    /// <c>true</c> if URP outputs volumetric clouds average depth; otherwise, <c>false</c>.
    /// </value>
    public bool OutputCloudsDepth
    {
        get { return outputDepth; }
        set { outputDepth = value; }
    }

    /// <summary>
    /// Gets or sets a value indicating whether URP also outputs volumetric clouds average depth to "_CameraDepthTexture".
    /// </summary>
    public bool OutputToSceneDepth
    {
        get { return depthTexture; }
        set { depthTexture = value; }
    }

    public enum CloudsRenderMode
    {
        [Tooltip("Always use Blit() to copy render textures.")]
        BlitTexture = 0,

        [Tooltip("Use CopyTexture() to copy render textures when supported.")]
        CopyTexture = 1
    }

    public enum CloudsAmbientMode
    {
        [Tooltip("Use URP default static ambient probe for volumetric clouds rendering.")]
        Static,

        [Tooltip("Use a fast dynamic ambient probe for volumetric clouds rendering.")]
        Dynamic
    }

    public enum CloudsUpscaleMode
    {
        [Tooltip("Use simple but fast filtering for volumetric clouds upscale.")]
        Bilinear,

        [Tooltip("Use more computationally expensive filtering for volumetric clouds upscale. \nThis blurs the cloud details but reduces the noise that may appear at lower clouds resolutions.")]
        Bilateral
    }

    public override void Create()
    {
        // Check if the volumetric clouds material uses the correct shader.
        if (material != null)
        {
            if (material.shader != Shader.Find(shaderName))
            {
            #if UNITY_EDITOR || DEBUG
                Debug.LogErrorFormat("Volumetric Clouds URP: Material shader is not {0}.", shaderName);
            #endif
                return;
            }
        }
        // No material applied.
        else
        {
        #if UNITY_EDITOR || DEBUG
            Debug.LogError("Volumetric Clouds URP: Material is empty.");
        #endif
            return;
        }

        // Store the current enable state of volumetric clouds in a global shader keyword
        bool isDebugger = DebugManager.instance.isAnyDebugUIActive;
        var stack = VolumeManager.instance.stack;
        VolumetricClouds cloudsVolume = stack.GetComponent<VolumetricClouds>();
        bool isVolumeActive = cloudsVolume != null && cloudsVolume.IsActive() && (!isDebugger || renderingDebugger);

        if (!isActive || !isVolumeActive)
            Shader.DisableKeyword(VOLUMETRIC_CLOUDS);
        else
            Shader.EnableKeyword(VOLUMETRIC_CLOUDS);

        if (volumetricCloudsPass == null)
        {
            volumetricCloudsPass = new(material, resolutionScale);
            volumetricCloudsPass.renderPassEvent = RenderPassEvent.BeforeRenderingTransparents; // Use camera previous matrix to do reprojection
        }
        else
        {
            // Update every frame to support runtime changes to these properties.
            volumetricCloudsPass.resolutionScale = resolutionScale;
            volumetricCloudsPass.upscaleMode = upscaleMode;
            volumetricCloudsPass.dynamicAmbientProbe = ambientProbe == CloudsAmbientMode.Dynamic;
        }

        if (volumetricCloudsAmbientPass == null)
        {
            volumetricCloudsAmbientPass = new(material);
            volumetricCloudsAmbientPass.renderPassEvent = RenderPassEvent.BeforeRenderingTransparents - 1;
        }

        if (volumetricCloudsShadowsPass == null)
        {
            volumetricCloudsShadowsPass = new(material);
            volumetricCloudsShadowsPass.renderPassEvent = RenderPassEvent.BeforeRendering;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (volumetricCloudsPass != null)
            volumetricCloudsPass.Dispose();
        if (volumetricCloudsAmbientPass != null)
            volumetricCloudsAmbientPass.Dispose();
        if (volumetricCloudsShadowsPass != null)
            volumetricCloudsShadowsPass.Dispose();
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (material == null)
        {
        #if UNITY_EDITOR || DEBUG
            Debug.LogErrorFormat("Volumetric Clouds URP: Material is empty.");
        #endif
            return;
        }

    #if UNITY_EDITOR
        bool isEditingPrefab = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage() != null;
        bool isSceneViewFocused = UnityEditor.SceneView.lastActiveSceneView != null && UnityEditor.SceneView.lastActiveSceneView.hasFocus;
        // Disable Volumetric Clouds when entering prefab mode.
        if (isEditingPrefab && isSceneViewFocused)
            return;
    #endif

        var stack = VolumeManager.instance.stack;
        VolumetricClouds cloudsVolume = stack.GetComponent<VolumetricClouds>();
        ColorAdjustments colorAdjustments = stack.GetComponent<ColorAdjustments>();
        bool isDebugger = DebugManager.instance.isAnyDebugUIActive;
        bool isVolumeActive = cloudsVolume != null && cloudsVolume.IsActive() && (!isDebugger || renderingDebugger);

        bool isProbeCamera = renderingData.cameraData.cameraType == CameraType.Reflection && reflectionProbe;

        if (isVolumeActive)
            Shader.EnableKeyword(VOLUMETRIC_CLOUDS);
        else
            Shader.DisableKeyword(VOLUMETRIC_CLOUDS);

        if (isVolumeActive && (renderingData.cameraData.cameraType == CameraType.Game || renderingData.cameraData.cameraType == CameraType.SceneView || isProbeCamera))
        {
        #if URP_PBSKY
            VisualEnvironment visualEnvironment = stack.GetComponent<VisualEnvironment>();

            // Check if the ambient probe is already updating dynamically.
            bool isDynamicPbrSky = visualEnvironment != null && visualEnvironment.IsActive() && visualEnvironment.skyAmbientMode.value == VisualEnvironment.SkyAmbientMode.Dynamic && Shader.IsKeywordEnabled(VISUAL_ENVIRONMENT_DYNAMIC_SKY);
            bool dynamicAmbientProbe = !isDynamicPbrSky && ambientProbe == CloudsAmbientMode.Dynamic;
        #else
            bool dynamicAmbientProbe = ambientProbe == CloudsAmbientMode.Dynamic;
        #endif
            volumetricCloudsPass.cloudsVolume = cloudsVolume;
            volumetricCloudsPass.colorAdjustments = colorAdjustments;
            volumetricCloudsPass.dynamicAmbientProbe = dynamicAmbientProbe;
            volumetricCloudsPass.renderMode = preferredRenderMode;
            volumetricCloudsPass.resetWindOnStart = resetOnStart;
            volumetricCloudsPass.outputDepth = depthTexture || outputDepth; // Implicitly enable clouds depth when we need to output to scene depth
            volumetricCloudsPass.outputToSceneDepth = depthTexture;
            volumetricCloudsPass.sunAttenuation = sunAttenuation;

            volumetricCloudsShadowsPass.cloudsVolume = cloudsVolume;

        #if URP_PBSKY
            PhysicallyBasedSky pbrSky = stack.GetComponent<PhysicallyBasedSky>();
            Fog fog = stack.GetComponent<Fog>();
            volumetricCloudsPass.hasAtmosphericScattering = visualEnvironment != null && visualEnvironment.IsActive() && visualEnvironment.skyType.value == (int)VisualEnvironment.SkyType.PhysicallyBased && pbrSky != null && pbrSky.IsActive() && pbrSky.atmosphericScattering.value;
            volumetricCloudsPass.hasAtmosphericScattering |= fog != null && fog.IsActive();
            volumetricCloudsPass.visualEnvVolume = visualEnvironment;
        #else
            volumetricCloudsPass.hasAtmosphericScattering = false;
        #endif

            renderer.EnqueuePass(volumetricCloudsPass);

            if (cloudsVolume.shadows.value)
            {
                // Check if URP supports "Light Cookies"
                UniversalRenderPipelineAsset asset = UniversalRenderPipeline.asset;
                if (asset.supportsLightCookies)
                {
                    isCookiePrinted = false;
                #if URP_PBSKY
                    volumetricCloudsShadowsPass.visualEnvVolume = visualEnvironment;
                #endif
                    renderer.EnqueuePass(volumetricCloudsShadowsPass);
                }
            #if UNITY_EDITOR || DEBUG
                else
                {
                    // URP may have stripped light cookie varients (in build), so skip the shadow cookie rendering
                    if (!isCookiePrinted) { Debug.LogWarning("Volumetric Clouds URP: Light Cookies are disabled in the active URP asset. The volumetric clouds shadows will not be rendered."); isCookiePrinted = true; }
                }
            #endif
            }

            // No need to render dynamic ambient probe for reflection probes.
            if (dynamicAmbientProbe && !isProbeCamera) { renderer.EnqueuePass(volumetricCloudsAmbientPass); }

            isLogPrinted = false;
        }
    #if UNITY_EDITOR || DEBUG
        else if (isDebugger && !renderingDebugger && !isLogPrinted)
        {
            Debug.Log("Volumetric Clouds URP: Disable effect to avoid affecting rendering debugging.");
            isLogPrinted = true;
        }
    #endif
    }

    public class VolumetricCloudsPass : ScriptableRenderPass
    {
        private const string rasterPassProfilerTag = "Trace Volumetric Clouds";
        private const string profilerTag = "Volumetric Clouds";
        private readonly ProfilingSampler m_ProfilingSampler = new ProfilingSampler(profilerTag);

        public VolumetricClouds cloudsVolume;
        public ColorAdjustments colorAdjustments;
    #if URP_PBSKY
        public VisualEnvironment visualEnvVolume;
    #endif
        public CloudsRenderMode renderMode;
        public float resolutionScale;
        public CloudsUpscaleMode upscaleMode;
        public bool dynamicAmbientProbe;
        public bool resetWindOnStart;
        public bool outputDepth;
        public bool outputToSceneDepth;
        public bool sunAttenuation;
        public bool hasAtmosphericScattering;

        private bool denoiseClouds;

        private RTHandle cloudsColorHandle;
        private RTHandle cloudsDepthHandle;
        private RTHandle accumulateHandle;
        private RTHandle historyHandle;
        private RTHandle cameraTempDepthHandle;

        private readonly Material cloudsMaterial;

        private readonly bool fastCopy = (SystemInfo.copyTextureSupport & CopyTextureSupport.Basic) != 0;

        private static readonly int numPrimarySteps = Shader.PropertyToID("_NumPrimarySteps");
        private static readonly int numLightSteps = Shader.PropertyToID("_NumLightSteps");
        private static readonly int maxStepSize = Shader.PropertyToID("_MaxStepSize");
        private static readonly int highestCloudAltitude = Shader.PropertyToID("_HighestCloudAltitude");
        private static readonly int lowestCloudAltitude = Shader.PropertyToID("_LowestCloudAltitude");
        private static readonly int shapeNoiseOffset = Shader.PropertyToID("_ShapeNoiseOffset");
        private static readonly int verticalShapeNoiseOffset = Shader.PropertyToID("_VerticalShapeNoiseOffset");
        private static readonly int globalOrientation = Shader.PropertyToID("_WindDirection");
        private static readonly int globalSpeed = Shader.PropertyToID("_WindVector");
        private static readonly int verticalShapeDisplacement = Shader.PropertyToID("_VerticalShapeWindDisplacement");
        private static readonly int verticalErosionDisplacement = Shader.PropertyToID("_VerticalErosionWindDisplacement");
        private static readonly int shapeSpeedMultiplier = Shader.PropertyToID("_MediumWindSpeed");
        private static readonly int erosionSpeedMultiplier = Shader.PropertyToID("_SmallWindSpeed");
        private static readonly int altitudeDistortion = Shader.PropertyToID("_AltitudeDistortion");
        private static readonly int densityMultiplier = Shader.PropertyToID("_DensityMultiplier");
        private static readonly int powderEffectIntensity = Shader.PropertyToID("_PowderEffectIntensity");
        private static readonly int shapeScale = Shader.PropertyToID("_ShapeScale");
        private static readonly int shapeFactor = Shader.PropertyToID("_ShapeFactor");
        private static readonly int erosionScale = Shader.PropertyToID("_ErosionScale");
        private static readonly int erosionFactor = Shader.PropertyToID("_ErosionFactor");
        private static readonly int erosionOcclusion = Shader.PropertyToID("_ErosionOcclusion");
        private static readonly int microErosionScale = Shader.PropertyToID("_MicroErosionScale");
        private static readonly int microErosionFactor = Shader.PropertyToID("_MicroErosionFactor");
        private static readonly int fadeInStart = Shader.PropertyToID("_FadeInStart");
        private static readonly int fadeInDistance = Shader.PropertyToID("_FadeInDistance");
        private static readonly int multiScattering = Shader.PropertyToID("_MultiScattering");
        private static readonly int scatteringTint = Shader.PropertyToID("_ScatteringTint");
        private static readonly int ambientProbeDimmer = Shader.PropertyToID("_AmbientProbeDimmer");
        private static readonly int sunLightDimmer = Shader.PropertyToID("_SunLightDimmer");
        private static readonly int earthRadius = Shader.PropertyToID("_EarthRadius");
        private static readonly int accumulationFactor = Shader.PropertyToID("_AccumulationFactor");
        private static readonly int improvedTransmittanceBlend = Shader.PropertyToID("_ImprovedTransmittanceBlend");
        //private static readonly int normalizationFactor = Shader.PropertyToID("_NormalizationFactor");
        private static readonly int cloudsCurveLut = Shader.PropertyToID("_CloudCurveTexture");
        private static readonly int cloudnearPlane = Shader.PropertyToID("_CloudNearPlane");
        private static readonly int sunColor = Shader.PropertyToID("_SunColor");
        private static readonly int planetCenterRadius = Shader.PropertyToID("_PlanetCenterRadius");
        private static readonly int postExposure = Shader.PropertyToID("_PostExposure");

        private static readonly int cameraDepthTexture = Shader.PropertyToID(_CameraDepthTexture);
        private static readonly int volumetricCloudsColorTexture = Shader.PropertyToID(_VolumetricCloudsColorTexture);
        private static readonly int volumetricCloudsHistoryTexture = Shader.PropertyToID(_VolumetricCloudsHistoryTexture);
        private static readonly int volumetricCloudsDepthTexture = Shader.PropertyToID(_VolumetricCloudsDepthTexture);
        private static readonly int volumetricCloudsLightingTexture = Shader.PropertyToID(_VolumetricCloudsLightingTexture); // Same as "_VolumetricCloudsColorTexture"

        // unity_SH is not available when performing full screen blit pass
        private static readonly int shAr = Shader.PropertyToID("clouds_SHAr");
        private static readonly int shAg = Shader.PropertyToID("clouds_SHAg");
        private static readonly int shAb = Shader.PropertyToID("clouds_SHAb");
        private static readonly int shBr = Shader.PropertyToID("clouds_SHBr");
        private static readonly int shBg = Shader.PropertyToID("clouds_SHBg");
        private static readonly int shBb = Shader.PropertyToID("clouds_SHBb");
        private static readonly int shC = Shader.PropertyToID("clouds_SHC");

        private const string localClouds = "_LOCAL_VOLUMETRIC_CLOUDS";
        private const string microErosion = "_CLOUDS_MICRO_EROSION";
        private const string lowResClouds = "_LOW_RESOLUTION_CLOUDS";
        private const string cloudsAmbientProbe = "_CLOUDS_AMBIENT_PROBE";
        private const string outputCloudsDepth = "_OUTPUT_CLOUDS_DEPTH";
        private const string physicallyBasedSun = "_PHYSICALLY_BASED_SUN";
        private const string perceptualBlending = "_PERCEPTUAL_BLENDING";

        private const string _CameraDepthTexture = "_CameraDepthTexture";
        private const string _VolumetricCloudsColorTexture = "_VolumetricCloudsColorTexture";
        private const string _VolumetricCloudsHistoryTexture = "_VolumetricCloudsHistoryTexture";
        private const string _VolumetricCloudsAccumulationTexture = "_VolumetricCloudsAccumulationTexture";
        private const string _VolumetricCloudsDepthTexture = "_VolumetricCloudsDepthTexture";
        private const string _VolumetricCloudsLightingTexture = "_VolumetricCloudsLightingTexture"; // Same as "_VolumetricCloudsColorTexture"
        private const string _CameraTempDepthTexture = "_CameraTempDepthTexture";

        private static readonly Vector4 m_ScaleBias = new Vector4(1.0f, 1.0f, 0.0f, 0.0f);

        private readonly static FieldInfo depthTextureFieldInfo = typeof(UniversalRenderer).GetField("m_DepthTexture", BindingFlags.NonPublic | BindingFlags.Instance);

        private Texture2D customLutPresetMap;
        private readonly Color[] customLutColorArray = new Color[customLutMapResolution];

        public const float earthRad = 6378100.0f;
        public const float windNormalizationFactor = 100000.0f; // NOISE_TEXTURE_NORMALIZATION_FACTOR in "VolumetricCloudsUtilities.hlsl"
        public const int customLutMapResolution = 64;

        // Wind offsets
        private bool prevIsPlaying;
        private float prevTotalTime = -1.0f;
        private float verticalShapeOffset = 0.0f;
        private float verticalErosionOffset = 0.0f;
        private Vector2 windVector = Vector2.zero;

        private static float square(float x) => x * x;

        private void UpdateMaterialProperties(Camera camera)
        {
        #if URP_PBSKY
            bool isVolumeActive = visualEnvVolume != null && visualEnvVolume.IsActive() && visualEnvVolume.skyType.value != 0;
            if (isVolumeActive)
            {
                if (visualEnvVolume.renderingSpace.value == VisualEnvironment.RenderingSpace.World) { cloudsMaterial.EnableKeyword(localClouds); }
                else { cloudsMaterial.DisableKeyword(localClouds); }
            }
            else
            {
                if (cloudsVolume.localClouds.value) { cloudsMaterial.EnableKeyword(localClouds); }
                else { cloudsMaterial.DisableKeyword(localClouds); }
            }
        #else
            if (cloudsVolume.localClouds.value) { cloudsMaterial.EnableKeyword(localClouds); }
            else { cloudsMaterial.DisableKeyword(localClouds); }
        #endif

            if (cloudsVolume.microErosion.value && cloudsVolume.microErosionFactor.value > 0.0f) { cloudsMaterial.EnableKeyword(microErosion); }
            else { cloudsMaterial.DisableKeyword(microErosion); }

            if (resolutionScale < 1.0f && upscaleMode == CloudsUpscaleMode.Bilateral) { cloudsMaterial.EnableKeyword(lowResClouds); }
            else { cloudsMaterial.DisableKeyword(lowResClouds); }

            if (dynamicAmbientProbe) { cloudsMaterial.EnableKeyword(cloudsAmbientProbe); }
            else { cloudsMaterial.DisableKeyword(cloudsAmbientProbe); }

            if (outputDepth) { cloudsMaterial.EnableKeyword(outputCloudsDepth); }
            else { cloudsMaterial.DisableKeyword(outputCloudsDepth); }

            if (sunAttenuation) { cloudsMaterial.EnableKeyword(physicallyBasedSun); }
            else { cloudsMaterial.DisableKeyword(physicallyBasedSun); }

            if (cloudsVolume.perceptualBlending.value > 0.0f) { cloudsMaterial.EnableKeyword(perceptualBlending); }
            else { cloudsMaterial.DisableKeyword(perceptualBlending); }

            cloudsMaterial.SetFloat(numPrimarySteps, cloudsVolume.numPrimarySteps.value);
            cloudsMaterial.SetFloat(numLightSteps, cloudsVolume.numLightSteps.value);
            cloudsMaterial.SetFloat(maxStepSize, cloudsVolume.altitudeRange.value / 8.0f);

        #if URP_PBSKY
            float4 planetCenterRad = visualEnvVolume.GetPlanetCenterRadius(camera.transform.position);
            float actualEarthRad = isVolumeActive ? planetCenterRad.w : Mathf.Lerp(1.0f, 0.025f, cloudsVolume.earthCurvature.value) * earthRad;
            planetCenterRad = visualEnvVolume.renderingSpace.value == VisualEnvironment.RenderingSpace.World ? planetCenterRad : float4(0.0f, -actualEarthRad, 0.0f, actualEarthRad);

            cloudsMaterial.SetVector(planetCenterRadius, planetCenterRad);
        #else
            float actualEarthRad = Mathf.Lerp(1.0f, 0.025f, cloudsVolume.earthCurvature.value) * earthRad;

            cloudsMaterial.SetVector(planetCenterRadius, float4(0.0f, -actualEarthRad, 0.0f, actualEarthRad));
        #endif

            float bottomAltitude = cloudsVolume.bottomAltitude.value + actualEarthRad;
            float highestAltitude = bottomAltitude + cloudsVolume.altitudeRange.value;
            cloudsMaterial.SetFloat(highestCloudAltitude, highestAltitude);
            cloudsMaterial.SetFloat(lowestCloudAltitude, bottomAltitude);
            cloudsMaterial.SetVector(shapeNoiseOffset, new Vector4(cloudsVolume.shapeOffset.value.x, cloudsVolume.shapeOffset.value.z, 0.0f, 0.0f));
            cloudsMaterial.SetFloat(verticalShapeNoiseOffset, cloudsVolume.shapeOffset.value.y);

            // Wind animation
            float totalTime = Application.isPlaying ? Time.time : Time.realtimeSinceStartup;
            float deltaTime = totalTime - prevTotalTime;
            if (prevTotalTime == -1.0f)
                deltaTime = 0.0f;

        #if UNITY_EDITOR
            if (UnityEditor.EditorApplication.isPaused)
                deltaTime = 0.0f;
        #endif

            // Conversion from km/h to m/s is the 0.277778f factor
            // We apply a minus to see something moving in the right direction
            deltaTime *= -0.277778f;

            float theta = cloudsVolume.globalOrientation.value / 180.0f * Mathf.PI;
            Vector2 windDirection = new Vector2(Mathf.Cos(theta), Mathf.Sin(theta));
            
            if (resetWindOnStart && prevIsPlaying != Application.isPlaying)
            {
                windVector = Vector2.zero;
                verticalShapeOffset = 0.0f;
                verticalErosionOffset = 0.0f;
            }
            else
            {
                windVector += deltaTime * cloudsVolume.globalSpeed.value * windDirection;
                verticalShapeOffset += deltaTime * cloudsVolume.verticalShapeWindSpeed.value;
                verticalErosionOffset += deltaTime * cloudsVolume.erosionSpeedMultiplier.value;
                // Reset the accumulated wind variables periodically to avoid extreme values.
                windVector.x %= windNormalizationFactor;
                windVector.y %= windNormalizationFactor;
                verticalShapeOffset %= windNormalizationFactor;
                verticalErosionOffset %= windNormalizationFactor;
            }

            // Update previous values
            prevTotalTime = totalTime;
            prevIsPlaying = Application.isPlaying;

            // We apply a minus to see something moving in the right direction
            cloudsMaterial.SetVector(globalOrientation, new Vector4(-windDirection.x, -windDirection.y, 0.0f, 0.0f));
            cloudsMaterial.SetVector(globalSpeed, windVector);
            cloudsMaterial.SetFloat(shapeSpeedMultiplier, cloudsVolume.shapeSpeedMultiplier.value);
            cloudsMaterial.SetFloat(erosionSpeedMultiplier, cloudsVolume.erosionSpeedMultiplier.value);
            cloudsMaterial.SetFloat(altitudeDistortion, cloudsVolume.altitudeDistortion.value * 0.25f);
            cloudsMaterial.SetFloat(verticalShapeDisplacement, verticalShapeOffset);
            cloudsMaterial.SetFloat(verticalErosionDisplacement, verticalErosionOffset);

            cloudsMaterial.SetFloat(densityMultiplier, cloudsVolume.densityMultiplier.value * cloudsVolume.densityMultiplier.value * 2.0f);
            cloudsMaterial.SetFloat(powderEffectIntensity, cloudsVolume.powderEffectIntensity.value);
            cloudsMaterial.SetFloat(shapeScale, cloudsVolume.shapeScale.value);
            cloudsMaterial.SetFloat(shapeFactor, cloudsVolume.shapeFactor.value);
            cloudsMaterial.SetFloat(erosionScale, cloudsVolume.erosionScale.value);
            cloudsMaterial.SetFloat(erosionFactor, cloudsVolume.erosionFactor.value);
            cloudsMaterial.SetFloat(erosionOcclusion, cloudsVolume.erosionOcclusion.value);
            cloudsMaterial.SetFloat(microErosionScale, cloudsVolume.microErosionScale.value);
            cloudsMaterial.SetFloat(microErosionFactor, cloudsVolume.microErosionFactor.value);

            bool autoFadeIn = cloudsVolume.fadeInMode.value == VolumetricClouds.CloudFadeInMode.Automatic;
            cloudsMaterial.SetFloat(fadeInStart, autoFadeIn ? Mathf.Max(cloudsVolume.altitudeRange.value * 0.2f, camera.nearClipPlane) : Mathf.Max(cloudsVolume.fadeInStart.value, camera.nearClipPlane));
            cloudsMaterial.SetFloat(fadeInDistance, autoFadeIn ? cloudsVolume.altitudeRange.value * 0.3f : cloudsVolume.fadeInDistance.value);
            cloudsMaterial.SetFloat(multiScattering, 1.0f - cloudsVolume.multiScattering.value * 0.95f);
            cloudsMaterial.SetColor(scatteringTint, Color.white - cloudsVolume.scatteringTint.value * 0.75f);
            cloudsMaterial.SetFloat(ambientProbeDimmer, cloudsVolume.ambientLightProbeDimmer.value);
            cloudsMaterial.SetFloat(sunLightDimmer, cloudsVolume.sunLightDimmer.value);
            cloudsMaterial.SetFloat(earthRadius, actualEarthRad);
            cloudsMaterial.SetFloat(accumulationFactor, cloudsVolume.temporalAccumulationFactor.value);
            cloudsMaterial.SetFloat(improvedTransmittanceBlend, cloudsVolume.perceptualBlending.value);
            Vector3 cameraPosPS = camera.transform.position - new Vector3(0.0f, -actualEarthRad, 0.0f);
            cloudsMaterial.SetFloat(cloudnearPlane, max(GetCloudNearPlane(cameraPosPS, bottomAltitude, highestAltitude), camera.nearClipPlane));

            // Custom cloud map is not supported yet.
            //float lowerCloudRadius = (bottomAltitude + highestAltitude) * 0.5f - actualEarthRad;
            //cloudsMaterial.SetFloat(normalizationFactor, Mathf.Sqrt((earthRad + lowerCloudRadius) * (earthRad + lowerCloudRadius) - earthRad * actualEarthRad));

            float postExposureLinear = colorAdjustments != null && colorAdjustments.active ? Mathf.Pow(2.0f, colorAdjustments.postExposure.value) : 1.0f;
            cloudsMaterial.SetFloat(postExposure, postExposureLinear);

            SetupAmbientProbeIfNeeded(cloudsMaterial);

            PrepareCustomLutData(cloudsVolume);
        }

        private void UpdateClouds(Light mainLight, Camera camera)
        {
            // When using PBSky, we already applied the sun attenuation to "_MainLightColor"
            if (sunAttenuation)
            {
                bool isLinearColorSpace = QualitySettings.activeColorSpace == ColorSpace.Linear;
                Color mainLightColor = Color.black;
                if (mainLight != null)
                    mainLightColor = (isLinearColorSpace ? mainLight.color.linear : mainLight.color.gamma) * (mainLight.useColorTemperature ? Mathf.CorrelatedColorTemperatureToRGB(mainLight.colorTemperature) : Color.white) * mainLight.intensity;

            #if URP_PHYSICAL_LIGHT
                bool isPhysicalLight = mainLight.GetComponent<AdditionalLightData>() != null;

                mainLightColor = isPhysicalLight ? mainLightColor : mainLightColor * PI;
            #else
                mainLightColor *= PI;
            #endif

                // Pass the actual main light color to volumetric clouds shader.
                cloudsMaterial.SetVector(sunColor, mainLightColor);
            }

            // Update preset values
            VolumetricClouds.CloudPresets cloudPreset = cloudsVolume.cloudPreset;
            cloudsVolume.cloudPreset = cloudPreset;

            UpdateMaterialProperties(camera);
            denoiseClouds = cloudsVolume.temporalAccumulationFactor.value >= 0.01f;
        }

        private void PrepareCustomLutData(VolumetricClouds clouds)
        {
            if (customLutPresetMap == null)
            {
                customLutPresetMap = new Texture2D(1, customLutMapResolution, GraphicsFormat.R16G16B16A16_SFloat, TextureCreationFlags.None)
                {
                    name = "Custom LUT Curve",
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp
                };
                customLutPresetMap.hideFlags = HideFlags.HideAndDontSave;
            }

            var pixels = customLutColorArray;

            var densityCurve = clouds.densityCurve.value;
            var erosionCurve = clouds.erosionCurve.value;
            var ambientOcclusionCurve = clouds.ambientOcclusionCurve.value;
            Color white = Color.white;
            if (densityCurve == null || densityCurve.length == 0)
            {
                for (int i = 0; i < customLutMapResolution; i++)
                    pixels[i] = white;
            }
            else
            {
                float step = 1.0f / (customLutMapResolution - 1f);

                for (int i = 0; i < customLutMapResolution; i++)
                {
                    float currTime = step * i;
                    float density = (i == 0 || i == customLutMapResolution - 1) ? 0 : Mathf.Clamp(densityCurve.Evaluate(currTime), 0.0f, 1.0f);
                    float erosion = Mathf.Clamp(erosionCurve.Evaluate(currTime), 0.0f, 1.0f);
                    float ambientOcclusion = Mathf.Clamp(1.0f - ambientOcclusionCurve.Evaluate(currTime), 0.0f, 1.0f);
                    pixels[i] = new Color(density, erosion, ambientOcclusion, 1.0f);
                }
            }

            customLutPresetMap.SetPixels(pixels);
            customLutPresetMap.Apply();

            cloudsMaterial.SetTexture(cloudsCurveLut, customLutPresetMap);
        }

        private void SetupAmbientProbeIfNeeded(Material cloudsMaterial)
        {
            if (!dynamicAmbientProbe)
            {
                SphericalHarmonicsL2 ambientProbe = RenderSettings.ambientProbe;

                cloudsMaterial.SetVector(shAr, new Vector4(ambientProbe[0, 3], ambientProbe[0, 1], ambientProbe[0, 2], ambientProbe[0, 0] - ambientProbe[0, 6]));
                cloudsMaterial.SetVector(shAg, new Vector4(ambientProbe[1, 3], ambientProbe[1, 1], ambientProbe[1, 2], ambientProbe[1, 0] - ambientProbe[1, 6]));
                cloudsMaterial.SetVector(shAb, new Vector4(ambientProbe[2, 3], ambientProbe[2, 1], ambientProbe[2, 2], ambientProbe[2, 0] - ambientProbe[2, 6]));
                cloudsMaterial.SetVector(shBr, new Vector4(ambientProbe[0, 4], ambientProbe[0, 5], ambientProbe[0, 6] * 3, ambientProbe[0, 7]));
                cloudsMaterial.SetVector(shBg, new Vector4(ambientProbe[1, 4], ambientProbe[1, 5], ambientProbe[1, 6] * 3, ambientProbe[1, 7]));
                cloudsMaterial.SetVector(shBb, new Vector4(ambientProbe[2, 4], ambientProbe[2, 5], ambientProbe[2, 6] * 3, ambientProbe[2, 7]));
                cloudsMaterial.SetVector(shC, new Vector4(ambientProbe[0, 8], ambientProbe[1, 8], ambientProbe[2, 8], 1));
            }
        }

        private static Vector2 IntersectSphere(float sphereRadius, float cosChi,
                                          float radialDistance, float rcpRadialDistance)
        {
            // r_o = float2(0, r)
            // r_d = float2(sinChi, cosChi)
            // p_s = r_o + t * r_d
            //
            // R^2 = dot(r_o + t * r_d, r_o + t * r_d)
            // R^2 = ((r_o + t * r_d).x)^2 + ((r_o + t * r_d).y)^2
            // R^2 = t^2 + 2 * dot(r_o, r_d) + dot(r_o, r_o)
            //
            // t^2 + 2 * dot(r_o, r_d) + dot(r_o, r_o) - R^2 = 0
            //
            // Solve: t^2 + (2 * b) * t + c = 0, where
            // b = r * cosChi,
            // c = r^2 - R^2.
            //
            // t = (-2 * b + sqrt((2 * b)^2 - 4 * c)) / 2
            // t = -b + sqrt(b^2 - c)
            // t = -b + sqrt((r * cosChi)^2 - (r^2 - R^2))
            // t = -b + r * sqrt((cosChi)^2 - 1 + (R/r)^2)
            // t = -b + r * sqrt(d)
            // t = r * (-cosChi + sqrt(d))
            //
            // Why do we do this? Because it is more numerically robust.

            float d = square(sphereRadius * rcpRadialDistance) - saturate(1 - cosChi * cosChi);

            // Return the value of 'd' for debugging purposes.
            return (d < 0.0f) ? new Vector2(-1.0f, -1.0f) : (radialDistance * new Vector2(-cosChi - sqrt(d),
                                                          -cosChi + sqrt(d)));
        }

        private static float GetCloudNearPlane(Vector3 originPS, float lowerBoundPS, float higherBoundPS)
        {
            float radialDistance = length(originPS);
            float rcpRadialDistance = rcp(radialDistance);
            float cosChi = 1.0f;
            Vector2 tInner = IntersectSphere(lowerBoundPS, cosChi, radialDistance, rcpRadialDistance);
            Vector2 tOuter = IntersectSphere(higherBoundPS, -cosChi, radialDistance, rcpRadialDistance);

            if (tInner.x < 0.0f && tInner.y >= 0.0f) // Below the lower bound
                return tInner.y;
            else // Inside or above the cloud volume
                return max(tOuter.x, 0.0f);
        }

        public VolumetricCloudsPass(Material material, float resolution)
        {
            cloudsMaterial = material;
            resolutionScale = resolution;
        }

        #region Non Render Graph Pass
        private Light GetMainLight(LightData lightData)
        {
            int shadowLightIndex = lightData.mainLightIndex;
            if (shadowLightIndex != -1)
            {
                VisibleLight shadowLight = lightData.visibleLights[shadowLightIndex];
                Light light = shadowLight.light;
                if ((light.shadows != LightShadows.None || RenderSettings.sun != null && !RenderSettings.sun.isActiveAndEnabled) && shadowLight.lightType == LightType.Directional)
                    return light;
            }

            return RenderSettings.sun;
        }

    #if UNITY_6000_0_OR_NEWER
        [Obsolete]
    #endif
        private readonly RTHandle[] cloudsRTHandles = new RTHandle[2]; // avoid GC allocation
        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            RenderTextureDescriptor desc = renderingData.cameraData.cameraTargetDescriptor;
            
            desc.msaaSamples = 1;
            desc.useMipMap = false;
            desc.depthBufferBits = 0;
        #if UNITY_6000_0_OR_NEWER
            RenderingUtils.ReAllocateHandleIfNeeded(ref historyHandle, desc, FilterMode.Point, TextureWrapMode.Clamp, name: _VolumetricCloudsHistoryTexture); // lighting.rgb only
        #else
            RenderingUtils.ReAllocateIfNeeded(ref historyHandle, desc, FilterMode.Point, TextureWrapMode.Clamp, name: _VolumetricCloudsHistoryTexture); // lighting.rgb only
        #endif

            desc.colorFormat = RenderTextureFormat.ARGBHalf; // lighting.rgb + transmittance.a
        #if UNITY_6000_0_OR_NEWER
            RenderingUtils.ReAllocateHandleIfNeeded(ref accumulateHandle, desc, FilterMode.Point, TextureWrapMode.Clamp, name: _VolumetricCloudsAccumulationTexture);
        #else
            RenderingUtils.ReAllocateIfNeeded(ref accumulateHandle, desc, FilterMode.Point, TextureWrapMode.Clamp, name: _VolumetricCloudsAccumulationTexture);
        #endif
            
            desc.width = (int)(desc.width * resolutionScale);
            desc.height = (int)(desc.height * resolutionScale);
        #if UNITY_6000_0_OR_NEWER
            RenderingUtils.ReAllocateHandleIfNeeded(ref cloudsColorHandle, desc, FilterMode.Bilinear, TextureWrapMode.Clamp, name: _VolumetricCloudsLightingTexture);
        #else
            RenderingUtils.ReAllocateIfNeeded(ref cloudsColorHandle, desc, FilterMode.Bilinear, TextureWrapMode.Clamp, name: _VolumetricCloudsLightingTexture);
        #endif
            cloudsMaterial.SetTexture(volumetricCloudsLightingTexture, cloudsColorHandle);

            desc.colorFormat = RenderTextureFormat.RFloat; // average z-depth
        #if UNITY_6000_0_OR_NEWER
            RenderingUtils.ReAllocateHandleIfNeeded(ref cloudsDepthHandle, desc, FilterMode.Point, TextureWrapMode.Clamp, name: _VolumetricCloudsDepthTexture);
        #else
            RenderingUtils.ReAllocateIfNeeded(ref cloudsDepthHandle, desc, FilterMode.Point, TextureWrapMode.Clamp, name: _VolumetricCloudsDepthTexture);
        #endif

        #if UNITY_6000_0_OR_NEWER
            RenderingUtils.ReAllocateHandleIfNeeded(ref cameraTempDepthHandle, desc, FilterMode.Point, TextureWrapMode.Clamp, name: _CameraTempDepthTexture);
        #else
            RenderingUtils.ReAllocateIfNeeded(ref cameraTempDepthHandle, desc, FilterMode.Point, TextureWrapMode.Clamp, name: _CameraTempDepthTexture);
        #endif

            cmd.SetGlobalTexture(volumetricCloudsColorTexture, cloudsColorHandle);
            cmd.SetGlobalTexture(volumetricCloudsLightingTexture, cloudsColorHandle); // Same as "_VolumetricCloudsColorTexture"
            cmd.SetGlobalTexture(volumetricCloudsDepthTexture, cloudsDepthHandle);

            cloudsMaterial.SetTexture(volumetricCloudsHistoryTexture, historyHandle);
            cloudsMaterial.SetTexture(volumetricCloudsDepthTexture, cloudsDepthHandle);

            ConfigureInput(ScriptableRenderPassInput.Depth);

            if (outputDepth)
            {
                cloudsRTHandles[0] = cloudsColorHandle;
                cloudsRTHandles[1] = cloudsDepthHandle;

                // RT-1: clouds lighting
                // RT-2: clouds depth
                ConfigureTarget(cloudsRTHandles, cloudsColorHandle);
            }
            else
            {
                ConfigureTarget(cloudsColorHandle, cloudsColorHandle);
            }
        }

    #if UNITY_6000_0_OR_NEWER
        [Obsolete]
    #endif
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            LightData lightData = renderingData.lightData;
            Light mainLight = GetMainLight(lightData);

            UpdateClouds(mainLight, renderingData.cameraData.camera);

            cloudsMaterial.SetTexture(cameraDepthTexture, null); // Use global texture

            RTHandle cameraColorHandle = renderingData.cameraData.renderer.cameraColorTargetHandle;

            CommandBuffer cmd = CommandBufferPool.Get();
            using (new ProfilingScope(cmd, m_ProfilingSampler))
            {
                //RenderTargetIdentifier[] cloudsHandles = new RenderTargetIdentifier[2];
                //cloudsRTHandles[0] = cloudsColorHandle;
                //cloudsRTHandles[1] = cloudsDepthHandle;
                //cmd.SetRenderTarget(cloudsHandles, cloudsColorHandle);
                // Clouds Rendering
                Blitter.BlitTexture(cmd, cameraColorHandle, m_ScaleBias, cloudsMaterial, pass: 0);
                
                // Clouds Upscale & Combine
                Blitter.BlitCameraTexture(cmd, cameraColorHandle, cameraColorHandle, RenderBufferLoadAction.Load, RenderBufferStoreAction.Store, cloudsMaterial, pass: hasAtmosphericScattering ? 7 : 1);

                if (outputToSceneDepth)
                {
                    // Using reflection to access the "_CameraDepthTexture" in compatibility mode
                    var renderer = renderingData.cameraData.renderer as UniversalRenderer;
                    var cameraDepthHandle = depthTextureFieldInfo.GetValue(renderer) as RTHandle;

                    Blitter.BlitCameraTexture(cmd, cameraDepthHandle, cameraTempDepthHandle);

                    // Handle both R32 and D32 texture format
                    cmd.SetRenderTarget(cameraDepthHandle, cameraDepthHandle);
                    Blitter.BlitTexture(cmd, cameraTempDepthHandle, m_ScaleBias, cloudsMaterial, pass: 6);
                }

                if (denoiseClouds)
                {
                    // Prepare Temporal Reprojection (copy source buffer: colorHandle.rgb + cloudsColorHandle.a)
                    Blitter.BlitCameraTexture(cmd, cameraColorHandle, accumulateHandle, cloudsMaterial, pass: 2);

                    // Temporal Reprojection
                    Blitter.BlitCameraTexture(cmd, accumulateHandle, cameraColorHandle, cloudsMaterial, pass: 3);

                    // Update history texture for temporal reprojection
                    bool canCopy = cameraColorHandle.rt.format == historyHandle.rt.format && cameraColorHandle.rt.antiAliasing == 1 && fastCopy;
                    if (canCopy && renderMode == CloudsRenderMode.CopyTexture) { cmd.CopyTexture(cameraColorHandle, historyHandle); }
                    else { Blitter.BlitCameraTexture(cmd, cameraColorHandle, historyHandle, RenderBufferLoadAction.Load, RenderBufferStoreAction.Store, cloudsMaterial, pass: 2); }
                }
            }
            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();
            CommandBufferPool.Release(cmd);
        }
        #endregion

    #if UNITY_6000_0_OR_NEWER
        #region Render Graph Pass
        private Light GetMainLight(UniversalLightData lightData)
        {
            int shadowLightIndex = lightData.mainLightIndex;
            if (shadowLightIndex != -1)
            {
                VisibleLight shadowLight = lightData.visibleLights[shadowLightIndex];
                Light light = shadowLight.light;
                if ((light.shadows != LightShadows.None || RenderSettings.sun != null && !RenderSettings.sun.isActiveAndEnabled) && shadowLight.lightType == LightType.Directional)
                    return light;
            }

            return RenderSettings.sun;
        }

        // This class stores the data needed by the pass, passed as parameter to the delegate function that executes the pass
        private class PassData
        {
            internal Material cloudsMaterial;
            internal Camera camera;

            internal CloudsUpscaleMode upscaleMode;

            internal float resolutionScale;

            internal bool canCopy;
            internal bool denoiseClouds;
            internal bool dynamicAmbientProbe;
            internal bool outputDepth;
            internal bool outputToSceneDepth;
            internal bool hasAtmosphericScattering;

            internal TextureHandle cameraColorHandle;
            internal TextureHandle activeDepthHandle;
            internal TextureHandle cameraDepthHandle;
            internal TextureHandle cloudsColorHandle;
            internal TextureHandle cloudsDepthHandle;
            internal TextureHandle accumulateHandle;
            internal TextureHandle historyHandle;

            internal TextureHandle cameraTempDepthHandle;
        }

        private class RasterPassData
        {
            internal Material cloudsMaterial;

            internal TextureHandle cameraColorHandle;
            internal TextureHandle cameraDepthHandle;
        }

        // This static method is used to execute the pass and passed as the RenderFunc delegate to the RenderGraph render pass
        static void ExecutePass(PassData data, UnsafeGraphContext context)
        {
            CommandBuffer cmd = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);

            // Clouds Upscale & Combine
            Blitter.BlitCameraTexture(cmd, data.cloudsColorHandle, data.cameraColorHandle, RenderBufferLoadAction.Load, RenderBufferStoreAction.Store, data.cloudsMaterial, pass: data.hasAtmosphericScattering ? 7 : 1);

            if (data.outputToSceneDepth)
            {
                Blitter.BlitCameraTexture(cmd, data.cameraDepthHandle, data.cameraTempDepthHandle);

                // Handle both R32 and D32 texture format
                context.cmd.SetRenderTarget(data.cameraDepthHandle, data.cameraDepthHandle);
                Blitter.BlitTexture(cmd, data.cameraTempDepthHandle, m_ScaleBias, data.cloudsMaterial, pass: 6);
            }

            if (data.denoiseClouds)
            {
                // Prepare Temporal Reprojection (copy source buffer: colorHandle.rgb + cloudsHandle.a)
                Blitter.BlitCameraTexture(cmd, data.cameraColorHandle, data.accumulateHandle, data.cloudsMaterial, pass: 2);

                // Temporal Reprojection
                Blitter.BlitCameraTexture(cmd, data.accumulateHandle, data.cameraColorHandle, data.cloudsMaterial, pass: 3);

                // Update history texture for temporal reprojection
                if (data.canCopy)
                    cmd.CopyTexture(data.cameraColorHandle, data.historyHandle);
                else
                    Blitter.BlitCameraTexture(cmd, data.cameraColorHandle, data.historyHandle, RenderBufferLoadAction.Load, RenderBufferStoreAction.Store, data.cloudsMaterial, pass: 2);

                data.cloudsMaterial.SetTexture(volumetricCloudsHistoryTexture, data.historyHandle);
            }

            context.cmd.SetRenderTarget(data.cameraColorHandle, data.activeDepthHandle);
        }

        static void ExecuteRasterPass(RasterPassData data, RasterGraphContext rgContext)
        {
            RasterCommandBuffer cmd = rgContext.cmd;

            data.cloudsMaterial.SetTexture(cameraDepthTexture, data.cameraDepthHandle);
            Blitter.BlitTexture(cmd, data.cameraColorHandle, m_ScaleBias, data.cloudsMaterial, pass: 0);
        }

        // This is where the renderGraph handle can be accessed.
        // Each ScriptableRenderPass can use the RenderGraph handle to add multiple render passes to the render graph
        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            // UniversalResourceData contains all the texture handles used by the renderer, including the active color and depth textures
            // The active color and depth textures are the main color and depth buffers that the camera renders into
            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
            UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
            UniversalLightData lightData = frameData.Get<UniversalLightData>();

            // add a raster render pass to the render graph, specifying the name and the data type that will be passed to the ExecuteRasterPass function
            using (var builder = renderGraph.AddRasterRenderPass<RasterPassData>(rasterPassProfilerTag, out var rasterPassData))
            {
                Light mainLight = GetMainLight(lightData);
                UpdateClouds(mainLight, cameraData.camera);

                // Get the active color texture through the frame data, and set it as the source texture for the blit
                rasterPassData.cameraColorHandle = resourceData.activeColorTexture;
                rasterPassData.cameraDepthHandle = resourceData.cameraDepthTexture;

                RenderTextureFormat cloudsHandleFormat = RenderTextureFormat.ARGBHalf; // lighting.rgb + transmittance.a
                RenderTextureFormat cloudsDepthHandleFormat = RenderTextureFormat.RFloat; // average z-depth

                RenderTextureDescriptor desc = cameraData.cameraTargetDescriptor;

                desc.msaaSamples = 1;
                desc.useMipMap = false;
                desc.depthBufferBits = 0;
                desc.colorFormat = cloudsHandleFormat;
                desc.width = (int)(desc.width * resolutionScale);
                desc.height = (int)(desc.height * resolutionScale);
                RenderingUtils.ReAllocateHandleIfNeeded(ref cloudsColorHandle, desc, FilterMode.Bilinear, TextureWrapMode.Clamp, name: _VolumetricCloudsLightingTexture);
                cloudsMaterial.SetTexture(volumetricCloudsLightingTexture, cloudsColorHandle);
                TextureHandle cloudsTextureHandle = renderGraph.ImportTexture(cloudsColorHandle);

                //builder.SetGlobalTextureAfterPass(cloudsTextureHandle, volumetricCloudsColorTexture);
                //builder.SetGlobalTextureAfterPass(cloudsTextureHandle, volumetricCloudsLightingTexture); // Same as "_VolumetricCloudsColorTexture"

                if (outputDepth)
                {
                    desc.colorFormat = cloudsDepthHandleFormat;

                    RenderingUtils.ReAllocateHandleIfNeeded(ref cloudsDepthHandle, desc, FilterMode.Point, TextureWrapMode.Clamp, name: _VolumetricCloudsDepthTexture);
                    cloudsMaterial.SetTexture(volumetricCloudsDepthTexture, cloudsDepthHandle);
                    TextureHandle cloudsDepthTextureHandle = renderGraph.ImportTexture(cloudsDepthHandle);
                    //builder.UseTexture(cloudsDepthTextureHandle, AccessFlags.Write);
                    //builder.SetGlobalTextureAfterPass(cloudsDepthTextureHandle, volumetricCloudsDepthTexture);

                    builder.SetRenderAttachment(cloudsDepthTextureHandle, 1);
                }

                // Fill up the passData with the data needed by the pass
                rasterPassData.cloudsMaterial = cloudsMaterial;

                ConfigureInput(ScriptableRenderPassInput.Depth);

                builder.UseTexture(rasterPassData.cameraColorHandle, AccessFlags.ReadWrite);
                builder.UseTexture(rasterPassData.cameraDepthHandle, AccessFlags.Read);

                builder.SetRenderAttachment(cloudsTextureHandle, 0);

                // Sets the render function.
                builder.SetRenderFunc((RasterPassData rasterPassData, RasterGraphContext rgContext) => ExecuteRasterPass(rasterPassData, rgContext));
            }

            // add an unsafe render pass to the render graph, specifying the name and the data type that will be passed to the ExecutePass function
            using (var builder = renderGraph.AddUnsafePass<PassData>(profilerTag, out var passData))
            {
                // Get the active color texture through the frame data, and set it as the source texture for the blit
                passData.cameraColorHandle = resourceData.activeColorTexture;
                passData.activeDepthHandle = resourceData.activeDepthTexture;
                passData.cameraDepthHandle = resourceData.cameraDepthTexture;

                RenderTextureFormat cloudsHandleFormat = RenderTextureFormat.ARGBHalf; // lighting.rgb + transmittance.a
                RenderTextureFormat cloudsDepthHandleFormat = RenderTextureFormat.RFloat; // average z-depth

                RenderTextureDescriptor desc = cameraData.cameraTargetDescriptor;

                desc.msaaSamples = 1;
                desc.useMipMap = false;
                desc.depthBufferBits = 0;
                desc.colorFormat = cloudsHandleFormat;

                TextureHandle accumulateHandle = UniversalRenderer.CreateRenderGraphTexture(renderGraph, desc, name: _VolumetricCloudsAccumulationTexture, false, FilterMode.Point, TextureWrapMode.Clamp);
                TextureHandle historyHandle = UniversalRenderer.CreateRenderGraphTexture(renderGraph, desc, name: _VolumetricCloudsHistoryTexture, false, FilterMode.Point, TextureWrapMode.Clamp);

                // Full resolution camera texture descriptor
                RenderTextureDescriptor tempDepthDesc = desc;
                TextureHandle cloudsTextureHandle = renderGraph.ImportTexture(cloudsColorHandle);

                builder.SetGlobalTextureAfterPass(cloudsTextureHandle, volumetricCloudsColorTexture);
                builder.SetGlobalTextureAfterPass(cloudsTextureHandle, volumetricCloudsLightingTexture); // Same as "_VolumetricCloudsColorTexture"

                if (outputDepth)
                {
                    TextureHandle cloudsDepthTextureHandle = renderGraph.ImportTexture(cloudsDepthHandle);
                    passData.cloudsDepthHandle = cloudsDepthTextureHandle;
                    builder.UseTexture(passData.cloudsDepthHandle, AccessFlags.Write);
                    builder.SetGlobalTextureAfterPass(cloudsDepthTextureHandle, volumetricCloudsDepthTexture);
                }

                if (outputToSceneDepth)
                {
                    tempDepthDesc.colorFormat = cloudsDepthHandleFormat;

                    TextureHandle tempDepthHandle = UniversalRenderer.CreateRenderGraphTexture(renderGraph, tempDepthDesc, name: _CameraTempDepthTexture, false, FilterMode.Point, TextureWrapMode.Clamp);
                    passData.cameraTempDepthHandle = tempDepthHandle;
                    builder.UseTexture(passData.cameraTempDepthHandle, AccessFlags.Write);
                }

                // Fill up the passData with the data needed by the pass
                passData.cloudsMaterial = cloudsMaterial;
                passData.camera = cameraData.camera;
                passData.upscaleMode = upscaleMode;
                passData.resolutionScale = resolutionScale;
                passData.canCopy = cameraData.cameraTargetDescriptor.colorFormat == cloudsHandleFormat && cameraData.cameraTargetDescriptor.msaaSamples == 1 && fastCopy;
                passData.denoiseClouds = denoiseClouds;
                passData.dynamicAmbientProbe = dynamicAmbientProbe;
                passData.outputDepth = outputDepth;
                passData.outputToSceneDepth = outputToSceneDepth && (cameraData.camera.cameraType == CameraType.Game || cameraData.camera.cameraType == CameraType.SceneView);
                passData.hasAtmosphericScattering = hasAtmosphericScattering;

                passData.cloudsColorHandle = cloudsTextureHandle;
                passData.accumulateHandle = accumulateHandle;
                passData.historyHandle = historyHandle;

                ConfigureInput(ScriptableRenderPassInput.Depth);

                // UnsafePasses don't setup the outputs using UseTextureFragment/UseTextureFragmentDepth, you should specify your writes with UseTexture instead
                builder.UseTexture(passData.cameraColorHandle, AccessFlags.ReadWrite);
                builder.UseTexture(passData.activeDepthHandle, AccessFlags.None);
                builder.UseTexture(passData.cameraDepthHandle, AccessFlags.Read);
                builder.UseTexture(passData.cloudsColorHandle, AccessFlags.Write);
                builder.UseTexture(passData.accumulateHandle, AccessFlags.Write);
                builder.UseTexture(passData.historyHandle, AccessFlags.ReadWrite);

                // Assign the ExecutePass function to the render pass delegate, which will be called by the render graph when executing the pass
                builder.SetRenderFunc((PassData data, UnsafeGraphContext context) => ExecutePass(data, context));
            }
        }
        #endregion
    #endif

        #region Shared
        public void Dispose()
        {
            cloudsColorHandle?.Release();
            cloudsDepthHandle?.Release();
            historyHandle?.Release();
            accumulateHandle?.Release();
            cameraTempDepthHandle?.Release();
        }
        #endregion
    }
    public class VolumetricCloudsAmbientPass : ScriptableRenderPass
    {
        private const string profilerTag = "Volumetric Clouds Ambient Probe";
        private readonly ProfilingSampler m_ProfilingSampler = new ProfilingSampler(profilerTag);

        private readonly Material cloudsMaterial;
        private RTHandle probeColorHandle;

        private const string _VolumetricCloudsAmbientProbe = "_VolumetricCloudsAmbientProbe";

        private const string STEREO_INSTANCING_ON = "STEREO_INSTANCING_ON";

        private static readonly int worldSpaceCameraPos = Shader.PropertyToID("_WorldSpaceCameraPos");
        private static readonly int disableSunDisk = Shader.PropertyToID("_DisableSunDisk");
        //private static readonly int unity_MatrixVP = Shader.PropertyToID("unity_MatrixVP");
        private static readonly int unity_MatrixInvVP = Shader.PropertyToID("unity_MatrixInvVP");
        private static readonly int scaledScreenParams = Shader.PropertyToID("_ScaledScreenParams");
        private static readonly int screenSize = Shader.PropertyToID("_ScreenSize");

        private static readonly int volumetricCloudsAmbientProbe = Shader.PropertyToID(_VolumetricCloudsAmbientProbe);

        // Modified from CoreUtils.lookAtList to swap the directions of up and down faces
        private static readonly Matrix4x4 frontView = new Matrix4x4(float4(-1, 0, 0, 0), float4(0, -1, 0, 0), float4(0, 0, -1, 0), float4(0, 0, 0, 1));
        private static readonly Matrix4x4 backView = new Matrix4x4(float4(1, 0, 0, 0), float4(0, -1, 0, 0), float4(0, 0, 1, 0), float4(0, 0, 0, 1));
        private static readonly Matrix4x4 upView = new Matrix4x4(float4(1, 0, 0, 0), float4(0, 0, -1, 0), float4(0, -1, 0, 0), float4(0, 0, 0, 1));
        private static readonly Matrix4x4 downView = new Matrix4x4(float4(1, 0, 0, 0), float4(0, 0, 1, 0), float4(0, 1, 0, 0), float4(0, 0, 0, 1));
        private static readonly Matrix4x4 rightView = new Matrix4x4(float4(0, 0, -1, 0), float4(0, -1, 0, 0), float4(1, 0, 0, 0), float4(0, 0, 0, 1));
        private static readonly Matrix4x4 leftView = new Matrix4x4(float4(0, 0, 1, 0), float4(0, -1, 0, 0), float4(-1, 0, 0, 0), float4(0, 0, 0, 1));

        // Cubemap Order: right, left, up, down, back, front. (+X, -X, +Y, -Y, +Z, -Z)
        private static readonly Matrix4x4[] skyViews = { rightView, leftView, upView, downView, backView, frontView };

    #if UNITY_6000_0_OR_NEWER
        private readonly RendererListHandle[] rendererListHandles = new RendererListHandle[6];
    #endif

        private readonly Matrix4x4[] skyViewMatrices = new Matrix4x4[6];

        private static readonly Matrix4x4 skyProjectionMatrix = Matrix4x4.Perspective(90.0f, 1.0f, 0.1f, 10.0f);
        private static readonly Vector4 skyViewScreenParams = new Vector4(16.0f, 16.0f, 1.0f + rcp(16.0f), 1.0f + rcp(16.0f));
        private static readonly Vector4 skyViewScreenSize = new Vector4(16.0f, 16.0f, rcp(16.0f), rcp(16.0f));

        public VolumetricCloudsAmbientPass(Material material)
        {
            cloudsMaterial = material;
        }

        #region Non Render Graph Pass
    #if UNITY_6000_0_OR_NEWER
        [Obsolete]
    #endif
        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            RenderTextureDescriptor desc = renderingData.cameraData.cameraTargetDescriptor;
            desc.msaaSamples = 1;
            desc.useMipMap = true;
            desc.autoGenerateMips = true;
            desc.width = 16;
            desc.height = 16;
            desc.dimension = TextureDimension.Cube;
            desc.depthStencilFormat = GraphicsFormat.None;
            desc.depthBufferBits = 0;

        #if UNITY_6000_0_OR_NEWER
            RenderingUtils.ReAllocateHandleIfNeeded(ref probeColorHandle, desc, FilterMode.Trilinear, TextureWrapMode.Clamp, name: _VolumetricCloudsAmbientProbe);
        #else
            RenderingUtils.ReAllocateIfNeeded(ref probeColorHandle, desc, FilterMode.Trilinear, TextureWrapMode.Clamp, name: _VolumetricCloudsAmbientProbe);
        #endif
            cloudsMaterial.SetTexture(volumetricCloudsAmbientProbe, probeColorHandle);

            ConfigureTarget(probeColorHandle, probeColorHandle);
        }

    #if UNITY_6000_0_OR_NEWER
        [Obsolete]
    #endif
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            // UpdateEnvironment() is another way to update ambient lighting but it's really slow.
            //DynamicGI.UpdateEnvironment();

            CommandBuffer cmd = CommandBufferPool.Get();

            Camera camera = renderingData.cameraData.camera;
            var desc = renderingData.cameraData.cameraTargetDescriptor;

            bool isStereoEnabled = camera.stereoEnabled;
            
            using (new ProfilingScope(cmd, m_ProfilingSampler))
            {
                if (isStereoEnabled)
                    cmd.DisableShaderKeyword(STEREO_INSTANCING_ON);

                float2 cameraResolution = float2(desc.width, desc.height);
                Vector3 cameraPositionWS = camera.transform.position;
                Vector4 cameraScreenSize = new Vector4(cameraResolution.x, cameraResolution.y, rcp(cameraResolution.x), rcp(cameraResolution.y));
                Vector4 cameraScreenParams = new Vector4(cameraResolution.x, cameraResolution.y, 1.0f + cameraScreenSize.z, 1.0f + cameraScreenSize.w);

                Matrix4x4 skyMatrixP = GL.GetGPUProjectionMatrix(skyProjectionMatrix, true);

                cmd.SetGlobalVector(worldSpaceCameraPos, Vector3.zero);
                cmd.SetGlobalFloat(disableSunDisk, 1.0f);

                cmd.SetGlobalVector(scaledScreenParams, skyViewScreenParams);
                cmd.SetGlobalVector(screenSize, skyViewScreenSize);

                for (int i = 0; i < 6; i++)
                {
                    CoreUtils.SetRenderTarget(cmd, probeColorHandle, ClearFlag.None, 0, (CubemapFace)i);

                    //var lookAt = Matrix4x4.LookAt(Vector3.zero, CoreUtils.lookAtList[i], CoreUtils.upVectorList[i]);
                    //Matrix4x4 viewMatrix = lookAt * Matrix4x4.Scale(new Vector3(1.0f, 1.0f, -1.0f)); // Need to scale -1.0 on Z to match what is being done in the camera.wolrdToCameraMatrix API. ...

                    Matrix4x4 viewMatrix = skyViews[i];
                    viewMatrix *= Matrix4x4.Scale(new Vector3(1.0f, 1.0f, -1.0f));
                    skyViewMatrices[i] = viewMatrix;

                    Matrix4x4 skyMatrixVP = skyMatrixP * skyViewMatrices[i];

                    // Camera matrices for skybox rendering
                    cmd.SetViewMatrix(skyViewMatrices[i]);
                    //cmd.SetGlobalMatrix(unity_MatrixVP, skyMatrixVP);
                    cmd.SetGlobalMatrix(unity_MatrixInvVP, skyMatrixVP.inverse);

                    // Can we exclude the sun disk in ambient probe?
                    RendererList rendererList = context.CreateSkyboxRendererList(camera, skyProjectionMatrix, skyViewMatrices[i]);
                    cmd.DrawRendererList(rendererList);
                }

                cmd.SetGlobalVector(worldSpaceCameraPos, cameraPositionWS);
                cmd.SetGlobalFloat(disableSunDisk, 0.0f);

                Matrix4x4 matrixVP = GL.GetGPUProjectionMatrix(camera.projectionMatrix, true) * camera.worldToCameraMatrix;

                // Camera matrices for objects rendering
                cmd.SetViewMatrix(camera.worldToCameraMatrix);
                //cmd.SetGlobalMatrix(unity_MatrixVP, matrixVP);
                cmd.SetGlobalMatrix(unity_MatrixInvVP, matrixVP.inverse);
                cmd.SetGlobalVector(scaledScreenParams, cameraScreenParams);
                cmd.SetGlobalVector(screenSize, cameraScreenSize);

                if (isStereoEnabled)
                    cmd.EnableShaderKeyword(STEREO_INSTANCING_ON);
            }

            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();
            
            CommandBufferPool.Release(cmd);
        }
        #endregion

    #if UNITY_6000_0_OR_NEWER
        #region Render Graph Pass
        private class PassData
        {
            internal Material cloudsMaterial;

            internal TextureHandle probeColorHandle;

            internal Vector3 cameraPositionWS;
            internal Vector4 cameraScreenParams;
            internal Vector4 cameraScreenSize;
            internal Matrix4x4 worldToCameraMatrix;
            internal Matrix4x4 projectionMatrix;

            internal RendererListHandle[] rendererListHandles;
            internal Matrix4x4[] skyViewMatrices;
            internal Matrix4x4 skyProjectionMatrix;

            internal bool isStereoEnabled;
        }

        // This static method is used to execute the pass and passed as the RenderFunc delegate to the RenderGraph render pass
        static void ExecutePass(PassData data, UnsafeGraphContext context)
        {
            CommandBuffer cmd = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);

            if (data.isStereoEnabled)
                cmd.DisableShaderKeyword(STEREO_INSTANCING_ON);

            context.cmd.SetGlobalVector(worldSpaceCameraPos, Vector3.zero);
            context.cmd.SetGlobalFloat(disableSunDisk, 1.0f);

            context.cmd.SetGlobalVector(scaledScreenParams, skyViewScreenParams);
            context.cmd.SetGlobalVector(screenSize, skyViewScreenSize);

            Matrix4x4 skyMatrixP = GL.GetGPUProjectionMatrix(data.skyProjectionMatrix, true);

            for (int i = 0; i < 6; i++)
            {
                CoreUtils.SetRenderTarget(cmd, data.probeColorHandle, ClearFlag.None, 0, (CubemapFace)i);

                Matrix4x4 skyMatrixVP = skyMatrixP * data.skyViewMatrices[i];

                // Camera matrices for skybox rendering
                cmd.SetViewMatrix(data.skyViewMatrices[i]);
                //cmd.SetProjectionMatrix(skyMatrixP);
                //context.cmd.SetGlobalMatrix(unity_MatrixVP, skyMatrixVP);
                context.cmd.SetGlobalMatrix(unity_MatrixInvVP, skyMatrixVP.inverse);

                context.cmd.DrawRendererList(data.rendererListHandles[i]);
            }

            data.cloudsMaterial.SetTexture(volumetricCloudsAmbientProbe, data.probeColorHandle);

            context.cmd.SetGlobalVector(worldSpaceCameraPos, data.cameraPositionWS);
            context.cmd.SetGlobalFloat(disableSunDisk, 0.0f);

            Matrix4x4 matrixVP = GL.GetGPUProjectionMatrix(data.projectionMatrix, true) * data.worldToCameraMatrix;

            // Camera matrices for objects rendering
            cmd.SetViewMatrix(data.worldToCameraMatrix);
            //cmd.SetProjectionMatrix(data.projectionMatrix);
            //context.cmd.SetGlobalMatrix(unity_MatrixVP, matrixVP);
            context.cmd.SetGlobalMatrix(unity_MatrixInvVP, matrixVP.inverse);
            context.cmd.SetGlobalVector(scaledScreenParams, data.cameraScreenParams);
            context.cmd.SetGlobalVector(screenSize, data.cameraScreenSize);

            if (data.isStereoEnabled)
                cmd.EnableShaderKeyword(STEREO_INSTANCING_ON);
        }

        // This is where the renderGraph handle can be accessed.
        // Each ScriptableRenderPass can use the RenderGraph handle to add multiple render passes to the render graph
        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            // add an unsafe render pass to the render graph, specifying the name and the data type that will be passed to the ExecutePass function
            using (var builder = renderGraph.AddUnsafePass<PassData>(profilerTag, out var passData))
            {
                // UniversalResourceData contains all the texture handles used by the renderer, including the active color and depth textures
                // The active color and depth textures are the main color and depth buffers that the camera renders into
                UniversalRenderingData universalRenderingData = frameData.Get<UniversalRenderingData>();
                UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
                UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();

                RenderTextureDescriptor desc = cameraData.cameraTargetDescriptor;

                float2 cameraResolution = float2(desc.width, desc.height);
                
                desc.msaaSamples = 1;
                desc.useMipMap = true;
                desc.autoGenerateMips = true;
                desc.width = 16;
                desc.height = 16;
                desc.dimension = TextureDimension.Cube;
                desc.depthBufferBits = 0;
                RenderingUtils.ReAllocateHandleIfNeeded(ref probeColorHandle, desc, FilterMode.Trilinear, TextureWrapMode.Clamp, name: _VolumetricCloudsAmbientProbe);
                TextureHandle probeColorTextureHandle = renderGraph.ImportTexture(probeColorHandle);
                passData.probeColorHandle = probeColorTextureHandle;
                passData.cloudsMaterial = cloudsMaterial;

                for (int i = 0; i < 6; i++)
                {
                    //var lookAt = Matrix4x4.LookAt(Vector3.zero, CoreUtils.lookAtList[i], CoreUtils.upVectorList[i]);
                    //Matrix4x4 viewMatrix = lookAt * Matrix4x4.Scale(new Vector3(1.0f, 1.0f, -1.0f)); // Need to scale -1.0 on Z to match what is being done in the camera.wolrdToCameraMatrix API. ...

                    Matrix4x4 viewMatrix = skyViews[i];
                    viewMatrix *= Matrix4x4.Scale(new Vector3(1.0f, 1.0f, -1.0f)); // Need to scale -1.0 on Z to match what is being done in the camera.wolrdToCameraMatrix API. ...
                    skyViewMatrices[i] = viewMatrix;
                    rendererListHandles[i] = renderGraph.CreateSkyboxRendererList(cameraData.camera, skyProjectionMatrix, viewMatrix);
                    builder.UseRendererList(rendererListHandles[i]);
                }

                // Fill up the passData with the data needed by the pass
                passData.rendererListHandles = rendererListHandles;
                passData.skyViewMatrices = skyViewMatrices;
                passData.skyProjectionMatrix = skyProjectionMatrix;
                passData.cloudsMaterial = cloudsMaterial;
                passData.cameraPositionWS = cameraData.camera.transform.position;
                passData.cameraScreenSize = new Vector4(cameraResolution.x, cameraResolution.y, rcp(cameraResolution.x), rcp(cameraResolution.y));
                passData.cameraScreenParams = new Vector4(cameraResolution.x, cameraResolution.y, 1.0f + passData.cameraScreenSize.z, 1.0f + passData.cameraScreenSize.w);
                passData.worldToCameraMatrix = cameraData.camera.worldToCameraMatrix;
                passData.projectionMatrix = cameraData.camera.projectionMatrix;
                passData.isStereoEnabled = cameraData.camera.stereoEnabled;

                // UnsafePasses don't setup the outputs using UseTextureFragment/UseTextureFragmentDepth, you should specify your writes with UseTexture instead
                builder.UseTexture(passData.probeColorHandle, AccessFlags.Write);

                // Global shader property changes are considered as global state modifications
                builder.AllowGlobalStateModification(true);

                // Assign the ExecutePass function to the render pass delegate, which will be called by the render graph when executing the pass
                builder.SetRenderFunc((PassData data, UnsafeGraphContext context) => ExecutePass(data, context));
            }
        }
        #endregion
    #endif

        #region Shared
        public void Dispose()
        {
            probeColorHandle?.Release();
        }
        #endregion
    }
    public class VolumetricCloudsShadowsPass : ScriptableRenderPass
    {
        private const string profilerTag = "Volumetric Clouds Shadows";
        private readonly ProfilingSampler m_ProfilingSampler = new ProfilingSampler(profilerTag);

        public VolumetricClouds cloudsVolume;
    #if URP_PBSKY
        public VisualEnvironment visualEnvVolume;
    #endif
        private readonly Material cloudsMaterial;

        private RTHandle shadowTextureHandle;
        private RTHandle intermediateShadowTextureHandle;

        private readonly Vector3[] frustumCorners = new Vector3[4];

        private Light targetLight;

        private static readonly int shadowCookieResolution = Shader.PropertyToID("_ShadowCookieResolution");
        private static readonly int shadowIntensity = Shader.PropertyToID("_ShadowIntensity");
        private static readonly int shadowOpacityFallback = Shader.PropertyToID("_ShadowOpacityFallback");
        private static readonly int cloudShadowSunOrigin = Shader.PropertyToID("_CloudShadowSunOrigin");
        private static readonly int cloudShadowSunRight = Shader.PropertyToID("_CloudShadowSunRight");
        private static readonly int cloudShadowSunUp = Shader.PropertyToID("_CloudShadowSunUp");
        private static readonly int cloudShadowSunForward = Shader.PropertyToID("_CloudShadowSunForward");
        private static readonly int cameraPositionPS = Shader.PropertyToID("_CameraPositionPS");
        private static readonly int volumetricCloudsShadowOriginToggle = Shader.PropertyToID("_VolumetricCloudsShadowOriginToggle");
        private static readonly int volumetricCloudsShadowScale = Shader.PropertyToID("_VolumetricCloudsShadowScale");
        //private static readonly int shadowPlaneOffset = Shader.PropertyToID("_ShadowPlaneOffset");

        private const string _VolumetricCloudsShadowTexture = "_VolumetricCloudsShadowTexture";
        private const string _VolumetricCloudsShadowTempTexture = "_VolumetricCloudsShadowTempTexture";

        private const string _LIGHT_COOKIES = "_LIGHT_COOKIES";
        private const string STEREO_INSTANCING_ON = "STEREO_INSTANCING_ON";

        private static readonly Matrix4x4 s_DirLightProj = Matrix4x4.Ortho(-0.5f, 0.5f, -0.5f, 0.5f, -0.5f, 0.5f);

        private static readonly int mainLightTexture = Shader.PropertyToID("_MainLightCookieTexture");
        private static readonly int mainLightWorldToLight = Shader.PropertyToID("_MainLightWorldToLight");
        private static readonly int mainLightCookieTextureFormat = Shader.PropertyToID("_MainLightCookieTextureFormat");

        public VolumetricCloudsShadowsPass(Material material)
        {
            cloudsMaterial = material;
        }

        #region Non Render Graph Pass
        private Light GetMainLight(LightData lightData)
        {
            int shadowLightIndex = lightData.mainLightIndex;
            if (shadowLightIndex != -1)
            {
                VisibleLight shadowLight = lightData.visibleLights[shadowLightIndex];
                Light light = shadowLight.light;
                if ((light.shadows != LightShadows.None || RenderSettings.sun != null && !RenderSettings.sun.isActiveAndEnabled) && shadowLight.lightType == LightType.Directional)
                    return light;
            }

            return RenderSettings.sun;
        }

    #if UNITY_6000_0_OR_NEWER
        [Obsolete]
    #endif
        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            // Should we support colored shadows?
            GraphicsFormat cookieFormat = GraphicsFormat.R16_UNorm; //option 2: R8_UNorm
        #if UNITY_2023_2_OR_NEWER
            bool useSingleChannel = SystemInfo.IsFormatSupported(cookieFormat, GraphicsFormatUsage.Render);
        #else
            bool useSingleChannel = SystemInfo.IsFormatSupported(cookieFormat, FormatUsage.Render);
        #endif
            cookieFormat = useSingleChannel ? cookieFormat : GraphicsFormat.B10G11R11_UFloatPack32;

            int shadowResolution = (int)cloudsVolume.shadowResolution.value;
            RenderTextureDescriptor desc = renderingData.cameraData.cameraTargetDescriptor;
            desc.msaaSamples = 1;
            desc.depthBufferBits = 0;
            desc.useMipMap = false;
            desc.graphicsFormat = cookieFormat;
            desc.height = shadowResolution;
            desc.width = shadowResolution;
            desc.dimension = TextureDimension.Tex2D;
            
        #if UNITY_6000_0_OR_NEWER
            RenderingUtils.ReAllocateHandleIfNeeded(ref shadowTextureHandle, desc, FilterMode.Bilinear, TextureWrapMode.Clamp, name: _VolumetricCloudsShadowTexture);
        #else
            RenderingUtils.ReAllocateIfNeeded(ref shadowTextureHandle, desc, FilterMode.Bilinear, TextureWrapMode.Clamp, name: _VolumetricCloudsShadowTexture);
        #endif

        #if UNITY_6000_0_OR_NEWER
            RenderingUtils.ReAllocateHandleIfNeeded(ref intermediateShadowTextureHandle, desc, FilterMode.Bilinear, TextureWrapMode.Clamp, name: _VolumetricCloudsShadowTempTexture);
        #else
            RenderingUtils.ReAllocateIfNeeded(ref intermediateShadowTextureHandle, desc, FilterMode.Bilinear, TextureWrapMode.Clamp, name: _VolumetricCloudsShadowTempTexture);
        #endif

            ConfigureTarget(shadowTextureHandle, shadowTextureHandle);
        }

    #if UNITY_6000_0_OR_NEWER
        [Obsolete]
    #endif
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            CameraData cameraData = renderingData.cameraData;
            Camera camera = cameraData.camera;
            LightData lightData = renderingData.lightData;

            bool isStereoEnabled = camera.stereoEnabled;

            // Get and update the main light
            Light light = GetMainLight(lightData);
            if (targetLight != light)
            {
                ResetShadowCookie();
                targetLight = light;
            }

            // Check if we need shadow cookie
            bool hasVolumetricCloudsShadows = targetLight != null && targetLight.isActiveAndEnabled && targetLight.intensity != 0.0f;
            if (!hasVolumetricCloudsShadows)
            {
                ResetShadowCookie();
                return;
            }

            CommandBuffer cmd = CommandBufferPool.Get();
            using (new ProfilingScope(cmd, m_ProfilingSampler))
            {
                if (isStereoEnabled)
                    cmd.DisableShaderKeyword(STEREO_INSTANCING_ON);

                Matrix4x4 wsToLSMat = targetLight.transform.worldToLocalMatrix;
                Matrix4x4 lsToWSMat = targetLight.transform.localToWorldMatrix;

                float3 cameraPos = camera.transform.position;

                float perspectiveCorrectedShadowDistance = cloudsVolume.shadowDistance.value / cos(camera.fieldOfView * Mathf.Deg2Rad * 0.5f);

                camera.CalculateFrustumCorners(new Rect(0, 0, 1, 1), camera.farClipPlane, Camera.MonoOrStereoscopicEye.Mono, frustumCorners);

                // Generate the light space bounds of the camera frustum
                Bounds lightSpaceBounds = new Bounds();
                lightSpaceBounds.SetMinMax(new Vector3(float.MaxValue, float.MaxValue, float.MaxValue), new Vector3(-float.MaxValue, -float.MaxValue, -float.MaxValue));
                lightSpaceBounds.Encapsulate(wsToLSMat.MultiplyPoint(cameraPos));
                for (int cornerIdx = 0; cornerIdx < 4; ++cornerIdx)
                {
                    Vector3 corner = frustumCorners[cornerIdx];
                    float diag = corner.magnitude;
                    corner = (corner / diag) * Mathf.Min(perspectiveCorrectedShadowDistance, diag);
                    Vector3 posLightSpace = wsToLSMat.MultiplyPoint(new float3(corner) + cameraPos);
                    lightSpaceBounds.Encapsulate(posLightSpace);

                    posLightSpace = wsToLSMat.MultiplyPoint(new float3(-corner) + cameraPos);
                    lightSpaceBounds.Encapsulate(posLightSpace);
                }

                // Compute the four corners we need
                float3 c0 = lsToWSMat.MultiplyPoint(lightSpaceBounds.center + new Vector3(-lightSpaceBounds.extents.x, -lightSpaceBounds.extents.y, lightSpaceBounds.extents.z));
                float3 c1 = lsToWSMat.MultiplyPoint(lightSpaceBounds.center + new Vector3(lightSpaceBounds.extents.x, -lightSpaceBounds.extents.y, lightSpaceBounds.extents.z));
                float3 c2 = lsToWSMat.MultiplyPoint(lightSpaceBounds.center + new Vector3(-lightSpaceBounds.extents.x, lightSpaceBounds.extents.y, lightSpaceBounds.extents.z));

            #if URP_PBSKY
                bool isVolumeActive = visualEnvVolume != null && visualEnvVolume.IsActive();

                float4 planetCenterRad = visualEnvVolume.GetPlanetCenterRadius(camera.transform.position);
                float actualEarthRad = isVolumeActive ? planetCenterRad.w : Mathf.Lerp(1.0f, 0.025f, cloudsVolume.earthCurvature.value) * VolumetricCloudsPass.earthRad;
                float3 planetCenterPos = isVolumeActive ? planetCenterRad.xyz : float3(0.0f, -actualEarthRad, 0.0f);
            #else
                float actualEarthRad = Mathf.Lerp(1.0f, 0.025f, cloudsVolume.earthCurvature.value) * VolumetricCloudsPass.earthRad;
                float3 planetCenterPos = float3(0.0f, -actualEarthRad, 0.0f);
            #endif

                float3 dirX = c1 - c0;
                float3 dirY = c2 - c0;

                // The shadow cookie size
                float2 regionSize = float2(length(dirX), length(dirY));

                int shadowResolution = (int)cloudsVolume.shadowResolution.value;

                // Update material properties
                cloudsMaterial.SetFloat(shadowCookieResolution, shadowResolution);
                //cloudsMaterial.SetFloat(shadowPlaneOffset, cloudsVolume.shadowPlaneHeightOffset.value);
                cloudsMaterial.SetFloat(shadowIntensity, cloudsVolume.shadowOpacity.value);
                cloudsMaterial.SetFloat(shadowOpacityFallback, 1.0f - cloudsVolume.shadowOpacityFallback.value);
                cloudsMaterial.SetVector(cloudShadowSunOrigin, float4(c0 - planetCenterPos, 1.0f));
                cloudsMaterial.SetVector(cloudShadowSunRight, float4(dirX, 0.0f));
                cloudsMaterial.SetVector(cloudShadowSunUp, float4(dirY, 0.0f));
                cloudsMaterial.SetVector(cloudShadowSunForward, float4(-targetLight.transform.forward, 0.0f));
                cloudsMaterial.SetVector(cameraPositionPS, float4(cameraPos - planetCenterPos, 0.0f));
                cmd.SetGlobalVector(volumetricCloudsShadowOriginToggle, float4(c0, 0.0f));
                cmd.SetGlobalVector(volumetricCloudsShadowScale, float4(regionSize, 0.0f, 0.0f)); // Used in physically based sky

                // Apply light cookie settings
                targetLight.cookie = null;
                UniversalAdditionalLightData additonal = targetLight.GetComponent<UniversalAdditionalLightData>();
                additonal.lightCookieSize = Vector2.one;
                additonal.lightCookieOffset = Vector2.zero;

                Vector2 uvScale = 1 / regionSize;
                float minHalfValue = Unity.Mathematics.half.MinValue;
                if (Mathf.Abs(uvScale.x) < minHalfValue)
                    uvScale.x = Mathf.Sign(uvScale.x) * minHalfValue;
                if (Mathf.Abs(uvScale.y) < minHalfValue)
                    uvScale.y = Mathf.Sign(uvScale.y) * minHalfValue;

                Matrix4x4 cookieUVTransform = Matrix4x4.Scale(new Vector3(uvScale.x, uvScale.y, 1));
                lsToWSMat.SetColumn(3, float4(cameraPos, 1));
                Matrix4x4 cookieMatrix = s_DirLightProj * cookieUVTransform * lsToWSMat.inverse;

                float cookieFormat = (float)GetLightCookieShaderFormat(shadowTextureHandle.rt.graphicsFormat);

                cmd.SetGlobalTexture(mainLightTexture, shadowTextureHandle);
                cmd.SetGlobalMatrix(mainLightWorldToLight, cookieMatrix);
                cmd.SetGlobalFloat(mainLightCookieTextureFormat, cookieFormat);
                cmd.EnableShaderKeyword(_LIGHT_COOKIES);

                // Render shadow cookie texture
                Blitter.BlitCameraTexture(cmd, shadowTextureHandle, shadowTextureHandle, cloudsMaterial, pass: 4);

                // Given the low number of steps available and the absence of noise in the integration, we try to reduce the artifacts by doing two consecutive 3x3 blur passes.
                Blitter.BlitCameraTexture(cmd, shadowTextureHandle, intermediateShadowTextureHandle, cloudsMaterial, pass: 5);
                Blitter.BlitCameraTexture(cmd, intermediateShadowTextureHandle, shadowTextureHandle, cloudsMaterial, pass: 5);

                if (isStereoEnabled)
                    cmd.EnableShaderKeyword(STEREO_INSTANCING_ON);
            }
            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();
            CommandBufferPool.Release(cmd);
        }
        #endregion

    #if UNITY_6000_0_OR_NEWER
        #region Render Graph Pass
        private Light GetMainLight(UniversalLightData lightData)
        {
            int shadowLightIndex = lightData.mainLightIndex;
            if (shadowLightIndex != -1)
            {
                VisibleLight shadowLight = lightData.visibleLights[shadowLightIndex];
                Light light = shadowLight.light;
                if ((light.shadows != LightShadows.None || RenderSettings.sun != null && !RenderSettings.sun.isActiveAndEnabled) && shadowLight.lightType == LightType.Directional)
                    return light;
            }

            return RenderSettings.sun;
        }

        private class PassData
        {
            internal Material cloudsMaterial;

            internal TextureHandle intermediateShadowTexture;
            internal TextureHandle shadowTexture;

            internal Matrix4x4 mainLightWorldToLight;
            internal float mainLightCookieTextureFormat;

            internal Vector4 shadowOriginToggle;
            internal Vector4 shadowScale;

            internal bool isStereoEnabled;
        }

        // This static method is used to execute the pass and passed as the RenderFunc delegate to the RenderGraph render pass
        static void ExecutePass(PassData data, UnsafeGraphContext context)
        {
            CommandBuffer cmd = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);

            if (data.isStereoEnabled)
                cmd.DisableShaderKeyword(STEREO_INSTANCING_ON);

            // Render shadow cookie texture
            Blitter.BlitCameraTexture(cmd, data.shadowTexture, data.shadowTexture, data.cloudsMaterial, pass: 4);

            // Given the low number of steps available and the absence of noise in the integration, we try to reduce the artifacts by doing two consecutive 3x3 blur passes.
            Blitter.BlitCameraTexture(cmd, data.shadowTexture, data.intermediateShadowTexture, data.cloudsMaterial, pass: 5);
            Blitter.BlitCameraTexture(cmd, data.intermediateShadowTexture, data.shadowTexture, data.cloudsMaterial, pass: 5);

            cmd.SetGlobalVector(volumetricCloudsShadowOriginToggle, data.shadowOriginToggle);
            cmd.SetGlobalVector(volumetricCloudsShadowScale, data.shadowScale); // Used in physically based sky

            cmd.SetGlobalTexture(mainLightTexture, data.shadowTexture);
            cmd.SetGlobalMatrix(mainLightWorldToLight, data.mainLightWorldToLight);
            cmd.SetGlobalFloat(mainLightCookieTextureFormat, data.mainLightCookieTextureFormat);
            cmd.EnableShaderKeyword(_LIGHT_COOKIES);

            if (data.isStereoEnabled)
                cmd.EnableShaderKeyword(STEREO_INSTANCING_ON);
        }

        // This is where the renderGraph handle can be accessed.
        // Each ScriptableRenderPass can use the RenderGraph handle to add multiple render passes to the render graph
        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            UniversalLightData lightData = frameData.Get<UniversalLightData>();
            UniversalRenderingData universalRenderingData = frameData.Get<UniversalRenderingData>();
            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
            UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();

            // Get and update the main light
            Light light = GetMainLight(lightData);
            if (targetLight != light)
            {
                ResetShadowCookie();
                targetLight = light;
            }

            // Check if we need shadow cookie
            bool hasVolumetricCloudsShadows = targetLight != null && targetLight.isActiveAndEnabled && targetLight.intensity != 0.0f;
            if (!hasVolumetricCloudsShadows)
            {
                ResetShadowCookie();
                return;
            }

            var camera = cameraData.camera;

            // add an unsafe render pass to the render graph, specifying the name and the data type that will be passed to the ExecutePass function
            using (var builder = renderGraph.AddUnsafePass<PassData>(profilerTag, out var passData))
            {
                // UniversalResourceData contains all the texture handles used by the renderer, including the active color and depth textures
                // The active color and depth textures are the main color and depth buffers that the camera renders into
                
                Matrix4x4 wsToLSMat = targetLight.transform.worldToLocalMatrix;
                Matrix4x4 lsToWSMat = targetLight.transform.localToWorldMatrix;

                float3 cameraPos = camera.transform.position;

                float perspectiveCorrectedShadowDistance = cloudsVolume.shadowDistance.value / cos(camera.fieldOfView * Mathf.Deg2Rad * 0.5f);

                camera.CalculateFrustumCorners(new Rect(0, 0, 1, 1), camera.farClipPlane, Camera.MonoOrStereoscopicEye.Mono, frustumCorners);

                // Generate the light space bounds of the camera frustum
                Bounds lightSpaceBounds = new Bounds();
                lightSpaceBounds.SetMinMax(new Vector3(float.MaxValue, float.MaxValue, float.MaxValue), new Vector3(-float.MaxValue, -float.MaxValue, -float.MaxValue));
                lightSpaceBounds.Encapsulate(wsToLSMat.MultiplyPoint(cameraPos));
                for (int cornerIdx = 0; cornerIdx < 4; ++cornerIdx)
                {
                    Vector3 corner = frustumCorners[cornerIdx];
                    float diag = corner.magnitude;
                    corner = (corner / diag) * Mathf.Min(perspectiveCorrectedShadowDistance, diag);
                    Vector3 posLightSpace = wsToLSMat.MultiplyPoint(float3(corner) + cameraPos);
                    lightSpaceBounds.Encapsulate(posLightSpace);

                    posLightSpace = wsToLSMat.MultiplyPoint(float3(-corner) + cameraPos);
                    lightSpaceBounds.Encapsulate(posLightSpace);
                }
                
                // Compute the four corners we need
                float3 c0 = lsToWSMat.MultiplyPoint(lightSpaceBounds.center + new Vector3(-lightSpaceBounds.extents.x, -lightSpaceBounds.extents.y, lightSpaceBounds.extents.z));
                float3 c1 = lsToWSMat.MultiplyPoint(lightSpaceBounds.center + new Vector3(lightSpaceBounds.extents.x, -lightSpaceBounds.extents.y, lightSpaceBounds.extents.z));
                float3 c2 = lsToWSMat.MultiplyPoint(lightSpaceBounds.center + new Vector3(-lightSpaceBounds.extents.x, lightSpaceBounds.extents.y, lightSpaceBounds.extents.z));

            #if URP_PBSKY
                bool isVolumeActive = visualEnvVolume != null && visualEnvVolume.IsActive();

                float4 planetCenterRad = visualEnvVolume.GetPlanetCenterRadius(camera.transform.position);
                float actualEarthRad = isVolumeActive ? planetCenterRad.w : Mathf.Lerp(1.0f, 0.025f, cloudsVolume.earthCurvature.value) * VolumetricCloudsPass.earthRad;
                float3 planetCenterPos = isVolumeActive ? planetCenterRad.xyz : float3(0.0f, -actualEarthRad, 0.0f);
            #else
                float actualEarthRad = Mathf.Lerp(1.0f, 0.025f, cloudsVolume.earthCurvature.value) * VolumetricCloudsPass.earthRad;
                float3 planetCenterPos = float3(0.0f, -actualEarthRad, 0.0f);
            #endif

                float3 dirX = c1 - c0;
                float3 dirY = c2 - c0;
                 
                // The shadow cookie size
                float2 regionSize = float2(length(dirX), length(dirY));

                // Should we support colored shadows?
                GraphicsFormat cookieTextureFormat = GraphicsFormat.R16_UNorm; //option 2: R8_UNorm
                bool useSingleChannel = SystemInfo.IsFormatSupported(cookieTextureFormat, GraphicsFormatUsage.Render);
                cookieTextureFormat = useSingleChannel ? cookieTextureFormat : GraphicsFormat.B10G11R11_UFloatPack32;

                int shadowResolution = (int)cloudsVolume.shadowResolution.value;
                RenderTextureDescriptor desc = cameraData.cameraTargetDescriptor;
                desc.msaaSamples = 1;
                desc.depthBufferBits = 0;
                desc.useMipMap = false;
                desc.graphicsFormat = cookieTextureFormat;
                desc.height = shadowResolution;
                desc.width = shadowResolution;
                desc.dimension = TextureDimension.Tex2D;
                RenderingUtils.ReAllocateHandleIfNeeded(ref shadowTextureHandle, desc, FilterMode.Bilinear, TextureWrapMode.Clamp, name: _VolumetricCloudsShadowTexture);
                TextureHandle shadowTexture = renderGraph.ImportTexture(shadowTextureHandle);

                TextureHandle intermediateShadowTexture = renderGraph.CreateTexture(new TextureDesc(shadowResolution, shadowResolution, false, false)
                { colorFormat = cookieTextureFormat, enableRandomWrite = false, name = _VolumetricCloudsShadowTempTexture });
                
                // Update material properties
                cloudsMaterial.SetFloat(shadowCookieResolution, shadowResolution);
                //cloudsMaterial.SetFloat(shadowPlaneOffset, cloudsVolume.shadowPlaneHeightOffset.value);
                cloudsMaterial.SetFloat(shadowIntensity, cloudsVolume.shadowOpacity.value);
                cloudsMaterial.SetFloat(shadowOpacityFallback, 1.0f - cloudsVolume.shadowOpacityFallback.value);
                cloudsMaterial.SetVector(cloudShadowSunOrigin, float4(c0 - planetCenterPos, 1.0f));
                cloudsMaterial.SetVector(cloudShadowSunRight, float4(dirX, 0.0f));
                cloudsMaterial.SetVector(cloudShadowSunUp, float4(dirY, 0.0f));
                cloudsMaterial.SetVector(cloudShadowSunForward, float4(-targetLight.transform.forward, 0.0f));
                cloudsMaterial.SetVector(cameraPositionPS, float4(cameraPos - planetCenterPos, 0.0f));
                cloudsMaterial.SetVector(volumetricCloudsShadowOriginToggle, float4(c0, 0.0f));

                // Apply light cookie settings
                targetLight.cookie = null;
                UniversalAdditionalLightData additonal = targetLight.GetComponent<UniversalAdditionalLightData>();
                additonal.lightCookieSize = Vector2.one;
                additonal.lightCookieOffset = Vector2.zero;

                // Apply shadow cookie
                Vector2 uvScale = 1 / regionSize;
                float minHalfValue = Unity.Mathematics.half.MinValue;
                if (Mathf.Abs(uvScale.x) < minHalfValue)
                    uvScale.x = Mathf.Sign(uvScale.x) * minHalfValue;
                if (Mathf.Abs(uvScale.y) < minHalfValue)
                    uvScale.y = Mathf.Sign(uvScale.y) * minHalfValue;

                Matrix4x4 cookieUVTransform = Matrix4x4.Scale(new Vector3(uvScale.x, uvScale.y, 1));
                //cookieUVTransform.SetColumn(3, new Vector4(uvScale.x, uvScale.y, 0, 1));
                lsToWSMat.SetColumn(3, float4(cameraPos, 1));
                Matrix4x4 cookieMatrix = s_DirLightProj * cookieUVTransform * lsToWSMat.inverse;

                float cookieFormat = (float)GetLightCookieShaderFormat(cookieTextureFormat);

                // Fill up the passData with the data needed by the pass
                passData.cloudsMaterial = cloudsMaterial;
                passData.shadowTexture = shadowTexture;
                passData.intermediateShadowTexture = intermediateShadowTexture;
                passData.mainLightWorldToLight = cookieMatrix;
                passData.mainLightCookieTextureFormat = cookieFormat;
                passData.shadowOriginToggle = float4(c0, 0.0f);
                passData.shadowScale = float4(regionSize, 0.0f, 0.0f);
                passData.isStereoEnabled = cameraData.camera.stereoEnabled;

                // UnsafePasses don't setup the outputs using UseTextureFragment/UseTextureFragmentDepth, you should specify your writes with UseTexture instead
                builder.UseTexture(passData.shadowTexture, AccessFlags.Write);
                builder.UseTexture(passData.intermediateShadowTexture, AccessFlags.Write); // We always write to it before reading

                // Shader keyword changes (_LIGHT_COOKIES) are considered as global state modifications
                builder.AllowGlobalStateModification(true);

                // Assign the ExecutePass function to the render pass delegate, which will be called by the render graph when executing the pass
                builder.SetRenderFunc((PassData data, UnsafeGraphContext context) => ExecutePass(data, context));
            }
        }
        #endregion
    #endif

        #region Shared
        private enum LightCookieShaderFormat
        {
            None = -1,

            RGB = 0,
            Alpha = 1,
            Red = 2
        }

        private LightCookieShaderFormat GetLightCookieShaderFormat(GraphicsFormat cookieFormat)
        {
            // TODO: convert this to use GraphicsFormatUtility
            switch (cookieFormat)
            {
                default:
                    return LightCookieShaderFormat.RGB;
                // A8, A16 GraphicsFormat does not expose yet.
                case (GraphicsFormat)54:
                case (GraphicsFormat)55:
                    return LightCookieShaderFormat.Alpha;
                case GraphicsFormat.R8_SRGB:
                case GraphicsFormat.R8_UNorm:
                case GraphicsFormat.R8_UInt:
                case GraphicsFormat.R8_SNorm:
                case GraphicsFormat.R8_SInt:
                case GraphicsFormat.R16_UNorm:
                case GraphicsFormat.R16_UInt:
                case GraphicsFormat.R16_SNorm:
                case GraphicsFormat.R16_SInt:
                case GraphicsFormat.R16_SFloat:
                case GraphicsFormat.R32_UInt:
                case GraphicsFormat.R32_SInt:
                case GraphicsFormat.R32_SFloat:
                case GraphicsFormat.R_BC4_SNorm:
                case GraphicsFormat.R_BC4_UNorm:
                case GraphicsFormat.R_EAC_SNorm:
                case GraphicsFormat.R_EAC_UNorm:
                    return LightCookieShaderFormat.Red;
            }
        }

        private void ResetShadowCookie()
        {
            if (targetLight != null)
            {
                targetLight.cookie = null;
                UniversalAdditionalLightData additionalData = targetLight.GetComponent<UniversalAdditionalLightData>();
                if (additionalData != null)
                {
                    additionalData.lightCookieSize = Vector2.one;
                    additionalData.lightCookieOffset = Vector2.zero;
                }
            }
        }

        public void Dispose()
        {
            ResetShadowCookie();
            shadowTextureHandle?.Release();
            intermediateShadowTextureHandle?.Release();
        }
        #endregion
    }
}
