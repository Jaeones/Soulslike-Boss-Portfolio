using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
public sealed class SoulPickupVisual : MonoBehaviour
{
    [SerializeField] private Material soulBillboardMaterial;
    [SerializeField] private Vector3 visualAnchorLocalOffset = new Vector3(0f, 0.18f, 0f);
    [SerializeField] private float bobAmplitude = 0.04f;
    [SerializeField] private float bobFrequency = 1.35f;
    [SerializeField] private float pulseFrequency = 1.8f;
    [SerializeField] private float coreSize = 0.9f;
    [SerializeField] private float haloSize = 1.25f;
    [SerializeField] private bool useGroundAuraQuad;
    [SerializeField] private float groundAuraSize = 1.6f;
    [SerializeField] private Color soulCoreColor = new Color(1f, 1f, 1f, 0.94f);
    [SerializeField] private Color soulHaloColor = new Color(0.78f, 0.9f, 1f, 0.46f);
    [SerializeField] private Color soulGroundColor = new Color(0.44f, 0.62f, 1f, 0.34f);
    [SerializeField] private float pointLightIntensity = 1f;
    [SerializeField] private float pointLightRange = 4.4f;

    private Transform _soulVisualRoot;
    private Transform _coreQuadTransform;
    private Transform _haloQuadTransform;
    private Transform _groundAuraTransform;
    private MeshRenderer _coreRenderer;
    private MeshRenderer _haloRenderer;
    private MeshRenderer _groundRenderer;
    private Light _soulLight;
    private MaterialPropertyBlock _corePropertyBlock;
    private MaterialPropertyBlock _haloPropertyBlock;
    private MaterialPropertyBlock _groundPropertyBlock;
    private bool _soulVisualCreated;

    private void LateUpdate()
    {
        if (!_soulVisualCreated || _soulVisualRoot == null)
        {
            return;
        }

        float time = Time.time;
        float bobOffset = Mathf.Sin(time * bobFrequency) * bobAmplitude;
        float pulse = 0.5f + 0.5f * Mathf.Sin(time * pulseFrequency);

        _soulVisualRoot.localPosition = visualAnchorLocalOffset + Vector3.up * bobOffset;

        if (_coreQuadTransform != null)
        {
            float scale = coreSize * Mathf.Lerp(0.92f, 1.08f, pulse);
            _coreQuadTransform.localScale = new Vector3(scale, scale, 1f);
        }

        if (_haloQuadTransform != null)
        {
            float scale = haloSize * Mathf.Lerp(0.96f, 1.18f, pulse);
            _haloQuadTransform.localScale = new Vector3(scale, scale, 1f);
        }

        if (useGroundAuraQuad && _groundAuraTransform != null)
        {
            float scale = groundAuraSize * Mathf.Lerp(0.95f, 1.08f, pulse);
            _groundAuraTransform.localScale = new Vector3(scale, scale, 1f);
        }

        ApplySoulRendererColor(
            _coreRenderer,
            _corePropertyBlock,
            ScaleColor(soulCoreColor, Mathf.Lerp(1.05f, 1.28f, pulse), Mathf.Lerp(0.92f, 1f, pulse)));
        ApplySoulRendererColor(
            _haloRenderer,
            _haloPropertyBlock,
            ScaleColor(soulHaloColor, Mathf.Lerp(0.88f, 1.18f, pulse), Mathf.Lerp(0.85f, 1f, pulse)));

        if (useGroundAuraQuad)
        {
            ApplySoulRendererColor(
                _groundRenderer,
                _groundPropertyBlock,
                ScaleColor(soulGroundColor, Mathf.Lerp(0.84f, 1.04f, pulse), Mathf.Lerp(0.82f, 0.98f, pulse)));
        }

        if (_soulLight != null)
        {
            _soulLight.color = Color.Lerp(soulHaloColor, soulCoreColor, 0.62f);
            _soulLight.intensity = pointLightIntensity * Mathf.Lerp(0.88f, 1.2f, pulse);
            _soulLight.range = pointLightRange * Mathf.Lerp(0.96f, 1.08f, pulse);
        }
    }

    public void Configure(
        Material billboardMaterial,
        Vector3 localOffset,
        float bobAmount,
        float bobRate,
        float pulseRate,
        float coreScale,
        float haloScale,
        bool createGroundAura,
        float groundScale,
        Color coreColor,
        Color haloColor,
        Color groundColor,
        float lightIntensity,
        float lightRange)
    {
        soulBillboardMaterial = billboardMaterial;
        visualAnchorLocalOffset = localOffset;
        bobAmplitude = bobAmount;
        bobFrequency = bobRate;
        pulseFrequency = pulseRate;
        coreSize = coreScale;
        haloSize = haloScale;
        useGroundAuraQuad = createGroundAura;
        groundAuraSize = groundScale;
        soulCoreColor = coreColor;
        soulHaloColor = haloColor;
        soulGroundColor = groundColor;
        pointLightIntensity = lightIntensity;
        pointLightRange = lightRange;

        EnsureSoulVisuals();
    }

    private void EnsureSoulVisuals()
    {
        if (_soulVisualCreated)
        {
            ApplyCurrentSettings();
            return;
        }

        if (soulBillboardMaterial == null)
        {
            return;
        }

        GameObject rootObject = new GameObject("SoulVisualRoot");
        rootObject.layer = gameObject.layer;
        _soulVisualRoot = rootObject.transform;
        _soulVisualRoot.SetParent(transform, false);
        _soulVisualRoot.localPosition = visualAnchorLocalOffset;

        _coreRenderer = CreateSoulQuad(
            "SoulCore",
            new Vector3(0f, 0.02f, 0f),
            Quaternion.identity,
            coreSize,
            true,
            out _coreQuadTransform);

        _haloRenderer = CreateSoulQuad(
            "SoulHalo",
            new Vector3(0f, 0.09f, 0.02f),
            Quaternion.identity,
            haloSize,
            true,
            out _haloQuadTransform);

        if (useGroundAuraQuad)
        {
            _groundRenderer = CreateSoulQuad(
                "SoulGroundAura",
                new Vector3(0f, -0.78f, 0f),
                Quaternion.Euler(90f, 0f, 0f),
                groundAuraSize,
                false,
                out _groundAuraTransform);
        }

        GameObject lightObject = new GameObject("SoulLight");
        lightObject.layer = gameObject.layer;
        lightObject.transform.SetParent(_soulVisualRoot, false);
        lightObject.transform.localPosition = new Vector3(0f, 0.05f, 0f);
        _soulLight = lightObject.AddComponent<Light>();
        _soulLight.type = LightType.Point;
        _soulLight.shadows = LightShadows.None;

        _corePropertyBlock = new MaterialPropertyBlock();
        _haloPropertyBlock = new MaterialPropertyBlock();
        _groundPropertyBlock = new MaterialPropertyBlock();
        _soulVisualCreated = true;
        ApplyCurrentSettings();
    }

    private void ApplyCurrentSettings()
    {
        if (!_soulVisualCreated || _soulVisualRoot == null)
        {
            return;
        }

        _soulVisualRoot.localPosition = visualAnchorLocalOffset;

        if (_coreRenderer != null)
        {
            _coreRenderer.sharedMaterial = soulBillboardMaterial;
        }

        if (_haloRenderer != null)
        {
            _haloRenderer.sharedMaterial = soulBillboardMaterial;
        }

        if (_groundRenderer != null)
        {
            _groundRenderer.sharedMaterial = soulBillboardMaterial;
            _groundRenderer.gameObject.SetActive(useGroundAuraQuad);
        }

        if (_coreQuadTransform != null)
        {
            _coreQuadTransform.localScale = new Vector3(coreSize, coreSize, 1f);
        }

        if (_haloQuadTransform != null)
        {
            _haloQuadTransform.localScale = new Vector3(haloSize, haloSize, 1f);
        }

        if (_groundAuraTransform != null)
        {
            _groundAuraTransform.localScale = new Vector3(groundAuraSize, groundAuraSize, 1f);
        }

        if (_soulLight != null)
        {
            _soulLight.color = Color.Lerp(soulHaloColor, soulCoreColor, 0.62f);
            _soulLight.intensity = pointLightIntensity;
            _soulLight.range = pointLightRange;
        }
    }

    private MeshRenderer CreateSoulQuad(
        string objectName,
        Vector3 localPosition,
        Quaternion localRotation,
        float size,
        bool billboard,
        out Transform quadTransform)
    {
        GameObject quadObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quadObject.name = objectName;
        quadObject.layer = gameObject.layer;
        quadTransform = quadObject.transform;
        quadTransform.SetParent(_soulVisualRoot, false);
        quadTransform.localPosition = localPosition;
        quadTransform.localRotation = localRotation;
        quadTransform.localScale = new Vector3(size, size, 1f);

        Collider quadCollider = quadObject.GetComponent<Collider>();
        if (quadCollider != null)
        {
            Destroy(quadCollider);
        }

        if (billboard)
        {
            quadObject.AddComponent<Billboard>();
            quadObject.transform.localRotation = Quaternion.identity;
        }

        MeshRenderer renderer = quadObject.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = soulBillboardMaterial;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = LightProbeUsage.Off;
        renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        return renderer;
    }

    private void ApplySoulRendererColor(MeshRenderer renderer, MaterialPropertyBlock propertyBlock, Color color)
    {
        if (renderer == null || propertyBlock == null)
        {
            return;
        }

        propertyBlock.Clear();
        propertyBlock.SetColor("_BaseColor", color);
        propertyBlock.SetColor("_Color", color);
        propertyBlock.SetColor("_EmissionColor", ScaleColor(color, 1.35f, 1f));
        renderer.SetPropertyBlock(propertyBlock);
    }

    private static Color ScaleColor(Color source, float brightness, float alphaMultiplier)
    {
        return new Color(
            Mathf.Clamp01(source.r * brightness),
            Mathf.Clamp01(source.g * brightness),
            Mathf.Clamp01(source.b * brightness),
            Mathf.Clamp01(source.a * alphaMultiplier));
    }
}
