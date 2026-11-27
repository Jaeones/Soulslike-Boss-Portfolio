using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

public sealed class DialogueChoiceOption
{
    public string Label { get; }
    public Action OnSelected { get; }
    public string Notice { get; }
    public Func<DialogueRequest> FollowUpRequestFactory { get; }

    public DialogueChoiceOption(string label, Action onSelected, string notice = null, Func<DialogueRequest> followUpRequestFactory = null)
    {
        Label = label;
        OnSelected = onSelected;
        Notice = notice;
        FollowUpRequestFactory = followUpRequestFactory;
    }
}

public sealed class DialogueRequest
{
    public string SpeakerName { get; }
    public string[] Lines { get; }
    public DialogueChoiceOption[] Choices { get; }
    public string ChoicePrompt { get; }
    public Action OnCancelled { get; }
    public Action OnCompleted { get; }
    public Action<int> OnLineShown { get; }
    public bool AllowManualAdvance { get; }
    public bool AllowCancel { get; }
    public bool ManagePlayerLock { get; }
    public bool AutoAdvance { get; }
    public float AutoAdvanceDelay { get; }

    public DialogueRequest(
        string speakerName,
        string[] lines,
        DialogueChoiceOption[] choices = null,
        string choicePrompt = null,
        Action onCancelled = null,
        Action onCompleted = null,
        Action<int> onLineShown = null,
        bool allowManualAdvance = true,
        bool allowCancel = true,
        bool managePlayerLock = true,
        bool autoAdvance = false,
        float autoAdvanceDelay = 0.8f)
    {
        SpeakerName = speakerName;
        Lines = lines ?? Array.Empty<string>();
        Choices = choices ?? Array.Empty<DialogueChoiceOption>();
        ChoicePrompt = choicePrompt;
        OnCancelled = onCancelled;
        OnCompleted = onCompleted;
        OnLineShown = onLineShown;
        AllowManualAdvance = allowManualAdvance;
        AllowCancel = allowCancel;
        ManagePlayerLock = managePlayerLock;
        AutoAdvance = autoAdvance;
        AutoAdvanceDelay = Mathf.Max(0f, autoAdvanceDelay);
    }
}

public sealed class SimpleDialogueManager : MonoBehaviour
{
    private const string RuntimeObjectName = "SimpleDialogueManagerRuntime";
    private const string DialogueCanvasName = "GameLoopUICanvas";
    private const string DialoguePanelName = "DialoguePanel";
    private const string DialogueBodyName = "DialogueText";
    private const string SpeakerTextName = "SpeakerText";
    private const string HintTextName = "HintText";
    private const string ChoiceRootName = "Choice";
    private const string ChoicePromptTextName = "ChoiceText";
    private const string YesButtonName = "YesButton";
    private const string NoButtonName = "NoButton";
    private const string YesButtonTextName = "Choice1Text";
    private const string NoButtonTextName = "Choice2Text";
    private const bool ShowSpeakerName = false;
    private const float TypingCharactersPerSecond = 52f;
    private const float TypingCommaPause = 0.05f;
    private const float TypingSentencePause = 0.09f;
    private const float TypingEllipsisPause = 0.14f;
    private const float TypingSkipDelay = 0.01f;
    private const float PanelTransitionDuration = 0.18f;
    private const float PanelHiddenYOffset = -28f;
    private const float ChoicePulseSpeed = 4.2f;
    private const string KoreanFontAssetName = "NotoSansKR-VariableFont_wght SDF";
    private const string KoreanFontAssetPath = "Assets/00Game/03Art/Font/NotoSansKR-VariableFont_wght SDF.asset";

    private static readonly Color ChoiceSelectedTextColor = Color.white;
    private static readonly Color ChoiceNormalTextColor = Color.white;
    private static readonly Color ChoiceSelectedBaseColor = new Color(0.85f, 0.72f, 0.31f, 0.94f);
    private static readonly Color ChoiceSelectedPulseColor = new Color(0.96f, 0.84f, 0.42f, 1f);
    private static readonly Color ChoiceNormalBackgroundColor = new Color(0.12f, 0.12f, 0.14f, 0.92f);

    private static SimpleDialogueManager _instance;
    private static TMP_FontAsset _cachedFont;

    private CanvasGroup _canvasGroup;
    private RectTransform _panelRect;
    private TMP_Text _speakerText;
    private TMP_Text _bodyText;
    private TMP_Text _hintText;
    private TMP_Text _choicePromptText;
    private string _defaultChoicePromptText;
    private TMP_Text[] _choiceTexts;
    private Image[] _choiceBackgrounds;
    private Button[] _choiceButtons;
    private RectTransform _choiceRoot;

    private DialogueRequest _currentRequest;
    private PlayerController _currentPlayer;
    private int _lineIndex;
    private int _choiceIndex;
    private bool _showingChoices;
    private bool _isTyping;
    private int _visibleCharacters;
    private int _targetVisibleCharacters;
    private float _typingDelayRemaining;
    private float _autoAdvanceElapsed;

    private bool _transitionActive;
    private bool _transitionToVisible;
    private float _transitionElapsed;
    private Vector2 _panelShownPosition;
    private Vector2 _panelHiddenPosition;

    public static bool IsOpen => _instance != null && (_instance._currentRequest != null || _instance._transitionActive);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        _instance = null;
        _cachedFont = null;
    }

    public static bool TryBegin(PlayerController player, DialogueRequest request)
    {
        if (player == null || request == null || request.Lines == null || request.Lines.Length == 0 || IsOpen)
        {
            return false;
        }

        EnsureInstance().Open(player, request);
        return true;
    }

    private static SimpleDialogueManager EnsureInstance()
    {
        if (_instance != null)
        {
            return _instance;
        }

        _instance = FindFirstObjectByType<SimpleDialogueManager>(FindObjectsInactive.Include);
        if (_instance != null)
        {
            _instance.EnsureUi();
            return _instance;
        }

        GameObject runtimeObject = new GameObject(RuntimeObjectName);
        _instance = runtimeObject.AddComponent<SimpleDialogueManager>();
        _instance.EnsureUi();
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
        EnsureUi();
    }

    private void Update()
    {
        float deltaTime = Time.unscaledDeltaTime;
        UpdateTransition(deltaTime);

        if (_currentRequest == null)
        {
            return;
        }

        if (_showingChoices)
        {
            UpdateChoicePulse();
            HandleChoiceInput();
            return;
        }

        if (_isTyping)
        {
            UpdateTyping(deltaTime);
        }
        else
        {
            UpdateAutoAdvance(deltaTime);
        }

        if (_currentRequest.AllowManualAdvance &&
            (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space)))
        {
            if (_isTyping)
            {
                CompleteTypingInstant();
            }
            else
            {
                AdvanceLine();
            }
        }
        else if (_currentRequest.AllowCancel && Input.GetKeyDown(KeyCode.Escape))
        {
            CancelDialogue();
        }
    }

    private void OnDestroy()
    {
        if (_instance == this)
        {
            _instance = null;
        }
    }

    private void Open(PlayerController player, DialogueRequest request)
    {
        _currentPlayer = player;
        _currentRequest = request;
        _lineIndex = 0;
        _choiceIndex = 0;
        _showingChoices = false;
        _autoAdvanceElapsed = 0f;

        if (_currentRequest.ManagePlayerLock)
        {
            _currentPlayer.BeginBonfireInteractionLock();
            _currentPlayer.Interaction?.ClearCandidates();
        }

        EnsureUi();
        if (!IsUiReady())
        {
            Debug.LogError("[SimpleDialogueManager] DialoguePanel UI binding is incomplete.");

            if (_currentRequest.ManagePlayerLock)
            {
                _currentPlayer.EndBonfireInteractionLock();
            }

            _currentPlayer = null;
            _currentRequest = null;
            return;
        }

        GameUiSfxPlayer.Play(UiSfxId.PromptAppear);
        ApplyCurrentLine();
        BeginTransition(visible: true);
    }

    private void AdvanceLine()
    {
        if (_currentRequest == null)
        {
            return;
        }

        if (_lineIndex < _currentRequest.Lines.Length - 1)
        {
            _lineIndex++;
            GameUiSfxPlayer.Play(UiSfxId.DialogueAdvance);
            ApplyCurrentLine();
            return;
        }

        if (_currentRequest.Choices != null && _currentRequest.Choices.Length > 0)
        {
            _showingChoices = true;
            _choiceIndex = 0;
            GameUiSfxPlayer.Play(UiSfxId.DialogueAdvance);
            RefreshChoiceUi();
            return;
        }

        CloseDialogue();
    }

    private void HandleChoiceInput()
    {
        if (_currentRequest == null || _currentRequest.Choices == null || _currentRequest.Choices.Length == 0)
        {
            CloseDialogue();
            return;
        }

        if (_currentRequest.AllowManualAdvance &&
            (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)))
        {
            _choiceIndex = (_choiceIndex - 1 + _currentRequest.Choices.Length) % _currentRequest.Choices.Length;
            GameUiSfxPlayer.Play(UiSfxId.Move);
            RefreshChoiceUi();
            return;
        }

        if (_currentRequest.AllowManualAdvance &&
            (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)))
        {
            _choiceIndex = (_choiceIndex + 1) % _currentRequest.Choices.Length;
            GameUiSfxPlayer.Play(UiSfxId.Move);
            RefreshChoiceUi();
            return;
        }

        if (_currentRequest.AllowManualAdvance &&
            (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space)))
        {
            SubmitCurrentChoice();
            return;
        }

        if (_currentRequest.AllowCancel && Input.GetKeyDown(KeyCode.Escape))
        {
            GameUiSfxPlayer.Play(UiSfxId.Back);
            CancelDialogue();
        }
    }

    private void UpdateTyping(float deltaTime)
    {
        if (!_isTyping || _currentRequest == null)
        {
            return;
        }

        _typingDelayRemaining -= deltaTime;
        string currentLine = _currentRequest.Lines[Mathf.Clamp(_lineIndex, 0, _currentRequest.Lines.Length - 1)];

        while (_typingDelayRemaining <= 0f && _visibleCharacters < _targetVisibleCharacters)
        {
            _visibleCharacters++;
            _bodyText.maxVisibleCharacters = _visibleCharacters;
            _typingDelayRemaining += GetTypingDelay(currentLine, _visibleCharacters);
        }

        if (_visibleCharacters >= _targetVisibleCharacters)
        {
            _isTyping = false;
            _bodyText.maxVisibleCharacters = int.MaxValue;
            _autoAdvanceElapsed = 0f;
            RefreshHintText();
        }
    }

    private void UpdateAutoAdvance(float deltaTime)
    {
        if (_currentRequest == null || !_currentRequest.AutoAdvance || _showingChoices || _isTyping)
        {
            return;
        }

        _autoAdvanceElapsed += deltaTime;
        if (_autoAdvanceElapsed < _currentRequest.AutoAdvanceDelay)
        {
            return;
        }

        _autoAdvanceElapsed = 0f;
        AdvanceLine();
    }

    private void CompleteTypingInstant()
    {
        if (!_isTyping)
        {
            return;
        }

        _isTyping = false;
        _visibleCharacters = _targetVisibleCharacters;
        _typingDelayRemaining = 0f;
        _autoAdvanceElapsed = 0f;
        _bodyText.maxVisibleCharacters = int.MaxValue;
        RefreshHintText();
    }

    private void CancelDialogue()
    {
        _currentRequest?.OnCancelled?.Invoke();
        CloseDialogue(completed: false);
    }

    private void CloseDialogue(bool completed = true)
    {
        Action onCompleted = completed ? _currentRequest?.OnCompleted : null;
        bool managePlayerLock = _currentRequest?.ManagePlayerLock ?? false;
        _currentRequest = null;
        _showingChoices = false;
        _lineIndex = 0;
        _choiceIndex = 0;
        _isTyping = false;
        _visibleCharacters = 0;
        _targetVisibleCharacters = 0;
        _typingDelayRemaining = 0f;
        _autoAdvanceElapsed = 0f;
        HideChoiceUi();

        if (_currentPlayer != null && managePlayerLock)
        {
            _currentPlayer.EndBonfireInteractionLock();
        }

        _currentPlayer = null;
        onCompleted?.Invoke();
        BeginTransition(visible: false);
    }

    private void ApplyCurrentLine()
    {
        if (_currentRequest == null)
        {
            return;
        }

        if (!IsUiReady())
        {
            EnsureUi();
            if (!IsUiReady())
            {
                return;
            }
        }

        SetDialogueBodyVisible(true);

        bool shouldShowSpeaker = ShowSpeakerName && !string.IsNullOrWhiteSpace(_currentRequest.SpeakerName);
        _speakerText.gameObject.SetActive(shouldShowSpeaker);
        if (shouldShowSpeaker)
        {
            _speakerText.text = _currentRequest.SpeakerName;
        }

        _bodyText.text = _currentRequest.Lines[Mathf.Clamp(_lineIndex, 0, _currentRequest.Lines.Length - 1)];
        _bodyText.ForceMeshUpdate();

        _targetVisibleCharacters = string.IsNullOrEmpty(_bodyText.text) ? 0 : _bodyText.text.Length;
        _visibleCharacters = 0;
        _typingDelayRemaining = 0f;
        _autoAdvanceElapsed = 0f;
        _isTyping = _targetVisibleCharacters > 0;
        _bodyText.maxVisibleCharacters = _isTyping ? 0 : int.MaxValue;

        _showingChoices = false;
        HideChoiceUi();
        RefreshHintText();
        _currentRequest.OnLineShown?.Invoke(_lineIndex);
    }

    private void SubmitCurrentChoice()
    {
        if (_currentRequest == null || _currentRequest.Choices == null || _currentRequest.Choices.Length == 0)
        {
            CloseDialogue();
            return;
        }

        _choiceIndex = Mathf.Clamp(_choiceIndex, 0, _currentRequest.Choices.Length - 1);
        DialogueChoiceOption choice = _currentRequest.Choices[_choiceIndex];
        GameUiSfxPlayer.Play(UiSfxId.Confirm);
        choice?.OnSelected?.Invoke();

        DialogueRequest followUpRequest = choice?.FollowUpRequestFactory?.Invoke();
        if (followUpRequest != null && followUpRequest.Lines != null && followUpRequest.Lines.Length > 0)
        {
            _currentRequest = followUpRequest;
            _lineIndex = 0;
            _choiceIndex = 0;
            _showingChoices = false;
            ApplyCurrentLine();
            return;
        }

        if (!string.IsNullOrWhiteSpace(choice?.Notice))
        {
            UIInteractionPrompt.ShowNotice(choice.Notice);
        }

        CloseDialogue();
    }

    private void RefreshChoiceUi()
    {
        if (_currentRequest == null || _currentRequest.Choices == null || _choiceTexts == null)
        {
            return;
        }

        SetDialogueBodyVisible(false);

        if (_choiceRoot != null)
        {
            _choiceRoot.gameObject.SetActive(true);
        }

        if (_choicePromptText != null)
        {
            _choicePromptText.text = string.IsNullOrWhiteSpace(_currentRequest.ChoicePrompt)
                ? _defaultChoicePromptText
                : _currentRequest.ChoicePrompt;
        }

        for (int i = 0; i < _choiceTexts.Length; i++)
        {
            bool active = i < _currentRequest.Choices.Length;
            if (_choiceButtons != null && i < _choiceButtons.Length && _choiceButtons[i] != null)
            {
                _choiceButtons[i].gameObject.SetActive(active);
                _choiceButtons[i].interactable = active;
            }
            else if (_choiceTexts[i] != null && _choiceTexts[i].transform.parent != null)
            {
                _choiceTexts[i].transform.parent.gameObject.SetActive(active);
            }

            if (!active)
            {
                continue;
            }

            _choiceTexts[i].text = _currentRequest.Choices[i].Label;
        }

        UpdateChoicePulse();
        RefreshHintText();
    }

    private void UpdateChoicePulse()
    {
        if (_choiceTexts == null || _choiceBackgrounds == null || _currentRequest?.Choices == null)
        {
            return;
        }

        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * ChoicePulseSpeed);
        for (int i = 0; i < _choiceTexts.Length; i++)
        {
            bool active = i < _currentRequest.Choices.Length;
            if (!active)
            {
                continue;
            }

            bool isSelected = i == _choiceIndex;
            _choiceTexts[i].color = isSelected ? ChoiceSelectedTextColor : ChoiceNormalTextColor;
            _choiceBackgrounds[i].color = isSelected
                ? Color.Lerp(ChoiceSelectedBaseColor, ChoiceSelectedPulseColor, pulse)
                : ChoiceNormalBackgroundColor;
        }
    }

    private void RefreshHintText()
    {
        if (_hintText == null)
        {
            return;
        }

        if (_currentRequest == null)
        {
            _hintText.text = string.Empty;
            return;
        }

        bool allowManualAdvance = _currentRequest.AllowManualAdvance;
        bool allowCancel = _currentRequest.AllowCancel;

        if (!allowManualAdvance && !allowCancel)
        {
            _hintText.text = string.Empty;
            return;
        }

        if (_showingChoices)
        {
            _hintText.text = allowManualAdvance && allowCancel
                ? "[A/D] Select    [E] Confirm    [ESC] Close"
                : allowManualAdvance
                    ? "[A/D] Select    [E] Confirm"
                    : "[ESC] Close";
            return;
        }

        if (_isTyping)
        {
            _hintText.text = allowManualAdvance && allowCancel
                ? "[E] Skip    [ESC] Close"
                : allowManualAdvance
                    ? "[E] Skip"
                    : "[ESC] Close";
            return;
        }

        bool canShowChoices = _currentRequest.Choices != null &&
                              _currentRequest.Choices.Length > 0 &&
                              _lineIndex >= _currentRequest.Lines.Length - 1;

        if (_currentRequest.AutoAdvance && !allowManualAdvance)
        {
            _hintText.text = allowCancel ? "[ESC] Close" : string.Empty;
            return;
        }

        _hintText.text = canShowChoices
            ? allowManualAdvance && allowCancel
                ? "[E] Show choices    [ESC] Close"
                : allowManualAdvance
                    ? "[E] Show choices"
                    : "[ESC] Close"
            : allowManualAdvance && allowCancel
                ? "[E] Next    [ESC] Close"
                : allowManualAdvance
                    ? "[E] Next"
                    : "[ESC] Close";
    }

    private void HideChoiceUi()
    {
        SetDialogueBodyVisible(true);

        if (_choiceRoot != null)
        {
            _choiceRoot.gameObject.SetActive(false);
        }

        if (_choicePromptText != null && !string.IsNullOrWhiteSpace(_defaultChoicePromptText))
        {
            _choicePromptText.text = _defaultChoicePromptText;
        }

        if (_choiceTexts == null)
        {
            return;
        }

        for (int i = 0; i < _choiceTexts.Length; i++)
        {
            if (_choiceButtons != null && i < _choiceButtons.Length && _choiceButtons[i] != null)
            {
                _choiceButtons[i].gameObject.SetActive(false);
                continue;
            }

            if (_choiceTexts[i] != null && _choiceTexts[i].transform.parent != null)
            {
                _choiceTexts[i].transform.parent.gameObject.SetActive(false);
            }
        }
    }

    private void BeginTransition(bool visible)
    {
        EnsureUi();
        if (_panelRect == null || _canvasGroup == null)
        {
            return;
        }

        _panelRect.gameObject.SetActive(true);
        _transitionActive = true;
        _transitionToVisible = visible;
        _transitionElapsed = 0f;
        _panelRect.anchoredPosition = visible ? _panelHiddenPosition : _panelShownPosition;
        _canvasGroup.alpha = visible ? 0f : 1f;
        _canvasGroup.blocksRaycasts = visible;
        _canvasGroup.interactable = visible;
    }

    private void UpdateTransition(float deltaTime)
    {
        if (!_transitionActive || _canvasGroup == null || _panelRect == null)
        {
            return;
        }

        _transitionElapsed = Mathf.Min(_transitionElapsed + deltaTime, PanelTransitionDuration);
        float progress = Mathf.Clamp01(_transitionElapsed / PanelTransitionDuration);
        float eased = 1f - Mathf.Pow(1f - progress, 3f);

        if (_transitionToVisible)
        {
            _canvasGroup.alpha = eased;
            _panelRect.anchoredPosition = Vector2.Lerp(_panelHiddenPosition, _panelShownPosition, eased);
        }
        else
        {
            _canvasGroup.alpha = 1f - eased;
            _panelRect.anchoredPosition = Vector2.Lerp(_panelShownPosition, _panelHiddenPosition, eased);
        }

        if (progress < 1f)
        {
            return;
        }

        _transitionActive = false;
        if (_transitionToVisible)
        {
            _canvasGroup.alpha = 1f;
            _panelRect.anchoredPosition = _panelShownPosition;
            return;
        }

        _canvasGroup.alpha = 0f;
        _panelRect.anchoredPosition = _panelHiddenPosition;
        _panelRect.gameObject.SetActive(false);
        _canvasGroup.blocksRaycasts = false;
        _canvasGroup.interactable = false;
    }

    private void EnsureUi()
    {
        if (_panelRect != null &&
            _canvasGroup != null &&
            _bodyText != null &&
            _speakerText != null &&
            _hintText != null &&
            _choicePromptText != null &&
            _choiceTexts != null &&
            _choiceButtons != null)
        {
            return;
        }

        Transform panelTransform = FindDialoguePanelTransform();
        if (panelTransform == null)
        {
            Debug.LogError("[SimpleDialogueManager] Could not find DialoguePanel under GameLoopUICanvas.");
            return;
        }

        _panelRect = panelTransform as RectTransform;
        _canvasGroup = panelTransform.GetComponent<CanvasGroup>();
        if (_canvasGroup == null)
        {
            _canvasGroup = panelTransform.gameObject.AddComponent<CanvasGroup>();
        }

        _panelShownPosition = _panelRect.anchoredPosition;
        _panelHiddenPosition = _panelShownPosition + new Vector2(0f, PanelHiddenYOffset);
        _panelRect.anchoredPosition = _panelHiddenPosition;
        _canvasGroup.alpha = 0f;
        _canvasGroup.blocksRaycasts = false;
        _canvasGroup.interactable = false;

        _bodyText = ResolveOrCreateBodyText(panelTransform);
        _speakerText = ResolveOrCreateSpeakerText(panelTransform);
        _hintText = ResolveOrCreateHintText(panelTransform);
        ResolveOrCreateChoiceUi(panelTransform);
        _speakerText.gameObject.SetActive(false);

        HideChoiceUi();
        panelTransform.gameObject.SetActive(false);
    }

    private bool IsUiReady()
    {
        return _panelRect != null &&
               _canvasGroup != null &&
               _speakerText != null &&
               _bodyText != null &&
               _hintText != null &&
               _choicePromptText != null &&
               _choiceTexts != null &&
               _choiceTexts.Length == 2 &&
               _choiceTexts[0] != null &&
               _choiceTexts[1] != null &&
               _choiceButtons != null &&
               _choiceButtons.Length == 2 &&
               _choiceButtons[0] != null &&
               _choiceButtons[1] != null &&
               _choiceBackgrounds != null &&
               _choiceBackgrounds.Length == 2 &&
               _choiceBackgrounds[0] != null &&
               _choiceBackgrounds[1] != null;
    }

    private TMP_Text ResolveOrCreateBodyText(Transform panelTransform)
    {
        TMP_Text bodyText = FindDeepChild(panelTransform, DialogueBodyName)?.GetComponent<TMP_Text>();
        if (bodyText != null)
        {
            bodyText.enableWordWrapping = true;
            return bodyText;
        }

        TMP_Text created = CreateLabel(DialogueBodyName, panelTransform, 28f, FontStyles.Normal, TextAlignmentOptions.TopLeft, Color.white);
        created.enableWordWrapping = true;
        RectTransform bodyRect = created.rectTransform;
        bodyRect.anchorMin = new Vector2(0f, 0f);
        bodyRect.anchorMax = new Vector2(1f, 1f);
        bodyRect.offsetMin = new Vector2(32f, 96f);
        bodyRect.offsetMax = new Vector2(-32f, -72f);
        return created;
    }

    private TMP_Text ResolveOrCreateSpeakerText(Transform panelTransform)
    {
        TMP_Text speakerText = FindDeepChild(panelTransform, SpeakerTextName)?.GetComponent<TMP_Text>();
        if (speakerText != null)
        {
            return speakerText;
        }

        TMP_Text created = CreateLabel(SpeakerTextName, panelTransform, 30f, FontStyles.Bold, TextAlignmentOptions.TopLeft, new Color(0.85f, 0.74f, 0.34f, 1f));
        RectTransform speakerRect = created.rectTransform;
        speakerRect.anchorMin = new Vector2(0f, 1f);
        speakerRect.anchorMax = new Vector2(1f, 1f);
        speakerRect.pivot = new Vector2(0f, 1f);
        speakerRect.offsetMin = new Vector2(32f, -56f);
        speakerRect.offsetMax = new Vector2(-32f, -12f);
        return created;
    }

    private TMP_Text ResolveOrCreateHintText(Transform panelTransform)
    {
        TMP_Text hintText = FindDeepChild(panelTransform, HintTextName)?.GetComponent<TMP_Text>();
        if (hintText != null)
        {
            return hintText;
        }

        TMP_Text created = CreateLabel(HintTextName, panelTransform, 20f, FontStyles.Normal, TextAlignmentOptions.BottomRight, new Color(0.8f, 0.8f, 0.8f, 0.95f));
        RectTransform hintRect = created.rectTransform;
        hintRect.anchorMin = new Vector2(0f, 0f);
        hintRect.anchorMax = new Vector2(1f, 0f);
        hintRect.pivot = new Vector2(1f, 0f);
        hintRect.offsetMin = new Vector2(32f, 18f);
        hintRect.offsetMax = new Vector2(-32f, 52f);
        return created;
    }

    private void ResolveOrCreateChoiceUi(Transform panelTransform)
    {
        Transform existingChoiceRoot = FindDeepChild(panelTransform, ChoiceRootName);
        GameObject choiceRootObject = existingChoiceRoot != null ? existingChoiceRoot.gameObject : CreateUiObject(ChoiceRootName, panelTransform, true);
        _choiceRoot = choiceRootObject.GetComponent<RectTransform>();

        _choicePromptText = ResolveOrCreateChoicePromptText(_choiceRoot);
        _defaultChoicePromptText = _choicePromptText != null ? _choicePromptText.text : string.Empty;

        _choiceTexts = new TMP_Text[2];
        _choiceButtons = new Button[2];
        _choiceBackgrounds = new Image[2];
        string[] buttonNames = { YesButtonName, NoButtonName };
        string[] labelNames = { YesButtonTextName, NoButtonTextName };

        for (int i = 0; i < 2; i++)
        {
            Transform existingButtonTransform = FindDeepChild(_choiceRoot, buttonNames[i]);
            GameObject choiceButtonObject = existingButtonTransform != null ? existingButtonTransform.gameObject : CreateUiObject(buttonNames[i], _choiceRoot, true);
            RectTransform choiceRect = choiceButtonObject.GetComponent<RectTransform>();
            if (existingButtonTransform == null)
            {
                choiceRect.anchorMin = new Vector2(0.5f, 0.5f);
                choiceRect.anchorMax = new Vector2(0.5f, 0.5f);
                choiceRect.pivot = new Vector2(0.5f, 0.5f);
                choiceRect.sizeDelta = new Vector2(300f, 48f);
                choiceRect.anchoredPosition = new Vector2(i == 0 ? -200f : 200f, -41f);
            }

            Image choiceImage = choiceButtonObject.GetComponent<Image>();
            if (choiceImage == null)
            {
                choiceImage = choiceButtonObject.AddComponent<Image>();
            }

            _choiceBackgrounds[i] = choiceImage;
            Button choiceButton = choiceButtonObject.GetComponent<Button>();
            if (choiceButton == null)
            {
                choiceButton = choiceButtonObject.AddComponent<Button>();
            }

            choiceButton.targetGraphic = choiceImage;
            _choiceButtons[i] = choiceButton;

            TMP_Text existingChoiceText = FindDeepChild(choiceButtonObject.transform, labelNames[i])?.GetComponent<TMP_Text>();
            TMP_Text choiceLabel = existingChoiceText ?? CreateLabel(labelNames[i], choiceButtonObject.transform, 24f, FontStyles.Normal, TextAlignmentOptions.Center, ChoiceNormalTextColor);
            RectTransform choiceTextRect = choiceLabel.rectTransform;
            choiceTextRect.anchorMin = Vector2.zero;
            choiceTextRect.anchorMax = Vector2.one;
            choiceTextRect.offsetMin = Vector2.zero;
            choiceTextRect.offsetMax = Vector2.zero;
            _choiceTexts[i] = choiceLabel;
        }

        EnsureChoiceButtonListeners();
    }

    private TMP_Text ResolveOrCreateChoicePromptText(Transform choiceRoot)
    {
        TMP_Text choicePromptText = FindDeepChild(choiceRoot, ChoicePromptTextName)?.GetComponent<TMP_Text>();
        if (choicePromptText != null)
        {
            return choicePromptText;
        }

        TMP_Text created = CreateLabel(ChoicePromptTextName, choiceRoot, 36f, FontStyles.Normal, TextAlignmentOptions.Center, Color.white);
        created.text = "선택하시겠습니까?";
        RectTransform promptRect = created.rectTransform;
        promptRect.anchorMin = new Vector2(0.5f, 0.5f);
        promptRect.anchorMax = new Vector2(0.5f, 0.5f);
        promptRect.pivot = new Vector2(0.5f, 0.5f);
        promptRect.sizeDelta = new Vector2(600f, 50f);
        promptRect.anchoredPosition = new Vector2(0f, 48f);
        return created;
    }

    private void EnsureChoiceButtonListeners()
    {
        if (_choiceButtons == null)
        {
            return;
        }

        for (int i = 0; i < _choiceButtons.Length; i++)
        {
            if (_choiceButtons[i] == null)
            {
                continue;
            }

            _choiceButtons[i].onClick.RemoveAllListeners();
            int capturedIndex = i;
            _choiceButtons[i].onClick.AddListener(() => HandleChoiceButtonClicked(capturedIndex));
        }
    }

    private void HandleChoiceButtonClicked(int index)
    {
        if (!_showingChoices || _currentRequest?.Choices == null || index < 0 || index >= _currentRequest.Choices.Length)
        {
            return;
        }

        _choiceIndex = index;
        RefreshChoiceUi();
        SubmitCurrentChoice();
    }

    private void SetDialogueBodyVisible(bool visible)
    {
        if (_bodyText != null)
        {
            _bodyText.gameObject.SetActive(visible);
        }
    }

    private static Transform FindDialoguePanelTransform()
    {
        GameObject canvasObject = GameObject.Find(DialogueCanvasName);
        if (canvasObject != null)
        {
            Transform direct = FindDeepChild(canvasObject.transform, DialoguePanelName);
            if (direct != null)
            {
                return direct;
            }
        }

        GameObject panelObject = GameObject.Find(DialoguePanelName);
        return panelObject != null ? panelObject.transform : null;
    }

    private static float GetTypingDelay(string line, int revealedCharacters)
    {
        if (string.IsNullOrEmpty(line) || revealedCharacters <= 0 || revealedCharacters > line.Length)
        {
            return TypingSkipDelay;
        }

        char currentCharacter = line[revealedCharacters - 1];
        float delay = 1f / TypingCharactersPerSecond;

        if (currentCharacter == '.' &&
            revealedCharacters >= 3 &&
            line[revealedCharacters - 2] == '.' &&
            line[revealedCharacters - 3] == '.')
        {
            return delay + TypingEllipsisPause;
        }

        switch (currentCharacter)
        {
            case ',':
            case ';':
            case ':':
                return delay + TypingCommaPause;
            case '.':
            case '!':
            case '?':
                return delay + TypingSentencePause;
            default:
                return delay;
        }
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

    private static GameObject CreateUiObject(string objectName, Transform parent, bool addImage = false)
    {
        Transform existing = parent.Find(objectName);
        if (existing != null)
        {
            return existing.gameObject;
        }

        GameObject go = addImage
            ? new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image))
            : new GameObject(objectName, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        go.layer = 5;
        return go;
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
        label.font = ResolveFont();
        label.fontSize = fontSize;
        label.fontStyle = fontStyle;
        label.color = color;
        label.alignment = alignment;
        label.raycastTarget = false;
        return label;
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
}
