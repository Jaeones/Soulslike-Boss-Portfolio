using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class SceneFlowManager : Singleton<SceneFlowManager>
{
    [Serializable]
    private sealed class LoadingLoreCardData
    {
        public string sceneName;
        public string displaySceneName;
        public string category;
        public string itemName;
        public string description;
        public string backdropResourcePath;
        public string iconResourcePath;
    }

    [Serializable]
    private sealed class LoadingLoreDatabaseData
    {
        public string titleText;
        public string statusDefaultText;
        public string statusRespawnText;
        public LoadingLoreCardData[] cards;
    }

    private readonly struct LoadingLoreCard
    {
        public LoadingLoreCard(
            string sceneName,
            string displaySceneName,
            string category,
            string itemName,
            string description,
            string backdropResourcePath,
            string iconResourcePath)
        {
            SceneName = sceneName;
            DisplaySceneName = displaySceneName;
            Category = category;
            ItemName = itemName;
            Description = description;
            BackdropResourcePath = backdropResourcePath;
            IconResourcePath = iconResourcePath;
        }

        public string SceneName { get; }
        public string DisplaySceneName { get; }
        public string Category { get; }
        public string ItemName { get; }
        public string Description { get; }
        public string BackdropResourcePath { get; }
        public string IconResourcePath { get; }
    }

    private const float ReferenceWidth = 3840f;
    private const float ReferenceHeight = 2160f;
    private const int ContinueSpawnRetryFrames = 60;
    private const int ContinueSpawnStabilizeFrames = 5;
    private const float MinimumLoadingOverlayDuration = 2f;
    private const float DisplayProgressCatchupSpeed = 1.8f;
    private const float PostFillHoldDuration = 0.3f;
    private const string EnglishFontResourcePath = "Loading/Fonts/Kurale-Regular SDF";
    private const string KoreanFontResourcePath = "Loading/Fonts/KBIZ한마음명조 B SDF";
    private const string LoadingLoreDatabaseResourcePath = "Loading/LoadingLoreDatabase";

    private static readonly Color BackdropTintColor = new(1f, 1f, 1f, 0.9f);
    private static readonly Color OverlayDimColor = new(0.02f, 0.02f, 0.03f, 0.52f);
    private static readonly Color EdgeVignetteColor = new(0.01f, 0.01f, 0.02f, 0.68f);
    private static readonly Color GoldAccentColor = new(0.86f, 0.73f, 0.49f, 1f);
    private static readonly Color PrimaryTextColor = new(0.95f, 0.95f, 0.93f, 1f);
    private static readonly Color SecondaryTextColor = new(0.76f, 0.75f, 0.7f, 0.96f);
    private static readonly Color ProgressTrackColor = new(0.14f, 0.14f, 0.16f, 0.74f);
    private static readonly Color ProgressFillColor = new(0.86f, 0.73f, 0.49f, 0.98f);
    private static readonly Color ProgressGlowColor = new(0.98f, 0.84f, 0.58f, 0.42f);
    private static readonly Color IconShadowColor = new(0f, 0f, 0f, 0.45f);

    private bool _isLoading;
    private float _loadingProgress;
    private string _loadingSceneName = string.Empty;
    private bool _spawnAtSavedCheckpointAfterLoad;
    private int _loadingCardSequence;
    private LoadingLoreCard _activeLoadingCard;
    private LoadingLoreCard[] _loadingLoreCards;
    private string _loadingTitleLabel = "NOW LOADING";
    private string _loadingStatusDefaultText = "길 너머의 공간을 준비하고 있습니다.";
    private string _loadingStatusRespawnText = "마지막으로 쉰 화톳불에서 되살아납니다.";

    private TMP_FontAsset _englishFont;
    private TMP_FontAsset _koreanFont;
    private GameObject _loadingOverlayRoot;
    private Image _loadingBackdropImage;
    private Image _loadingEdgeVignette;
    private Image _loadingProgressFill;
    private Image _loadingProgressGlow;
    private Image _loadingIconShadow;
    private Image _loadingIconImage;
    private TMP_Text _loadingTitleText;
    private TMP_Text _loadingSceneText;
    private TMP_Text _loadingCategoryText;
    private TMP_Text _loadingItemNameText;
    private TMP_Text _loadingDescriptionText;
    private TMP_Text _loadingStatusText;
    private TMP_Text _loadingProgressText;

    public bool IsLoading => _isLoading;

    protected override void Awake()
    {
        base.Awake();

        if (Instance != this)
        {
            return;
        }

        LoadLoadingLoreDatabase();
        EnsureLoadingOverlay(SceneManager.GetActiveScene());
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    public bool TryLoadNewGame()
    {
        _spawnAtSavedCheckpointAfterLoad = false;
        SaveGameService.Instance.CreateNewGame(TitleFlowConstants.GameSceneName);
        BeginLoad(TitleFlowConstants.GameSceneName);
        return true;
    }

    public bool TryLoadContinue()
    {
        if (!SaveGameService.Instance.TryGetContinueScene(out string targetSceneName))
        {
            return false;
        }

        _spawnAtSavedCheckpointAfterLoad = true;
        BeginLoad(targetSceneName);
        return true;
    }

    public bool TryLoadRespawnAtSavedCheckpoint(string targetSceneName)
    {
        string normalizedSceneName = TitleFlowConstants.NormalizeSceneName(targetSceneName);
        if (_isLoading || string.IsNullOrWhiteSpace(normalizedSceneName))
        {
            return false;
        }

        _spawnAtSavedCheckpointAfterLoad = true;
        BeginLoad(normalizedSceneName);
        return true;
    }

    public bool TryLoadScene(string targetSceneName, bool spawnAtSavedCheckpoint = false)
    {
        string normalizedSceneName = TitleFlowConstants.NormalizeSceneName(targetSceneName);
        if (_isLoading || string.IsNullOrWhiteSpace(normalizedSceneName))
        {
            return false;
        }

        _spawnAtSavedCheckpointAfterLoad = spawnAtSavedCheckpoint;
        BeginLoad(normalizedSceneName);
        return true;
    }

    public void SaveCurrentScene(string sceneName)
    {
        SaveGameService.Instance.SaveCurrentScene(sceneName);
    }

    private void LoadLoadingLoreDatabase()
    {
        LoadingLoreDatabaseData database = CreateDefaultLoadingLoreDatabase();
        TextAsset databaseAsset = Resources.Load<TextAsset>(LoadingLoreDatabaseResourcePath);

        if (databaseAsset != null && !string.IsNullOrWhiteSpace(databaseAsset.text))
        {
            try
            {
                LoadingLoreDatabaseData loadedDatabase = JsonUtility.FromJson<LoadingLoreDatabaseData>(databaseAsset.text);
                if (loadedDatabase != null)
                {
                    database = MergeLoadingLoreDatabase(database, loadedDatabase);
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[SceneFlowManager] Failed to parse loading lore database: {exception.Message}");
            }
        }

        ApplyLoadingLoreDatabase(database);
    }

    private static LoadingLoreDatabaseData CreateDefaultLoadingLoreDatabase()
    {
        return new LoadingLoreDatabaseData
        {
            titleText = "NOW LOADING",
            statusDefaultText = "길 너머의 공간을 준비하고 있습니다.",
            statusRespawnText = "마지막으로 쉰 화톳불에서 되살아납니다.",
            cards = new[]
            {
                new LoadingLoreCardData
                {
                    sceneName = string.Empty,
                    displaySceneName = "재의 기억",
                    category = "화톳불",
                    itemName = "재는 길을 기억한다",
                    description = "화톳불은 쉼터이자 귀환점이다. 마지막으로 몸을 녹인 불꽃이 패배한 당신을 다시 불러낸다.",
                    backdropResourcePath = "Loading/Backdrops/LoadingBackdrop_Bonfire",
                    iconResourcePath = "Loading/Icons/Astus"
                },
                new LoadingLoreCardData
                {
                    sceneName = "Castle",
                    displaySceneName = "중세 성",
                    category = "유물",
                    itemName = "종추",
                    description = "무거운 철의 종추. 종을 울리는 순간, 다리 너머에서 당신을 기다리는 의식과 결투의 형식이 바뀐다.",
                    backdropResourcePath = "Loading/Backdrops/LoadingBackdrop_Castle",
                    iconResourcePath = "Loading/Icons/Bell_Pepper"
                },
                new LoadingLoreCardData
                {
                    sceneName = "Boss",
                    displaySceneName = "결투의 다리",
                    category = "보스",
                    itemName = "오를레아, 저무는 은총",
                    description = "다리 끝의 기사는 침입을 용서하지 않는다. 말을 건 뒤에는 강철로 자격을 증명해야 한다.",
                    backdropResourcePath = "Loading/Backdrops/LoadingBackdrop_Boss",
                    iconResourcePath = "Loading/Icons/PlayerWeapon"
                }
            }
        };
    }

    private static LoadingLoreDatabaseData MergeLoadingLoreDatabase(LoadingLoreDatabaseData fallback, LoadingLoreDatabaseData loaded)
    {
        return new LoadingLoreDatabaseData
        {
            titleText = string.IsNullOrWhiteSpace(loaded.titleText) ? fallback.titleText : loaded.titleText,
            statusDefaultText = string.IsNullOrWhiteSpace(loaded.statusDefaultText) ? fallback.statusDefaultText : loaded.statusDefaultText,
            statusRespawnText = string.IsNullOrWhiteSpace(loaded.statusRespawnText) ? fallback.statusRespawnText : loaded.statusRespawnText,
            cards = loaded.cards != null && loaded.cards.Length > 0 ? loaded.cards : fallback.cards
        };
    }

    private void ApplyLoadingLoreDatabase(LoadingLoreDatabaseData database)
    {
        _loadingTitleLabel = string.IsNullOrWhiteSpace(database.titleText) ? "NOW LOADING" : database.titleText;
        _loadingStatusDefaultText = string.IsNullOrWhiteSpace(database.statusDefaultText)
            ? "길 너머의 공간을 준비하고 있습니다."
            : database.statusDefaultText;
        _loadingStatusRespawnText = string.IsNullOrWhiteSpace(database.statusRespawnText)
            ? "마지막으로 쉰 화톳불에서 되살아납니다."
            : database.statusRespawnText;

        LoadingLoreCardData[] cards = database.cards;
        if (cards == null || cards.Length == 0)
        {
            cards = CreateDefaultLoadingLoreDatabase().cards;
        }

        _loadingLoreCards = new LoadingLoreCard[cards.Length];
        for (int i = 0; i < cards.Length; i++)
        {
            LoadingLoreCardData card = cards[i] ?? new LoadingLoreCardData();
            _loadingLoreCards[i] = new LoadingLoreCard(
                card.sceneName,
                card.displaySceneName,
                card.category,
                card.itemName,
                card.description,
                card.backdropResourcePath,
                card.iconResourcePath);
        }
    }

    private void BeginLoad(string sceneName)
    {
        string normalizedSceneName = TitleFlowConstants.NormalizeSceneName(sceneName);
        if (_isLoading || string.IsNullOrWhiteSpace(normalizedSceneName))
        {
            return;
        }

        EnsureLoadingOverlay();
        StartCoroutine(LoadSceneCoroutine(normalizedSceneName));
    }

    private IEnumerator LoadSceneCoroutine(string sceneName)
    {
        _isLoading = true;
        _loadingProgress = 0f;
        _loadingSceneName = sceneName;
        _activeLoadingCard = SelectLoadingLoreCard(sceneName, _spawnAtSavedCheckpointAfterLoad);
        SetLoadingOverlayVisible(true);
        RefreshLoadingOverlay();

        AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName);
        if (operation == null)
        {
            _isLoading = false;
            _loadingSceneName = string.Empty;
            SetLoadingOverlayVisible(false);
            yield break;
        }

        operation.allowSceneActivation = false;
        float overlayElapsed = 0f;
        bool heldForPostFill = false;

        while (!operation.isDone)
        {
            overlayElapsed += Time.unscaledDeltaTime;

            float actualProgress = Mathf.Clamp01(operation.progress / 0.9f);
            float timelineProgress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(overlayElapsed / MinimumLoadingOverlayDuration));
            float targetDisplayProgress = Mathf.Min(actualProgress, timelineProgress);

            _loadingProgress = Mathf.MoveTowards(
                _loadingProgress,
                targetDisplayProgress,
                DisplayProgressCatchupSpeed * Time.unscaledDeltaTime);

            RefreshLoadingOverlay();

            bool canActivateScene =
                operation.progress >= 0.9f &&
                overlayElapsed >= MinimumLoadingOverlayDuration &&
                _loadingProgress >= 0.999f;

            if (canActivateScene)
            {
                if (!heldForPostFill)
                {
                    heldForPostFill = true;
                    float holdUntil = Time.unscaledTime + PostFillHoldDuration;
                    while (Time.unscaledTime < holdUntil)
                    {
                        overlayElapsed += Time.unscaledDeltaTime;
                        RefreshLoadingOverlay();
                        yield return null;
                    }
                }

                operation.allowSceneActivation = true;
            }

            yield return null;
        }

        _loadingProgress = 1f;
        RefreshLoadingOverlay();
        yield return null;

        if (_spawnAtSavedCheckpointAfterLoad)
        {
            yield return StartCoroutine(TryPlacePlayerAtStartupCheckpoint(true));
            _spawnAtSavedCheckpointAfterLoad = false;
        }

        _isLoading = false;
        _loadingSceneName = string.Empty;
        SetLoadingOverlayVisible(false);
    }

    private IEnumerator TryPlacePlayerAtStartupCheckpoint(bool preferSavedCheckpoint)
    {
        string savedCheckpointId = preferSavedCheckpoint
            ? SaveGameService.Instance.GetQuestProgressCopy().currentCheckpointId
            : string.Empty;

        for (int frame = 0; frame < ContinueSpawnRetryFrames; frame++)
        {
            CheckpointManager checkpointManager = CheckpointManager.Instance;
            PlayerController player = FindFirstObjectByType<PlayerController>(FindObjectsInactive.Exclude);

            if (checkpointManager != null &&
                player != null &&
                TryRespawnPlayerAtCheckpoint(checkpointManager, player, savedCheckpointId))
            {
                yield break;
            }

            yield return null;
        }

        Debug.LogWarning($"[SceneFlowManager] Scene loaded, but failed to place player at a checkpoint. checkpointId={savedCheckpointId}");
    }

    private static bool TryRespawnPlayerAtCheckpoint(CheckpointManager checkpointManager, PlayerController player, string checkpointId)
    {
        if (checkpointManager == null || player == null)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(checkpointId) &&
            !checkpointManager.TryRestoreCheckpointById(checkpointId))
        {
            return false;
        }

        if (!checkpointManager.TryGetRespawnPose(player.transform, out Vector3 spawnPosition, out Quaternion spawnRotation))
        {
            return false;
        }

        Instance.StartCoroutine(ForcePlayerSpawnPose(player, spawnPosition, spawnRotation));
        return true;
    }

    private static IEnumerator ForcePlayerSpawnPose(PlayerController player, Vector3 spawnPosition, Quaternion spawnRotation)
    {
        if (player == null)
        {
            yield break;
        }

        player.RespawnFromCheckpoint(spawnPosition, spawnRotation);
        GameLoopEvents.RaisePlayerRespawned(player, null);

        for (int frame = 0; frame < ContinueSpawnStabilizeFrames; frame++)
        {
            player.transform.SetPositionAndRotation(spawnPosition, spawnRotation);
            player.SetVelocity(Vector3.zero);
            yield return null;
        }
    }

    private void EnsureLoadingOverlay()
    {
        EnsureLoadingOverlay(SceneManager.GetActiveScene());
    }

    private void EnsureLoadingOverlay(Scene targetScene)
    {
        _englishFont = LoadFontAsset(EnglishFontResourcePath, TMP_Settings.defaultFontAsset);
        _koreanFont = LoadFontAsset(KoreanFontResourcePath, TMP_Settings.defaultFontAsset);

        if (TryBindSceneLoadingOverlay(targetScene))
        {
            SetLoadingOverlayVisible(_isLoading);
            return;
        }

        if (_loadingOverlayRoot != null)
        {
            return;
        }

        GameObject canvasObject = new("TitleLoadingOverlay");
        canvasObject.layer = 5;

        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5000;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(ReferenceWidth, ReferenceHeight);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        canvasObject.AddComponent<GraphicRaycaster>();

        RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
        canvasRect.anchorMin = Vector2.zero;
        canvasRect.anchorMax = Vector2.one;
        canvasRect.offsetMin = Vector2.zero;
        canvasRect.offsetMax = Vector2.zero;

        GameObject backdrop = CreateUiObject("Backdrop", canvasObject.transform);
        AddFullStretchRect(backdrop);
        _loadingBackdropImage = backdrop.AddComponent<Image>();
        _loadingBackdropImage.color = BackdropTintColor;
        _loadingBackdropImage.preserveAspect = false;

        GameObject dimmer = CreateUiObject("Dimmer", canvasObject.transform);
        AddFullStretchRect(dimmer);
        Image dimmerImage = dimmer.AddComponent<Image>();
        dimmerImage.color = OverlayDimColor;

        GameObject edgeVignette = CreateUiObject("EdgeVignette", canvasObject.transform);
        AddFullStretchRect(edgeVignette);
        _loadingEdgeVignette = edgeVignette.AddComponent<Image>();
        _loadingEdgeVignette.color = EdgeVignetteColor;

        GameObject contentRoot = CreateUiObject("ContentRoot", canvasObject.transform);
        RectTransform contentRect = contentRoot.AddComponent<RectTransform>();
        contentRect.anchorMin = Vector2.zero;
        contentRect.anchorMax = Vector2.one;
        contentRect.pivot = new Vector2(0.5f, 0.5f);
        contentRect.offsetMin = Vector2.zero;
        contentRect.offsetMax = Vector2.zero;

        _loadingTitleText = CreateLabel(
            text: "NOW LOADING",
            parent: contentRoot.transform,
            fontAsset: _englishFont,
            fontSize: 42f,
            color: GoldAccentColor,
            anchorMin: new Vector2(0.5f, 1f),
            anchorMax: new Vector2(0.5f, 1f),
            pivot: new Vector2(0.5f, 1f),
            anchoredPosition: new Vector2(0f, -120f),
            sizeDelta: new Vector2(700f, 56f));

        _loadingSceneText = CreateLabel(
            text: string.Empty,
            parent: contentRoot.transform,
            fontAsset: _koreanFont,
            fontSize: 64f,
            color: PrimaryTextColor,
            anchorMin: new Vector2(0.5f, 1f),
            anchorMax: new Vector2(0.5f, 1f),
            pivot: new Vector2(0.5f, 1f),
            anchoredPosition: new Vector2(0f, -192f),
            sizeDelta: new Vector2(1400f, 88f));

        GameObject divider = CreateUiObject("Divider", contentRoot.transform);
        RectTransform dividerRect = divider.AddComponent<RectTransform>();
        dividerRect.anchorMin = new Vector2(0.5f, 1f);
        dividerRect.anchorMax = new Vector2(0.5f, 1f);
        dividerRect.pivot = new Vector2(0.5f, 1f);
        dividerRect.sizeDelta = new Vector2(520f, 2f);
        dividerRect.anchoredPosition = new Vector2(0f, -304f);
        Image dividerImage = divider.AddComponent<Image>();
        dividerImage.color = new Color(GoldAccentColor.r, GoldAccentColor.g, GoldAccentColor.b, 0.78f);

        GameObject presentationRoot = CreateUiObject("PresentationRoot", contentRoot.transform);
        RectTransform presentationRect = presentationRoot.AddComponent<RectTransform>();
        presentationRect.anchorMin = new Vector2(0.14f, 0.5f);
        presentationRect.anchorMax = new Vector2(0.86f, 0.5f);
        presentationRect.pivot = new Vector2(0.5f, 0.5f);
        presentationRect.sizeDelta = new Vector2(0f, 320f);
        presentationRect.anchoredPosition = new Vector2(0f, -36f);

        GameObject iconShadow = CreateUiObject("IconShadow", presentationRoot.transform);
        RectTransform iconShadowRect = iconShadow.AddComponent<RectTransform>();
        iconShadowRect.anchorMin = new Vector2(0f, 0.5f);
        iconShadowRect.anchorMax = new Vector2(0f, 0.5f);
        iconShadowRect.pivot = new Vector2(0.5f, 0.5f);
        iconShadowRect.sizeDelta = new Vector2(220f, 220f);
        iconShadowRect.anchoredPosition = new Vector2(122f, 10f);
        _loadingIconShadow = iconShadow.AddComponent<Image>();
        _loadingIconShadow.color = IconShadowColor;

        GameObject icon = CreateUiObject("ItemIcon", presentationRoot.transform);
        RectTransform iconRect = icon.AddComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0f, 0.5f);
        iconRect.anchorMax = new Vector2(0f, 0.5f);
        iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.sizeDelta = new Vector2(172f, 172f);
        iconRect.anchoredPosition = new Vector2(122f, -6f);
        _loadingIconImage = icon.AddComponent<Image>();
        _loadingIconImage.color = Color.white;
        _loadingIconImage.preserveAspect = true;

        GameObject textColumn = CreateUiObject("TextColumn", presentationRoot.transform);
        RectTransform textColumnRect = textColumn.AddComponent<RectTransform>();
        textColumnRect.anchorMin = new Vector2(0f, 0.5f);
        textColumnRect.anchorMax = new Vector2(0f, 0.5f);
        textColumnRect.pivot = new Vector2(0f, 0.5f);
        textColumnRect.sizeDelta = new Vector2(660f, 220f);
        textColumnRect.anchoredPosition = new Vector2(260f, 0f);

        _loadingCategoryText = CreateLabel(
            text: string.Empty,
            parent: textColumn.transform,
            fontAsset: _koreanFont,
            fontSize: 22f,
            color: GoldAccentColor,
            anchorMin: new Vector2(0f, 1f),
            anchorMax: new Vector2(0f, 1f),
            pivot: new Vector2(0f, 1f),
            anchoredPosition: new Vector2(0f, 0f),
            sizeDelta: new Vector2(640f, 32f),
            alignment: TextAlignmentOptions.Left);

        _loadingItemNameText = CreateLabel(
            text: string.Empty,
            parent: textColumn.transform,
            fontAsset: _koreanFont,
            fontSize: 40f,
            color: PrimaryTextColor,
            anchorMin: new Vector2(0f, 1f),
            anchorMax: new Vector2(0f, 1f),
            pivot: new Vector2(0f, 1f),
            anchoredPosition: new Vector2(0f, -44f),
            sizeDelta: new Vector2(640f, 52f),
            alignment: TextAlignmentOptions.Left);

        _loadingDescriptionText = CreateLabel(
            text: string.Empty,
            parent: textColumn.transform,
            fontAsset: _koreanFont,
            fontSize: 24f,
            color: SecondaryTextColor,
            anchorMin: new Vector2(0f, 1f),
            anchorMax: new Vector2(0f, 1f),
            pivot: new Vector2(0f, 1f),
            anchoredPosition: new Vector2(0f, -114f),
            sizeDelta: new Vector2(640f, 126f),
            alignment: TextAlignmentOptions.TopLeft,
            enableWordWrapping: true);

        _loadingStatusText = CreateLabel(
            text: string.Empty,
            parent: contentRoot.transform,
            fontAsset: _koreanFont,
            fontSize: 24f,
            color: SecondaryTextColor,
            anchorMin: new Vector2(0.5f, 0f),
            anchorMax: new Vector2(0.5f, 0f),
            pivot: new Vector2(0.5f, 0f),
            anchoredPosition: new Vector2(0f, 188f),
            sizeDelta: new Vector2(1280f, 42f));

        GameObject progressBar = CreateUiObject("ProgressBar", contentRoot.transform);
        RectTransform progressBarRect = progressBar.AddComponent<RectTransform>();
        progressBarRect.anchorMin = new Vector2(0.2f, 0f);
        progressBarRect.anchorMax = new Vector2(0.8f, 0f);
        progressBarRect.pivot = new Vector2(0.5f, 0f);
        progressBarRect.sizeDelta = new Vector2(0f, 6f);
        progressBarRect.anchoredPosition = new Vector2(0f, 116f);
        Image progressTrackImage = progressBar.AddComponent<Image>();
        progressTrackImage.color = ProgressTrackColor;

        GameObject fill = CreateUiObject("Fill", progressBar.transform);
        RectTransform fillRect = fill.AddComponent<RectTransform>();
        fillRect.anchorMin = new Vector2(0f, 0f);
        fillRect.anchorMax = new Vector2(0f, 1f);
        fillRect.pivot = new Vector2(0f, 0.5f);
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
        fillRect.sizeDelta = new Vector2(0f, 0f);
        _loadingProgressFill = fill.AddComponent<Image>();
        _loadingProgressFill.color = ProgressFillColor;

        GameObject glow = CreateUiObject("Glow", fill.transform);
        RectTransform glowRect = glow.AddComponent<RectTransform>();
        glowRect.anchorMin = Vector2.zero;
        glowRect.anchorMax = Vector2.one;
        glowRect.offsetMin = new Vector2(-4f, -4f);
        glowRect.offsetMax = new Vector2(4f, 4f);
        _loadingProgressGlow = glow.AddComponent<Image>();
        _loadingProgressGlow.color = ProgressGlowColor;

        _loadingProgressText = CreateLabel(
            text: "0%",
            parent: contentRoot.transform,
            fontAsset: _koreanFont,
            fontSize: 18f,
            color: SecondaryTextColor,
            anchorMin: new Vector2(0.5f, 0f),
            anchorMax: new Vector2(0.5f, 0f),
            pivot: new Vector2(0.5f, 0f),
            anchoredPosition: new Vector2(0f, 54f),
            sizeDelta: new Vector2(200f, 28f));

        _loadingOverlayRoot = canvasObject;
        SetLoadingOverlayVisible(false);
    }

    private bool TryBindSceneLoadingOverlay(Scene targetScene)
    {
        if (!targetScene.IsValid() || !targetScene.isLoaded)
        {
            return false;
        }

        GameObject overlayRoot = FindSceneLoadingCanvas(targetScene);

        if (overlayRoot == null)
        {
            return false;
        }

        RectTransform overlayRect = overlayRoot.GetComponent<RectTransform>();
        if (overlayRect == null)
        {
            return false;
        }

        overlayRect.localScale = Vector3.one;
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;
        overlayRect.anchoredPosition = Vector2.zero;

        Canvas canvas = overlayRoot.GetComponent<Canvas>();
        if (canvas != null)
        {
            canvas.overrideSorting = true;
            canvas.sortingOrder = 5000;
        }

        if (_loadingOverlayRoot != null && _loadingOverlayRoot != overlayRoot && _loadingOverlayRoot.name == "TitleLoadingOverlay")
        {
            Destroy(_loadingOverlayRoot);
        }

        _loadingOverlayRoot = overlayRoot;
        _loadingBackdropImage = null;
        _loadingEdgeVignette = null;
        _loadingIconShadow = null;
        _loadingIconImage = null;
        _loadingCategoryText = null;
        _loadingItemNameText = null;
        _loadingStatusText = null;
        _loadingProgressText = null;

        _loadingTitleText = FindChildComponent<TextMeshProUGUI>(overlayRoot.transform, "Image/Text (TMP) (1)");
        _loadingSceneText = FindChildComponent<TextMeshProUGUI>(overlayRoot.transform, "Image/Text (TMP)");
        _loadingDescriptionText = _loadingSceneText != null
            ? FindChildComponent<TextMeshProUGUI>(_loadingSceneText.transform, "Text (TMP)")
            : null;

        _loadingProgressFill = FindChildComponent<Image>(overlayRoot.transform, "LoadingBar/FillViewport/Fill");
        _loadingProgressGlow = FindChildComponent<Image>(overlayRoot.transform, "LoadingBar/FillViewport/Fill/Glow");

        return _loadingProgressFill != null;
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureLoadingOverlay(scene);
    }

    private void RefreshLoadingOverlay()
    {
        if (_loadingOverlayRoot == null)
        {
            return;
        }

        if (_loadingBackdropImage != null)
        {
            _loadingBackdropImage.sprite = LoadSprite(_activeLoadingCard.BackdropResourcePath);
        }

        if (_loadingTitleText != null)
        {
            _loadingTitleText.text = _loadingTitleLabel;
            _loadingTitleText.font = _englishFont != null ? _englishFont : _loadingTitleText.font;
        }

        if (_loadingSceneText != null)
        {
            _loadingSceneText.text = ResolveSceneDisplayName(_activeLoadingCard, _loadingSceneName);
            _loadingSceneText.font = _koreanFont != null ? _koreanFont : _loadingSceneText.font;
        }

        if (_loadingCategoryText != null)
        {
            _loadingCategoryText.text = _activeLoadingCard.Category ?? string.Empty;
            _loadingCategoryText.font = _koreanFont != null ? _koreanFont : _loadingCategoryText.font;
        }

        if (_loadingItemNameText != null)
        {
            _loadingItemNameText.text = _activeLoadingCard.ItemName ?? string.Empty;
            _loadingItemNameText.font = _koreanFont != null ? _koreanFont : _loadingItemNameText.font;
        }

        if (_loadingDescriptionText != null)
        {
            _loadingDescriptionText.text = _activeLoadingCard.Description ?? string.Empty;
            _loadingDescriptionText.font = _koreanFont != null ? _koreanFont : _loadingDescriptionText.font;
        }

        if (_loadingIconImage != null)
        {
            Sprite iconSprite = LoadSprite(_activeLoadingCard.IconResourcePath);
            _loadingIconImage.sprite = iconSprite;
            _loadingIconImage.enabled = iconSprite != null;
        }

        if (_loadingIconShadow != null)
        {
            _loadingIconShadow.enabled = _loadingIconImage != null && _loadingIconImage.enabled;
        }

        if (_loadingStatusText != null)
        {
            _loadingStatusText.text = _spawnAtSavedCheckpointAfterLoad
                ? _loadingStatusRespawnText
                : _loadingStatusDefaultText;
            _loadingStatusText.font = _koreanFont != null ? _koreanFont : _loadingStatusText.font;
        }

        if (_loadingProgressText != null)
        {
            _loadingProgressText.text = $"{Mathf.RoundToInt(_loadingProgress * 100f)}%";
            _loadingProgressText.font = _koreanFont != null ? _koreanFont : _loadingProgressText.font;
        }

        if (_loadingProgressFill != null)
        {
            RectTransform fillRect = _loadingProgressFill.rectTransform;
            fillRect.anchorMax = new Vector2(_loadingProgress, 1f);
        }

        if (_loadingProgressGlow != null)
        {
            Color glowColor = ProgressGlowColor;
            glowColor.a = 0.22f + Mathf.PingPong(Time.unscaledTime * 0.9f, 0.16f);
            _loadingProgressGlow.color = glowColor;
        }

        if (_loadingEdgeVignette != null)
        {
            Color vignetteColor = EdgeVignetteColor;
            vignetteColor.a = 0.64f + Mathf.PingPong(Time.unscaledTime * 0.12f, 0.06f);
            _loadingEdgeVignette.color = vignetteColor;
        }
    }

    private void SetLoadingOverlayVisible(bool visible)
    {
        if (_loadingOverlayRoot == null)
        {
            return;
        }

        _loadingOverlayRoot.SetActive(visible);
        if (visible)
        {
            RefreshLoadingOverlay();
        }
    }

    private LoadingLoreCard SelectLoadingLoreCard(string sceneName, bool preferBonfireCard)
    {
        if (_loadingLoreCards == null || _loadingLoreCards.Length == 0)
        {
            ApplyLoadingLoreDatabase(CreateDefaultLoadingLoreDatabase());
        }

        if (preferBonfireCard)
        {
            for (int i = 0; i < _loadingLoreCards.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(_loadingLoreCards[i].SceneName))
                {
                    return _loadingLoreCards[i];
                }
            }
        }

        for (int i = 0; i < _loadingLoreCards.Length; i++)
        {
            if (string.Equals(_loadingLoreCards[i].SceneName, sceneName, StringComparison.OrdinalIgnoreCase))
            {
                return _loadingLoreCards[i];
            }
        }

        int fallbackIndex = _loadingCardSequence % _loadingLoreCards.Length;
        _loadingCardSequence = (_loadingCardSequence + 1) & int.MaxValue;
        return _loadingLoreCards[fallbackIndex];
    }

    private static string ResolveSceneDisplayName(LoadingLoreCard card, string fallbackSceneName)
    {
        if (!string.IsNullOrWhiteSpace(card.DisplaySceneName))
        {
            return card.DisplaySceneName;
        }

        return fallbackSceneName switch
        {
            "Castle" => "중세 성",
            "Boss" => "결투의 다리",
            "Title" => "타이틀",
            _ => string.IsNullOrWhiteSpace(fallbackSceneName) ? "새 지역" : fallbackSceneName
        };
    }

    private static TMP_FontAsset LoadFontAsset(string resourcePath, TMP_FontAsset fallback)
    {
        TMP_FontAsset fontAsset = Resources.Load<TMP_FontAsset>(resourcePath);
        return fontAsset != null ? fontAsset : fallback;
    }

    private static Sprite LoadSprite(string resourcePath)
    {
        if (string.IsNullOrWhiteSpace(resourcePath))
        {
            return null;
        }

        return Resources.Load<Sprite>(resourcePath);
    }

    private static RectTransform AddFullStretchRect(GameObject gameObject)
    {
        RectTransform rect = gameObject.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return rect;
    }

    private static TMP_Text CreateLabel(
        string text,
        Transform parent,
        TMP_FontAsset fontAsset,
        float fontSize,
        Color color,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 anchoredPosition,
        Vector2 sizeDelta,
        TextAlignmentOptions alignment = TextAlignmentOptions.Center,
        bool enableWordWrapping = false)
    {
        GameObject labelObject = CreateUiObject($"Label_{text}", parent);
        RectTransform rect = labelObject.AddComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.sizeDelta = sizeDelta;
        rect.anchoredPosition = anchoredPosition;

        TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.font = fontAsset != null ? fontAsset : TMP_Settings.defaultFontAsset;
        label.fontSize = fontSize;
        label.color = color;
        label.alignment = alignment;
        label.enableWordWrapping = enableWordWrapping;
        label.overflowMode = enableWordWrapping ? TextOverflowModes.Ellipsis : TextOverflowModes.Overflow;
        label.raycastTarget = false;
        return label;
    }

    private static GameObject CreateUiObject(string objectName, Transform parent)
    {
        GameObject go = new GameObject(objectName);
        go.transform.SetParent(parent, false);
        go.layer = parent.gameObject.layer;
        return go;
    }

    private static T FindChildComponent<T>(Transform root, string relativePath) where T : Component
    {
        if (root == null || string.IsNullOrWhiteSpace(relativePath))
        {
            return null;
        }

        Transform child = root.Find(relativePath);
        return child != null ? child.GetComponent<T>() : null;
    }

    private static GameObject FindSceneLoadingCanvas(Scene scene)
    {
        GameObject[] rootObjects = scene.GetRootGameObjects();
        for (int i = 0; i < rootObjects.Length; i++)
        {
            if (rootObjects[i] == null)
            {
                continue;
            }

            Canvas[] canvases = rootObjects[i].GetComponentsInChildren<Canvas>(true);
            for (int j = 0; j < canvases.Length; j++)
            {
                if (canvases[j] == null)
                {
                    continue;
                }

                string canvasName = canvases[j].gameObject.name;
                if (string.Equals(canvasName, "LodingCanvas", StringComparison.Ordinal) ||
                    string.Equals(canvasName, "LoadingCanvas", StringComparison.Ordinal))
                {
                    return canvases[j].gameObject;
                }
            }
        }

        return null;
    }
}
