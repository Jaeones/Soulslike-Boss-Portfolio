using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class UIItemPickupPanel : MonoBehaviour
{
    private const string CanvasRootName = "GameLoopUICanvas";
    private const string PanelName = "ItemPanel";
    private const string ItemNameTextName = "ItemNameText";
    private const string ItemImageName = "ItemImage";
    private const string RuntimeHostName = "UIItemPickupPanelRuntimeHost";

    private static UIItemPickupPanel _instance;

    private GameObject _panelRoot;
    private TMP_Text _itemNameText;
    private Image _itemImage;
    private string _defaultItemName;
    private Sprite _defaultItemSprite;
    private PlayerController _lockedPlayer;
    private Action _confirmAction;
    private KeyCode _confirmKey = KeyCode.E;
    private int _openedFrame = -1;
    private bool _isOpen;

    public static bool IsOpen => _instance != null && _instance._isOpen;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        _instance = null;
    }

    public static bool TryShow(
        PlayerController player,
        string itemName,
        KeyCode confirmKey,
        Action onConfirm,
        string actionText = null,
        Sprite itemSprite = null)
    {
        UIItemPickupPanel instance = EnsureInstance();
        return instance != null && instance.ShowInternal(player, itemName, confirmKey, onConfirm, actionText, itemSprite);
    }

    private static UIItemPickupPanel EnsureInstance()
    {
        if (_instance != null)
        {
            return _instance;
        }

        _instance = FindFirstObjectByType<UIItemPickupPanel>(FindObjectsInactive.Include);
        if (_instance != null)
        {
            _instance.EnsureUi();
            return _instance;
        }

        GameObject host = new GameObject(RuntimeHostName);
        _instance = host.AddComponent<UIItemPickupPanel>();
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
        if (!_isOpen || Time.frameCount == _openedFrame)
        {
            return;
        }

        if (Input.GetKeyDown(_confirmKey))
        {
            CloseInternal(confirmPickup: true);
        }
    }

    private void OnDisable()
    {
        if (_isOpen)
        {
            CloseInternal(confirmPickup: false);
        }
    }

    private void OnDestroy()
    {
        if (_instance == this)
        {
            _instance = null;
        }

        if (_isOpen)
        {
            CloseInternal(confirmPickup: false);
        }
    }

    private bool ShowInternal(
        PlayerController player,
        string itemName,
        KeyCode confirmKey,
        Action onConfirm,
        string actionText,
        Sprite itemSprite)
    {
        EnsureUi();
        if (_panelRoot == null || _itemNameText == null)
        {
            Debug.LogError("[UIItemPickupPanel] Could not bind ItemPanel UI.");
            return false;
        }

        if (_isOpen)
        {
            CloseInternal(confirmPickup: false);
        }

        _lockedPlayer = player;
        _confirmAction = onConfirm;
        _confirmKey = confirmKey == KeyCode.None ? KeyCode.E : confirmKey;
        _openedFrame = Time.frameCount;
        _isOpen = true;

        if (_lockedPlayer != null)
        {
            _lockedPlayer.BeginExternalInteractionLock();
            _lockedPlayer.Interaction?.ClearCandidates();
        }

        _itemNameText.text = string.IsNullOrWhiteSpace(itemName) ? _defaultItemName : itemName;

        if (_itemImage != null)
        {
            _itemImage.sprite = itemSprite != null ? itemSprite : _defaultItemSprite;
        }

        _panelRoot.SetActive(true);
        GameUiSfxPlayer.Play(UiSfxId.ItemPickupOpen);
        return true;
    }

    private void CloseInternal(bool confirmPickup)
    {
        Action confirmAction = confirmPickup ? _confirmAction : null;

        if (confirmPickup)
        {
            GameUiSfxPlayer.Play(UiSfxId.Confirm);
        }

        _confirmAction = null;
        _isOpen = false;
        _openedFrame = -1;

        RestoreDefaults();

        if (_panelRoot != null)
        {
            _panelRoot.SetActive(false);
        }

        if (_lockedPlayer != null)
        {
            _lockedPlayer.EndExternalInteractionLock();
            _lockedPlayer = null;
        }

        confirmAction?.Invoke();
    }

    private void EnsureUi()
    {
        if (_panelRoot != null && _itemNameText != null)
        {
            return;
        }

        if (!TryBindSceneUi())
        {
            return;
        }

        _defaultItemName = _itemNameText != null ? _itemNameText.text : string.Empty;
        _defaultItemSprite = _itemImage != null ? _itemImage.sprite : null;

        if (_panelRoot.activeSelf)
        {
            _panelRoot.SetActive(false);
        }
    }

    private bool TryBindSceneUi()
    {
        Transform panelTransform = FindItemPanelTransform();
        if (panelTransform == null)
        {
            return false;
        }

        _panelRoot = panelTransform.gameObject;
        _itemNameText = FindDeepChild(panelTransform, ItemNameTextName)?.GetComponent<TMP_Text>();
        _itemImage = FindDeepChild(panelTransform, ItemImageName)?.GetComponent<Image>();
        return _itemNameText != null;
    }

    private static Transform FindItemPanelTransform()
    {
        GameObject canvasRoot = GameObject.Find(CanvasRootName);
        if (canvasRoot != null)
        {
            Transform direct = FindDeepChild(canvasRoot.transform, PanelName);
            if (direct != null)
            {
                return direct;
            }
        }

        Transform[] transforms = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < transforms.Length; i++)
        {
            if (transforms[i] != null && transforms[i].name == PanelName)
            {
                return transforms[i];
            }
        }

        return null;
    }

    private void RestoreDefaults()
    {
        if (_itemNameText != null)
        {
            _itemNameText.text = _defaultItemName;
        }

        if (_itemImage != null)
        {
            _itemImage.sprite = _defaultItemSprite;
        }
    }

    private static Transform FindDeepChild(Transform root, string childName)
    {
        if (root == null || string.IsNullOrWhiteSpace(childName))
        {
            return null;
        }

        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i].name == childName)
            {
                return children[i];
            }
        }

        return null;
    }
}
