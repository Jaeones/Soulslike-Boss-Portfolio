using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
public sealed class BellWavePulse : MonoBehaviour
{
    [SerializeField] private Material waveMaterial;
    [SerializeField] private Vector3 localOffset = Vector3.zero;
    [SerializeField] private bool billboard = true;
    [SerializeField] private bool playOnEnable = true;
    [SerializeField] private bool destroyAfterPlay = true;
    [SerializeField] [Min(0f)] private float startDelay = 0f;
    [SerializeField] [Min(0.01f)] private float duration = 0.4f;
    [SerializeField] [Min(0.01f)] private float startScale = 0.85f;
    [SerializeField] [Min(0.01f)] private float endScale = 4.8f;
    [SerializeField] [Min(0f)] private float peakOpacity = 1f;
    [SerializeField] [Min(0.1f)] private float fadeExponent = 1.6f;

    private Transform _waveTransform;
    private MeshRenderer _waveRenderer;
    private MaterialPropertyBlock _propertyBlock;
    private float _elapsed;
    private bool _playing;

    private void Awake()
    {
        EnsureWaveQuad();
    }

    private void OnEnable()
    {
        if (playOnEnable)
        {
            Play();
        }
    }

    public void Play()
    {
        EnsureWaveQuad();
        _elapsed = 0f;
        _playing = true;
        enabled = true;
        SetWaveVisible(false);
    }

    private void Update()
    {
        if (!_playing)
        {
            return;
        }

        _elapsed += Time.deltaTime;
        if (_elapsed < startDelay)
        {
            SetWaveVisible(false);
            return;
        }

        float localTime = _elapsed - startDelay;
        if (localTime >= duration)
        {
            _playing = false;
            SetWaveVisible(false);

            if (destroyAfterPlay)
            {
                Destroy(gameObject);
            }
            else
            {
                enabled = false;
            }

            return;
        }

        if (_waveTransform == null || _waveRenderer == null || waveMaterial == null)
        {
            return;
        }

        float t = Mathf.Clamp01(localTime / duration);
        float eased = 1f - Mathf.Pow(1f - t, 2f);
        float scale = Mathf.Lerp(startScale, endScale, eased);
        float opacity = peakOpacity * Mathf.Pow(1f - t, fadeExponent);

        _waveTransform.localPosition = localOffset;
        _waveTransform.localScale = new Vector3(scale, scale, 1f);
        _propertyBlock.Clear();
        _propertyBlock.SetFloat("_Opacity", opacity);
        _waveRenderer.SetPropertyBlock(_propertyBlock);
        SetWaveVisible(true);
    }

    private void EnsureWaveQuad()
    {
        if (_waveTransform != null && _waveRenderer != null)
        {
            if (waveMaterial != null)
            {
                _waveRenderer.sharedMaterial = waveMaterial;
            }

            return;
        }

        Transform existing = transform.Find("WaveQuad");
        GameObject waveObject = existing != null ? existing.gameObject : GameObject.CreatePrimitive(PrimitiveType.Quad);
        waveObject.name = "WaveQuad";
        waveObject.layer = gameObject.layer;

        _waveTransform = waveObject.transform;
        _waveTransform.SetParent(transform, false);
        _waveTransform.localPosition = localOffset;
        _waveTransform.localRotation = Quaternion.identity;
        _waveTransform.localScale = new Vector3(startScale, startScale, 1f);

        Collider quadCollider = waveObject.GetComponent<Collider>();
        if (quadCollider != null)
        {
            Destroy(quadCollider);
        }

        Billboard billboardComponent = waveObject.GetComponent<Billboard>();
        if (billboard && billboardComponent == null)
        {
            billboardComponent = waveObject.AddComponent<Billboard>();
        }
        else if (!billboard && billboardComponent != null)
        {
            Destroy(billboardComponent);
        }

        _waveRenderer = waveObject.GetComponent<MeshRenderer>();
        _waveRenderer.sharedMaterial = waveMaterial;
        _waveRenderer.shadowCastingMode = ShadowCastingMode.Off;
        _waveRenderer.receiveShadows = false;
        _waveRenderer.lightProbeUsage = LightProbeUsage.Off;
        _waveRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        _propertyBlock = new MaterialPropertyBlock();
        SetWaveVisible(false);
    }

    private void SetWaveVisible(bool visible)
    {
        if (_waveRenderer != null)
        {
            _waveRenderer.enabled = visible;
        }
    }
}
