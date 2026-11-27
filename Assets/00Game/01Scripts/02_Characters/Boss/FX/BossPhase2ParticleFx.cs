using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

public sealed class BossPhase2ParticleFx : MonoBehaviour
{
    public enum ParticlePreset
    {
        BodyBurst = 0,
        WeaponIgnite = 1,
        ResidualAura = 2,
        AmbientMotes = 3
    }

    private enum TextureKind
    {
        SoulBurst = 0,
        Ash = 1
    }

    private enum ShapeKind
    {
        Cone = 0,
        Sphere = 1,
        Circle = 2
    }

    [Header("Preset")]
    [SerializeField] private ParticlePreset preset = ParticlePreset.BodyBurst;
    [SerializeField] private bool usePresetValues = true;

    [Header("Playback")]
    [SerializeField] private bool playOnEnable = true;
    [SerializeField] private bool destroyAfterPlay = true;
    [SerializeField] private bool loop = false;
    [SerializeField] private float startDelay = 0f;
    [SerializeField] private float autoStopAfter = 0f;
    [SerializeField] private Vector3 localOffset = Vector3.zero;
    [SerializeField] private Vector3 localEulerAngles = Vector3.zero;

    [Header("Particle")]
    [SerializeField] private TextureKind textureKind = TextureKind.SoulBurst;
    [SerializeField] private ShapeKind shapeKind = ShapeKind.Cone;
    [SerializeField] private float systemDuration = 0.5f;
    [SerializeField] private float shapeRadius = 0.08f;
    [SerializeField] private float coneAngle = 18f;
    [SerializeField] private int burstCountMin = 8;
    [SerializeField] private int burstCountMax = 12;
    [SerializeField] private float emissionRate = 12f;
    [SerializeField] private Vector2 lifetimeRange = new(0.24f, 0.38f);
    [SerializeField] private Vector2 speedRange = new(1.6f, 3.2f);
    [SerializeField] private Vector2 sizeRange = new(0.12f, 0.24f);
    [SerializeField] private float gravityModifier = 0.2f;
    [SerializeField] private float noiseStrength = 0.18f;
    [SerializeField] private float noiseFrequency = 0.45f;
    [SerializeField] private Color primaryColor = new(0.45f, 0.08f, 0.06f, 0.9f);
    [SerializeField] private Color secondaryColor = new(0.82f, 0.72f, 0.56f, 0.75f);

    private const string UrpParticleShaderName = "Universal Render Pipeline/Particles/Unlit";
    private const string FallbackParticleShaderName = "Particles/Standard Unlit";
    private const string SoulBurstTexturePath = "FX/BossDeath/BossDeath_SoulBurst";
    private const string AshTexturePath = "FX/BossDeath/BossDeath_Ash";

    private static Texture2D s_soulBurstTexture;
    private static Texture2D s_ashTexture;

    private GameObject _particleRoot;
    private ParticleSystem _particleSystem;
    private Material _materialInstance;
    private Coroutine _playRoutine;

    private void Awake()
    {
        if (usePresetValues)
        {
            ApplyPreset();
        }

        EnsureParticleSystem();
    }

    private void OnEnable()
    {
        if (Application.isPlaying && playOnEnable)
        {
            Play();
        }
    }

    private void OnDisable()
    {
        if (_playRoutine != null)
        {
            StopCoroutine(_playRoutine);
            _playRoutine = null;
        }

        if (_particleSystem != null)
        {
            _particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    private void OnDestroy()
    {
        if (_materialInstance != null)
        {
            Destroy(_materialInstance);
        }
    }

    public void Play()
    {
        if (!isActiveAndEnabled)
        {
            return;
        }

        if (_playRoutine != null)
        {
            StopCoroutine(_playRoutine);
        }

        _playRoutine = StartCoroutine(CoPlay());
    }

    public void StopAndDestroy(float lingerSeconds = 0.18f)
    {
        if (_particleSystem != null)
        {
            _particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        Destroy(gameObject, Mathf.Max(0.05f, lingerSeconds + lifetimeRange.y));
    }

    private IEnumerator CoPlay()
    {
        EnsureParticleSystem();

        if (_particleSystem == null)
        {
            yield break;
        }

        _particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        if (startDelay > 0f)
        {
            yield return new WaitForSeconds(startDelay);
        }

        _particleSystem.gameObject.SetActive(true);
        _particleSystem.Play(true);

        if (!loop)
        {
            int burstCount = Random.Range(burstCountMin, burstCountMax + 1);
            if (burstCount > 0)
            {
                _particleSystem.Emit(burstCount);
            }
        }

        if (loop && autoStopAfter > 0f)
        {
            yield return new WaitForSeconds(autoStopAfter);
            _particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        if (destroyAfterPlay)
        {
            float cleanupDelay = Mathf.Max(systemDuration, autoStopAfter) + lifetimeRange.y + 0.3f;
            Destroy(gameObject, cleanupDelay);
        }

        _playRoutine = null;
    }

    private void EnsureParticleSystem()
    {
        if (_particleSystem != null)
        {
            return;
        }

        Shader particleShader = Shader.Find(UrpParticleShaderName);
        if (particleShader == null)
        {
            particleShader = Shader.Find(FallbackParticleShaderName);
        }

        if (particleShader == null)
        {
            enabled = false;
            return;
        }

        _particleRoot = new GameObject("Particles");
        _particleRoot.layer = gameObject.layer;
        _particleRoot.transform.SetParent(transform, false);
        _particleRoot.transform.localPosition = localOffset;
        _particleRoot.transform.localRotation = Quaternion.Euler(localEulerAngles);
        _particleRoot.transform.localScale = Vector3.one;
        _particleRoot.SetActive(false);

        _particleSystem = _particleRoot.AddComponent<ParticleSystem>();
        _particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystemRenderer particleRenderer = _particleRoot.GetComponent<ParticleSystemRenderer>();
        particleRenderer.shadowCastingMode = ShadowCastingMode.Off;
        particleRenderer.receiveShadows = false;
        particleRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
        particleRenderer.sortMode = ParticleSystemSortMode.Distance;

        _materialInstance = new Material(particleShader)
        {
            name = $"Runtime_{name}_Particles"
        };

        LoadTextures();
        Texture mainTexture = textureKind == TextureKind.Ash ? s_ashTexture : s_soulBurstTexture;
        ApplyParticleMaterial(_materialInstance, mainTexture);
        particleRenderer.sharedMaterial = _materialInstance;

        ConfigureParticleSystem(_particleSystem);
        _particleRoot.SetActive(true);
        _particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private void ConfigureParticleSystem(ParticleSystem particleSystem)
    {
        ParticleSystem.MainModule main = particleSystem.main;
        main.duration = Mathf.Max(0.1f, systemDuration);
        main.loop = loop;
        main.prewarm = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.startLifetime = new ParticleSystem.MinMaxCurve(lifetimeRange.x, lifetimeRange.y);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speedRange.x, speedRange.y);
        main.startSize = new ParticleSystem.MinMaxCurve(sizeRange.x, sizeRange.y);
        main.startRotation3D = false;
        main.startColor = Color.white;
        main.gravityModifier = gravityModifier;
        main.maxParticles = loop ? 256 : Mathf.Max(64, burstCountMax * 4);
        main.stopAction = ParticleSystemStopAction.None;

        ParticleSystem.EmissionModule emission = particleSystem.emission;
        emission.enabled = true;
        emission.rateOverTime = loop ? emissionRate : 0f;
        emission.SetBursts(System.Array.Empty<ParticleSystem.Burst>());

        ParticleSystem.ShapeModule shape = particleSystem.shape;
        shape.enabled = true;
        shape.radius = shapeRadius;
        shape.randomDirectionAmount = 0f;

        switch (shapeKind)
        {
            case ShapeKind.Cone:
                shape.shapeType = ParticleSystemShapeType.Cone;
                shape.angle = coneAngle;
                break;

            case ShapeKind.Sphere:
                shape.shapeType = ParticleSystemShapeType.Sphere;
                break;

            case ShapeKind.Circle:
                shape.shapeType = ParticleSystemShapeType.Circle;
                break;
        }

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particleSystem.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient colorGradient = new Gradient();
        colorGradient.SetKeys(
            new[]
            {
                new GradientColorKey(primaryColor, 0f),
                new GradientColorKey(secondaryColor, 0.55f),
                new GradientColorKey(primaryColor, 1f)
            },
            new[]
            {
                new GradientAlphaKey(primaryColor.a, 0f),
                new GradientAlphaKey(Mathf.Max(primaryColor.a, secondaryColor.a), 0.2f),
                new GradientAlphaKey(secondaryColor.a * 0.55f, 0.72f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(colorGradient);

        ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = particleSystem.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        AnimationCurve sizeCurve = new AnimationCurve(
            new Keyframe(0f, 0.2f),
            new Keyframe(0.22f, 1f),
            new Keyframe(1f, 0f));
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        ParticleSystem.NoiseModule noise = particleSystem.noise;
        noise.enabled = noiseStrength > 0.001f;
        noise.strength = noiseStrength;
        noise.frequency = noiseFrequency;
        noise.scrollSpeed = 0.22f;
        noise.octaveCount = 1;
        noise.damping = true;

        ParticleSystem.VelocityOverLifetimeModule velocityOverLifetime = particleSystem.velocityOverLifetime;
        ParticleSystem.MinMaxCurve zeroVelocity = new ParticleSystem.MinMaxCurve(0f);
        velocityOverLifetime.enabled = false;
        velocityOverLifetime.space = ParticleSystemSimulationSpace.Local;
        velocityOverLifetime.x = zeroVelocity;
        velocityOverLifetime.y = zeroVelocity;
        velocityOverLifetime.z = zeroVelocity;
    }

    private void ApplyPreset()
    {
        switch (preset)
        {
            case ParticlePreset.BodyBurst:
                loop = false;
                destroyAfterPlay = true;
                startDelay = 0f;
                autoStopAfter = 0f;
                localOffset = new Vector3(0f, 0.1f, 0f);
                localEulerAngles = Vector3.zero;
                textureKind = TextureKind.SoulBurst;
                shapeKind = ShapeKind.Cone;
                systemDuration = 0.45f;
                shapeRadius = 0.36f;
                coneAngle = 20f;
                burstCountMin = 10;
                burstCountMax = 15;
                emissionRate = 0f;
                lifetimeRange = new Vector2(0.26f, 0.38f);
                speedRange = new Vector2(4.8f, 8.4f);
                sizeRange = new Vector2(0.54f, 1.02f);
                gravityModifier = 0.25f;
                noiseStrength = 0.12f;
                noiseFrequency = 0.45f;
                primaryColor = new Color(0.42f, 0.06f, 0.05f, 0.88f);
                secondaryColor = new Color(0.75f, 0.62f, 0.47f, 0.68f);
                break;

            case ParticlePreset.WeaponIgnite:
                loop = true;
                destroyAfterPlay = false;
                startDelay = 0f;
                autoStopAfter = 0f;
                localOffset = new Vector3(0f, 0.05f, 0f);
                localEulerAngles = Vector3.zero;
                textureKind = TextureKind.SoulBurst;
                shapeKind = ShapeKind.Sphere;
                systemDuration = 1f;
                shapeRadius = 0.24f;
                coneAngle = 12f;
                burstCountMin = 0;
                burstCountMax = 0;
                emissionRate = 18f;
                lifetimeRange = new Vector2(0.18f, 0.3f);
                speedRange = new Vector2(0.95f, 1.8f);
                sizeRange = new Vector2(0.24f, 0.48f);
                gravityModifier = -0.06f;
                noiseStrength = 0.15f;
                noiseFrequency = 0.5f;
                primaryColor = new Color(0.45f, 0.07f, 0.05f, 0.75f);
                secondaryColor = new Color(0.78f, 0.68f, 0.52f, 0.48f);
                break;

            case ParticlePreset.ResidualAura:
                loop = true;
                destroyAfterPlay = true;
                startDelay = 0f;
                autoStopAfter = 0.95f;
                localOffset = new Vector3(0f, 0.08f, 0f);
                localEulerAngles = Vector3.zero;
                textureKind = TextureKind.SoulBurst;
                shapeKind = ShapeKind.Circle;
                systemDuration = 1.1f;
                shapeRadius = 1.02f;
                coneAngle = 10f;
                burstCountMin = 0;
                burstCountMax = 0;
                emissionRate = 14f;
                lifetimeRange = new Vector2(0.4f, 0.75f);
                speedRange = new Vector2(0.45f, 1.1f);
                sizeRange = new Vector2(0.54f, 0.84f);
                gravityModifier = -0.05f;
                noiseStrength = 0.25f;
                noiseFrequency = 0.38f;
                primaryColor = new Color(0.32f, 0.05f, 0.04f, 0.42f);
                secondaryColor = new Color(0.72f, 0.61f, 0.47f, 0.24f);
                break;

            case ParticlePreset.AmbientMotes:
                loop = true;
                destroyAfterPlay = true;
                startDelay = 0f;
                autoStopAfter = 0.85f;
                localOffset = new Vector3(0f, 0.12f, 0f);
                localEulerAngles = Vector3.zero;
                textureKind = TextureKind.Ash;
                shapeKind = ShapeKind.Sphere;
                systemDuration = 1f;
                shapeRadius = 1.35f;
                coneAngle = 14f;
                burstCountMin = 0;
                burstCountMax = 0;
                emissionRate = 10f;
                lifetimeRange = new Vector2(0.5f, 0.9f);
                speedRange = new Vector2(0.35f, 0.9f);
                sizeRange = new Vector2(0.18f, 0.36f);
                gravityModifier = -0.015f;
                noiseStrength = 0.12f;
                noiseFrequency = 0.32f;
                primaryColor = new Color(0.36f, 0.2f, 0.16f, 0.22f);
                secondaryColor = new Color(0.62f, 0.55f, 0.44f, 0.1f);
                break;
        }
    }

    private static void ApplyParticleMaterial(Material material, Texture texture)
    {
        if (material == null)
        {
            return;
        }

        if (material.HasProperty("_BaseMap"))
        {
            material.SetTexture("_BaseMap", texture);
        }

        if (material.HasProperty("_MainTex"))
        {
            material.SetTexture("_MainTex", texture);
        }

        if (material.HasProperty("_EmissionMap"))
        {
            material.SetTexture("_EmissionMap", texture);
        }

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", Color.white);
        }

        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", Color.white);
        }

        if (material.HasProperty("_EmissionColor"))
        {
            material.SetColor("_EmissionColor", Color.white * 0.5f);
        }

        if (material.HasProperty("_Surface"))
        {
            material.SetFloat("_Surface", 1f);
        }

        if (material.HasProperty("_Blend"))
        {
            material.SetFloat("_Blend", 0f);
        }

        if (material.HasProperty("_SrcBlend"))
        {
            material.SetFloat("_SrcBlend", 5f);
        }

        if (material.HasProperty("_DstBlend"))
        {
            material.SetFloat("_DstBlend", 10f);
        }

        if (material.HasProperty("_ZWrite"))
        {
            material.SetFloat("_ZWrite", 0f);
        }

        if (material.HasProperty("_Cull"))
        {
            material.SetFloat("_Cull", 0f);
        }

        material.renderQueue = 3000;
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.EnableKeyword("_EMISSION");
    }

    private static void LoadTextures()
    {
        if (s_soulBurstTexture == null)
        {
            s_soulBurstTexture = Resources.Load<Texture2D>(SoulBurstTexturePath);
        }

        if (s_ashTexture == null)
        {
            s_ashTexture = Resources.Load<Texture2D>(AshTexturePath);
        }
    }
}
