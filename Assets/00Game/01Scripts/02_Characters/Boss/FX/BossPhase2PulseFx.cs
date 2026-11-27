using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

public sealed class BossPhase2PulseFx : MonoBehaviour
{
    public enum PulsePreset
    {
        PreWakeCore = 0,
        GroundBurst = 1
    }

    [Header("Preset")]
    [SerializeField] private PulsePreset preset = PulsePreset.PreWakeCore;
    [SerializeField] private bool usePresetValues = true;

    [Header("Playback")]
    [SerializeField] private bool playOnEnable = true;
    [SerializeField] private bool destroyAfterPlay = true;
    [SerializeField] private bool billboard = false;
    [SerializeField] private Vector3 localOffset = Vector3.zero;
    [SerializeField] private Vector3 localEulerAngles = Vector3.zero;
    [SerializeField] private float startDelay = 0f;
    [SerializeField] private float duration = 0.42f;

    [Header("Shape")]
    [SerializeField] private Vector3 startScale = Vector3.one;
    [SerializeField] private Vector3 endScale = Vector3.one * 2f;
    [SerializeField] [Min(0f)] private float peakOpacity = 1f;
    [SerializeField] [Min(0.25f)] private float fadeExponent = 1.5f;

    [Header("Shader")]
    [SerializeField] [Range(0f, 1f)] private float shaderMode = 0f;
    [SerializeField] private Color baseColor = new(0.35f, 0.07f, 0.06f, 1f);
    [SerializeField] private Color accentColor = new(0.73f, 0.63f, 0.52f, 1f);
    [SerializeField] [Range(0f, 3f)] private float bodyIntensity = 0.22f;
    [SerializeField] [Range(0f, 3f)] private float ringIntensity = 1.15f;
    [SerializeField] [Range(0f, 1f)] private float maskStrength = 1f;
    [SerializeField] [Range(0f, 1f)] private float noiseTextureStrength = 0.45f;
    [SerializeField] [Range(0f, 1f)] private float detailStrength = 0.4f;
    [SerializeField] private float detailScale = 1.6f;
    [SerializeField] private float bandWidth = 0.18f;
    [SerializeField] private float bandFeather = 0.12f;
    [SerializeField] private float coreWidth = 0.08f;
    [SerializeField] private float coreFeather = 0.05f;
    [SerializeField] [Range(0f, 3f)] private float coreIntensity = 1.4f;
    [SerializeField] private float secondaryOffset = 0.32f;
    [SerializeField] private float secondaryWidth = 0.06f;
    [SerializeField] private float secondaryFeather = 0.05f;
    [SerializeField] [Range(0f, 3f)] private float secondaryIntensity = 0.8f;
    [SerializeField] [Range(0f, 2f)] private float headGlow = 0.35f;
    [SerializeField] private float tailFade = 0.18f;
    [SerializeField] private float ringRadius = 0.52f;
    [SerializeField] private float ringWidth = 0.1f;
    [SerializeField] private float noiseScale = 4f;
    [SerializeField] [Range(0f, 1f)] private float noiseStrength = 0.35f;
    [SerializeField] [Range(-2f, 2f)] private float flowSpeedX = 0.05f;
    [SerializeField] [Range(-2f, 2f)] private float flowSpeedY = 0.16f;
    [SerializeField] private float edgeFade = 0.12f;

    private const string ShaderName = "Custom/VFX/BossAttackPulse";
    private const string MaskTexturePath = "FX/BossDeath/BossDeath_PulseMask";
    private const string NoiseTexturePath = "FX/BossDeath/BossDeath_NoiseDetail";

    private static Texture2D s_maskTexture;
    private static Texture2D s_noiseTexture;
    private static Texture2D s_detailTexture;

    private Transform _visualTransform;
    private MeshRenderer _renderer;
    private Material _materialInstance;
    private Coroutine _playRoutine;

    private void Awake()
    {
        if (usePresetValues)
        {
            ApplyPreset();
        }

        EnsureVisual();
    }

    private void OnEnable()
    {
        if (Application.isPlaying && playOnEnable)
        {
            Play();
        }
    }

    private void LateUpdate()
    {
        if (!billboard || _visualTransform == null)
        {
            return;
        }

        Camera targetCamera = Camera.main;
        if (targetCamera == null)
        {
            return;
        }

        Vector3 toCamera = _visualTransform.position - targetCamera.transform.position;
        if (toCamera.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        Quaternion facingRotation = Quaternion.LookRotation(toCamera.normalized, Vector3.up);
        _visualTransform.rotation = facingRotation * Quaternion.Euler(localEulerAngles);
    }

    private void OnDisable()
    {
        if (_playRoutine != null)
        {
            StopCoroutine(_playRoutine);
            _playRoutine = null;
        }

        if (_renderer != null)
        {
            _renderer.enabled = false;
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

    private IEnumerator CoPlay()
    {
        EnsureVisual();

        if (_renderer == null || _materialInstance == null)
        {
            yield break;
        }

        _renderer.enabled = false;
        SetOpacity(0f);

        if (startDelay > 0f)
        {
            yield return new WaitForSeconds(startDelay);
        }

        _renderer.enabled = true;

        if (duration <= 0.001f)
        {
            ApplyProgress(1f);
        }
        else
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                float normalized = Mathf.Clamp01(elapsed / duration);
                ApplyProgress(normalized);
                elapsed += Time.deltaTime;
                yield return null;
            }

            ApplyProgress(1f);
        }

        SetOpacity(0f);
        _renderer.enabled = false;
        _playRoutine = null;

        if (destroyAfterPlay)
        {
            Destroy(gameObject);
        }
    }

    private void EnsureVisual()
    {
        if (_renderer != null)
        {
            return;
        }

        Shader pulseShader = Shader.Find(ShaderName);
        if (pulseShader == null)
        {
            enabled = false;
            return;
        }

        GameObject visualObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
        visualObject.name = "PulseQuad";
        visualObject.layer = gameObject.layer;
        visualObject.transform.SetParent(transform, false);
        visualObject.transform.localPosition = localOffset;
        visualObject.transform.localRotation = Quaternion.Euler(localEulerAngles);
        visualObject.transform.localScale = Vector3.one;

        Collider quadCollider = visualObject.GetComponent<Collider>();
        if (quadCollider != null)
        {
            Destroy(quadCollider);
        }

        _visualTransform = visualObject.transform;
        _renderer = visualObject.GetComponent<MeshRenderer>();
        _renderer.shadowCastingMode = ShadowCastingMode.Off;
        _renderer.receiveShadows = false;
        _renderer.allowOcclusionWhenDynamic = false;
        _renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;

        _materialInstance = new Material(pulseShader)
        {
            name = $"Runtime_{name}_Pulse"
        };

        LoadTextures();
        _materialInstance.SetTexture("_MaskTex", s_maskTexture);
        _materialInstance.SetTexture("_NoiseTex", s_noiseTexture);
        _materialInstance.SetTexture("_DetailTex", s_detailTexture);
        ApplyShaderState();

        _renderer.sharedMaterial = _materialInstance;
        _renderer.enabled = false;
    }

    private void ApplyProgress(float normalized)
    {
        if (_visualTransform == null)
        {
            return;
        }

        float scaleT = Mathf.SmoothStep(0f, 1f, normalized);
        _visualTransform.localScale = Vector3.LerpUnclamped(startScale, endScale, scaleT);

        float shaped = Mathf.Pow(Mathf.Sin(normalized * Mathf.PI), fadeExponent);
        SetOpacity(peakOpacity * shaped);
    }

    private void SetOpacity(float opacity)
    {
        if (_materialInstance == null)
        {
            return;
        }

        _materialInstance.SetFloat("_Opacity", Mathf.Max(0f, opacity));
    }

    private void ApplyShaderState()
    {
        if (_materialInstance == null)
        {
            return;
        }

        _materialInstance.SetFloat("_Mode", shaderMode);
        _materialInstance.SetColor("_BaseColor", baseColor);
        _materialInstance.SetColor("_AccentColor", accentColor);
        _materialInstance.SetFloat("_BodyIntensity", bodyIntensity);
        _materialInstance.SetFloat("_RingIntensity", ringIntensity);
        _materialInstance.SetFloat("_MaskStrength", maskStrength);
        _materialInstance.SetFloat("_NoiseTexStrength", noiseTextureStrength);
        _materialInstance.SetFloat("_DetailStrength", detailStrength);
        _materialInstance.SetFloat("_DetailScale", detailScale);
        _materialInstance.SetFloat("_BandWidth", bandWidth);
        _materialInstance.SetFloat("_BandFeather", bandFeather);
        _materialInstance.SetFloat("_CoreWidth", coreWidth);
        _materialInstance.SetFloat("_CoreFeather", coreFeather);
        _materialInstance.SetFloat("_CoreIntensity", coreIntensity);
        _materialInstance.SetFloat("_SecondaryOffset", secondaryOffset);
        _materialInstance.SetFloat("_SecondaryWidth", secondaryWidth);
        _materialInstance.SetFloat("_SecondaryFeather", secondaryFeather);
        _materialInstance.SetFloat("_SecondaryIntensity", secondaryIntensity);
        _materialInstance.SetFloat("_HeadGlow", headGlow);
        _materialInstance.SetFloat("_TailFade", tailFade);
        _materialInstance.SetFloat("_RingRadius", ringRadius);
        _materialInstance.SetFloat("_RingWidth", ringWidth);
        _materialInstance.SetFloat("_NoiseScale", noiseScale);
        _materialInstance.SetFloat("_NoiseStrength", noiseStrength);
        _materialInstance.SetFloat("_FlowSpeedX", flowSpeedX);
        _materialInstance.SetFloat("_FlowSpeedY", flowSpeedY);
        _materialInstance.SetFloat("_EdgeFade", edgeFade);
    }

    private void ApplyPreset()
    {
        switch (preset)
        {
            case PulsePreset.PreWakeCore:
                billboard = true;
                localOffset = new Vector3(0f, 0.14f, 0.03f);
                localEulerAngles = Vector3.zero;
                startDelay = 0f;
                duration = 0.32f;
                startScale = new Vector3(0.22f, 0.5f, 1f);
                endScale = new Vector3(0.42f, 0.9f, 1f);
                peakOpacity = 0.33f;
                fadeExponent = 2.1f;
                shaderMode = 0f;
                baseColor = new Color(0.18f, 0.03f, 0.03f, 1f);
                accentColor = new Color(0.62f, 0.5f, 0.38f, 1f);
                bodyIntensity = 0.18f;
                ringIntensity = 0.18f;
                maskStrength = 0.85f;
                noiseTextureStrength = 0.35f;
                detailStrength = 0.28f;
                detailScale = 1.2f;
                bandWidth = 0.16f;
                bandFeather = 0.16f;
                coreWidth = 0.06f;
                coreFeather = 0.06f;
                coreIntensity = 0.85f;
                secondaryOffset = 0.34f;
                secondaryWidth = 0.05f;
                secondaryFeather = 0.06f;
                secondaryIntensity = 0.45f;
                headGlow = 0.18f;
                tailFade = 0.28f;
                ringRadius = 0.5f;
                ringWidth = 0.08f;
                noiseScale = 3.2f;
                noiseStrength = 0.22f;
                flowSpeedX = 0.04f;
                flowSpeedY = 0.1f;
                edgeFade = 0.18f;
                break;

            case PulsePreset.GroundBurst:
                billboard = false;
                localOffset = new Vector3(0f, 0.04f, 0f);
                localEulerAngles = new Vector3(90f, 0f, 0f);
                startDelay = 0f;
                duration = 0.52f;
                startScale = new Vector3(0.7f, 0.7f, 1f);
                endScale = new Vector3(4.6f, 4.6f, 1f);
                peakOpacity = 1.15f;
                fadeExponent = 1.35f;
                shaderMode = 1f;
                baseColor = new Color(0.42f, 0.06f, 0.05f, 1f);
                accentColor = new Color(0.78f, 0.68f, 0.5f, 1f);
                bodyIntensity = 0.12f;
                ringIntensity = 1.45f;
                maskStrength = 1f;
                noiseTextureStrength = 0.52f;
                detailStrength = 0.55f;
                detailScale = 1.9f;
                bandWidth = 0.12f;
                bandFeather = 0.1f;
                coreWidth = 0.04f;
                coreFeather = 0.04f;
                coreIntensity = 0.7f;
                secondaryOffset = 0.32f;
                secondaryWidth = 0.08f;
                secondaryFeather = 0.08f;
                secondaryIntensity = 0.52f;
                headGlow = 0.22f;
                tailFade = 0.1f;
                ringRadius = 0.56f;
                ringWidth = 0.09f;
                noiseScale = 4.8f;
                noiseStrength = 0.4f;
                flowSpeedX = 0.12f;
                flowSpeedY = 0.28f;
                edgeFade = 0.08f;
                break;
        }
    }

    private static void LoadTextures()
    {
        if (s_maskTexture == null)
        {
            s_maskTexture = Resources.Load<Texture2D>(MaskTexturePath);
        }

        if (s_noiseTexture == null)
        {
            s_noiseTexture = Resources.Load<Texture2D>(NoiseTexturePath);
        }

        if (s_detailTexture == null)
        {
            s_detailTexture = Resources.Load<Texture2D>(NoiseTexturePath);
        }
    }
}
