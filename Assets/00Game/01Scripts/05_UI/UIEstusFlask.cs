using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UIEstusFlask : MonoBehaviour
{
    [Header("Binding")]
    [SerializeField] private PlayerController player;
    [SerializeField] private GameObject root;
    [SerializeField] private bool hideWhenPlayerMissing;

    [Header("UI References")]
    [SerializeField] private TMP_Text countText;
    [SerializeField] private Image[] useSlots;

    [Header("Colors")]
    [SerializeField] private Color availableColor = Color.white;
    [SerializeField] private Color emptyColor = new Color(1f, 1f, 1f, 0.2f);

    private PlayerController _boundPlayer;
    private UIBindingMediator _bindingMediator;

    private void Awake()
    {
        EnsureReferences();

        _bindingMediator = new UIBindingMediator(BindPlayer, UnbindPlayer);
        ValidateSceneReferences(true);
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
        _bindingMediator.Rebind(player);
        RefreshUI();
    }

    private void OnDisable()
    {
        GameLoopEvents.PlayerRespawned -= HandlePlayerRespawned;
        _bindingMediator.Clear();
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

        if (_boundPlayer != null)
        {
            _boundPlayer.OnFlaskChanged += HandleEstusChanged;
        }

        ApplyRootVisibility();
        RefreshUI();
    }

    public void UnbindPlayer()
    {
        if (_boundPlayer != null)
        {
            _boundPlayer.OnFlaskChanged -= HandleEstusChanged;
            _boundPlayer = null;
        }

        ApplyRootVisibility();
    }

    private void HandleEstusChanged(int current, int max)
    {
        UpdateUI(current, max);
    }

    private void HandlePlayerRespawned(PlayerController respawnedPlayer, Transform _)
    {
        player = respawnedPlayer;
        _bindingMediator.Rebind(player);
    }

    private void RefreshUI()
    {
        if (_boundPlayer == null)
        {
            UpdateUI(0, 0);
            return;
        }

        UpdateUI(_boundPlayer.CurrentFlaskCount, _boundPlayer.MaxFlaskCount);
    }

    private void UpdateUI(int current, int max)
    {
        EnsureReferences();

        if (countText != null)
        {
            countText.text = current.ToString();
        }

        if (useSlots == null)
        {
            return;
        }

        for (int i = 0; i < useSlots.Length; i++)
        {
            if (useSlots[i] == null)
            {
                continue;
            }

            useSlots[i].color = i < current ? availableColor : emptyColor;
        }
    }

    private void EnsureReferences()
    {
        if (root == null)
        {
            root = gameObject;
        }

        if (countText == null)
        {
            countText = GetComponentInChildren<TMP_Text>(true);
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

    private void ValidateSceneReferences(bool logOnlyOnce)
    {
        bool hasUseSlots = useSlots != null && useSlots.Length > 0;
        SceneReferenceValidator.Entry[] entries =
        {
            SceneReferenceValidator.Recommended(nameof(countText), countText, "에스투스 카운트 텍스트"),
            SceneReferenceValidator.Recommended(nameof(useSlots), hasUseSlots, "에스투스 슬롯 이미지 배열"),
            SceneReferenceValidator.RuntimeBound(nameof(player), player, "PlayerRespawned에서 재바인딩")
        };

        if (logOnlyOnce)
        {
            SceneReferenceValidator.ValidateOnce(this, nameof(UIEstusFlask), entries);
            return;
        }

        SceneReferenceValidator.Validate(this, nameof(UIEstusFlask), entries);
    }
}
