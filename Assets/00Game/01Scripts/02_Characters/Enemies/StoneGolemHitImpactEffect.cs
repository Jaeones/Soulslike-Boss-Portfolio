using UnityEngine;

[DisallowMultipleComponent]
public sealed class StoneGolemHitImpactEffect : MonoBehaviour
{
    [Header("Material")]
    [SerializeField] private Material shardMaterial;

    [Header("Burst Counts")]
    [SerializeField] private int minPrimaryShards = 10;
    [SerializeField] private int maxPrimaryShards = 14;
    [SerializeField] private int minSecondaryChips = 14;
    [SerializeField] private int maxSecondaryChips = 20;

    [Header("Motion")]
    [SerializeField] private float primaryForwardSpeed = 5.4f;
    [SerializeField] private float primaryLateralSpeed = 1.8f;
    [SerializeField] private float primaryUpwardLift = 1.1f;
    [SerializeField] private float secondaryForwardSpeed = 3.2f;
    [SerializeField] private float secondaryLateralSpeed = 2.9f;
    [SerializeField] private float secondaryUpwardLift = 0.6f;
    [SerializeField] private float gravityModifier = 1.35f;
    [SerializeField] private float autoDestroyDelay = 2.4f;

    [Header("Colors")]
    [SerializeField] private Color darkStoneColor = new Color(0.39f, 0.35f, 0.3f, 0.95f);
    [SerializeField] private Color lightStoneColor = new Color(0.72f, 0.67f, 0.6f, 0.9f);
    [SerializeField] private Color chipDustColor = new Color(0.66f, 0.6f, 0.52f, 0.48f);

    private ParticleSystem _primaryShards;
    private ParticleSystem _secondaryChips;
    private Material _runtimeFallbackMaterial;
    private bool _isBuilt;

    private void Awake()
    {
        BuildIfNeeded();
        EmitImpact();
        Destroy(gameObject, Mathf.Max(0.5f, autoDestroyDelay));
    }

    private void OnDestroy()
    {
        if (_runtimeFallbackMaterial != null)
        {
            Destroy(_runtimeFallbackMaterial);
        }
    }

    private void BuildIfNeeded()
    {
        if (_isBuilt)
        {
            return;
        }

        _primaryShards = CreateParticleSystem("PrimaryShards", 48, gravityModifier, 0.18f);
        _secondaryChips = CreateParticleSystem("SecondaryChips", 64, gravityModifier * 0.85f, 0.12f);
        _isBuilt = true;
    }

    private ParticleSystem CreateParticleSystem(string objectName, int maxParticles, float gravity, float maxParticleSize)
    {
        GameObject particleObject = new GameObject(objectName);
        particleObject.SetActive(false);
        particleObject.transform.SetParent(transform, false);
        particleObject.transform.localPosition = Vector3.zero;
        particleObject.transform.localRotation = Quaternion.identity;

        ParticleSystem particleSystem = particleObject.AddComponent<ParticleSystem>();
        particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = particleSystem.main;
        main.duration = 1.5f;
        main.loop = false;
        main.playOnAwake = false;
        main.maxParticles = maxParticles;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startSpeed = 0f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1.05f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.2f);
        main.gravityModifier = gravity;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;

        var emission = particleSystem.emission;
        emission.enabled = false;

        var shape = particleSystem.shape;
        shape.enabled = false;

        var velocityOverLifetime = particleSystem.velocityOverLifetime;
        velocityOverLifetime.enabled = false;
        velocityOverLifetime.space = ParticleSystemSimulationSpace.Local;
        velocityOverLifetime.x = new ParticleSystem.MinMaxCurve(0f);
        velocityOverLifetime.y = new ParticleSystem.MinMaxCurve(0f);
        velocityOverLifetime.z = new ParticleSystem.MinMaxCurve(0f);

        var colorOverLifetime = particleSystem.colorOverLifetime;
        colorOverLifetime.enabled = true;
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(CreateAlphaFadeGradient());

        var sizeOverLifetime = particleSystem.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, CreateSizeCurve());

        var noise = particleSystem.noise;
        noise.enabled = true;
        noise.strength = new ParticleSystem.MinMaxCurve(0.05f, 0.18f);
        noise.frequency = 0.45f;
        noise.scrollSpeed = 0.2f;
        noise.damping = true;
        noise.quality = ParticleSystemNoiseQuality.Medium;

        ParticleSystemRenderer renderer = particleObject.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = ResolveShardMaterial();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sortMode = ParticleSystemSortMode.Distance;
        renderer.alignment = ParticleSystemRenderSpace.View;
        renderer.allowRoll = true;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        renderer.maxParticleSize = maxParticleSize;
        renderer.minParticleSize = 0.001f;

        particleObject.SetActive(true);
        particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        return particleSystem;
    }

    private void EmitImpact()
    {
        if (_primaryShards == null || _secondaryChips == null)
        {
            return;
        }

        Vector3 impactNormal = transform.forward.sqrMagnitude > 0.0001f ? transform.forward.normalized : Vector3.up;
        Vector3 tangent = Vector3.Cross(impactNormal, Vector3.up);
        if (tangent.sqrMagnitude <= 0.0001f)
        {
            tangent = Vector3.Cross(impactNormal, Vector3.right);
        }

        tangent.Normalize();
        Vector3 bitangent = Vector3.Cross(tangent, impactNormal).normalized;
        Vector3 upwardAxis = Vector3.ProjectOnPlane(Vector3.up, impactNormal);
        if (upwardAxis.sqrMagnitude <= 0.0001f)
        {
            upwardAxis = bitangent;
        }

        upwardAxis.Normalize();

        EmitShardBurst(
            _primaryShards,
            Random.Range(minPrimaryShards, maxPrimaryShards + 1),
            transform.position + impactNormal * 0.04f,
            impactNormal,
            tangent,
            bitangent,
            upwardAxis,
            primaryForwardSpeed,
            primaryLateralSpeed,
            primaryUpwardLift,
            0.13f,
            0.24f,
            0.72f,
            1.05f,
            darkStoneColor,
            lightStoneColor);

        EmitShardBurst(
            _secondaryChips,
            Random.Range(minSecondaryChips, maxSecondaryChips + 1),
            transform.position + impactNormal * 0.02f,
            impactNormal,
            tangent,
            bitangent,
            upwardAxis,
            secondaryForwardSpeed,
            secondaryLateralSpeed,
            secondaryUpwardLift,
            0.05f,
            0.12f,
            0.45f,
            0.82f,
            lightStoneColor,
            chipDustColor);

        _primaryShards.Play();
        _secondaryChips.Play();
    }

    private void EmitShardBurst(
        ParticleSystem system,
        int count,
        Vector3 origin,
        Vector3 normal,
        Vector3 tangent,
        Vector3 bitangent,
        Vector3 upwardAxis,
        float forwardSpeed,
        float lateralSpeed,
        float upwardLift,
        float minSize,
        float maxSize,
        float minLifetime,
        float maxLifetime,
        Color nearColor,
        Color farColor)
    {
        for (int i = 0; i < count; i++)
        {
            Vector3 lateralOffset =
                tangent * Random.Range(-0.08f, 0.08f) +
                bitangent * Random.Range(-0.08f, 0.08f);

            Vector3 lateralVelocity =
                tangent * Random.Range(-lateralSpeed, lateralSpeed) +
                bitangent * Random.Range(-lateralSpeed * 0.55f, lateralSpeed * 0.55f);

            Vector3 velocity =
                normal * Random.Range(forwardSpeed * 0.72f, forwardSpeed * 1.18f) +
                lateralVelocity +
                upwardAxis * Random.Range(0f, upwardLift);

            ParticleSystem.EmitParams emitParams = new ParticleSystem.EmitParams
            {
                position = origin + lateralOffset,
                velocity = velocity,
                startLifetime = Random.Range(minLifetime, maxLifetime),
                startSize = Random.Range(minSize, maxSize),
                startColor = Color.Lerp(nearColor, farColor, Random.value),
                rotation = Random.Range(0f, Mathf.PI * 2f),
                angularVelocity = Random.Range(-6.5f, 6.5f)
            };

            system.Emit(emitParams, 1);
        }
    }

    private Material ResolveShardMaterial()
    {
        if (shardMaterial != null)
        {
            return shardMaterial;
        }

        if (_runtimeFallbackMaterial != null)
        {
            return _runtimeFallbackMaterial;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null)
        {
            shader = Shader.Find("Sprites/Default");
        }

        if (shader == null)
        {
            return null;
        }

        _runtimeFallbackMaterial = new Material(shader)
        {
            name = "StoneGolemHitShard_Fallback"
        };

        if (_runtimeFallbackMaterial.HasProperty("_Surface"))
        {
            _runtimeFallbackMaterial.SetFloat("_Surface", 1f);
        }

        if (_runtimeFallbackMaterial.HasProperty("_Blend"))
        {
            _runtimeFallbackMaterial.SetFloat("_Blend", 0f);
        }

        if (_runtimeFallbackMaterial.HasProperty("_Cull"))
        {
            _runtimeFallbackMaterial.SetFloat("_Cull", 0f);
        }

        if (_runtimeFallbackMaterial.HasProperty("_ZWrite"))
        {
            _runtimeFallbackMaterial.SetFloat("_ZWrite", 0f);
        }

        if (_runtimeFallbackMaterial.HasProperty("_BaseColor"))
        {
            _runtimeFallbackMaterial.SetColor("_BaseColor", Color.white);
        }

        return _runtimeFallbackMaterial;
    }

    private static Gradient CreateAlphaFadeGradient()
    {
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(Color.white, 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(1f, 0.08f),
                new GradientAlphaKey(0.9f, 0.55f),
                new GradientAlphaKey(0f, 1f)
            });
        return gradient;
    }

    private static AnimationCurve CreateSizeCurve()
    {
        return new AnimationCurve(
            new Keyframe(0f, 0.72f),
            new Keyframe(0.18f, 1f),
            new Keyframe(0.72f, 0.88f),
            new Keyframe(1f, 0.46f));
    }
}
