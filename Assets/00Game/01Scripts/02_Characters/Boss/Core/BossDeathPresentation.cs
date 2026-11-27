using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public sealed class BossDeathPresentation : MonoBehaviour
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
    private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
    private static readonly int BumpMapId = Shader.PropertyToID("_BumpMap");
    private static readonly int BumpScaleId = Shader.PropertyToID("_BumpScale");
    private static readonly int MetallicGlossMapId = Shader.PropertyToID("_MetallicGlossMap");
    private static readonly int MetallicId = Shader.PropertyToID("_Metallic");
    private static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");
    private static readonly int GlossinessId = Shader.PropertyToID("_Glossiness");
    private static readonly int OcclusionMapId = Shader.PropertyToID("_OcclusionMap");
    private static readonly int OcclusionStrengthId = Shader.PropertyToID("_OcclusionStrength");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
    private static readonly int DissolveAmountId = Shader.PropertyToID("_DissolveAmount");
    private static readonly int DissolveOriginYId = Shader.PropertyToID("_DissolveOriginY");
    private static readonly int DissolveHeightInvId = Shader.PropertyToID("_DissolveHeightInv");
    private static readonly int DissolveNoiseScaleId = Shader.PropertyToID("_DissolveNoiseScale");
    private static readonly int DissolveNoiseStrengthId = Shader.PropertyToID("_DissolveNoiseStrength");
    private static readonly int DissolveEdgeWidthId = Shader.PropertyToID("_DissolveEdgeWidth");
    private static readonly int DissolveEdgeColorId = Shader.PropertyToID("_DissolveEdgeColor");
    private static readonly int GlowBoostId = Shader.PropertyToID("_GlowBoost");
    private static readonly int OpacityId = Shader.PropertyToID("_Opacity");
    private static readonly int RingRadiusId = Shader.PropertyToID("_RingRadius");
    private static readonly int AccentColorId = Shader.PropertyToID("_AccentColor");
    private static readonly int ModeId = Shader.PropertyToID("_Mode");
    private const string TextureResourceRoot = "FX/BossDeath/";

    [Header("Timings")]
    [SerializeField] [Range(0.1f, 1f)] private float presentationSpeed = 0.5f;
    [SerializeField] private float glowBuildDuration = 0.85f;
    [SerializeField] private float explosionFlashDuration = 0.28f;
    [SerializeField] private float dissolveDuration = 1.6f;
    [SerializeField] private float lingerDuration = 0.35f;

    [Header("Glow")]
    [SerializeField] private Color glowColor = new Color(0.96f, 0.88f, 0.72f, 1f);
    [SerializeField] private Color dissolveEdgeColor = new Color(0.98f, 0.92f, 0.78f, 1f);
    [SerializeField] private float maxGlowBoost = 1.9f;
    [SerializeField] private float lightIntensity = 2.6f;
    [SerializeField] private float lightRange = 5.5f;
    [SerializeField] private float explosionGlowBoost = 2.4f;
    [SerializeField] private float explosionLightMultiplier = 2.2f;
    [SerializeField] private float explosionEmissionBoost = 1.6f;

    [Header("Dissolve")]
    [SerializeField] private float dissolveNoiseScale = 3.8f;
    [SerializeField] private float dissolveNoiseStrength = 0.3f;
    [SerializeField] private float dissolveEdgeWidth = 0.09f;

    [Header("FX")]
    [SerializeField] private float pulseHeightOffset = 0.15f;
    [SerializeField] private float pulseScaleMultiplier = 2.5f;
    [SerializeField] private float explosionPulseOpacity = 1.35f;
    [SerializeField] private float explosionPulseScale = 1.95f;
    [SerializeField] private int burstParticleCount = 72;
    [SerializeField] private int ashParticleCount = 96;

    [Header("Screen Flash")]
    [SerializeField] [Range(0f, 1f)] private float screenFlashPeakWeight = 0.82f;
    [SerializeField] private float screenFlashBloomIntensity = 7.5f;
    [SerializeField] private float screenFlashPostExposure = 1.15f;
    [SerializeField] private float screenFlashContrast = -12f;
    [SerializeField] private Color screenFlashColorFilter = new Color(1f, 0.96f, 0.88f, 1f);
    [SerializeField] private float explosionAfterglowStrength = 0.34f;
    [SerializeField] private float explosionAfterglowFalloff = 0.55f;

    private readonly List<Renderer> targetRenderers = new List<Renderer>();
    private readonly List<Material[]> originalMaterials = new List<Material[]>();
    private readonly List<ShadowCastingMode> originalShadowModes = new List<ShadowCastingMode>();
    private readonly List<bool> originalReceiveShadows = new List<bool>();
    private readonly List<Material[]> runtimeMaterials = new List<Material[]>();

    private Shader deathShader;
    private Bounds combinedBounds;
    private bool hasBounds;
    private bool prepared;
    private bool isPlaying;

    private GameObject fxRoot;
    private Light glowLightComponent;
    private Material pulseMaterial;
    private Transform pulseTransform;
    private ParticleSystem burstParticles;
    private ParticleSystem ashParticles;
    private Volume screenFlashVolume;
    private VolumeProfile screenFlashProfile;
    private Bloom screenFlashBloom;
    private ColorAdjustments screenFlashColorAdjustments;

    public IEnumerator PlaySequence()
    {
        if (isPlaying)
        {
            yield break;
        }

        isPlaying = true;
        PrepareIfNeeded();

        if (!prepared || runtimeMaterials.Count == 0)
        {
            isPlaying = false;
            yield break;
        }

        if (EffectManager.Instance != null)
        {
            EffectManager.Instance.DoCameraShake(0.22f, 0.16f);
        }

        if (ashParticles != null)
        {
            ashParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ashParticles.Play(true);
        }

        float playbackSpeed = Mathf.Max(0.1f, presentationSpeed);
        float glowElapsed = 0f;
        while (glowElapsed < glowBuildDuration)
        {
            glowElapsed += Time.deltaTime * playbackSpeed;
            float t = Mathf.Clamp01(glowElapsed / glowBuildDuration);
            float eased = Mathf.SmoothStep(0f, 1f, t);
            UpdatePresentation(eased * 0.18f, eased, 0.35f + eased * 0.65f, 0f);
            UpdatePulse(eased, 0f, 0f, 0f);
            UpdateScreenFlash(eased * 0.08f);
            yield return null;
        }

        if (burstParticles != null)
        {
            burstParticles.Emit(burstParticleCount + Mathf.RoundToInt(burstParticleCount * 0.65f));
        }

        if (EffectManager.Instance != null)
        {
            EffectManager.Instance.DoCameraShake(0.34f, 0.2f);
        }

        float explosionElapsed = 0f;
        while (explosionElapsed < explosionFlashDuration)
        {
            explosionElapsed += Time.deltaTime * playbackSpeed;
            float t = Mathf.Clamp01(explosionElapsed / explosionFlashDuration);
            float blastStrength = 1f - Mathf.SmoothStep(0f, 1f, t);
            UpdatePresentation(0.18f + t * 0.07f, 1f, 1f, blastStrength);
            UpdatePulse(1f, 0f, blastStrength, t);
            UpdateScreenFlash(blastStrength);
            yield return null;
        }

        float dissolveElapsed = 0f;
        while (dissolveElapsed < dissolveDuration)
        {
            dissolveElapsed += Time.deltaTime * playbackSpeed;
            float t = Mathf.Clamp01(dissolveElapsed / dissolveDuration);
            float eased = Mathf.SmoothStep(0f, 1f, t);
            float afterglow = Mathf.Pow(1f - eased, explosionAfterglowFalloff) * explosionAfterglowStrength;
            UpdatePresentation(eased, 1f - eased * 0.18f, 1f, afterglow);
            UpdatePulse(1f, eased, afterglow, 1f);
            UpdateScreenFlash(afterglow);
            yield return null;
        }

        SetRenderersEnabled(false);
        DetachFxRoot();

        float lingerElapsed = 0f;
        while (lingerElapsed < lingerDuration)
        {
            lingerElapsed += Time.deltaTime * playbackSpeed;
            UpdatePulse(1f, 1f, 0f, 1f);
            UpdateScreenFlash(0f);
            yield return null;
        }

        UpdateScreenFlash(0f);
        CleanupRuntimeMaterials();
        isPlaying = false;
    }

    private void PrepareIfNeeded()
    {
        if (prepared)
        {
            return;
        }

        deathShader = Shader.Find("Custom/Character/BossDeathDissolve");
        if (deathShader == null)
        {
            Debug.LogWarning("BossDeathPresentation: dissolve shader not found.");
            return;
        }

        CacheRenderers();
        if (targetRenderers.Count == 0)
        {
            return;
        }

        CreateRuntimeMaterials();
        CreateFxRoot();
        prepared = runtimeMaterials.Count > 0;
    }

    private void CacheRenderers()
    {
        targetRenderers.Clear();
        originalMaterials.Clear();
        originalShadowModes.Clear();
        originalReceiveShadows.Clear();
        runtimeMaterials.Clear();
        hasBounds = false;

        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || renderer is ParticleSystemRenderer || renderer is TrailRenderer || renderer is LineRenderer)
            {
                continue;
            }

            targetRenderers.Add(renderer);
            originalMaterials.Add(renderer.sharedMaterials);
            originalShadowModes.Add(renderer.shadowCastingMode);
            originalReceiveShadows.Add(renderer.receiveShadows);

            if (!hasBounds)
            {
                combinedBounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                combinedBounds.Encapsulate(renderer.bounds);
            }
        }
    }

    private void CreateRuntimeMaterials()
    {
        if (!hasBounds)
        {
            return;
        }

        float height = Mathf.Max(combinedBounds.size.y, 0.01f);
        float originY = combinedBounds.min.y;
        float heightInv = 1f / height;

        for (int rendererIndex = 0; rendererIndex < targetRenderers.Count; rendererIndex++)
        {
            Renderer renderer = targetRenderers[rendererIndex];
            Material[] sourceMaterials = originalMaterials[rendererIndex];
            Material[] replacementMaterials = new Material[sourceMaterials.Length];

            for (int materialIndex = 0; materialIndex < sourceMaterials.Length; materialIndex++)
            {
                Material source = sourceMaterials[materialIndex];
                Material runtime = new Material(deathShader)
                {
                    name = source != null ? source.name + "_BossDeath" : "BossDeathDissolve_Runtime"
                };

                CopyMaterialProperties(source, runtime);
                runtime.SetFloat(DissolveAmountId, 0f);
                runtime.SetFloat(DissolveOriginYId, originY);
                runtime.SetFloat(DissolveHeightInvId, heightInv);
                runtime.SetFloat(DissolveNoiseScaleId, dissolveNoiseScale);
                runtime.SetFloat(DissolveNoiseStrengthId, dissolveNoiseStrength);
                runtime.SetFloat(DissolveEdgeWidthId, dissolveEdgeWidth);
                runtime.SetColor(DissolveEdgeColorId, dissolveEdgeColor);
                runtime.SetFloat(GlowBoostId, 0f);

                replacementMaterials[materialIndex] = runtime;
            }

            renderer.materials = replacementMaterials;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            runtimeMaterials.Add(replacementMaterials);
        }
    }

    private void CopyMaterialProperties(Material source, Material destination)
    {
        if (destination == null)
        {
            return;
        }

        destination.SetColor(BaseColorId, ResolveBaseColor(source));

        if (source == null)
        {
            return;
        }

        Texture baseMap = source.HasProperty(BaseMapId) ? source.GetTexture(BaseMapId) : source.HasProperty(MainTexId) ? source.GetTexture(MainTexId) : null;
        if (baseMap != null)
        {
            destination.SetTexture(BaseMapId, baseMap);
        }

        Vector2 scale = Vector2.one;
        Vector2 offset = Vector2.zero;
        if (source.HasProperty(BaseMapId))
        {
            scale = source.GetTextureScale(BaseMapId);
            offset = source.GetTextureOffset(BaseMapId);
        }
        else if (source.HasProperty(MainTexId))
        {
            scale = source.GetTextureScale(MainTexId);
            offset = source.GetTextureOffset(MainTexId);
        }

        destination.SetTextureScale(BaseMapId, scale);
        destination.SetTextureOffset(BaseMapId, offset);

        if (source.HasProperty(BumpMapId))
        {
            Texture bumpMap = source.GetTexture(BumpMapId);
            if (bumpMap != null)
            {
                destination.SetTexture(BumpMapId, bumpMap);
            }
        }

        if (source.HasProperty(BumpScaleId))
        {
            destination.SetFloat(BumpScaleId, source.GetFloat(BumpScaleId));
        }

        if (source.HasProperty(MetallicGlossMapId))
        {
            Texture metallicGlossMap = source.GetTexture(MetallicGlossMapId);
            if (metallicGlossMap != null)
            {
                destination.SetTexture(MetallicGlossMapId, metallicGlossMap);
            }
        }

        if (source.HasProperty(MetallicId))
        {
            destination.SetFloat(MetallicId, source.GetFloat(MetallicId));
        }

        if (source.HasProperty(SmoothnessId))
        {
            destination.SetFloat(SmoothnessId, source.GetFloat(SmoothnessId));
        }
        else if (source.HasProperty(GlossinessId))
        {
            destination.SetFloat(SmoothnessId, source.GetFloat(GlossinessId));
        }

        if (source.HasProperty(OcclusionMapId))
        {
            Texture occlusionMap = source.GetTexture(OcclusionMapId);
            if (occlusionMap != null)
            {
                destination.SetTexture(OcclusionMapId, occlusionMap);
            }
        }

        if (source.HasProperty(OcclusionStrengthId))
        {
            destination.SetFloat(OcclusionStrengthId, source.GetFloat(OcclusionStrengthId));
        }

        Color emissionColor = source.HasProperty(EmissionColorId) ? source.GetColor(EmissionColorId) : Color.black;
        destination.SetColor(EmissionColorId, emissionColor);
    }

    private Color ResolveBaseColor(Material source)
    {
        if (source == null)
        {
            return Color.white;
        }

        if (source.HasProperty(BaseColorId))
        {
            return source.GetColor(BaseColorId);
        }

        if (source.HasProperty(ColorId))
        {
            return source.GetColor(ColorId);
        }

        return Color.white;
    }

    private void CreateFxRoot()
    {
        fxRoot = new GameObject("BossDeathFX_Runtime");
        fxRoot.transform.position = hasBounds ? combinedBounds.center : transform.position;

        glowLightComponent = CreateGlowLight(fxRoot.transform);
        pulseTransform = CreatePulseRing(fxRoot.transform, out pulseMaterial);
        burstParticles = CreateBurstParticles(fxRoot.transform);
        ashParticles = CreateAshParticles(fxRoot.transform);
        CreateScreenFlashVolume(fxRoot.transform);
    }

    private Light CreateGlowLight(Transform parentTransform)
    {
        GameObject lightObject = new GameObject("SoulLight");
        lightObject.transform.SetParent(parentTransform, false);
        lightObject.transform.position = combinedBounds.center + Vector3.up * combinedBounds.extents.y * 0.35f;

        Light pointLight = lightObject.AddComponent<Light>();
        pointLight.type = LightType.Point;
        pointLight.color = glowColor;
        pointLight.intensity = 0f;
        pointLight.range = lightRange;
        pointLight.shadows = LightShadows.None;
        return pointLight;
    }

    private Transform CreatePulseRing(Transform parentTransform, out Material material)
    {
        material = null;
        Shader pulseShader = Shader.Find("Custom/VFX/BossAttackPulse");
        if (pulseShader == null)
        {
            return null;
        }

        GameObject ringObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
        ringObject.name = "SoulPulseRing";
        ringObject.transform.SetParent(parentTransform, false);
        ringObject.transform.position = new Vector3(combinedBounds.center.x, combinedBounds.min.y + pulseHeightOffset, combinedBounds.center.z);
        ringObject.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        Destroy(ringObject.GetComponent<Collider>());

        MeshRenderer meshRenderer = ringObject.GetComponent<MeshRenderer>();
        material = new Material(pulseShader)
        {
            name = "BossDeathPulse_Runtime"
        };

        material.SetFloat(OpacityId, 0f);
        material.SetFloat(ModeId, 1f);
        material.SetFloat(RingRadiusId, 0.36f);
        material.SetColor(BaseColorId, glowColor * 0.65f);
        material.SetColor(AccentColorId, glowColor);
        ApplyPulseTextures(material);
        meshRenderer.sharedMaterial = material;
        meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;

        float ringScale = Mathf.Max(combinedBounds.extents.x, combinedBounds.extents.z) * pulseScaleMultiplier;
        ringObject.transform.localScale = Vector3.one * ringScale;
        return ringObject.transform;
    }

    private ParticleSystem CreateBurstParticles(Transform parentTransform)
    {
        GameObject particleObject = new GameObject("SoulBurst");
        particleObject.transform.SetParent(parentTransform, false);
        particleObject.transform.position = combinedBounds.center + Vector3.up * combinedBounds.extents.y * 0.25f;

        ParticleSystem particleSystem = particleObject.AddComponent<ParticleSystem>();
        particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ConfigureParticleRenderer(particleObject.GetComponent<ParticleSystemRenderer>(), glowColor, true, LoadEffectTexture("BossDeath_SoulBurst"));

        var main = particleSystem.main;
        main.playOnAwake = false;
        main.duration = 1.2f;
        main.loop = false;
        main.simulationSpeed = Mathf.Max(0.1f, presentationSpeed);
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.4f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.8f, 4.2f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.18f);
        main.startColor = new ParticleSystem.MinMaxGradient(glowColor, dissolveEdgeColor);
        main.maxParticles = Mathf.Max(128, burstParticleCount);
        main.gravityModifier = 0f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = particleSystem.emission;
        emission.enabled = false;

        var shape = particleSystem.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = Mathf.Max(combinedBounds.extents.x, combinedBounds.extents.z) * 0.45f;

        var velocityOverLifetime = particleSystem.velocityOverLifetime;
        velocityOverLifetime.enabled = true;
        velocityOverLifetime.space = ParticleSystemSimulationSpace.World;
        velocityOverLifetime.x = new ParticleSystem.MinMaxCurve(0f, 0f);
        velocityOverLifetime.y = new ParticleSystem.MinMaxCurve(0.8f, 1.5f);
        velocityOverLifetime.z = new ParticleSystem.MinMaxCurve(0f, 0f);

        var colorOverLifetime = particleSystem.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(glowColor, 0f),
                new GradientColorKey(dissolveEdgeColor, 0.45f),
                new GradientColorKey(new Color(0.35f, 0.38f, 0.42f, 1f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(1f, 0.08f),
                new GradientAlphaKey(0.85f, 0.6f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);

        var noise = particleSystem.noise;
        noise.enabled = true;
        noise.strength = 0.45f;
        noise.frequency = 0.6f;

        return particleSystem;
    }

    private ParticleSystem CreateAshParticles(Transform parentTransform)
    {
        GameObject particleObject = new GameObject("SoulAsh");
        particleObject.transform.SetParent(parentTransform, false);
        particleObject.transform.position = combinedBounds.center;

        ParticleSystem particleSystem = particleObject.AddComponent<ParticleSystem>();
        particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ConfigureParticleRenderer(particleObject.GetComponent<ParticleSystemRenderer>(), dissolveEdgeColor, false, LoadEffectTexture("BossDeath_Ash"));

        var main = particleSystem.main;
        main.playOnAwake = false;
        main.duration = glowBuildDuration + explosionFlashDuration + dissolveDuration + lingerDuration;
        main.loop = false;
        main.simulationSpeed = Mathf.Max(0.1f, presentationSpeed);
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.1f, 1.9f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 1.35f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.24f);
        main.startColor = new ParticleSystem.MinMaxGradient(dissolveEdgeColor, new Color(0.55f, 0.58f, 0.62f, 1f));
        main.maxParticles = Mathf.Max(160, ashParticleCount);
        main.gravityModifier = -0.02f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = particleSystem.emission;
        emission.enabled = true;
        emission.rateOverTime = 36f;

        var shape = particleSystem.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.ConeVolume;
        shape.angle = 18f;
        shape.radius = Mathf.Max(combinedBounds.extents.x, combinedBounds.extents.z) * 0.42f;
        shape.length = Mathf.Max(0.5f, combinedBounds.extents.y);

        var velocityOverLifetime = particleSystem.velocityOverLifetime;
        velocityOverLifetime.enabled = true;
        velocityOverLifetime.space = ParticleSystemSimulationSpace.World;
        velocityOverLifetime.y = new ParticleSystem.MinMaxCurve(0.8f, 1.8f);
        velocityOverLifetime.x = new ParticleSystem.MinMaxCurve(-0.25f, 0.25f);
        velocityOverLifetime.z = new ParticleSystem.MinMaxCurve(-0.25f, 0.25f);

        var colorOverLifetime = particleSystem.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(dissolveEdgeColor, 0f),
                new GradientColorKey(new Color(0.8f, 0.82f, 0.86f, 1f), 0.35f),
                new GradientColorKey(new Color(0.35f, 0.38f, 0.44f, 1f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(0.8f, 0.12f),
                new GradientAlphaKey(0.5f, 0.72f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);

        var noise = particleSystem.noise;
        noise.enabled = true;
        noise.strength = 0.35f;
        noise.frequency = 0.7f;

        return particleSystem;
    }

    private void ConfigureParticleRenderer(ParticleSystemRenderer renderer, Color tint, bool additive, Texture2D texture)
    {
        if (renderer == null)
        {
            return;
        }

        Shader shader = Shader.Find("Custom/VFX/BossDeathParticle");
        if (shader == null)
        {
            shader = Shader.Find("Sprites/Default");
        }

        if (shader == null)
        {
            return;
        }

        Material material = new Material(shader)
        {
            name = additive ? "BossDeathBurstParticle_Runtime" : "BossDeathAshParticle_Runtime"
        };

        if (material.HasProperty(BaseColorId))
        {
            material.SetColor(BaseColorId, tint);
        }

        if (material.HasProperty(ColorId))
        {
            material.SetColor(ColorId, tint);
        }

        if (material.HasProperty(EmissionColorId))
        {
            material.SetColor(EmissionColorId, tint * 0.8f);
        }

        if (texture != null)
        {
            if (material.HasProperty(BaseMapId))
            {
                material.SetTexture(BaseMapId, texture);
            }

            if (material.HasProperty(MainTexId))
            {
                material.SetTexture(MainTexId, texture);
            }
        }

        if (material.HasProperty(Shader.PropertyToID("_Intensity")))
        {
            material.SetFloat(Shader.PropertyToID("_Intensity"), additive ? 1.15f : 0.6f);
        }

        if (material.HasProperty(Shader.PropertyToID("_Cutoff")))
        {
            material.SetFloat(Shader.PropertyToID("_Cutoff"), additive ? 0.05f : 0.08f);
        }

        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
    }

    private void ApplyPulseTextures(Material material)
    {
        if (material == null)
        {
            return;
        }

        Texture2D mask = LoadEffectTexture("BossDeath_PulseMask");
        Texture2D noise = LoadEffectTexture("BossDeath_NoiseDetail");

        int maskId = Shader.PropertyToID("_MaskTex");
        int noiseId = Shader.PropertyToID("_NoiseTex");
        int detailId = Shader.PropertyToID("_DetailTex");

        if (mask != null && material.HasProperty(maskId))
        {
            material.SetTexture(maskId, mask);
        }

        if (noise != null)
        {
            if (material.HasProperty(noiseId))
            {
                material.SetTexture(noiseId, noise);
            }

            if (material.HasProperty(detailId))
            {
                material.SetTexture(detailId, noise);
            }
        }
    }

    private Texture2D LoadEffectTexture(string name)
    {
        return Resources.Load<Texture2D>(TextureResourceRoot + name);
    }

    private void CreateScreenFlashVolume(Transform parentTransform)
    {
        GameObject volumeObject = new GameObject("ScreenFlashVolume");
        volumeObject.transform.SetParent(parentTransform, false);
        volumeObject.transform.position = hasBounds ? combinedBounds.center : transform.position;

        screenFlashVolume = volumeObject.AddComponent<Volume>();
        screenFlashVolume.isGlobal = true;
        screenFlashVolume.priority = 200f;
        screenFlashVolume.weight = 0f;

        screenFlashProfile = ScriptableObject.CreateInstance<VolumeProfile>();
        screenFlashVolume.profile = screenFlashProfile;

        screenFlashBloom = screenFlashProfile.Add<Bloom>(true);
        screenFlashBloom.active = true;
        screenFlashBloom.intensity.overrideState = true;
        screenFlashBloom.threshold.overrideState = true;
        screenFlashBloom.scatter.overrideState = true;
        screenFlashBloom.tint.overrideState = true;
        screenFlashBloom.intensity.value = 0f;
        screenFlashBloom.threshold.value = 0.75f;
        screenFlashBloom.scatter.value = 0.8f;
        screenFlashBloom.tint.value = screenFlashColorFilter;

        screenFlashColorAdjustments = screenFlashProfile.Add<ColorAdjustments>(true);
        screenFlashColorAdjustments.active = true;
        screenFlashColorAdjustments.postExposure.overrideState = true;
        screenFlashColorAdjustments.colorFilter.overrideState = true;
        screenFlashColorAdjustments.contrast.overrideState = true;
        screenFlashColorAdjustments.postExposure.value = 0f;
        screenFlashColorAdjustments.colorFilter.value = Color.white;
        screenFlashColorAdjustments.contrast.value = 0f;
    }

    private void UpdateScreenFlash(float strength)
    {
        if (screenFlashVolume == null || screenFlashBloom == null || screenFlashColorAdjustments == null)
        {
            return;
        }

        float clampedStrength = Mathf.Clamp01(strength);
        screenFlashVolume.weight = clampedStrength * screenFlashPeakWeight;
        screenFlashBloom.intensity.value = screenFlashBloomIntensity * clampedStrength;
        screenFlashColorAdjustments.postExposure.value = screenFlashPostExposure * clampedStrength;
        screenFlashColorAdjustments.contrast.value = screenFlashContrast * clampedStrength;
        screenFlashColorAdjustments.colorFilter.value = Color.Lerp(Color.white, screenFlashColorFilter, clampedStrength);
    }

    private void UpdatePresentation(float dissolveAmount, float glowT, float pulseT, float blastStrength)
    {
        float glowBoost = Mathf.Lerp(0.15f, maxGlowBoost, glowT) + explosionGlowBoost * blastStrength;
        Color emission = Color.Lerp(Color.black, glowColor * (maxGlowBoost * 0.55f), glowT);
        emission += glowColor * (explosionEmissionBoost * blastStrength);

        for (int rendererIndex = 0; rendererIndex < runtimeMaterials.Count; rendererIndex++)
        {
            Material[] materials = runtimeMaterials[rendererIndex];
            for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
            {
                Material material = materials[materialIndex];
                if (material == null)
                {
                    continue;
                }

                material.SetFloat(DissolveAmountId, dissolveAmount);
                material.SetFloat(GlowBoostId, glowBoost);
                material.SetColor(EmissionColorId, emission);
                material.SetColor(DissolveEdgeColorId, Color.Lerp(dissolveEdgeColor, glowColor, pulseT * 0.35f));
            }
        }

        if (glowLightComponent != null)
        {
            glowLightComponent.intensity = lightIntensity * (glowT + explosionLightMultiplier * blastStrength);
            glowLightComponent.color = Color.Lerp(dissolveEdgeColor, glowColor, pulseT * 0.45f);
        }
    }

    private void UpdatePulse(float glowT, float dissolveT, float blastStrength, float blastProgress)
    {
        if (pulseMaterial == null || pulseTransform == null)
        {
            return;
        }

        float opacity = Mathf.Lerp(0f, 0.72f, glowT) * (1f - dissolveT * 0.55f);
        float ringRadius = Mathf.Lerp(0.28f, 0.72f, dissolveT);
        float scale = Mathf.Lerp(0.75f, 1.45f, dissolveT);

        if (blastStrength > 0f)
        {
            opacity = Mathf.Max(opacity, Mathf.Lerp(explosionPulseOpacity, 0.55f, blastProgress));
            ringRadius = Mathf.Lerp(0.16f, 0.98f, blastProgress);
            scale = Mathf.Max(scale, Mathf.Lerp(0.92f, explosionPulseScale, blastProgress));
        }

        pulseMaterial.SetFloat(OpacityId, opacity);
        pulseMaterial.SetFloat(RingRadiusId, ringRadius);
        pulseMaterial.SetColor(BaseColorId, Color.Lerp(glowColor * 0.65f, Color.white, blastStrength * 0.45f));
        pulseMaterial.SetColor(AccentColorId, Color.Lerp(glowColor, Color.white, blastStrength * 0.75f));
        pulseTransform.localScale = Vector3.one * Mathf.Max(0.01f, Mathf.Max(combinedBounds.extents.x, combinedBounds.extents.z) * pulseScaleMultiplier * scale);
    }

    private void SetRenderersEnabled(bool enabled)
    {
        for (int i = 0; i < targetRenderers.Count; i++)
        {
            if (targetRenderers[i] != null)
            {
                targetRenderers[i].enabled = enabled;
            }
        }
    }

    private void DetachFxRoot()
    {
        if (fxRoot == null)
        {
            return;
        }

        fxRoot.transform.SetParent(null, true);
        Destroy(fxRoot, 3f);
    }

    private void CleanupRuntimeMaterials()
    {
        if (screenFlashProfile != null)
        {
            Destroy(screenFlashProfile);
            screenFlashProfile = null;
        }

        screenFlashVolume = null;
        screenFlashBloom = null;
        screenFlashColorAdjustments = null;

        for (int rendererIndex = 0; rendererIndex < targetRenderers.Count; rendererIndex++)
        {
            Renderer renderer = targetRenderers[rendererIndex];
            if (renderer == null)
            {
                continue;
            }

            renderer.shadowCastingMode = originalShadowModes[rendererIndex];
            renderer.receiveShadows = originalReceiveShadows[rendererIndex];
        }
    }
}
