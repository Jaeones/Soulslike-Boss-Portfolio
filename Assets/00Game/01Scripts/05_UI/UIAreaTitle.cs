using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

public sealed class UIAreaTitle : MonoBehaviour
{
    private const string AreaPanelName = "AreaPanel";
    private const string AreaNameTextName = "AreaNameText";
    private const string AreaUnderlineName = "AreaUnderline";
    private const string RuntimePanelName = "AreaPanelRuntime";
    private const string RuntimeControllerName = "UIAreaTitleRuntimeController";
    private const string KoreanFontAssetName = "NotoSansKR-VariableFont_wght SDF";
    private const string KoreanFontAssetPath = "Assets/00Game/03Art/Font/NotoSansKR-VariableFont_wght SDF.asset";
    private const float BasePanelYOffset = 64f;

    private static UIAreaTitle _instance;
    private static TMP_FontAsset _cachedFont;
    private static CoroutineHost _coroutineHost;

    [SerializeField] private GameObject panelRoot;
    [SerializeField] private TMP_Text areaNameText;

    private CanvasGroup _canvasGroup;
    private Coroutine _sequenceCoroutine;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        _instance = null;
        _coroutineHost = null;
    }

    public static void Show(string areaName, float holdDuration = 2.4f, float fadeDuration = 0.6f)
    {
        if (string.IsNullOrWhiteSpace(areaName))
        {
            return;
        }

        EnsureInstance().ShowInternal(areaName, holdDuration, fadeDuration);
    }

    private static UIAreaTitle EnsureInstance()
    {
        if (_instance != null)
        {
            return _instance;
        }

        _instance = FindFirstObjectByType<UIAreaTitle>(FindObjectsInactive.Include);
        if (_instance != null)
        {
            _instance.EnsureBindings();
            return _instance;
        }

        GameObject controllerObject = new GameObject(RuntimeControllerName);
        DontDestroyOnLoad(controllerObject);
        _instance = controllerObject.AddComponent<UIAreaTitle>();
        _instance.EnsureBindings();
        return _instance;
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        EnsureBindings();
    }

    private void OnDestroy()
    {
        StopSequence();

        if (_instance == this)
        {
            _instance = null;
        }
    }

    private void ShowInternal(string areaName, float holdDuration, float fadeDuration)
    {
        EnsureBindings();
        StopSequence();

        areaNameText.text = areaName;
        panelRoot.SetActive(true);
        GameUiSfxPlayer.Play(UiSfxId.AreaTitleReveal);
        _sequenceCoroutine = EnsureCoroutineHost().StartCoroutine(PlaySequence(Mathf.Max(0.1f, holdDuration), Mathf.Max(0.05f, fadeDuration)));
    }

    private IEnumerator PlaySequence(float holdDuration, float fadeDuration)
    {
        yield return FadeTo(1f, fadeDuration);
        yield return new WaitForSecondsRealtime(holdDuration);
        yield return FadeTo(0f, fadeDuration);
        _sequenceCoroutine = null;
        panelRoot.SetActive(false);
    }

    private IEnumerator FadeTo(float targetAlpha, float duration)
    {
        float startAlpha = _canvasGroup.alpha;
        if (Mathf.Approximately(startAlpha, targetAlpha))
        {
            _canvasGroup.alpha = targetAlpha;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            _canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, t);
            yield return null;
        }

        _canvasGroup.alpha = targetAlpha;
    }

    private void StopSequence()
    {
        if (_sequenceCoroutine == null)
        {
            return;
        }

        if (_coroutineHost != null)
        {
            _coroutineHost.StopCoroutine(_sequenceCoroutine);
        }

        _sequenceCoroutine = null;
    }

    private void EnsureBindings()
    {
        if (panelRoot == null)
        {
            RectTransform areaPanel = FindAreaPanel();
            if (areaPanel != null)
            {
                panelRoot = areaPanel.gameObject;
            }
        }

        if (panelRoot == null)
        {
            panelRoot = CreateFallbackRuntimePanel();
        }

        if (areaNameText == null)
        {
            Transform textTransform = panelRoot.transform.Find(AreaNameTextName);
            if (textTransform != null)
            {
                areaNameText = textTransform.GetComponent<TMP_Text>();
            }
        }

        if (areaNameText == null)
        {
            areaNameText = panelRoot.GetComponentInChildren<TMP_Text>(true);
        }

        if (areaNameText == null)
        {
            areaNameText = CreateFallbackText(panelRoot.transform);
        }

        if (panelRoot.transform.Find(AreaUnderlineName) == null)
        {
            CreateFallbackUnderline(panelRoot.transform);
        }

        _canvasGroup = panelRoot.GetComponent<CanvasGroup>();
        if (_canvasGroup == null)
        {
            _canvasGroup = panelRoot.AddComponent<CanvasGroup>();
        }

        _canvasGroup.interactable = false;
        _canvasGroup.blocksRaycasts = false;
        _canvasGroup.alpha = 0f;
        panelRoot.SetActive(false);
    }

    private TMP_Text CreateFallbackText(Transform parent)
    {
        GameObject textObject = new GameObject(AreaNameTextName, typeof(RectTransform));
        textObject.transform.SetParent(parent, false);
        textObject.layer = 5;

        TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
        text.font = ResolveFont();
        text.fontSize = 84f;
        text.alignment = TextAlignmentOptions.Center;
        text.color = new Color(0.92f, 0.9f, 0.84f, 0.96f);
        text.enableWordWrapping = false;
        text.characterSpacing = 10f;
        text.raycastTarget = false;

        RectTransform rect = text.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, 18f);
        rect.sizeDelta = new Vector2(1400f, 120f);

        return text;
    }

    private static RectTransform FindAreaPanel()
    {
        RectTransform[] rectTransforms = Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (RectTransform rectTransform in rectTransforms)
        {
            if (rectTransform != null && rectTransform.name == AreaPanelName)
            {
                return rectTransform;
            }
        }

        return null;
    }

    private static GameObject CreateFallbackRuntimePanel()
    {
        Transform parent = null;
        GameObject uiRoot = GameObject.Find("GameUIRoot");
        if (uiRoot != null)
        {
            parent = uiRoot.transform;
        }

        GameObject panelObject = new GameObject(RuntimePanelName, typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
        if (parent != null)
        {
            panelObject.transform.SetParent(parent, false);
        }

        RectTransform rect = panelObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, BasePanelYOffset);
        rect.sizeDelta = new Vector2(1400f, 220f);

        Image image = panelObject.GetComponent<Image>();
        image.color = Color.clear;
        image.raycastTarget = false;

        Canvas canvas = panelObject.GetComponent<Canvas>();
        if (canvas == null)
        {
            canvas = panelObject.AddComponent<Canvas>();
        }

        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 170;

        CanvasScaler scaler = panelObject.GetComponent<CanvasScaler>();
        if (scaler == null)
        {
            scaler = panelObject.AddComponent<CanvasScaler>();
        }

        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        if (panelObject.GetComponent<GraphicRaycaster>() == null)
        {
            panelObject.AddComponent<GraphicRaycaster>();
        }

        return panelObject;
    }

    private RectTransform CreateFallbackUnderline(Transform parent)
    {
        GameObject underlineObject = new GameObject(AreaUnderlineName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        underlineObject.transform.SetParent(parent, false);
        underlineObject.layer = 5;
        RectTransform rect = underlineObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, -26f);
        rect.sizeDelta = new Vector2(760f, 2f);

        Image image = underlineObject.GetComponent<Image>();
        image.color = new Color(1f, 1f, 1f, 0.58f);
        image.raycastTarget = false;
        return rect;
    }

    private static CoroutineHost EnsureCoroutineHost()
    {
        if (_coroutineHost != null)
        {
            return _coroutineHost;
        }

        _coroutineHost = FindFirstObjectByType<CoroutineHost>(FindObjectsInactive.Include);
        if (_coroutineHost != null)
        {
            return _coroutineHost;
        }

        GameObject hostObject = new GameObject($"{RuntimeControllerName}_CoroutineHost");
        DontDestroyOnLoad(hostObject);
        _coroutineHost = hostObject.AddComponent<CoroutineHost>();
        return _coroutineHost;
    }

    private static TMP_FontAsset ResolveFont()
    {
        if (_cachedFont != null)
        {
            return _cachedFont;
        }

        TMP_FontAsset[] loadedFonts = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
        foreach (TMP_FontAsset fontAsset in loadedFonts)
        {
            if (fontAsset != null && fontAsset.name == KoreanFontAssetName)
            {
                _cachedFont = fontAsset;
                return _cachedFont;
            }
        }

#if UNITY_EDITOR
        _cachedFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(KoreanFontAssetPath);
        if (_cachedFont != null)
        {
            return _cachedFont;
        }
#endif

        _cachedFont = TMP_Settings.defaultFontAsset;
        return _cachedFont;
    }

    private sealed class CoroutineHost : MonoBehaviour
    {
    }
}
