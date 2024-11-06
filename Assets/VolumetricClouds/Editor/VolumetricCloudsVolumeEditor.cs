using System.Reflection;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[CanEditMultipleObjects]
#if UNITY_2022_2_OR_NEWER
[CustomEditor(typeof(VolumetricClouds))]
#else
[VolumeComponentEditor(typeof(VolumetricClouds))]
#endif
class VolumetricCloudsEditor : VolumeComponentEditor
{
    // TODO: implement the missing features

    // General
    SerializedDataParameter m_Enable;
    SerializedDataParameter m_LocalClouds;

    // Shape
    //SerializedDataParameter m_CloudControl;

    //SerializedDataParameter m_CloudSimpleMode;
    SerializedDataParameter m_CloudPreset;
    SerializedDataParameter m_DensityCurve;
    SerializedDataParameter m_ErosionCurve;
    SerializedDataParameter m_AmbientOcclusionCurve;

    //SerializedDataParameter m_CumulusMap;
    //SerializedDataParameter m_CumulusMapMultiplier;
    //SerializedDataParameter m_AltoStratusMap;
    //SerializedDataParameter m_AltoStratusMapMultiplier;
    //SerializedDataParameter m_CumulonimbusMap;
    //SerializedDataParameter m_CumulonimbusMapMultiplier;
    //SerializedDataParameter m_RainMap;
    //SerializedDataParameter m_CloudMapResolution;

    //SerializedDataParameter m_CloudMap;
    //SerializedDataParameter m_CloudLut;

    //SerializedDataParameter m_CloudTiling;
    //SerializedDataParameter m_CloudOffset;

    SerializedDataParameter m_BottomAltitude;
    SerializedDataParameter m_AltitudeRange;
    SerializedDataParameter m_FadeInMode;
    SerializedDataParameter m_FadeInStart;
    SerializedDataParameter m_FadeInDistance;

    // Shape
    // General
    SerializedDataParameter m_DensityMultiplier;
    // Shape
    SerializedDataParameter m_ShapeFactor;
    SerializedDataParameter m_ShapeScale;
    SerializedDataParameter m_ShapeOffset;
    SerializedDataParameter m_EarthCurvature;
    // Erosion
    SerializedDataParameter m_ErosionFactor;
    SerializedDataParameter m_ErosionScale;
    //SerializedDataParameter m_ErosionNoiseType;
    // Micro-erosion
    SerializedDataParameter m_MicroErosion;
    SerializedDataParameter m_MicroErosionFactor;
    SerializedDataParameter m_MicroErosionScale;

    // Lighting
    SerializedDataParameter m_ScatteringTint;
    SerializedDataParameter m_PowderEffectIntensity;
    SerializedDataParameter m_MultiScattering;
    SerializedDataParameter m_AmbientLightProbeDimmer;
    SerializedDataParameter m_SunLightDimmer;
    SerializedDataParameter m_ErosionOcclusion;

    // Wind
    SerializedDataParameter m_GlobalWindSpeed;
    SerializedDataParameter m_Orientation;
    //SerializedDataParameter m_CloudMapSpeedMultiplier;
    SerializedDataParameter m_ShapeSpeedMultiplier;
    SerializedDataParameter m_ErosionSpeedMultiplier;
    SerializedDataParameter m_VerticalShapeWindSpeed;
    SerializedDataParameter m_VerticalErosionWindSpeed;
    SerializedDataParameter m_AltitudeDistortion;

    // Quality
    SerializedDataParameter m_TemporalAccumulationFactor;
    //SerializedDataParameter m_GhostingReduction;
    SerializedDataParameter m_PerceptualBlending;
    SerializedDataParameter m_NumPrimarySteps;
    SerializedDataParameter m_NumLightSteps;

    // Shadows
    SerializedDataParameter m_Shadows;
    SerializedDataParameter m_ShadowResolution;
    SerializedDataParameter m_ShadowDistance;
    SerializedDataParameter m_ShadowOpacity;
    SerializedDataParameter m_ShadowOpacityFallback;

    const string k_RendererDataList = "m_RendererDataList";

    const string k_VolumetricCloudsRendererFeature = "VolumetricCloudsURP";
    const string k_NoRendererFeatureMessage = "Volumetric Clouds renderer feature is disabled in the active URP renderer.";
    const string k_RendererFeatureOffMessage = "\"Volumetric Clouds\" is disabled in the active URP renderer.";
    const string k_RenderingDebuggerMessage = "\"Volumetric Clouds\" is disabled to avoid affecting rendering debugging.";

    static public readonly GUIContent k_PerceptualBlending = EditorGUIUtility.TrTextContent("Perceptual Blending", "When enabled, the clouds will blend in a perceptual way with the environment. This may cause artifacts when the sky is over-exposed.");

    const string k_FixButtonName = "Fix";
    const string k_EnableButtonName = "Enable";

    public override void OnEnable()
    {
        var o = new PropertyFetcher<VolumetricClouds>(serializedObject);

        RenderDataListFieldInfo = typeof(UniversalRenderPipelineAsset).GetField(k_RendererDataList, BindingFlags.Instance | BindingFlags.NonPublic);

        // General
        m_Enable = Unpack(o.Find(x => x.state));
        m_LocalClouds = Unpack(o.Find(x => x.localClouds));

        // Shape
        //m_CloudControl = Unpack(o.Find(x => x.cloudControl));

        //m_CloudSimpleMode = Unpack(o.Find(x => x.cloudSimpleMode));
        m_CloudPreset = Unpack(o.Find(x => x.cloudPreset));
        m_DensityCurve = Unpack(o.Find(x => x.densityCurve));
        m_ErosionCurve = Unpack(o.Find(x => x.erosionCurve));
        m_AmbientOcclusionCurve = Unpack(o.Find(x => x.ambientOcclusionCurve));

        //m_CumulusMap = Unpack(o.Find(x => x.cumulusMap));
        //m_CumulusMapMultiplier = Unpack(o.Find(x => x.cumulusMapMultiplier));
        //m_AltoStratusMap = Unpack(o.Find(x => x.altoStratusMap));
        //m_AltoStratusMapMultiplier = Unpack(o.Find(x => x.altoStratusMapMultiplier));
        //m_CumulonimbusMap = Unpack(o.Find(x => x.cumulonimbusMap));
        //m_CumulonimbusMapMultiplier = Unpack(o.Find(x => x.cumulonimbusMapMultiplier));
        //m_RainMap = Unpack(o.Find(x => x.rainMap));
        //m_CloudMapResolution = Unpack(o.Find(x => x.cloudMapResolution));

        //m_CloudMap = Unpack(o.Find(x => x.cloudMap));
        //m_CloudLut = Unpack(o.Find(x => x.cloudLut));

        //m_CloudTiling = Unpack(o.Find(x => x.cloudTiling));
        //m_CloudOffset = Unpack(o.Find(x => x.cloudOffset));

        m_BottomAltitude = Unpack(o.Find(x => x.bottomAltitude));
        m_AltitudeRange = Unpack(o.Find(x => x.altitudeRange));

        m_FadeInMode = Unpack(o.Find(x => x.fadeInMode));
        m_FadeInStart = Unpack(o.Find(x => x.fadeInStart));
        m_FadeInDistance = Unpack(o.Find(x => x.fadeInDistance));

        m_DensityMultiplier = Unpack(o.Find(x => x.densityMultiplier));
        m_ShapeFactor = Unpack(o.Find(x => x.shapeFactor));
        m_ShapeScale = Unpack(o.Find(x => x.shapeScale));
        m_ShapeOffset = Unpack(o.Find(x => x.shapeOffset));
        m_EarthCurvature = Unpack(o.Find(x => x.earthCurvature));
        m_ErosionFactor = Unpack(o.Find(x => x.erosionFactor));
        m_ErosionScale = Unpack(o.Find(x => x.erosionScale));
        //m_ErosionNoiseType = Unpack(o.Find(x => x.erosionNoiseType));

        // Micro-erosion
        m_MicroErosion = Unpack(o.Find(x => x.microErosion));
        m_MicroErosionFactor = Unpack(o.Find(x => x.microErosionFactor));
        m_MicroErosionScale = Unpack(o.Find(x => x.microErosionScale));

        // Lighting
        m_ScatteringTint = Unpack(o.Find(x => x.scatteringTint));
        m_PowderEffectIntensity = Unpack(o.Find(x => x.powderEffectIntensity));
        m_MultiScattering = Unpack(o.Find(x => x.multiScattering));
        m_AmbientLightProbeDimmer = Unpack(o.Find(x => x.ambientLightProbeDimmer));
        m_SunLightDimmer = Unpack(o.Find(x => x.sunLightDimmer));
        m_ErosionOcclusion = Unpack(o.Find(x => x.erosionOcclusion));

        // Wind
        m_Orientation = Unpack(o.Find(x => x.globalOrientation));
        m_GlobalWindSpeed = Unpack(o.Find(x => x.globalSpeed));
        //m_CloudMapSpeedMultiplier = Unpack(o.Find(x => x.cloudMapSpeedMultiplier));
        m_ShapeSpeedMultiplier = Unpack(o.Find(x => x.shapeSpeedMultiplier));
        m_ErosionSpeedMultiplier = Unpack(o.Find(x => x.erosionSpeedMultiplier));
        m_AltitudeDistortion = Unpack(o.Find(x => x.altitudeDistortion));
        m_VerticalShapeWindSpeed = Unpack(o.Find(x => x.verticalShapeWindSpeed));
        m_VerticalErosionWindSpeed = Unpack(o.Find(x => x.verticalErosionWindSpeed));

        // Quality
        m_TemporalAccumulationFactor = Unpack(o.Find(x => x.temporalAccumulationFactor));
        //m_GhostingReduction = Unpack(o.Find(x => x.ghostingReduction));
        m_PerceptualBlending = Unpack(o.Find(x => x.perceptualBlending));
        m_NumPrimarySteps = Unpack(o.Find(x => x.numPrimarySteps));
        m_NumLightSteps = Unpack(o.Find(x => x.numLightSteps));

        // Shadows
        m_Shadows = Unpack(o.Find(x => x.shadows));
        m_ShadowResolution = Unpack(o.Find(x => x.shadowResolution));
        m_ShadowDistance = Unpack(o.Find(x => x.shadowDistance));
        m_ShadowOpacity = Unpack(o.Find(x => x.shadowOpacity));
        m_ShadowOpacityFallback = Unpack(o.Find(x => x.shadowOpacityFallback));

        base.OnEnable();
    }

    public override void OnInspectorGUI()
    {
        var clouds = GetRendererFeature(k_VolumetricCloudsRendererFeature) as VolumetricCloudsURP;
        if (clouds == null)
        {
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(k_NoRendererFeatureMessage, MessageType.Error, wide: true);
            return;
        }
        else if (!clouds.isActive)
        {
            EditorGUILayout.Space();
            CoreEditorUtils.DrawFixMeBox(k_RendererFeatureOffMessage, MessageType.Warning, k_FixButtonName, () =>
            {
                clouds.SetActive(true);
                GUIUtility.ExitGUI();
            });
            EditorGUILayout.Space();
        }

        bool showDebuggerMessage = DebugManager.instance.isAnyDebugUIActive && !clouds.RenderingDebugger;
        bool enableClouds = m_Enable.value.boolValue && m_Enable.overrideState.boolValue;

        if (clouds.isActive && enableClouds && showDebuggerMessage)
        {
            EditorGUILayout.Space();
            CoreEditorUtils.DrawFixMeBox(k_RenderingDebuggerMessage, MessageType.Warning, k_EnableButtonName, () =>
            {
                clouds.RenderingDebugger = true;
                GUIUtility.ExitGUI();
            });
            EditorGUILayout.Space();
        }

        PropertyField(m_Enable);
        PropertyField(m_LocalClouds);

        bool hasCloudMap = CloudsShapeUI();

        //DrawHeader("Wind");
        PropertyField(m_GlobalWindSpeed);
        if (showAdditionalProperties)
        {
            using (new IndentLevelScope())
            {
                //if (hasCloudMap)
                    //PropertyField(m_CloudMapSpeedMultiplier);
                PropertyField(m_ShapeSpeedMultiplier);
                PropertyField(m_ErosionSpeedMultiplier);
            }
        }
        PropertyField(m_Orientation);
        using (new IndentLevelScope())
        {
            PropertyField(m_AltitudeDistortion);
        }

        PropertyField(m_VerticalShapeWindSpeed);
        PropertyField(m_VerticalErosionWindSpeed);

        //DrawHeader("Lighting");
        {
            PropertyField(m_AmbientLightProbeDimmer);
            PropertyField(m_SunLightDimmer);
            PropertyField(m_ErosionOcclusion);
            PropertyField(m_ScatteringTint);
            PropertyField(m_PowderEffectIntensity);
            PropertyField(m_MultiScattering);
        }

        //DrawHeader("Shadows");
        {
            PropertyField(m_Shadows);
            using (new IndentLevelScope())
            {
                PropertyField(m_ShadowResolution);
                PropertyField(m_ShadowOpacity);
                PropertyField(m_ShadowDistance);
                PropertyField(m_ShadowOpacityFallback);
            }
        }

        //DrawHeader("Quality");
        {
            PropertyField(m_TemporalAccumulationFactor);
            //PropertyField(m_GhostingReduction);

            // Here we intentionally choose to display the perceptual blending as a toggle and not as float value to prevent the user from inputing arbitrary values
            // between 0.0f and 1.0f while preserving the ability to interpolate/blend between volumes.
            using (var scope = new OverridablePropertyScope(m_PerceptualBlending, k_PerceptualBlending, this))
                m_PerceptualBlending.value.floatValue = EditorGUILayout.Toggle(k_PerceptualBlending, m_PerceptualBlending.value.floatValue == 1.0f) ? 1.0f : 0.0f;

            PropertyField(m_NumPrimarySteps);
            PropertyField(m_NumLightSteps);
            PropertyField(m_FadeInMode);
            using (new IndentLevelScope())
            {
                if ((VolumetricClouds.CloudFadeInMode)m_FadeInMode.value.enumValueIndex == (VolumetricClouds.CloudFadeInMode.Manual))
                {
                    PropertyField(m_FadeInStart);
                    PropertyField(m_FadeInDistance);
                }
            }
        }
    }
    void LoadPresetValues(VolumetricClouds.CloudPresets preset, bool microDetails)
        {
            switch (preset)
            {
                case VolumetricClouds.CloudPresets.Sparse:
                {
                    m_DensityMultiplier.value.floatValue = 0.4f;
                    if (microDetails)
                    {
                        m_ShapeFactor.value.floatValue = 0.925f;
                        m_ShapeScale.value.floatValue = 5.0f;
                        m_ErosionFactor.value.floatValue = 0.85f;
                        m_ErosionScale.value.floatValue = 75.0f;
                        m_MicroErosionFactor.value.floatValue = 0.65f;
                        m_MicroErosionScale.value.floatValue = 300.0f;
                    }
                    else
                    {
                        m_ShapeFactor.value.floatValue = 0.95f;
                        m_ShapeScale.value.floatValue = 5.0f;
                        m_ErosionFactor.value.floatValue = 0.8f;
                        m_ErosionScale.value.floatValue = 107.0f;
                    }

                    // Curves
                    m_DensityCurve.value.animationCurveValue = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.05f, 1.0f), new Keyframe(0.75f, 1.0f), new Keyframe(1.0f, 0.0f));
                    m_ErosionCurve.value.animationCurveValue = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.1f, 0.9f), new Keyframe(1.0f, 1.0f));
                    m_AmbientOcclusionCurve.value.animationCurveValue = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.25f, 0.5f), new Keyframe(1.0f, 0.0f));

                    // Layer properties
                    m_BottomAltitude.value.floatValue = 3000.0f;
                    m_AltitudeRange.value.floatValue = 1000.0f;
                }
                break;
                case VolumetricClouds.CloudPresets.Cloudy:
                {
                    m_DensityMultiplier.value.floatValue = 0.4f;

                    if (microDetails)
                    {
                        m_ShapeFactor.value.floatValue = 0.875f;
                        m_ShapeScale.value.floatValue = 5.0f;
                        m_ErosionFactor.value.floatValue = 0.9f;
                        m_ErosionScale.value.floatValue = 75.0f;
                        m_MicroErosionFactor.value.floatValue = 0.65f;
                        m_MicroErosionScale.value.floatValue = 300.0f;
                    }
                    else
                    {
                        m_ShapeFactor.value.floatValue = 0.9f;
                        m_ShapeScale.value.floatValue = 5.0f;
                        m_ErosionFactor.value.floatValue = 0.8f;
                        m_ErosionScale.value.floatValue = 107.0f;
                    }

                    // Curves
                    m_DensityCurve.value.animationCurveValue = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.15f, 1.0f), new Keyframe(1.0f, 0.1f));
                    m_ErosionCurve.value.animationCurveValue = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.1f, 0.9f), new Keyframe(1.0f, 1.0f));
                    m_AmbientOcclusionCurve.value.animationCurveValue = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.25f, 0.4f), new Keyframe(1.0f, 0.0f));

                    // Layer properties
                    m_BottomAltitude.value.floatValue = 1200.0f;
                    m_AltitudeRange.value.floatValue = 2000.0f;
                }
                break;
                case VolumetricClouds.CloudPresets.Overcast:
                {
                    m_DensityMultiplier.value.floatValue = 0.3f;

                    if (microDetails)
                    {
                        m_ShapeFactor.value.floatValue = 0.45f;
                        m_ShapeScale.value.floatValue = 5.0f;
                        m_ErosionFactor.value.floatValue = 0.7f;
                        m_ErosionScale.value.floatValue = 75.0f;
                        m_MicroErosionFactor.value.floatValue = 0.5f;
                        m_MicroErosionScale.value.floatValue = 300.0f;
                    }
                    else
                    {
                        m_ShapeFactor.value.floatValue = 0.5f;
                        m_ShapeScale.value.floatValue = 5.0f;
                        m_ErosionFactor.value.floatValue = 0.5f;
                        m_ErosionScale.value.floatValue = 107.0f;
                    }

                    // Curves
                    m_DensityCurve.value.animationCurveValue = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.05f, 1.0f), new Keyframe(0.9f, 0.0f), new Keyframe(1.0f, 0.0f));
                    m_ErosionCurve.value.animationCurveValue = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.1f, 0.9f), new Keyframe(1.0f, 1.0f));
                    m_AmbientOcclusionCurve.value.animationCurveValue = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(1.0f, 0.0f));

                    // Layer properties
                    m_BottomAltitude.value.floatValue = 1500.0f;
                    m_AltitudeRange.value.floatValue = 2500.0f;
                }
                break;
                case VolumetricClouds.CloudPresets.Stormy:
                {
                    m_DensityMultiplier.value.floatValue = 0.35f;

                    if (microDetails)
                    {
                        m_ShapeFactor.value.floatValue = 0.825f;
                        m_ShapeScale.value.floatValue = 5.0f;
                        m_ErosionFactor.value.floatValue = 0.9f;
                        m_ErosionScale.value.floatValue = 75.0f;
                        m_MicroErosionFactor.value.floatValue = 0.6f;
                        m_MicroErosionScale.value.floatValue = 300.0f;
                    }
                    else
                    {
                        m_ShapeFactor.value.floatValue = 0.85f;
                        m_ShapeScale.value.floatValue = 5.0f;
                        m_ErosionFactor.value.floatValue = 0.75f;
                        m_ErosionScale.value.floatValue = 107.0f;
                    }

                    // Curves
                    m_DensityCurve.value.animationCurveValue = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.037f, 1.0f), new Keyframe(0.6f, 1.0f), new Keyframe(1.0f, 0.0f));
                    m_ErosionCurve.value.animationCurveValue = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.05f, 0.8f), new Keyframe(0.2438f, 0.9498f), new Keyframe(0.5f, 1.0f), new Keyframe(0.93f, 0.9268f), new Keyframe(1.0f, 1.0f));
                    m_AmbientOcclusionCurve.value.animationCurveValue = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.1f, 0.4f), new Keyframe(1.0f, 0.0f));

                    // Layer properties
                    m_BottomAltitude.value.floatValue = 1000.0f;
                    m_AltitudeRange.value.floatValue = 5000.0f;
                }
                break;
                default:
                    break;
            }
        }

    void SimpleControlMode(bool controlChanged)
    {
        VolumetricClouds.CloudPresets previousControlPreset = (VolumetricClouds.CloudPresets)m_CloudPreset.value.enumValueIndex;

        // Start checking for changes
        EditorGUI.BeginChangeCheck();

        // Display the preset list
        //using (new IndentLevelScope())
        {
            PropertyField(m_CloudPreset);
            VolumetricClouds.CloudPresets controlPreset = (VolumetricClouds.CloudPresets)m_CloudPreset.value.enumValueIndex;

            // Has the cloud preset property changed?
            if (EditorGUI.EndChangeCheck() || previousControlPreset != controlPreset)
            {
                // If it was changed to anything but custom, this means we need to load the values into the volume
                if (controlPreset != VolumetricClouds.CloudPresets.Custom)
                {
                    LoadPresetValues(controlPreset, m_MicroErosion.value.boolValue);
                }
            }

            if (controlPreset != VolumetricClouds.CloudPresets.Custom)
            {
                // If we are in simple mode and the preset button is enabled, we need to enable all the
                // subsidiary properties. This is different from the quality settings, all the properties need to be forced
                // If a preset is selected and active.
                m_DensityMultiplier.overrideState.boolValue = m_CloudPreset.overrideState.boolValue;
                m_DensityCurve.overrideState.boolValue = m_CloudPreset.overrideState.boolValue;
                m_ShapeFactor.overrideState.boolValue = m_CloudPreset.overrideState.boolValue;
                m_ShapeScale.overrideState.boolValue = m_CloudPreset.overrideState.boolValue;
                m_ErosionFactor.overrideState.boolValue = m_CloudPreset.overrideState.boolValue;
                m_ErosionScale.overrideState.boolValue = m_CloudPreset.overrideState.boolValue;
                //m_ErosionNoiseType.overrideState.boolValue = m_CloudPreset.overrideState.boolValue;
                m_ErosionCurve.overrideState.boolValue = m_CloudPreset.overrideState.boolValue;
                m_MicroErosionFactor.overrideState.boolValue = m_CloudPreset.overrideState.boolValue;
                m_MicroErosionScale.overrideState.boolValue = m_CloudPreset.overrideState.boolValue;
                m_AmbientOcclusionCurve.overrideState.boolValue = m_CloudPreset.overrideState.boolValue;
                m_BottomAltitude.overrideState.boolValue = m_CloudPreset.overrideState.boolValue;
                m_AltitudeRange.overrideState.boolValue = m_CloudPreset.overrideState.boolValue;
            }

            // Start checking for changes
            EditorGUI.BeginChangeCheck();

            // We can only touch the properties if the preset is overridden on this volume
            using (new EditorGUI.DisabledScope(!(m_CloudPreset.overrideState.boolValue)))
            {
                using (new IndentLevelScope())
                {
                    PropertyField(m_DensityMultiplier);
                    PropertyField(m_DensityCurve);
                    PropertyField(m_ShapeFactor);
                    PropertyField(m_ShapeScale);
                    PropertyField(m_ErosionFactor);
                    PropertyField(m_ErosionScale);
                    //PropertyField(m_ErosionNoiseType);
                    PropertyField(m_ErosionCurve);
                }
            }

            if (EditorGUI.EndChangeCheck())
            {
                // Has the any of the properties have changed and we were not in the custom mode, it means we need to switch to the custom mode
                if (controlPreset != VolumetricClouds.CloudPresets.Custom)
                {
                    m_CloudPreset.value.enumValueIndex = (int)VolumetricClouds.CloudPresets.Custom;
                }
            }

            using (new IndentLevelScope())
            {
                PropertyField(m_MicroErosion);
                if (m_MicroErosion.value.boolValue)
                {
                    PropertyField(m_MicroErosionFactor);
                    PropertyField(m_MicroErosionScale);
                }
            }

            // Start checking for changes
            EditorGUI.BeginChangeCheck();

            using (new EditorGUI.DisabledScope(!(m_CloudPreset.overrideState.boolValue)))
            {
                using (new IndentLevelScope())
                {

                    PropertyField(m_AmbientOcclusionCurve);

                    // Layer properties
                    PropertyField(m_BottomAltitude);
                    PropertyField(m_AltitudeRange);
                }
            }

            if (EditorGUI.EndChangeCheck())
            {
                // Has the any of the properties have changed and we were not in the custom mode, it means we need to switch to the custom mode
                if (controlPreset != VolumetricClouds.CloudPresets.Custom)
                {
                    m_CloudPreset.value.enumValueIndex = (int)VolumetricClouds.CloudPresets.Custom;
                }
            }
        }
    }

    bool CloudsShapeUI()
    {
        //EditorGUILayout.LabelField("Shape", EditorStyles.miniLabel);

        bool hasCloudMap = false;
        SimpleControlMode(controlChanged: false);

        // Evaluate the previous control Mode
        //VolumetricClouds.CloudControl previousControlMode = (VolumetricClouds.CloudControl)m_CloudControl.value.enumValueIndex;
        //PropertyField(m_CloudControl);
        //VolumetricClouds.CloudControl controlMode = (VolumetricClouds.CloudControl)m_CloudControl.value.enumValueIndex;

        /*
        bool hasCloudMap = true;
        using (new IndentLevelScope())
        {
            if (controlMode == VolumetricClouds.CloudControl.Advanced)
                AdvancedControlMode();
            else if (controlMode == VolumetricClouds.CloudControl.Manual)
                ManualControlMode();
            else
            {
                hasCloudMap = false;
                SimpleControlMode(previousControlMode != controlMode);
            }
        }
        */

        // Additional properties
        PropertyField(m_ShapeOffset);
        PropertyField(m_EarthCurvature);

        // For the other sections
        return hasCloudMap;
    }

    /// <summary>
    /// Check if the Volumetric Clouds renderer feature has been added.
    /// From "https://forum.unity.com/threads/enable-or-disable-render-features-at-runtime.932571/"
    /// </summary>
    #region Reflection
    private static FieldInfo RenderDataListFieldInfo;

    private static ScriptableRendererData[] GetRendererDataList(UniversalRenderPipelineAsset asset = null)
    {
        try
        {
            if (asset == null)
                asset = (UniversalRenderPipelineAsset)GraphicsSettings.currentRenderPipeline;

            if (asset == null)
                return null;
 
            if (RenderDataListFieldInfo == null)
                return null;
 
            var renderDataList = (ScriptableRendererData[])RenderDataListFieldInfo.GetValue(asset);
            return renderDataList;
        }
        catch
        {
            // Fail silently if reflection failed.
            return null;
        }
    }

    private static ScriptableRendererFeature GetRendererFeature(string typeName)
    {
        var renderDataList = GetRendererDataList();
        if (renderDataList == null || renderDataList.Length == 0)
            return null;

        foreach (var renderData in renderDataList)
        {
            foreach (var rendererFeature in renderData.rendererFeatures)
            {
                if (rendererFeature == null)
                    continue;

                if (rendererFeature.GetType().Name.Contains(typeName))
                {
                    return rendererFeature;
                }
            }
        }

        return null;
    }
    #endregion
}
