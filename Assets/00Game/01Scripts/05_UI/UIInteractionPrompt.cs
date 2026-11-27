using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;
#if UNITY_EDITOR
using UnityEditor;
#endif

public sealed class UIInteractionPrompt : MonoBehaviour
{
    private const string CanvasRootName = "GameLoopUICanvas";
    private const string PanelName = "PromptPanel";
    private const string ContentName = "PromptContent";
    private const string KeyBadgeName = "KeyBadge";
    private const string KeyTextName = "KeyText";
    private const string ActionTextName = "ActionText";
    private const int SortingOrder = 140;
    private const float ReferenceWidth = 1920f;
    private const float ReferenceHeight = 1080f;
    private const string KoreanFontAssetName = "NotoSansKR-VariableFont_wght SDF";
    private const string KoreanFontAssetPath = "Assets/00Game/03Art/Font/NotoSansKR-VariableFont_wght SDF.asset";
    private const float MinPanelWidth = 420f;
    private const float MaxPanelWidth = 860f;
    private const float PanelHorizontalPadding = 44f;
    private const float HiddenKeyBadgeWidth = 0f;
    private const float VisibleKeyBadgeMinWidth = 18f;
    private const float KeyBadgeHorizontalPadding = 10f;

    private static UIInteractionPrompt _instance;
    private static TMP_FontAsset _cachedPromptFont;

    private CanvasGroup _canvasGroup;
    private RectTransform _panelRect;
    private Image _panelImage;
    private RectTransform _contentRect;
    private HorizontalLayoutGroup _contentLayout;
    private TMP_Text _keyText;
    private TMP_Text _actionText;
    private RectTransform _actionRect;
    private GameObject _keyBadgeRoot;
    private RectTransform _keyBadgeRect;
    private Image _keyBadgeImage;
    private LayoutElement _keyBadgeLayoutElement;
    private LayoutElement _actionLayoutElement;
    private Object _currentOwner;
    private Coroutine _noticeCoroutine;
    private bool _usesScenePanel;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        _instance = null;
    }

    public static void Show(Object owner, KeyCode interactionKey, string actionText)
    {
        if (owner == null)
        {
            return;
        }

        EnsureInstance().ShowInternal(owner, interactionKey, actionText);
    }

    public static void Hide(Object owner)
    {
        if (_instance == null || owner == null)
        {
            return;
        }

        _instance.HideInternal(owner);
    }

    private static UIInteractionPrompt EnsureInstance()
    {
        if (_instance != null)
        {
            return _instance;
        }

        _instance = FindFirstObjectByType<UIInteractionPrompt>(FindObjectsInactive.Include);
        if (_instance != null)
        {
            _instance.EnsureUi();
            return _instance;
        }

        Transform promptPanel = FindScenePromptPanel();
        if (promptPanel != null)
        {
            _instance = promptPanel.GetComponent<UIInteractionPrompt>();
            if (_instance == null)
            {
                _instance = promptPanel.gameObject.AddComponent<UIInteractionPrompt>();
            }

            _instance._usesScenePanel = true;
            _instance.EnsureUi();
            return _instance;
        }

        Debug.LogError("[UIInteractionPrompt] Could not find GameLoopUICanvas/PromptPanel in scene.");
        return null;
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        EnsureUi();
    }

    private void OnDestroy()
    {
        if (_instance == this)
        {
            _instance = null;
        }
    }

    private void ShowInternal(Object owner, KeyCode interactionKey, string actionText)
    {
        EnsureUi();

        if (_keyBadgeRoot == null || _actionText == null || _canvasGroup == null)
        {
            return;
        }

        if (_noticeCoroutine != null)
        {
            StopCoroutine(_noticeCoroutine);
            _noticeCoroutine = null;
        }

        _currentOwner = owner;
        _keyBadgeRoot.SetActive(true);
        _keyText.text = interactionKey.ToString().ToUpperInvariant();
        _actionText.text = string.IsNullOrWhiteSpace(actionText) ? "Interact" : actionText;
        ApplyLayout(showKeyBadge: true);
        ApplyVisualStyle(showKeyBadge: true);
        RefreshDynamicLayout(showKeyBadge: true);

        gameObject.SetActive(true);
        _canvasGroup.alpha = 1f;
        GameUiSfxPlayer.Play(UiSfxId.PromptAppear, 0.9f);
    }

    public static void ShowNotice(string actionText, float duration = 1.6f)
    {
        EnsureInstance().ShowNoticeInternal(actionText, duration);
    }

    private void HideInternal(Object owner)
    {
        if (_currentOwner != owner)
        {
            return;
        }

        _currentOwner = null;
        HideImmediate();
    }

    private void ShowNoticeInternal(string actionText, float duration)
    {
        EnsureUi();

        if (_keyBadgeRoot == null || _actionText == null || _canvasGroup == null)
        {
            return;
        }

        if (_noticeCoroutine != null)
        {
            StopCoroutine(_noticeCoroutine);
        }

        _currentOwner = null;
        _keyBadgeRoot.SetActive(false);
        _actionText.text = string.IsNullOrWhiteSpace(actionText) ? "Interact" : actionText;
        ApplyLayout(showKeyBadge: false);
        ApplyVisualStyle(showKeyBadge: false);
        RefreshDynamicLayout(showKeyBadge: false);

        gameObject.SetActive(true);
        _canvasGroup.alpha = 1f;
        GameUiSfxPlayer.Play(UiSfxId.PromptAppear);
        _noticeCoroutine = StartCoroutine(HideNoticeAfterDelay(Mathf.Max(0.1f, duration)));
    }

    private void HideImmediate()
    {
        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = 0f;
        }

        gameObject.SetActive(false);
    }

    private void EnsureUi()
    {
        if (_canvasGroup != null && _keyText != null && _actionText != null)
        {
            return;
        }

        if (TryBindSceneUi())
        {
            HideImmediate();
            return;
        }

        Debug.LogError("[UIInteractionPrompt] PromptPanel binding is incomplete.");
    }

    private bool TryBindSceneUi()
    {
        Transform panelTransform = _usesScenePanel ? transform : FindScenePromptPanel();
        if (panelTransform == null)
        {
            return false;
        }

        _usesScenePanel = true;
        _panelRect = panelTransform as RectTransform;
        if (_panelRect == null)
        {
            return false;
        }

        _canvasGroup = GetComponent<CanvasGroup>();
        if (_canvasGroup == null)
        {
            _canvasGroup = panelTransform.gameObject.AddComponent<CanvasGroup>();
        }

        _panelImage = panelTransform.GetComponent<Image>();
        _contentRect = FindDeepChild(panelTransform, ContentName) as RectTransform;
        _contentLayout = _contentRect != null ? _contentRect.GetComponent<HorizontalLayoutGroup>() : null;
        _keyBadgeRoot = FindDeepChild(panelTransform, KeyBadgeName)?.gameObject;
        _keyBadgeRect = _keyBadgeRoot != null ? _keyBadgeRoot.GetComponent<RectTransform>() : null;
        _keyBadgeImage = _keyBadgeRoot != null ? _keyBadgeRoot.GetComponent<Image>() : null;
        _keyBadgeLayoutElement = _keyBadgeRoot != null ? _keyBadgeRoot.GetComponent<LayoutElement>() : null;
        _keyText = FindDeepChild(panelTransform, KeyTextName)?.GetComponent<TMP_Text>();
        _actionText = FindDeepChild(panelTransform, ActionTextName)?.GetComponent<TMP_Text>();
        _actionRect = _actionText != null ? _actionText.rectTransform : null;
        _actionLayoutElement = _actionText != null ? _actionText.GetComponent<LayoutElement>() : null;

        if (_keyBadgeRoot != null && _keyBadgeLayoutElement == null)
        {
            _keyBadgeLayoutElement = _keyBadgeRoot.AddComponent<LayoutElement>();
        }

        if (_actionText != null && _actionLayoutElement == null)
        {
            _actionLayoutElement = _actionText.gameObject.AddComponent<LayoutElement>();
        }

        if (_panelImage == null ||
            _contentRect == null ||
            _contentLayout == null ||
            _keyBadgeRoot == null ||
            _keyBadgeRect == null ||
            _keyBadgeImage == null ||
            _keyText == null ||
            _actionText == null ||
            _actionRect == null)
        {
            return false;
        }

        ApplyLayout(showKeyBadge: true);
        ApplyVisualStyle(showKeyBadge: true);
        return true;
    }

    private void EnsureCanvasRoot()
    {
        if (_usesScenePanel)
        {
            return;
        }

        RectTransform rootRect = GetComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;
        rootRect.localScale = Vector3.one;

        Canvas canvas = GetComponent<Canvas>();
        if (canvas == null)
        {
            canvas = gameObject.AddComponent<Canvas>();
        }

        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = SortingOrder;

        CanvasScaler scaler = GetComponent<CanvasScaler>();
        if (scaler == null)
        {
            scaler = gameObject.AddComponent<CanvasScaler>();
        }

        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(ReferenceWidth, ReferenceHeight);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        if (GetComponent<GraphicRaycaster>() == null)
        {
            gameObject.AddComponent<GraphicRaycaster>();
        }
    }

    private static GameObject CreateUiObject(string objectName, Transform parent)
    {
        Transform existing = parent.Find(objectName);
        if (existing != null)
        {
            return existing.gameObject;
        }

        GameObject go = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        go.layer = 5;
        return go;
    }

    private static GameObject CreateRectObject(string objectName, Transform parent)
    {
        Transform existing = parent.Find(objectName);
        if (existing != null)
        {
            return existing.gameObject;
        }

        GameObject go = new GameObject(objectName, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        go.layer = 5;
        return go;
    }

    private IEnumerator HideNoticeAfterDelay(float duration)
    {
        yield return new WaitForSecondsRealtime(duration);
        _noticeCoroutine = null;
        HideImmediate();
    }

    private void ApplyLayout(bool showKeyBadge)
    {
        if (_actionText == null || _keyBadgeRoot == null || _contentLayout == null)
        {
            return;
        }

        _keyBadgeRoot.SetActive(showKeyBadge);
        _contentLayout.spacing = showKeyBadge ? 8f : 0f;
        _contentLayout.childControlWidth = false;
        _contentLayout.childForceExpandWidth = false;
        _actionText.alignment = showKeyBadge ? TextAlignmentOptions.MidlineLeft : TextAlignmentOptions.Center;
    }

    private void ApplyVisualStyle(bool showKeyBadge)
    {
        if (_panelRect == null || _panelImage == null || _contentRect == null || _contentLayout == null || _keyBadgeRect == null || _keyBadgeImage == null || _keyText == null || _actionText == null)
        {
            return;
        }

        Sprite promptBackgroundSprite = _panelImage.sprite;
        if (promptBackgroundSprite != null)
        {
            _panelRect.sizeDelta = new Vector2(420f, 52f);
            _panelRect.anchoredPosition = new Vector2(0f, 58f);

            _panelImage.sprite = promptBackgroundSprite;
            _panelImage.type = Image.Type.Simple;
            _panelImage.color = Color.white;
            _panelImage.preserveAspect = true;
            _panelImage.raycastTarget = false;

            _contentRect.anchoredPosition = new Vector2(0f, -1f);
            _contentLayout.spacing = showKeyBadge ? 10f : 0f;

            _keyBadgeRect.sizeDelta = new Vector2(18f, 18f);
            _keyBadgeRect.anchoredPosition = Vector2.zero;
            _keyBadgeImage.color = new Color(1f, 1f, 1f, 0f);

            _keyText.rectTransform.anchoredPosition = Vector2.zero;
            _keyText.fontSize = 13f;
            _keyText.fontStyle = FontStyles.Normal;
            _keyText.color = new Color(0.74f, 0.74f, 0.74f, 0.78f);
            _keyText.enableWordWrapping = false;

            _actionText.fontSize = 15f;
            _actionText.fontStyle = FontStyles.Normal;
            _actionText.color = new Color(0.72f, 0.72f, 0.72f, 0.84f);
            _actionText.enableWordWrapping = false;
            return;
        }

        _panelRect.sizeDelta = new Vector2(420f, 52f);
        _panelRect.anchoredPosition = new Vector2(0f, 92f);

        _panelImage.sprite = null;
        _panelImage.type = Image.Type.Simple;
        _panelImage.color = new Color(0.05f, 0.05f, 0.06f, 0.9f);
        _panelImage.preserveAspect = false;
        _panelImage.raycastTarget = false;

        _contentRect.anchoredPosition = Vector2.zero;
        _contentLayout.spacing = showKeyBadge ? 8f : 0f;

        _keyBadgeRect.sizeDelta = new Vector2(56f, 34f);
        _keyBadgeRect.anchoredPosition = Vector2.zero;
        _keyBadgeImage.color = new Color(0.78f, 0.64f, 0.23f, 0.95f);

        _keyText.rectTransform.anchoredPosition = Vector2.zero;
        _keyText.fontSize = 20f;
        _keyText.fontStyle = FontStyles.Bold;
        _keyText.color = new Color(0.08f, 0.08f, 0.08f, 1f);
        _keyText.enableWordWrapping = false;

        _actionText.fontSize = 24f;
        _actionText.fontStyle = FontStyles.Normal;
        _actionText.color = Color.white;
        _actionText.enableWordWrapping = false;
    }

    private void RefreshDynamicLayout(bool showKeyBadge)
    {
        if (_panelRect == null || _contentRect == null || _keyText == null || _actionText == null || _keyBadgeRect == null)
        {
            return;
        }

        Canvas.ForceUpdateCanvases();
        _keyText.ForceMeshUpdate();
        _actionText.ForceMeshUpdate();

        float keyBadgeWidth = HiddenKeyBadgeWidth;
        if (showKeyBadge)
        {
            keyBadgeWidth = Mathf.Max(VisibleKeyBadgeMinWidth, Mathf.Ceil(_keyText.preferredWidth) + KeyBadgeHorizontalPadding);
        }

        float actionWidth = Mathf.Ceil(_actionText.preferredWidth) + 2f;
        float spacing = showKeyBadge ? _contentLayout.spacing : 0f;
        float panelWidth = Mathf.Clamp(keyBadgeWidth + spacing + actionWidth + PanelHorizontalPadding, MinPanelWidth, MaxPanelWidth);
        float maxActionWidth = Mathf.Max(0f, panelWidth - PanelHorizontalPadding - keyBadgeWidth - spacing);
        actionWidth = Mathf.Min(actionWidth, maxActionWidth);
        float contentOffsetX = showKeyBadge ? -0.5f * (keyBadgeWidth + spacing) : 0f;

        if (_keyBadgeLayoutElement != null)
        {
            _keyBadgeLayoutElement.minWidth = keyBadgeWidth;
            _keyBadgeLayoutElement.preferredWidth = keyBadgeWidth;
            _keyBadgeLayoutElement.flexibleWidth = 0f;
        }

        if (_actionLayoutElement != null)
        {
            _actionLayoutElement.minWidth = actionWidth;
            _actionLayoutElement.preferredWidth = actionWidth;
            _actionLayoutElement.flexibleWidth = 0f;
        }

        _keyBadgeRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, keyBadgeWidth);
        _actionRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, actionWidth);
        _panelRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, panelWidth);
        _contentRect.anchoredPosition = new Vector2(contentOffsetX, _contentRect.anchoredPosition.y);

        LayoutRebuilder.ForceRebuildLayoutImmediate(_contentRect);
        LayoutRebuilder.ForceRebuildLayoutImmediate(_panelRect);
    }

    private static TMP_Text CreateLabel(string objectName, Transform parent, float fontSize, FontStyles fontStyle, TextAlignmentOptions alignment, Color color)
    {
        Transform existing = parent.Find(objectName);
        if (existing != null)
        {
            TMP_Text existingText = existing.GetComponent<TMP_Text>();
            if (existingText != null)
            {
                return existingText;
            }
        }

        GameObject labelObject = new GameObject(objectName, typeof(RectTransform));
        labelObject.transform.SetParent(parent, false);
        labelObject.layer = 5;

        TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();
        label.font = ResolvePromptFont();
        label.fontSize = fontSize;
        label.fontStyle = fontStyle;
        label.color = color;
        label.alignment = alignment;
        label.enableWordWrapping = false;
        label.raycastTarget = false;
        return label;
    }

    private static TMP_FontAsset ResolvePromptFont()
    {
        if (_cachedPromptFont != null)
        {
            return _cachedPromptFont;
        }

        TMP_FontAsset[] loadedFonts = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
        foreach (TMP_FontAsset fontAsset in loadedFonts)
        {
            if (fontAsset != null && fontAsset.name == KoreanFontAssetName)
            {
                _cachedPromptFont = fontAsset;
                return _cachedPromptFont;
            }
        }

#if UNITY_EDITOR
        _cachedPromptFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(KoreanFontAssetPath);
        if (_cachedPromptFont != null)
        {
            return _cachedPromptFont;
        }
#endif

        _cachedPromptFont = TMP_Settings.defaultFontAsset;
        return _cachedPromptFont;
    }

    private static Transform FindScenePromptPanel()
    {
        GameObject canvasObject = GameObject.Find(CanvasRootName);
        if (canvasObject == null)
        {
            canvasObject = GameObject.Find("GameLoopUICanvas");
        }

        if (canvasObject == null)
        {
            return null;
        }

        return FindDeepChild(canvasObject.transform, PanelName);
    }

    private static Transform FindDeepChild(Transform parent, string childName)
    {
        if (parent == null)
        {
            return null;
        }

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name == childName)
            {
                return child;
            }

            Transform nested = FindDeepChild(child, childName);
            if (nested != null)
            {
                return nested;
            }
        }

        return null;
    }
}
