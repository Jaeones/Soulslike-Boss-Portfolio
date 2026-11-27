using UnityEngine;

public class FogColorChange : MonoBehaviour
{
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int TintColorId = Shader.PropertyToID("_TintColor");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    [Header("Color Range")]
    [SerializeField] private Color colorA = new(0.43529412f, 0.5019608f, 0.8980392f, 86f / 255f);
    [SerializeField] private Color colorB = new(0.49411765f, 0.77254903f, 0.9607843f, 86f / 255f);

    [Header("Animation")]
    [SerializeField] private float cycleDuration = 6f;
    [SerializeField] private AnimationCurve pulseCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private bool useUnscaledTime;
    [SerializeField] private float rendererTintStrength = 1.6f;
    [SerializeField] private float emissionStrength = 0.35f;

    [Header("Targets")]
    [SerializeField] private bool includeChildren = true;
    [SerializeField] private ParticleSystem[] targetParticleSystems;
    [SerializeField] private ParticleSystemRenderer[] targetRenderers;

    private float timer;
    private MaterialPropertyBlock propertyBlock;

    private void Awake()
    {
        CacheTargets();
        ApplyColor(0f);
    }

    private void OnEnable()
    {
        CacheTargets();
        ApplyColor(0f);
    }

    private void OnValidate()
    {
        cycleDuration = Mathf.Max(0.01f, cycleDuration);
        rendererTintStrength = Mathf.Max(0f, rendererTintStrength);
        emissionStrength = Mathf.Max(0f, emissionStrength);
        colorA.a = 86f / 255f;
        colorB.a = 86f / 255f;

        if (!Application.isPlaying)
        {
            CacheTargets();
            ApplyColor(0f);
        }
    }

    private void Update()
    {
        if (targetParticleSystems == null || targetParticleSystems.Length == 0)
        {
            return;
        }

        timer += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        float rawT = Mathf.PingPong(timer / cycleDuration, 1f);
        float t = pulseCurve.Evaluate(rawT);
        ApplyColor(t);
    }

    private void CacheTargets()
    {
        if (includeChildren)
        {
            targetParticleSystems = GetComponentsInChildren<ParticleSystem>(true);
        }
        else if (targetParticleSystems == null || targetParticleSystems.Length == 0)
        {
            ParticleSystem particleSystem = GetComponent<ParticleSystem>();
            targetParticleSystems = particleSystem == null ? System.Array.Empty<ParticleSystem>() : new[] { particleSystem };
        }

        if (includeChildren)
        {
            targetRenderers = GetComponentsInChildren<ParticleSystemRenderer>(true);
        }
        else if (targetRenderers == null || targetRenderers.Length == 0)
        {
            ParticleSystemRenderer particleRenderer = GetComponent<ParticleSystemRenderer>();
            targetRenderers = particleRenderer == null ? System.Array.Empty<ParticleSystemRenderer>() : new[] { particleRenderer };
        }

        propertyBlock ??= new MaterialPropertyBlock();
    }

    private void ApplyColor(float t)
    {
        Color currentColor = Color.Lerp(colorA, colorB, t);
        currentColor.a = 86f / 255f;

        for (int i = 0; i < targetParticleSystems.Length; i++)
        {
            ParticleSystem particleSystem = targetParticleSystems[i];
            if (particleSystem == null)
            {
                continue;
            }

            var main = particleSystem.main;
            main.startColor = currentColor;
        }

        if (targetRenderers == null)
        {
            return;
        }

        Color tintColor = currentColor * rendererTintStrength;
        tintColor.a = currentColor.a;
        Color emissionColor = currentColor * emissionStrength;
        emissionColor.a = 1f;

        for (int i = 0; i < targetRenderers.Length; i++)
        {
            ParticleSystemRenderer particleRenderer = targetRenderers[i];
            if (particleRenderer == null)
            {
                continue;
            }

            particleRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(ColorId, tintColor);
            propertyBlock.SetColor(BaseColorId, tintColor);
            propertyBlock.SetColor(TintColorId, tintColor);
            propertyBlock.SetColor(EmissionColorId, emissionColor);
            particleRenderer.SetPropertyBlock(propertyBlock);
        }
    }
}
