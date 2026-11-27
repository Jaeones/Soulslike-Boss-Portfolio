using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

public sealed class TitleMenuController : MonoBehaviour
{
    private sealed class MenuEntryBinding
    {
        public Button Button;
        public GameObject HoverRoot;
        public GameObject SelectedLine;
        public TMP_Text Label;
        public Action Invoke;
        public Func<bool> IsInteractable;
    }

    private const string NewGameLabel = "NewGame";
    private const string LegacyPlayLabel = "Play";
    private const string ContinueLabel = "Load Game";
    private const string ExitLabel = "Exit";
    private const string LeaveLabel = "Leave";
    private const string SettingLabel = "Setting";
    private const string HelpLabel = "Help";
    private const string SideTabMenuName = "Side_TabMenu";
    private const string SettingsButtonName = "Button_Setting";
    private const string LegacySubMenuName = "Sub_Menu";
    private const string MenuOverlayCanvasName = "MenuOverlayCanvas";

    private Canvas _rootCanvas;
    private Button _playButton;
    private Button _continueButton;
    private Button _exitButton;
    private Button _settingsButton;
    private TMP_Text _playEntryLabel;
    private TMP_Text _continueEntryLabel;
    private TMP_Text _exitEntryLabel;
    private TMP_Text _settingsEntryLabel;
    private GameObject _legacySubMenu;
    private SettingsMenuController _menuOverlayController;
    private readonly List<MenuEntryBinding> _menuBindings = new List<MenuEntryBinding>();
    private readonly List<RaycastResult> _raycastResults = new List<RaycastResult>();
    private PointerEventData _pointerEventData;
    private MenuEntryBinding _currentHoveredBinding;
    private bool _usingKeyboardNavigation;
    private int _keyboardBindingIndex = -1;
    private Vector3 _lastMousePosition;
    private bool _hasLastMousePosition;
    private Color _selectedTextColor = Color.white;
    private static readonly Color DefaultUnselectedTextColor = Color.white;

    private void Awake()
    {
        _ = SceneFlowManager.Instance;
        _ = SaveGameService.Instance;
        _ = OptionsService.Instance;

        Application.targetFrameRate = 60;
    }

    private void Start()
    {
        CacheSceneReferences();
        ConfigureMenuOverlayForTitle();
        WireButtons();
        HideLegacyPanels();
        RefreshUI();
    }

    private void OnEnable()
    {
        RefreshUI();
    }

    private void OnDisable()
    {
        _usingKeyboardNavigation = false;
        _keyboardBindingIndex = -1;
        _hasLastMousePosition = false;
        SetHoveredBinding(null);
    }

    private void Update()
    {
        if (SceneFlowManager.Instance.IsLoading)
        {
            SetHoveredBinding(null);
            return;
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (IsOptionsOpen())
            {
                GameUiSfxPlayer.Play(UiSfxId.Back);
                SetOptionsVisible(false);
            }
        }

        UpdateNavigationInputMode();
        UpdateHoveredBindingVisual();
        TryHandlePointerActivation();
    }

    private void CacheSceneReferences()
    {
        _rootCanvas = GetComponent<Canvas>();
        if (_rootCanvas == null)
        {
            _rootCanvas = GetComponentInParent<Canvas>();
        }

        if (_rootCanvas == null)
        {
            _rootCanvas = FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
        }

        EnsureRootCanvasRaycaster();

        Transform searchRoot = _rootCanvas != null ? _rootCanvas.transform : transform;
        Transform menuRoot = FindDescendantByName(searchRoot, SideTabMenuName);
        Transform buttonSearchRoot = menuRoot != null ? menuRoot : searchRoot;

        _playButton = FindButtonByText(buttonSearchRoot, NewGameLabel);
        if (_playButton == null)
        {
            _playButton = FindButtonByText(buttonSearchRoot, LegacyPlayLabel);
        }
        _continueButton = FindButtonByText(buttonSearchRoot, ContinueLabel);
        _exitButton = FindButtonByText(buttonSearchRoot, ExitLabel);
        if (_exitButton == null)
        {
            _exitButton = FindButtonByText(buttonSearchRoot, LeaveLabel);
        }

        _settingsButton = FindButtonByName(buttonSearchRoot, SettingsButtonName);
        if (_settingsButton == null)
        {
            _settingsButton = FindButtonByName(searchRoot, SettingsButtonName);
        }

        _playEntryLabel = _playButton != null ? FindTextUnderButton(_playButton) : FindTextByContent(buttonSearchRoot, NewGameLabel);
        if (_playEntryLabel == null)
        {
            _playEntryLabel = FindTextByContent(buttonSearchRoot, LegacyPlayLabel);
        }
        _continueEntryLabel = _continueButton != null ? FindTextUnderButton(_continueButton) : FindTextByContent(buttonSearchRoot, ContinueLabel);
        _exitEntryLabel = _exitButton != null ? FindTextUnderButton(_exitButton) : FindTextByContent(buttonSearchRoot, ExitLabel);
        if (_exitEntryLabel == null)
        {
            _exitEntryLabel = FindTextByContent(buttonSearchRoot, LeaveLabel);
        }

        _settingsEntryLabel = FindTextByContent(buttonSearchRoot, SettingLabel);
        if (_settingsEntryLabel == null)
        {
            _settingsEntryLabel = FindTextByContent(buttonSearchRoot, HelpLabel);
        }
        if (_settingsEntryLabel == null && _settingsButton != null)
        {
            _settingsEntryLabel = FindTextUnderButton(_settingsButton);
        }

        Transform legacySubMenu = FindDescendantByName(searchRoot, LegacySubMenuName);
        _legacySubMenu = legacySubMenu != null ? legacySubMenu.gameObject : null;
        _menuOverlayController = FindMenuOverlayController();

        CacheMenuBindings();
    }

    private void EnsureRootCanvasRaycaster()
    {
        if (_rootCanvas == null)
        {
            return;
        }

        if (_rootCanvas.GetComponent<GraphicRaycaster>() != null)
        {
            return;
        }

        _rootCanvas.gameObject.AddComponent<GraphicRaycaster>();
    }

    private void ConfigureMenuOverlayForTitle()
    {
        if (_menuOverlayController == null)
        {
            return;
        }

        _menuOverlayController.ConfigureStandaloneSettingsMode(true, SettingsMenuController.SettingsTabType.Graphics);
        _menuOverlayController.InitializeMenuState();
        _menuOverlayController.gameObject.SetActive(false);
    }

    private void WireButtons()
    {
        if (_playButton != null)
        {
            _playButton.onClick.RemoveListener(HandleNewGame);
            _playButton.onClick.AddListener(HandleNewGame);
        }

        if (_continueButton != null)
        {
            _continueButton.onClick.RemoveListener(HandleContinue);
            _continueButton.onClick.AddListener(HandleContinue);
        }

        if (_exitButton != null)
        {
            _exitButton.onClick.RemoveListener(HandleQuit);
            _exitButton.onClick.AddListener(HandleQuit);
        }

        if (_settingsButton != null)
        {
            _settingsButton.onClick.RemoveListener(ToggleOptions);
            _settingsButton.onClick.AddListener(ToggleOptions);
        }
    }

    private void HideLegacyPanels()
    {
        if (_legacySubMenu != null)
        {
            _legacySubMenu.SetActive(false);
        }
    }

    private void CacheMenuBindings()
    {
        _menuBindings.Clear();
        CacheSelectedTextColor();

        RegisterMenuBinding(_playButton, _playEntryLabel, HandleNewGame, () => true);
        RegisterMenuBinding(_continueButton, _continueEntryLabel, HandleContinue, () => SaveGameService.Instance.HasSave);
        RegisterMenuBinding(_settingsEntryLabel != null ? null : _settingsButton, _settingsEntryLabel, ToggleOptions, () => true);
        RegisterMenuBinding(_exitButton, _exitEntryLabel, HandleQuit, () => true);

        _keyboardBindingIndex = -1;
        SetHoveredBinding(null);
    }

    private void CacheSelectedTextColor()
    {
        TMP_Text playLabel = _playEntryLabel;
        if (playLabel == null)
        {
            return;
        }

        _selectedTextColor = playLabel.color;
    }

    private void RegisterMenuBinding(Button button, TMP_Text label, Action invoke, Func<bool> isInteractable)
    {
        Transform hoverTransform = button != null ? button.transform : label != null ? label.transform : null;
        if (hoverTransform == null)
        {
            return;
        }

        Transform selectedLine = FindDescendantByName(hoverTransform, "Selected_line");
        MenuEntryBinding binding = new MenuEntryBinding
        {
            Button = button,
            HoverRoot = hoverTransform.gameObject,
            SelectedLine = selectedLine != null ? selectedLine.gameObject : null,
            Label = label,
            Invoke = invoke,
            IsInteractable = isInteractable
        };

        if (binding.SelectedLine != null)
        {
            binding.SelectedLine.SetActive(false);
        }

        if (binding.Label != null)
        {
            binding.Label.color = DefaultUnselectedTextColor;
        }

        _menuBindings.Add(binding);
    }

    private void RefreshUI()
    {
        bool hasSave = SaveGameService.Instance.HasSave;

        if (_continueButton != null)
        {
            _continueButton.interactable = hasSave;
        }

        if (_continueEntryLabel != null)
        {
            _continueEntryLabel.text = hasSave ? ContinueLabel : "Load Game (No Save)";
        }

        MenuEntryBinding selectedBinding = GetCurrentKeyboardBinding();
        if (selectedBinding != null && !IsBindingInteractable(selectedBinding))
        {
            _keyboardBindingIndex = GetFirstInteractableBindingIndex();
        }
    }

    private void ToggleOptions()
    {
        GameUiSfxPlayer.Play(UiSfxId.Confirm);
        SetOptionsVisible(!IsOptionsOpen());
    }

    private bool IsOptionsOpen()
    {
        return _menuOverlayController != null && _menuOverlayController.IsStandaloneSettingsVisible;
    }

    private void SetOptionsVisible(bool visible)
    {
        if (_menuOverlayController == null)
        {
            return;
        }

        if (visible)
        {
            _menuOverlayController.OpenStandaloneSettings();
            return;
        }

        _menuOverlayController.CloseStandaloneSettings();
    }

    private void UpdateHoveredBindingVisual()
    {
        if (EventSystem.current == null)
        {
            SetHoveredBinding(null);
            return;
        }

        MenuEntryBinding hoveredBinding = GetBindingUnderMouse();

        if (_usingKeyboardNavigation)
        {
            if (GetCurrentKeyboardBinding() == null)
            {
                _keyboardBindingIndex = GetFirstInteractableBindingIndex();
            }

            SetHoveredBinding(GetCurrentKeyboardBinding());
            return;
        }

        SetHoveredBinding(hoveredBinding);
    }

    private void UpdateNavigationInputMode()
    {
        if (DidMouseInputOccur())
        {
            _usingKeyboardNavigation = false;
            return;
        }

        int direction = GetKeyboardNavigationDirection();
        bool activate = DidKeyboardActivationInputOccur();
        if (direction == 0 && !activate)
        {
            return;
        }

        _usingKeyboardNavigation = true;

        if (direction != 0)
        {
            MoveKeyboardSelection(direction);
        }

        if (activate)
        {
            TryInvokeBinding(GetCurrentKeyboardBinding());
        }
    }

    private bool DidMouseInputOccur()
    {
        Vector3 mousePosition = Input.mousePosition;
        bool mouseMoved = _hasLastMousePosition && (_lastMousePosition - mousePosition).sqrMagnitude > 0.01f;
        _lastMousePosition = mousePosition;
        _hasLastMousePosition = true;

        return mouseMoved ||
               Input.GetMouseButtonDown(0) ||
               Input.GetMouseButtonDown(1) ||
               Input.GetMouseButtonDown(2);
    }

    private static int GetKeyboardNavigationDirection()
    {
        if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W))
        {
            return -1;
        }

        if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S))
        {
            return 1;
        }

        return 0;
    }

    private static bool DidKeyboardActivationInputOccur()
    {
        return Input.GetKeyDown(KeyCode.Return) ||
               Input.GetKeyDown(KeyCode.KeypadEnter) ||
               Input.GetKeyDown(KeyCode.Space);
    }

    private void MoveKeyboardSelection(int direction)
    {
        if (_menuBindings.Count == 0)
        {
            return;
        }

        if (_keyboardBindingIndex < 0)
        {
            _keyboardBindingIndex = GetFirstInteractableBindingIndex();
            return;
        }

        int index = _keyboardBindingIndex;
        for (int i = 0; i < _menuBindings.Count; i++)
        {
            index += direction;
            if (index < 0)
            {
                index = _menuBindings.Count - 1;
            }
            else if (index >= _menuBindings.Count)
            {
                index = 0;
            }

            if (IsBindingInteractable(_menuBindings[index]))
            {
                _keyboardBindingIndex = index;
                return;
            }
        }
    }

    private MenuEntryBinding GetBindingUnderMouse()
    {
        if (_pointerEventData == null)
        {
            _pointerEventData = new PointerEventData(EventSystem.current);
        }

        _pointerEventData.Reset();
        _pointerEventData.position = Input.mousePosition;
        _raycastResults.Clear();
        EventSystem.current.RaycastAll(_pointerEventData, _raycastResults);

        for (int i = 0; i < _raycastResults.Count; i++)
        {
            GameObject hitObject = _raycastResults[i].gameObject;
            if (hitObject == null)
            {
                continue;
            }

            MenuEntryBinding binding = GetBindingForHitObject(hitObject);
            if (binding != null && IsBindingInteractable(binding))
            {
                return binding;
            }
        }

        return null;
    }

    private MenuEntryBinding GetBindingForHitObject(GameObject hitObject)
    {
        Transform hitTransform = hitObject.transform;
        for (int i = 0; i < _menuBindings.Count; i++)
        {
            MenuEntryBinding binding = _menuBindings[i];
            if (binding.HoverRoot == null)
            {
                continue;
            }

            Transform hoverTransform = binding.HoverRoot.transform;
            if (hitTransform == hoverTransform || hitTransform.IsChildOf(hoverTransform))
            {
                return binding;
            }
        }

        return null;
    }

    private void SetHoveredBinding(MenuEntryBinding binding)
    {
        if (_currentHoveredBinding == binding)
        {
            return;
        }

        bool shouldPlayMoveSfx = binding != null && binding != _currentHoveredBinding;
        _currentHoveredBinding = binding;
        for (int i = 0; i < _menuBindings.Count; i++)
        {
            MenuEntryBinding current = _menuBindings[i];
            bool isCurrent = current == _currentHoveredBinding;

            if (current.SelectedLine != null)
            {
                current.SelectedLine.SetActive(isCurrent);
            }

            if (current.Label != null)
            {
                current.Label.color = isCurrent ? _selectedTextColor : DefaultUnselectedTextColor;
            }
        }

        if (shouldPlayMoveSfx)
        {
            GameUiSfxPlayer.Play(UiSfxId.Move);
        }
    }

    private MenuEntryBinding GetCurrentKeyboardBinding()
    {
        if (_keyboardBindingIndex < 0 || _keyboardBindingIndex >= _menuBindings.Count)
        {
            return null;
        }

        return _menuBindings[_keyboardBindingIndex];
    }

    private int GetFirstInteractableBindingIndex()
    {
        for (int i = 0; i < _menuBindings.Count; i++)
        {
            if (IsBindingInteractable(_menuBindings[i]))
            {
                return i;
            }
        }

        return -1;
    }

    private bool IsBindingInteractable(MenuEntryBinding binding)
    {
        return binding != null && (binding.IsInteractable == null || binding.IsInteractable());
    }

    private void TryHandlePointerActivation()
    {
        if (_usingKeyboardNavigation || !Input.GetMouseButtonDown(0))
        {
            return;
        }

        if (_currentHoveredBinding == null || _currentHoveredBinding.Button != null)
        {
            return;
        }

        TryInvokeBinding(_currentHoveredBinding);
    }

    private void TryInvokeBinding(MenuEntryBinding binding)
    {
        if (!IsBindingInteractable(binding) || binding.Invoke == null)
        {
            return;
        }

        binding.Invoke();
    }

    private void HandleNewGame()
    {
        GameUiSfxPlayer.Play(UiSfxId.Confirm);
        SetOptionsVisible(false);
        SceneFlowManager.Instance.TryLoadNewGame();
    }

    private void HandleContinue()
    {
        if (!SaveGameService.Instance.HasSave)
        {
            return;
        }

        GameUiSfxPlayer.Play(UiSfxId.Confirm);
        SetOptionsVisible(false);
        SceneFlowManager.Instance.TryLoadContinue();
        RefreshUI();
    }

    private void HandleQuit()
    {
        GameUiSfxPlayer.Play(UiSfxId.Confirm);
#if UNITY_EDITOR
        EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private static SettingsMenuController FindMenuOverlayController()
    {
        SettingsMenuController[] controllers = FindObjectsByType<SettingsMenuController>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        for (int i = 0; i < controllers.Length; i++)
        {
            if (controllers[i] != null && controllers[i].gameObject.name == MenuOverlayCanvasName)
            {
                return controllers[i];
            }
        }

        return controllers.Length > 0 ? controllers[0] : null;
    }

    private static Button FindButtonByText(Transform root, string label)
    {
        if (root == null)
        {
            return null;
        }

        string normalizedTarget = Normalize(label);
        Button[] buttons = root.GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            TMP_Text[] texts = buttons[i].GetComponentsInChildren<TMP_Text>(true);
            for (int j = 0; j < texts.Length; j++)
            {
                if (Normalize(texts[j].text) == normalizedTarget)
                {
                    return buttons[i];
                }
            }
        }

        return null;
    }

    private static Button FindButtonByName(Transform root, string objectName)
    {
        if (root == null)
        {
            return null;
        }

        Button[] buttons = root.GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i].name == objectName)
            {
                return buttons[i];
            }
        }

        return null;
    }

    private static TMP_Text FindTextUnderButton(Button button)
    {
        if (button == null)
        {
            return null;
        }

        TMP_Text[] texts = button.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(texts[i].text))
            {
                return texts[i];
            }
        }

        return null;
    }

    private static TMP_Text FindTextByContent(Transform root, string label)
    {
        if (root == null)
        {
            return null;
        }

        string normalizedTarget = Normalize(label);
        TMP_Text[] texts = root.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            if (Normalize(texts[i].text) == normalizedTarget)
            {
                return texts[i];
            }
        }

        return null;
    }

    private static Transform FindDescendantByName(Transform root, string objectName)
    {
        if (root == null)
        {
            return null;
        }

        if (root.name == objectName)
        {
            return root;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform result = FindDescendantByName(root.GetChild(i), objectName);
            if (result != null)
            {
                return result;
            }
        }

        return null;
    }

    private static string Normalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return value.Replace(" ", string.Empty).Replace("\n", string.Empty).Trim().ToLowerInvariant();
    }
}
