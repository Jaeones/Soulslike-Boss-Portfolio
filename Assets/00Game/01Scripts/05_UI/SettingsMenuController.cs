using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class SettingsMenuController : MonoBehaviour
{
    public enum SettingsTabType
    {
        Gameplay,
        Graphics,
        Audio,
        KeyBinding,
        Controller
    }

    [Header("Managed Roots")]
    [SerializeField] private GameObject pauseMenuRoot;
    [SerializeField] private GameObject settingUIRoot;
    [SerializeField] private GameObject gamePlayRoot;
    [SerializeField] private GameObject graphicsRoot;
    [SerializeField] private GameObject audioRoot;
    [SerializeField] private GameObject keyBindingRoot;
    [SerializeField] private GameObject controllerRoot;

    [Header("Overlay Helpers")]
    [SerializeField] private GameObject dimmer;
    [SerializeField] private GameObject inputBlocker;

    [Header("Defaults")]
    [SerializeField] private SettingsTabType defaultTab = SettingsTabType.Gameplay;
    [SerializeField] private bool initializeOnAwake = true;

    [Header("Input")]
    [SerializeField] private bool listenForEscapeInput = true;
    [SerializeField] private bool closeSettingsFirstOnEscape = true;
    [SerializeField] private KeyCode toggleKey = KeyCode.Escape;

    [Header("Pause")]
    [SerializeField] private bool pauseTimeScaleOnOpen = true;
    [SerializeField] private bool enablePauseMenuButtonNavigation = true;
    [SerializeField] private KeyCode pauseMenuUpKey = KeyCode.UpArrow;
    [SerializeField] private KeyCode pauseMenuDownKey = KeyCode.DownArrow;
    [SerializeField] private KeyCode pauseMenuAlternateUpKey = KeyCode.W;
    [SerializeField] private KeyCode pauseMenuAlternateDownKey = KeyCode.S;

    private bool _hasCapturedTimeScale;
    private float _cachedTimeScale = 1f;
    private bool _standaloneSettingsOnlyMode;
    private SettingsTabType _standaloneDefaultTab = SettingsTabType.Graphics;
    private SettingsTabType? _currentVisibleTab;
    private readonly List<Button> _pauseButtons = new List<Button>();
    private readonly List<Image> _pauseButtonImages = new List<Image>();
    private readonly List<Sprite> _pauseButtonNormalSprites = new List<Sprite>();
    private readonly Dictionary<Button, Sprite> _pauseButtonNormalSpriteMap = new Dictionary<Button, Sprite>();
    private EventSystem _eventSystem;
    private int _selectedPauseButtonIndex = -1;

    public bool IsPauseMenuOpen => pauseMenuRoot != null && pauseMenuRoot.activeSelf;
    public bool IsSettingsOpen => settingUIRoot != null && settingUIRoot.activeSelf;
    public bool IsAnyMenuOpen => IsPauseMenuOpen || IsSettingsOpen;
    public bool IsStandaloneSettingsVisible => _standaloneSettingsOnlyMode && gameObject.activeSelf && IsSettingsOpen;

    private void Awake()
    {
        EnsureEventSystemExists();
        CachePauseButtons();
        ValidateConfiguration();

        if (!initializeOnAwake)
        {
            ApplyCursorState();
            return;
        }

        InitializeMenuState();
        ApplyCursorState();
    }

    private void Update()
    {
        ApplyCursorState();

        if (IsPauseMenuOpen)
        {
            HandlePauseMenuNavigationInput();
            SyncPauseButtonSelectionFromEventSystem();
        }

        if (!listenForEscapeInput || !Input.GetKeyDown(toggleKey))
        {
            return;
        }

        HandleEscapeInput();
    }

    public void InitializeMenuState()
    {
        HideAllTabs();
        SetActiveIfAssigned(settingUIRoot, false);
        SetActiveIfAssigned(pauseMenuRoot, false);
        SetOverlayState(false);
        ClearPauseButtonSelection(false);
        ExitPausedState();
        ApplyCursorState();
    }

    public void OpenPauseMenu()
    {
        if (_standaloneSettingsOnlyMode)
        {
            OpenStandaloneSettings();
            return;
        }

        GameUiSfxPlayer.Play(UiSfxId.PauseOpen);
        EnterPausedState();
        SetActiveIfAssigned(settingUIRoot, false);
        SetActiveIfAssigned(pauseMenuRoot, true);
        HideAllTabs();
        SetOverlayState(true);
        SelectDefaultPauseButton();
        ApplyCursorState();
    }

    public void ClosePauseMenu()
    {
        if (_standaloneSettingsOnlyMode)
        {
            CloseStandaloneSettings();
            return;
        }

        GameUiSfxPlayer.Play(UiSfxId.PauseClose);
        SetActiveIfAssigned(pauseMenuRoot, false);
        SetActiveIfAssigned(settingUIRoot, false);
        HideAllTabs();
        SetOverlayState(false);
        ClearPauseButtonSelection(true);
        ExitPausedState();
        ApplyCursorState();
    }

    public void OpenSettings()
    {
        if (_standaloneSettingsOnlyMode)
        {
            OpenStandaloneSettings();
            return;
        }

        GameUiSfxPlayer.Play(UiSfxId.Confirm);
        EnterPausedState();
        SetActiveIfAssigned(pauseMenuRoot, false);
        SetActiveIfAssigned(settingUIRoot, true);
        ClearPauseButtonSelection(false);
        ShowTab(defaultTab);
        ApplyCursorState();
    }

    public void CloseSettingsToPause()
    {
        if (_standaloneSettingsOnlyMode)
        {
            CloseStandaloneSettings();
            return;
        }

        GameUiSfxPlayer.Play(UiSfxId.Back);
        HideAllTabs();
        SetActiveIfAssigned(settingUIRoot, false);

        if (pauseMenuRoot != null)
        {
            SetActiveIfAssigned(pauseMenuRoot, true);
            SetOverlayState(true);
            SelectDefaultPauseButton();
            ApplyCursorState();
            return;
        }

        SetOverlayState(false);
        ExitPausedState();
        ApplyCursorState();
    }

    public void ConfigureStandaloneSettingsMode(bool enabled, SettingsTabType defaultStandaloneTab = SettingsTabType.Graphics)
    {
        _standaloneSettingsOnlyMode = enabled;
        _standaloneDefaultTab = defaultStandaloneTab;
    }

    public void OpenStandaloneSettings()
    {
        GameUiSfxPlayer.Play(UiSfxId.PauseOpen);
        SetRootActive(true);
        SetActiveIfAssigned(pauseMenuRoot, false);
        ShowTab(_standaloneDefaultTab);
        ApplyCursorState();
    }

    public void CloseStandaloneSettings()
    {
        GameUiSfxPlayer.Play(UiSfxId.Back);
        HideAllTabs();
        SetActiveIfAssigned(settingUIRoot, false);
        SetActiveIfAssigned(pauseMenuRoot, false);
        SetOverlayState(false);
        ClearPauseButtonSelection(true);
        ExitPausedState();
        SetRootActive(false);
        ApplyCursorState();
    }

    public void ShowGameplayTab()
    {
        ShowTab(SettingsTabType.Gameplay);
    }

    public void ShowGraphicsTab()
    {
        ShowTab(SettingsTabType.Graphics);
    }

    public void ShowAudioTab()
    {
        ShowTab(SettingsTabType.Audio);
    }

    public void ShowKeyBindingTab()
    {
        ShowTab(SettingsTabType.KeyBinding);
    }

    public void ShowControllerTab()
    {
        ShowTab(SettingsTabType.Controller);
    }

    public void ReturnToTitle()
    {
        GameUiSfxPlayer.Play(UiSfxId.Confirm);

        HideAllTabs();
        SetActiveIfAssigned(settingUIRoot, false);
        SetActiveIfAssigned(pauseMenuRoot, false);
        SetOverlayState(false);
        ClearPauseButtonSelection(true);
        ExitPausedState();
        ApplyCursorState();

        string activeSceneName = TitleFlowConstants.NormalizeSceneName(SceneManager.GetActiveScene().name);
        if (string.Equals(activeSceneName, TitleFlowConstants.TitleSceneName, System.StringComparison.Ordinal))
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
            return;
        }

        if (SceneFlowManager.Instance != null &&
            SceneFlowManager.Instance.TryLoadScene(TitleFlowConstants.TitleSceneName))
        {
            return;
        }

        SceneManager.LoadScene(TitleFlowConstants.TitleSceneName);
    }

    public void ShowTab(SettingsTabType tab)
    {
        bool shouldPlayMoveSfx = _currentVisibleTab.HasValue && _currentVisibleTab.Value != tab;
        EnterPausedState();
        SetActiveIfAssigned(settingUIRoot, true);
        SetActiveIfAssigned(pauseMenuRoot, false);
        HideAllTabs();

        GameObject target = GetTabRoot(tab);
        if (target != null)
        {
            target.SetActive(true);
        }

        SetOverlayState(true);
        _currentVisibleTab = tab;
        ApplyCursorState();

        if (shouldPlayMoveSfx)
        {
            GameUiSfxPlayer.Play(UiSfxId.Move);
        }
    }

    private void HandleEscapeInput()
    {
        if (_standaloneSettingsOnlyMode)
        {
            if (IsSettingsOpen)
            {
                CloseStandaloneSettings();
            }

            return;
        }

        if (IsSettingsOpen && closeSettingsFirstOnEscape)
        {
            CloseSettingsToPause();
            return;
        }

        if (IsPauseMenuOpen)
        {
            ClosePauseMenu();
            return;
        }

        OpenPauseMenu();
    }

    private GameObject GetTabRoot(SettingsTabType tab)
    {
        switch (tab)
        {
            case SettingsTabType.Gameplay:
                return gamePlayRoot;
            case SettingsTabType.Graphics:
                return graphicsRoot;
            case SettingsTabType.Audio:
                return audioRoot;
            case SettingsTabType.KeyBinding:
                return keyBindingRoot;
            case SettingsTabType.Controller:
                return controllerRoot;
            default:
                return null;
        }
    }

    private void HideAllTabs()
    {
        SetActiveIfAssigned(gamePlayRoot, false);
        SetActiveIfAssigned(graphicsRoot, false);
        SetActiveIfAssigned(audioRoot, false);
        SetActiveIfAssigned(keyBindingRoot, false);
        SetActiveIfAssigned(controllerRoot, false);
        _currentVisibleTab = null;
    }

    private void SetOverlayState(bool isVisible)
    {
        SetActiveIfAssigned(dimmer, isVisible);
        SetActiveIfAssigned(inputBlocker, isVisible);
    }

    private void EnterPausedState()
    {
        if (!ShouldPauseTimeScale() || _hasCapturedTimeScale)
        {
            return;
        }

        _cachedTimeScale = Time.timeScale;
        _hasCapturedTimeScale = true;
        Time.timeScale = 0f;
    }

    private void ExitPausedState()
    {
        if (!ShouldPauseTimeScale() || !_hasCapturedTimeScale)
        {
            return;
        }

        Time.timeScale = _cachedTimeScale;
        _hasCapturedTimeScale = false;
        _cachedTimeScale = 1f;
    }

    private bool ShouldPauseTimeScale()
    {
        return pauseTimeScaleOnOpen && !_standaloneSettingsOnlyMode;
    }

    private void SetRootActive(bool isActive)
    {
        if (gameObject.activeSelf != isActive)
        {
            gameObject.SetActive(isActive);
        }
    }

    private static void SetActiveIfAssigned(GameObject target, bool isActive)
    {
        if (target != null)
        {
            target.SetActive(isActive);
        }
    }

    private void ValidateConfiguration()
    {
        SceneReferenceValidator.ValidateOnce(
            this,
            "SettingsMenuController",
            SceneReferenceValidator.Required("settingUIRoot", settingUIRoot, "SettingUI 전체 루트"),
            SceneReferenceValidator.Required("gamePlayRoot", gamePlayRoot, "Gameplay 탭 루트"),
            SceneReferenceValidator.Required("graphicsRoot", graphicsRoot, "Graphics 탭 루트"),
            SceneReferenceValidator.Required("audioRoot", audioRoot, "Audio 탭 루트"),
            SceneReferenceValidator.Required("keyBindingRoot", keyBindingRoot, "KeyBinding 탭 루트"),
            SceneReferenceValidator.Required("controllerRoot", controllerRoot, "Controller 탭 루트"),
            SceneReferenceValidator.Recommended("pauseMenuRoot", pauseMenuRoot, "CloseSettingsToPause 복귀 대상"),
            SceneReferenceValidator.Recommended("dimmer", dimmer, "메뉴 오버레이 배경"),
            SceneReferenceValidator.Recommended("inputBlocker", inputBlocker, "HUD 입력 차단")
        );
    }

    private void ApplyCursorState()
    {
        if (_standaloneSettingsOnlyMode)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            return;
        }

        if (IsAnyMenuOpen)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            return;
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void LateUpdate()
    {
        if (!IsPauseMenuOpen)
        {
            return;
        }

        RefreshPauseButtonVisuals();
    }

    private void HandlePauseMenuNavigationInput()
    {
        if (!enablePauseMenuButtonNavigation)
        {
            return;
        }

        CachePauseButtons();
        if (_pauseButtons.Count == 0)
        {
            return;
        }

        int direction = 0;
        if (Input.GetKeyDown(pauseMenuUpKey) || Input.GetKeyDown(pauseMenuAlternateUpKey))
        {
            direction = -1;
        }
        else if (Input.GetKeyDown(pauseMenuDownKey) || Input.GetKeyDown(pauseMenuAlternateDownKey))
        {
            direction = 1;
        }

        if (direction == 0)
        {
            return;
        }

        int targetIndex = _selectedPauseButtonIndex;
        if (targetIndex < 0)
        {
            targetIndex = direction > 0 ? 0 : _pauseButtons.Count - 1;
        }
        else
        {
            targetIndex = Mathf.Clamp(targetIndex + direction, 0, _pauseButtons.Count - 1);
        }

        SelectPauseButton(targetIndex, true, true);
    }

    private void EnsureEventSystemExists()
    {
        _eventSystem = EventSystem.current;
        if (_eventSystem != null)
        {
            return;
        }

        EventSystem[] existingEventSystems = FindObjectsByType<EventSystem>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        if (existingEventSystems.Length > 0)
        {
            _eventSystem = existingEventSystems[0];
            return;
        }

        GameObject eventSystemRoot = new GameObject("EventSystem");
        _eventSystem = eventSystemRoot.AddComponent<EventSystem>();
        eventSystemRoot.AddComponent<StandaloneInputModule>();
    }

    private void CachePauseButtons()
    {
        _pauseButtons.Clear();
        _pauseButtonImages.Clear();
        _pauseButtonNormalSprites.Clear();

        if (pauseMenuRoot == null)
        {
            return;
        }

        Button[] buttons = pauseMenuRoot.GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i] == null || !buttons[i].interactable)
            {
                continue;
            }

            Image image = buttons[i].targetGraphic as Image;
            if (image == null)
            {
                image = buttons[i].GetComponent<Image>();
            }

            if (image == null)
            {
                continue;
            }

            _pauseButtons.Add(buttons[i]);
            if (!_pauseButtonNormalSpriteMap.ContainsKey(buttons[i]))
            {
                _pauseButtonNormalSpriteMap.Add(buttons[i], image.sprite);
            }
        }

        _pauseButtons.Sort((left, right) => GetButtonSortY(right).CompareTo(GetButtonSortY(left)));

        _pauseButtonImages.Clear();
        _pauseButtonNormalSprites.Clear();
        for (int i = 0; i < _pauseButtons.Count; i++)
        {
            Image image = _pauseButtons[i].targetGraphic as Image;
            if (image == null)
            {
                image = _pauseButtons[i].GetComponent<Image>();
            }

            _pauseButtonImages.Add(image);
            _pauseButtonNormalSprites.Add(_pauseButtonNormalSpriteMap.TryGetValue(_pauseButtons[i], out Sprite normalSprite)
                ? normalSprite
                : (image != null ? image.sprite : null));
        }
    }

    private void SelectDefaultPauseButton()
    {
        CachePauseButtons();
        if (_pauseButtons.Count == 0)
        {
            return;
        }

        int selectedIndex = GetPauseButtonIndex(_eventSystem != null ? _eventSystem.currentSelectedGameObject : null);
        if (selectedIndex < 0)
        {
            selectedIndex = 0;
        }

        SelectPauseButton(selectedIndex, true, false);
    }

    private void SelectPauseButton(int buttonIndex, bool updateEventSystemSelection, bool playMoveSfx)
    {
        CachePauseButtons();
        if (buttonIndex < 0 || buttonIndex >= _pauseButtons.Count)
        {
            return;
        }

        if (_selectedPauseButtonIndex == buttonIndex)
        {
            RefreshPauseButtonVisuals();
            return;
        }

        _selectedPauseButtonIndex = buttonIndex;
        RefreshPauseButtonVisuals();

        if (updateEventSystemSelection)
        {
            EnsureEventSystemExists();
            if (_eventSystem != null)
            {
                _eventSystem.SetSelectedGameObject(_pauseButtons[buttonIndex].gameObject);
            }
        }

        if (playMoveSfx)
        {
            GameUiSfxPlayer.Play(UiSfxId.Move);
        }
    }

    private void ClearPauseButtonSelection(bool clearEventSystemSelection)
    {
        _selectedPauseButtonIndex = -1;
        RefreshPauseButtonVisuals();

        if (!clearEventSystemSelection)
        {
            return;
        }

        EnsureEventSystemExists();
        if (_eventSystem == null)
        {
            return;
        }

        if (GetPauseButtonIndex(_eventSystem.currentSelectedGameObject) >= 0)
        {
            _eventSystem.SetSelectedGameObject(null);
        }
    }

    private void SyncPauseButtonSelectionFromEventSystem()
    {
        EnsureEventSystemExists();
        if (_eventSystem == null)
        {
            return;
        }

        int selectedIndex = GetPauseButtonIndex(_eventSystem.currentSelectedGameObject);
        if (selectedIndex >= 0 && selectedIndex != _selectedPauseButtonIndex)
        {
            SelectPauseButton(selectedIndex, false, false);
        }
    }

    private void RefreshPauseButtonVisuals()
    {
        if (_pauseButtons.Count == 0 || _pauseButtonImages.Count != _pauseButtons.Count)
        {
            return;
        }

        for (int i = 0; i < _pauseButtons.Count; i++)
        {
            Image image = _pauseButtonImages[i];
            if (image == null)
            {
                continue;
            }

            Sprite selectedSprite = _pauseButtons[i].spriteState.selectedSprite;
            Sprite targetSprite = i == _selectedPauseButtonIndex && selectedSprite != null
                ? selectedSprite
                : _pauseButtonNormalSprites[i];

            if (image.sprite != targetSprite)
            {
                image.sprite = targetSprite;
            }
        }
    }

    private int GetPauseButtonIndex(GameObject selectedObject)
    {
        if (selectedObject == null)
        {
            return -1;
        }

        Button selectedButton = selectedObject.GetComponent<Button>();
        if (selectedButton == null)
        {
            selectedButton = selectedObject.GetComponentInParent<Button>();
        }

        if (selectedButton == null)
        {
            return -1;
        }

        for (int i = 0; i < _pauseButtons.Count; i++)
        {
            if (_pauseButtons[i] == selectedButton)
            {
                return i;
            }
        }

        return -1;
    }

    private static float GetButtonSortY(Button button)
    {
        RectTransform rectTransform = button != null ? button.transform as RectTransform : null;
        return rectTransform != null ? rectTransform.anchoredPosition.y : float.MinValue;
    }
}
