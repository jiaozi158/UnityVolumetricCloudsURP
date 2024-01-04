using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Experimental.Rendering;

/// <summary>
/// A renderer feature that adds volumetric clouds support to the URP volume.
/// </summary>
[DisallowMultipleRendererFeature("Volumetric Clouds URP")]
[Tooltip("Add this Renderer Feature to support volumetric clouds in URP Volume.")]
public class VolumetricCloudsURP : ScriptableRendererFeature
{
    [Header("Setup")]
    [Tooltip("The material of volumetric clouds shader.")]
    [SerializeField] private Material material;
    [Tooltip("Enable this to render volumetric clouds in Rendering Debugger view. This is disabled by default to avoid affecting the individual lighting previews.")]
    [SerializeField] private bool renderingDebugger = false;

    [Header("Performance")]
    [Range(0.5f, 1.0f), Tooltip("The resolution scale for volumetric clouds rendering.")]
    [SerializeField] private float resolutionScale = 1.0f;
    [Tooltip("Specifies if URP renders volumetric clouds in both real-time and baked reflection probes. Volumetric clouds in real-time reflection probes may reduce performace.")]
    [SerializeField] private bool reflectionProbe = false;
    [Tooltip("Specifies the preferred texture render mode for volumetric clouds. The Copy Texture mode should be more performant.")]
    [SerializeField] private CloudsRenderMode preferredRenderMode = CloudsRenderMode.CopyTexture;

    [Header("Lighting")]
    [Tooltip("Specifies the volumetric clouds ambient probe update frequency.")]
    [SerializeField] private CloudsAmbientMode ambientProbe = CloudsAmbientMode.Dynamic;

    private const string shaderName = "Hidden/Sky/VolumetricClouds";
    private VolumetricCloudsPass volumetricCloudsPass;
    private VolumetricCloudsAmbientPass volumetricCloudsAmbientPass;
    
    // Pirnt message only once when using the rendering debugger.
    private bool isLogPrinted = false;

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
    /// The resolution scale for volumetric clouds rendering, ranging from 0.5 to 1.0.
    /// </value>
    public float ResolutionScale
    {
        get { return resolutionScale; }
        set { resolutionScale = Mathf.Clamp(value, 0.5f, 1.0f); }
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

    public override void Create()
    {
        // Check if the volumetric clouds material uses the correct shader.
        if (material != null)
        {
            if (material.shader != Shader.Find(shaderName))
            {
                Debug.LogErrorFormat("Volumetric Clouds URP: Material shader is not {0}.", shaderName);
                return;
            }
        }
        // No material applied.
        else
        {
            //Debug.LogError("Volumetric Clouds URP: Material is empty.");
            return;
        }

        if (volumetricCloudsPass == null)
        {
            volumetricCloudsPass = new(material, resolutionScale);
            volumetricCloudsPass.renderPassEvent = RenderPassEvent.AfterRenderingSkybox; // Use camera previous matrix to do reprojection
        }
        else
        {
            // Update every frame to support runtime changes to these properties.
            volumetricCloudsPass.resolutionScale = resolutionScale;
            volumetricCloudsPass.dynamicAmbientProbe = ambientProbe == CloudsAmbientMode.Dynamic;
        }

        if (volumetricCloudsAmbientPass == null)
        {
            volumetricCloudsAmbientPass = new(material);
            volumetricCloudsAmbientPass.renderPassEvent = RenderPassEvent.BeforeRendering;
        }
        else
        {
            // Update every frame to support runtime changes to these properties.
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (volumetricCloudsPass != null)
            volumetricCloudsPass.Dispose();
        if (volumetricCloudsAmbientPass != null)
            volumetricCloudsAmbientPass.Dispose();
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (material == null)
        {
            Debug.LogErrorFormat("Volumetric Clouds URP: Material is empty.");
            return;
        }

        var stack = VolumeManager.instance.stack;
        VolumetricClouds cloudsVolume = stack.GetComponent<VolumetricClouds>();
        bool isActive = cloudsVolume != null && cloudsVolume.IsActive();
        bool isDebugger = DebugManager.instance.isAnyDebugUIActive;

        bool isProbeCamera = renderingData.cameraData.cameraType == CameraType.Reflection && reflectionProbe;

        if (isActive && (renderingData.cameraData.cameraType == CameraType.Game || renderingData.cameraData.cameraType == CameraType.SceneView || isProbeCamera) && (!isDebugger || renderingDebugger))
        {
            bool dynamicAmbientProbe = ambientProbe == CloudsAmbientMode.Dynamic;
            volumetricCloudsPass.cloudsVolume = cloudsVolume;
            volumetricCloudsPass.dynamicAmbientProbe = dynamicAmbientProbe;
            // If the probe is at the origin, we assume it's a global probe.
            // We disable local clouds in that case because the far clipping plane of a global probe is only 10.
            volumetricCloudsPass.isGlobalProbeCamera = isProbeCamera && renderingData.cameraData.camera.transform.position == Vector3.zero;

            // No need to render dynamic ambient probe for reflection probes.
            renderer.EnqueuePass(volumetricCloudsPass);
            if (dynamicAmbientProbe && !isProbeCamera) { renderer.EnqueuePass(volumetricCloudsAmbientPass); }

            isLogPrinted = false;
        }
        else if (isDebugger && !isLogPrinted)
        {
            Debug.Log("Volumetric Clouds URP: Disable effect to avoid affecting rendering debugging.");
            isLogPrinted = true;
        }
    }

    public class VolumetricCloudsPass : ScriptableRenderPass
    {
        public VolumetricClouds cloudsVolume;
        public float resolutionScale;
        public bool dynamicAmbientProbe;
        public bool isGlobalProbeCamera;

        private bool denoiseClouds;

        private RTHandle cloudsHandle;
        private RTHandle accumulateHandle;
        private RTHandle historyHandle;

        private readonly Material cloudsMaterial;
        private readonly bool fastCopy = (SystemInfo.copyTextureSupport & CopyTextureSupport.Basic) != 0;
        
        private static readonly int numPrimarySteps = Shader.PropertyToID("_NumPrimarySteps");
        private static readonly int numLightSteps = Shader.PropertyToID("_NumLightSteps");
        private static readonly int highestCloudAltitude = Shader.PropertyToID("_HighestCloudAltitude");
        private static readonly int lowestCloudAltitude = Shader.PropertyToID("_LowestCloudAltitude");
        private static readonly int shapeNoiseOffset = Shader.PropertyToID("_ShapeNoiseOffset");
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
        private static readonly int cloudsCurveLut = Shader.PropertyToID("_CloudCurveTexture");

        private static readonly string localClouds = "_LOCAL_VOLUMETRIC_CLOUDS";
        private static readonly string microErosion = "_CLOUDS_MICRO_EROSION";
        private static readonly string lowResClouds = "_LOW_RESOLUTION_CLOUDS";
        private static readonly string cloudsAmbientProbe = "_CLOUDS_AMBIENT_PROBE";

        private Texture2D customLutPresetMap;
        private readonly Color32[] customLutColorArray = new Color32[customLutMapResolution];

        private const float earthRad = 6378100.0f;
        private const int customLutMapResolution = 64;

        private void UpdateMaterialProperties()
        {
            if (cloudsVolume.localClouds.value && (!isGlobalProbeCamera)) { cloudsMaterial.EnableKeyword(localClouds); }
            else { cloudsMaterial.DisableKeyword(localClouds); }

            if (cloudsVolume.microErosion.value && cloudsVolume.microErosionFactor.value > 0.0f) { cloudsMaterial.EnableKeyword(microErosion); }
            else { cloudsMaterial.DisableKeyword(microErosion); }

            if (resolutionScale < 1.0f) { cloudsMaterial.EnableKeyword(lowResClouds); }
            else { cloudsMaterial.DisableKeyword(lowResClouds); }

            if (dynamicAmbientProbe) { cloudsMaterial.EnableKeyword(cloudsAmbientProbe); }
            else { cloudsMaterial.DisableKeyword(cloudsAmbientProbe); }

            cloudsMaterial.SetFloat(numPrimarySteps, cloudsVolume.numPrimarySteps.value);
            cloudsMaterial.SetFloat(numLightSteps, cloudsVolume.numLightSteps.value);
            cloudsMaterial.SetFloat(highestCloudAltitude, cloudsVolume.bottomAltitude.value + cloudsVolume.altitudeRange.value);
            cloudsMaterial.SetFloat(lowestCloudAltitude, cloudsVolume.bottomAltitude.value);

            cloudsMaterial.SetVector(shapeNoiseOffset, new Vector4(cloudsVolume.shapeOffset.value.x, cloudsVolume.shapeOffset.value.y, cloudsVolume.shapeOffset.value.z, 0.0f));
            cloudsMaterial.SetFloat(altitudeDistortion, cloudsVolume.altitudeDistortion.value);
            cloudsMaterial.SetFloat(densityMultiplier, cloudsVolume.densityMultiplier.value);
            cloudsMaterial.SetFloat(powderEffectIntensity, cloudsVolume.powderEffectIntensity.value);
            cloudsMaterial.SetFloat(shapeScale, cloudsVolume.shapeScale.value);
            cloudsMaterial.SetFloat(shapeFactor, cloudsVolume.shapeFactor.value);
            cloudsMaterial.SetFloat(erosionScale, cloudsVolume.erosionScale.value);
            cloudsMaterial.SetFloat(erosionFactor, cloudsVolume.erosionFactor.value);
            cloudsMaterial.SetFloat(erosionOcclusion, cloudsVolume.erosionOcclusion.value);
            cloudsMaterial.SetFloat(microErosionScale, cloudsVolume.microErosionScale.value);
            cloudsMaterial.SetFloat(microErosionFactor, cloudsVolume.microErosionFactor.value);

            bool autoFadeIn = cloudsVolume.fadeInMode.value == VolumetricClouds.CloudFadeInMode.Automatic;
            cloudsMaterial.SetFloat(fadeInStart, autoFadeIn ? 0.0f : cloudsVolume.fadeInStart.value);
            cloudsMaterial.SetFloat(fadeInDistance, autoFadeIn ? 5000.0f : cloudsVolume.fadeInDistance.value);
            cloudsMaterial.SetFloat(multiScattering, cloudsVolume.multiScattering.value);
            cloudsMaterial.SetColor(scatteringTint, cloudsVolume.scatteringTint.value);
            cloudsMaterial.SetFloat(ambientProbeDimmer, cloudsVolume.ambientLightProbeDimmer.value);
            cloudsMaterial.SetFloat(sunLightDimmer, cloudsVolume.sunLightDimmer.value);
            cloudsMaterial.SetFloat(earthRadius, Mathf.Lerp(1.0f, 0.025f, cloudsVolume.earthCurvature.value) * earthRad);
            cloudsMaterial.SetFloat(accumulationFactor, cloudsVolume.temporalAccumulationFactor.value);

            PrepareCustomLutData(cloudsVolume);
        }

        private void UpdateClouds()
        {
            // Update preset values
            VolumetricClouds.CloudPresets cloudPreset = cloudsVolume.cloudPreset;
            cloudsVolume.cloudPreset = cloudPreset;

            UpdateMaterialProperties();
            denoiseClouds = cloudsVolume.temporalAccumulationFactor.value >= 0.01f;
        }

        private void PrepareCustomLutData(VolumetricClouds clouds)
        {
            if (customLutPresetMap == null)
            {
                customLutPresetMap = new Texture2D(1, customLutMapResolution, (GraphicsFormat)TextureFormat.RGBA32, TextureCreationFlags.None)
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
            if (densityCurve == null || densityCurve.length == 0)
            {
                for (int i = 0; i < customLutMapResolution; i++)
                    pixels[i] = Color.white;
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

            customLutPresetMap.SetPixels32(pixels);
            customLutPresetMap.Apply();

            cloudsMaterial.SetTexture(cloudsCurveLut, customLutPresetMap);
        }

        public VolumetricCloudsPass(Material material, float resolution)
        {
            cloudsMaterial = material;
            resolutionScale = resolution;
        }

        public void Dispose()
        {
            cloudsHandle?.Release();
            historyHandle?.Release();
            accumulateHandle?.Release();
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            RenderTextureDescriptor desc = renderingData.cameraData.cameraTargetDescriptor;
            
            desc.msaaSamples = 1;
            desc.useMipMap = false;
            desc.depthBufferBits = 0;
            desc.colorFormat = RenderTextureFormat.ARGBHalf; // lighting.rgb + transmittance.a

            RenderingUtils.ReAllocateIfNeeded(ref accumulateHandle, desc, FilterMode.Point, TextureWrapMode.Clamp, name: "_VolumetricCloudsAccumulationTexture");
            RenderingUtils.ReAllocateIfNeeded(ref historyHandle, desc, FilterMode.Point, TextureWrapMode.Clamp, name: "_VolumetricCloudsHistoryTexture");

            desc.width = (int)(desc.width * resolutionScale);
            desc.height = (int)(desc.height * resolutionScale);
            RenderingUtils.ReAllocateIfNeeded(ref cloudsHandle, desc, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_VolumetricCloudsColorTexture");

            cmd.SetGlobalTexture("_VolumetricCloudsColorTexture", cloudsHandle);
            cmd.SetGlobalTexture("_VolumetricCloudsHistoryTexture", historyHandle);

            ConfigureInput(ScriptableRenderPassInput.Depth);
            ConfigureTarget(cloudsHandle, cloudsHandle);
        }

        public override void FrameCleanup(CommandBuffer cmd)
        {
            if (cloudsHandle != null)
                cmd.ReleaseTemporaryRT(Shader.PropertyToID(cloudsHandle.name));
            if (historyHandle != null)
                cmd.ReleaseTemporaryRT(Shader.PropertyToID(historyHandle.name));
            if (accumulateHandle != null)
                cmd.ReleaseTemporaryRT(Shader.PropertyToID(accumulateHandle.name));
        }

        public override void OnCameraCleanup(CommandBuffer cmd)
        {
            cloudsHandle = null;
            historyHandle = null;
            accumulateHandle = null;
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            UpdateClouds();

            RTHandle colorHandle = renderingData.cameraData.renderer.cameraColorTargetHandle;

            CommandBuffer cmd = CommandBufferPool.Get();
            using (new ProfilingScope(cmd, new ProfilingSampler("Volumetric Clouds")))
            {
                // Clouds Rendering
                Blitter.BlitCameraTexture(cmd, cloudsHandle, cloudsHandle, cloudsMaterial, pass: 0);

                // Clouds Upscale & Combine
                Blitter.BlitCameraTexture(cmd, colorHandle, colorHandle, cloudsMaterial, pass: 1);

                // Prepare Temporal Reprojection (copy source buffer)
                bool canCopy = colorHandle.rt.format == accumulateHandle.rt.format && colorHandle.rt.antiAliasing == 1 && fastCopy;
                if (canCopy && denoiseClouds) { cmd.CopyTexture(colorHandle, accumulateHandle); }
                else if (denoiseClouds) { Blitter.BlitCameraTexture(cmd, colorHandle, accumulateHandle, cloudsMaterial, pass: 2); }

                // Temporal Reprojection
                if (denoiseClouds) { Blitter.BlitCameraTexture(cmd, accumulateHandle, colorHandle, cloudsMaterial, pass: 3); }

                // Update history texture for temporal reprojection
                canCopy = colorHandle.rt.format == historyHandle.rt.format && colorHandle.rt.antiAliasing == 1 && fastCopy;
                if (canCopy && denoiseClouds) { cmd.CopyTexture(colorHandle, historyHandle); }
                else if (denoiseClouds) { Blitter.BlitCameraTexture(cmd, colorHandle, historyHandle, RenderBufferLoadAction.Load, RenderBufferStoreAction.Store, cloudsMaterial, pass: 2); }
            }
            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();
            CommandBufferPool.Release(cmd);
        }
    }


    public class VolumetricCloudsAmbientPass : ScriptableRenderPass
    {
        private readonly Material cloudsMaterial;
        public RTHandle probeHandle;

        // left, right, up, down, back, front
        readonly Vector3[] cubemapDirs = new Vector3[6] { Vector3.forward, Vector3.back, Vector3.left, Vector3.right, Vector3.up, Vector3.down };
        readonly Vector3[] cubemapUps = new Vector3[6] { Vector3.down, Vector3.down, Vector3.back, Vector3.forward, Vector3.left, Vector3.left };

        public VolumetricCloudsAmbientPass(Material material)
        {
            cloudsMaterial = material;
        }

        public void Dispose()
        {
            probeHandle?.Release();
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            RenderTextureDescriptor desc = renderingData.cameraData.cameraTargetDescriptor;
            desc.depthBufferBits = 0;
            desc.msaaSamples = 1;
            desc.useMipMap = true;
            desc.autoGenerateMips = true;
            desc.width = 16;
            desc.height = 16;
            desc.dimension = TextureDimension.Cube;
            RenderingUtils.ReAllocateIfNeeded(ref probeHandle, desc, FilterMode.Trilinear, TextureWrapMode.Clamp, name: "_VolumetricCloudsAmbientProbe");
            cloudsMaterial.SetTexture("_VolumetricCloudsAmbientProbe", probeHandle);

            ConfigureTarget(probeHandle, probeHandle);
        }

        public override void FrameCleanup(CommandBuffer cmd)
        {
            if (probeHandle != null)
                cmd.ReleaseTemporaryRT(Shader.PropertyToID(probeHandle.name));
        }

        public override void OnCameraCleanup(CommandBuffer cmd)
        {
            probeHandle = null;
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            // UpdateEnvironment() is another way to update ambient lighting but it's really slow.
            //DynamicGI.UpdateEnvironment();

            Camera camera = renderingData.cameraData.camera;
            CommandBuffer cmd = CommandBufferPool.Get();
            using (new ProfilingScope(cmd, new ProfilingSampler("Volumetric Clouds Ambient Probe")))
            {
                for (int i = 0; i < 6; i++)
                {
                    CoreUtils.SetRenderTarget(cmd, probeHandle, ClearFlag.None, 0, (CubemapFace)i);

                    Matrix4x4 viewMatrix = Matrix4x4.TRS(Vector3.zero, Quaternion.LookRotation(cubemapDirs[i], cubemapUps[i]), Vector3.one);
                    if (i == 3) { viewMatrix *= Matrix4x4.Rotate(Quaternion.Euler(Vector3.down * 180.0f)); }
                    if (i == 4) { viewMatrix *= Matrix4x4.Rotate(Quaternion.Euler(Vector3.left * 90.0f));  }
                    if (i == 5) { viewMatrix *= Matrix4x4.Rotate(Quaternion.Euler(Vector3.right * 90.0f)); }

                    // Set the Near & Far Plane to 0.1 and 10
                    Matrix4x4 projectionMatrix = Matrix4x4.Perspective(90.0f, 1.0f, 0.1f, 10.0f);
                    cmd.SetViewProjectionMatrices(viewMatrix, projectionMatrix);

                    // Can we exclude the sun disk in ambient probe?
                    RendererList rendererList = context.CreateSkyboxRendererList(camera, projectionMatrix, viewMatrix);
                    cmd.DrawRendererList(rendererList);
                }
            }
            cmd.SetViewProjectionMatrices(camera.worldToCameraMatrix, camera.projectionMatrix);
            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();
            
            CommandBufferPool.Release(cmd);
        }
    }
}
