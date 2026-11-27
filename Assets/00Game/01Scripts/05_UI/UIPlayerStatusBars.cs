using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIPlayerStatusBars : MonoBehaviour
{
    private const string RuntimeDamageOverlayName = "RuntimePlayerDamageOverlay";

    [Header("Binding")]
    [SerializeField] private PlayerController player;
    [SerializeField] private GameObject root;
    [SerializeField] private bool hideWhenPlayerMissing;

    [Header("HP UI")]
    [SerializeField] private Slider hpSlider;
    [SerializeField] private Image hpFill;
    [SerializeField] private TMP_Text hpText;

    [Header("Stamina UI")]
    [SerializeField] private Slider staminaSlider;
    [SerializeField] private Image staminaFill;
    [SerializeField] private TMP_Text staminaText;

    [Header("Damage Overlay")]
    [SerializeField] private bool enableDamageOverlay = true;
    [SerializeField] private Color damageOverlayColor = new Color(0.45f, 0.03f, 0.03f, 0.22f);
    [SerializeField] [Min(0f)] private float damageOverlayMaxAlpha = 0.22f;
    [SerializeField] [Min(0.01f)] private float damageOverlayFadeInDuration = 0.03f;
    [SerializeField] [Min(0.01f)] private float damageOverlayFadeOutDuration = 0.2f;
    [SerializeField] private Color hpDamagePulseColor = new Color(1f, 0.72f, 0.72f, 1f);
    [SerializeField] [Min(0.01f)] private float hpDamagePulseDuration = 0.18f;

    private PlayerController _boundPlayer;
    private UIBindingMediator _bindingMediator;
    private Canvas _parentCanvas;
    private Image _damageOverlayImage;
    private Color _defaultHpFillColor = Color.white;
    private bool _hasDefaultHpFillColor;
    private bool _hasRecordedHp;
    private float _lastRecordedHp;
    private float _damageOverlayAlpha;
    private float _damageOverlayPeakAlpha;
    private float _damageOverlayElapsed;
    private bool _damageOverlayFadingOut;
    private bool _damageOverlayPlaying;
    private float _hpPulseElapsed;
    private bool _hpPulsePlaying;

    private void Awake()
    {
        if (root == null)
        {
            root = gameObject;
        }

        _bindingMediator = new UIBindingMediator(BindPlayer, UnbindPlayer);
        ValidateSceneReferences(true);
        CacheUiDefaults();
        EnsureDamageOverlay();
        ApplyRootVisibility();
        RefreshUI();
    }

    [ContextMenu("Validate Scene References")]
    private void ValidateSceneReferencesContext()
    {
        ValidateSceneReferences(false);
    }

    private void OnEnable()
    {
        GameLoopEvents.PlayerRespawned += HandlePlayerRespawned;
        TryBindPlayer();
        RefreshUI();
    }

    private void OnDisable()
    {
        GameLoopEvents.PlayerRespawned -= HandlePlayerRespawned;
        _bindingMediator.Clear();
        StopDamageOverlay();
        StopHpPulse();
    }

    public void BindPlayer(PlayerController targetPlayer)
    {
        if (_boundPlayer == targetPlayer)
        {
            RefreshUI();
            return;
        }

        UnbindPlayer();
        player = targetPlayer;
        _boundPlayer = targetPlayer;

        if (_boundPlayer == null)
        {
            _hasRecordedHp = false;
            ApplyRootVisibility();
            return;
        }

        _boundPlayer.OnPlayerHpChanged += HandlePlayerHpChanged;
        _boundPlayer.OnPlayerStaminaChanged += HandlePlayerStaminaChanged;
        _lastRecordedHp = _boundPlayer.CurrentHp;
        _hasRecordedHp = true;
        ApplyRootVisibility();
        RefreshUI();
    }

    public void UnbindPlayer()
    {
        if (_boundPlayer != null)
        {
            _boundPlayer.OnPlayerHpChanged -= HandlePlayerHpChanged;
            _boundPlayer.OnPlayerStaminaChanged -= HandlePlayerStaminaChanged;
            _boundPlayer = null;
        }

        _hasRecordedHp = false;
        ApplyRootVisibility();
    }

    private void TryBindPlayer()
    {
        _bindingMediator.Rebind(player);
    }

    private void RefreshUI()
    {
        if (_boundPlayer == null)
        {
            SetHpUI(0f, 1f);
            SetStaminaUI(0f, 1f);
            return;
        }

        SetHpUI(_boundPlayer.CurrentHp, _boundPlayer.MaxHp);
        SetStaminaUI(_boundPlayer.CurrentStamina, _boundPlayer.MaxStamina);
        _lastRecordedHp = _boundPlayer.CurrentHp;
        _hasRecordedHp = true;
    }

    private void HandlePlayerHpChanged(float current, float max)
    {
        if (_hasRecordedHp && current < _lastRecordedHp - 0.001f)
        {
            float safeMax = Mathf.Max(1f, max);
            float damageRatio = Mathf.Clamp01((_lastRecordedHp - current) / safeMax);
            TriggerDamageOverlay(damageRatio);
            TriggerHpPulse();
        }

        SetHpUI(current, max);
        _lastRecordedHp = current;
        _hasRecordedHp = true;
    }

    private void HandlePlayerStaminaChanged(float current, float max)
    {
        SetStaminaUI(current, max);
    }

    private void HandlePlayerRespawned(PlayerController respawnedPlayer, Transform _)
    {
        player = respawnedPlayer;
        _bindingMediator.Rebind(player);
    }

    private void Update()
    {
        TickDamageOverlay();
        TickHpPulse();
    }

    private void SetHpUI(float current, float max)
    {
        SetBarValue(hpSlider, hpFill, current, max);

        if (hpText != null)
        {
            hpText.text = $"{Mathf.CeilToInt(current)} / {Mathf.CeilToInt(max)}";
        }
    }

    private void SetStaminaUI(float current, float max)
    {
        SetBarValue(staminaSlider, staminaFill, current, max);

        if (staminaText != null)
        {
            staminaText.text = $"{Mathf.CeilToInt(current)} / {Mathf.CeilToInt(max)}";
        }
    }

    private void SetBarValue(Slider slider, Image fillImage, float current, float max)
    {
        float safeMax = Mathf.Max(1f, max);
        float clamped = Mathf.Clamp(current, 0f, safeMax);

        if (slider != null)
        {
            slider.minValue = 0f;
            slider.maxValue = safeMax;
            slider.value = clamped;
        }

        if (fillImage != null)
        {
            fillImage.fillAmount = clamped / safeMax;
        }
    }

    private void ApplyRootVisibility()
    {
        if (root == null)
        {
            return;
        }

        bool shouldShow = !hideWhenPlayerMissing || _boundPlayer != null;
        if (root.activeSelf != shouldShow)
        {
            root.SetActive(shouldShow);
        }
    }

    private void CacheUiDefaults()
    {
        if (hpFill != null)
        {
            _defaultHpFillColor = hpFill.color;
            _hasDefaultHpFillColor = true;
        }
    }

    private void EnsureDamageOverlay()
    {
        if (!enableDamageOverlay)
        {
            return;
        }

        if (_damageOverlayImage != null)
        {
            return;
        }

        if (root != null)
        {
            _parentCanvas = root.GetComponentInParent<Canvas>();
        }

        if (_parentCanvas == null)
        {
            _parentCanvas = GetComponentInParent<Canvas>();
        }

        if (_parentCanvas == null)
        {
            return;
        }

        Transform existing = _parentCanvas.transform.Find(RuntimeDamageOverlayName);
        GameObject overlayObject;
        if (existing != null)
        {
            overlayObject = existing.gameObject;
        }
        else
        {
            overlayObject = new GameObject(RuntimeDamageOverlayName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            overlayObject.transform.SetParent(_parentCanvas.transform, false);
        }

        RectTransform overlayRect = overlayObject.GetComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;
        overlayRect.SetAsLastSibling();

        _damageOverlayImage = overlayObject.GetComponent<Image>();
        _damageOverlayImage.raycastTarget = false;
        Color overlayColor = damageOverlayColor;
        overlayColor.a = 0f;
        _damageOverlayImage.color = overlayColor;
    }

    private void TriggerDamageOverlay(float damageRatio)
    {
        if (!enableDamageOverlay)
        {
            return;
        }

        EnsureDamageOverlay();
        if (_damageOverlayImage == null)
        {
            return;
        }

        float peakAlpha = Mathf.Lerp(damageOverlayMaxAlpha * 0.7f, damageOverlayMaxAlpha, Mathf.Clamp01(damageRatio * 3f));
        _damageOverlayPeakAlpha = Mathf.Max(_damageOverlayAlpha, peakAlpha);
        _damageOverlayElapsed = 0f;
        _damageOverlayFadingOut = false;
        _damageOverlayPlaying = true;
    }

    private void TickDamageOverlay()
    {
        if (!_damageOverlayPlaying || _damageOverlayImage == null)
        {
            return;
        }

        float deltaTime = Time.unscaledDeltaTime;
        if (!_damageOverlayFadingOut)
        {
            _damageOverlayElapsed += deltaTime;
            float fadeInDuration = Mathf.Max(0.01f, damageOverlayFadeInDuration);
            float t = Mathf.Clamp01(_damageOverlayElapsed / fadeInDuration);
            _damageOverlayAlpha = Mathf.Lerp(_damageOverlayAlpha, _damageOverlayPeakAlpha, t);
            if (t >= 1f)
            {
                _damageOverlayAlpha = _damageOverlayPeakAlpha;
                _damageOverlayElapsed = 0f;
                _damageOverlayFadingOut = true;
            }
        }
        else
        {
            _damageOverlayElapsed += deltaTime;
            float fadeOutDuration = Mathf.Max(0.01f, damageOverlayFadeOutDuration);
            float t = Mathf.Clamp01(_damageOverlayElapsed / fadeOutDuration);
            _damageOverlayAlpha = Mathf.Lerp(_damageOverlayPeakAlpha, 0f, t);
            if (t >= 1f)
            {
                StopDamageOverlay();
            }
        }

        Color overlayColor = damageOverlayColor;
        overlayColor.a = _damageOverlayAlpha;
        _damageOverlayImage.color = overlayColor;
    }

    private void StopDamageOverlay()
    {
        _damageOverlayPlaying = false;
        _damageOverlayFadingOut = false;
        _damageOverlayElapsed = 0f;
        _damageOverlayAlpha = 0f;
        _damageOverlayPeakAlpha = 0f;

        if (_damageOverlayImage != null)
        {
            Color overlayColor = damageOverlayColor;
            overlayColor.a = 0f;
            _damageOverlayImage.color = overlayColor;
        }
    }

    private void TriggerHpPulse()
    {
        if (hpFill == null || !_hasDefaultHpFillColor)
        {
            return;
        }

        _hpPulseElapsed = 0f;
        _hpPulsePlaying = true;
        hpFill.color = hpDamagePulseColor;
    }

    private void TickHpPulse()
    {
        if (!_hpPulsePlaying || hpFill == null || !_hasDefaultHpFillColor)
        {
            return;
        }

        _hpPulseElapsed += Time.unscaledDeltaTime;
        float duration = Mathf.Max(0.01f, hpDamagePulseDuration);
        float t = Mathf.Clamp01(_hpPulseElapsed / duration);
        hpFill.color = Color.Lerp(hpDamagePulseColor, _defaultHpFillColor, t);

        if (t >= 1f)
        {
            StopHpPulse();
        }
    }

    private void StopHpPulse()
    {
        _hpPulseElapsed = 0f;
        _hpPulsePlaying = false;

        if (hpFill != null && _hasDefaultHpFillColor)
        {
            hpFill.color = _defaultHpFillColor;
        }
    }

    private void ValidateSceneReferences(bool logOnlyOnce)
    {
        SceneReferenceValidator.Entry[] entries =
        {
            SceneReferenceValidator.Required(nameof(hpSlider), hpSlider, "HP 슬라이더"),
            SceneReferenceValidator.Required(nameof(staminaSlider), staminaSlider, "스태미나 슬라이더"),
            SceneReferenceValidator.RuntimeBound(nameof(player), player, "PlayerRespawned에서 재바인딩")
        };

        if (logOnlyOnce)
        {
            SceneReferenceValidator.ValidateOnce(this, nameof(UIPlayerStatusBars), entries);
            return;
        }

        SceneReferenceValidator.Validate(this, nameof(UIPlayerStatusBars), entries);
    }
}
