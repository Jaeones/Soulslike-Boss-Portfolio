using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[RequireComponent(typeof(Checkpoint))]
[RequireComponent(typeof(Collider))]
public sealed class BonfireCheckpoint : MonoBehaviour, IPlayerInteractable
{
    private enum BonfireSequenceState
    {
        None,
        Igniting,
        StartingRest,
        Resting,
        EndingRest
    }

    [Header("Interaction")]
    [SerializeField] private KeyCode interactionKey = KeyCode.E;
    [SerializeField] private string igniteInteractionLabel = "화톳불을 점화한다";
    [FormerlySerializedAs("interactionLabel")]
    [SerializeField] private string activeInteractionLabel = "화톳불에서 쉰다";
    [SerializeField] private string restExitLabel = "휴식 종료";
    [SerializeField] private int interactionPriority = 100;
    [SerializeField] private bool allowRepeatInteraction = true;
    [SerializeField] private bool logInteraction;

    [Header("References")]
    [SerializeField] private Checkpoint checkpoint;

    [Header("Rest Effects")]
    [SerializeField] private bool restoreHealthOnInteract = true;
    [SerializeField] private bool refillStaminaOnInteract = true;
    [SerializeField] private bool refillFlasksOnInteract = true;

    [Header("Bonfire Animation")]
    [SerializeField] private float stateEnterTimeout = 0.75f;
    [SerializeField] private float stateExitTimeout = 3f;
    [SerializeField] private bool logSequence;

    [Header("Visual State")]
    [SerializeField] private GameObject inactiveVisualRoot;
    [SerializeField] private GameObject activeVisualRoot;
    [SerializeField] private GameObject flameVfx;

    [Header("Debug Activation Tint")]
    [SerializeField] private bool useActivationTint = true;
    [SerializeField] private Renderer[] activationTintRenderers;
    [SerializeField] private Color activationTintColor = Color.red;

    private PlayerController _interactingPlayer;
    private PlayerController _sequencePlayer;
    private bool _isActivated;
    private QuestProgressionManager _subscribedQuestProgressionManager;
    private MaterialPropertyBlock _propertyBlock;
    private Coroutine _interactionRoutine;
    private BonfireSequenceState _sequenceState;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    public bool RequiresManualInteraction => true;
    public Object PromptOwner => this;
    public Transform InteractionTransform => transform;
    public bool IsActivated => _isActivated;

    private void Reset()
    {
        checkpoint = GetComponent<Checkpoint>();
        CacheActivationTintRenderers();

        Collider triggerCollider = GetComponent<Collider>();
        if (triggerCollider != null)
        {
            triggerCollider.isTrigger = true;
        }
    }

    private void Awake()
    {
        if (checkpoint == null)
        {
            checkpoint = GetComponent<Checkpoint>();
        }

        _propertyBlock = new MaterialPropertyBlock();
        CacheActivationTintRenderers();
    }

    private void Start()
    {
        SubscribeToQuestProgressionManager();
        SyncActivationFromCheckpointState();
    }

    private void OnEnable()
    {
        PlayerInteractableRegistry.Register(this);
        SubscribeToQuestProgressionManager();
        SyncActivationFromCheckpointState();
    }

    private void Update()
    {
        if (_sequenceState != BonfireSequenceState.Resting || _sequencePlayer == null || _sequencePlayer.IsDead)
        {
            return;
        }

        if (!Input.GetKeyDown(_sequencePlayer.InteractionKey))
        {
            return;
        }

        UIInteractionPrompt.Hide(this);
        _sequenceState = BonfireSequenceState.EndingRest;
    }

    private void OnTriggerEnter(Collider other)
    {
        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player == null)
        {
            return;
        }

        _interactingPlayer = player;
        player.Interaction?.RegisterCandidate(this);
    }

    private void OnTriggerExit(Collider other)
    {
        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player == null || player != _interactingPlayer)
        {
            return;
        }

        player.Interaction?.UnregisterCandidate(this);
        _interactingPlayer = null;
    }

    private void OnDisable()
    {
        PlayerInteractableRegistry.Unregister(this);
        CancelInteractionSequence();
        UnsubscribeFromQuestProgressionManager();

        if (_interactingPlayer != null)
        {
            _interactingPlayer.Interaction?.UnregisterCandidate(this);
            _interactingPlayer = null;
        }
    }

    public bool CanInteract(PlayerController player)
    {
        if (!isActiveAndEnabled || player == null || player.IsDead || checkpoint == null)
        {
            return false;
        }

        bool supportsKey = interactionKey == KeyCode.None || interactionKey == player.InteractionKey;
        if (!supportsKey)
        {
            return false;
        }

        return allowRepeatInteraction || !_isActivated;
    }

    public int GetInteractionPriority(PlayerController player)
    {
        return interactionPriority;
    }

    public string GetInteractionLabel(PlayerController player)
    {
        return _isActivated ? GetActiveInteractionLabel() : GetIgniteInteractionLabel();
    }

    public void Interact(PlayerController player)
    {
        if (!CanInteract(player) || CheckpointManager.Instance == null || _interactionRoutine != null)
        {
            return;
        }

        _interactionRoutine = StartCoroutine(RunInteractionSequence(player));
    }

    private IEnumerator RunInteractionSequence(PlayerController player)
    {
        if (player == null)
        {
            _interactionRoutine = null;
            yield break;
        }

        _sequencePlayer = player;
        _sequencePlayer.BeginBonfireInteractionLock();
        UIInteractionPrompt.Hide(this);

        try
        {
            if (!_isActivated)
            {
                yield return RunIgniteSequence(player);
                yield break;
            }

            yield return RunRestSequence(player);
        }
        finally
        {
            UIInteractionPrompt.Hide(this);

            if (_sequencePlayer != null)
            {
                _sequencePlayer.EndBonfireInteractionLock();
            }

            _sequencePlayer = null;
            _sequenceState = BonfireSequenceState.None;
            _interactionRoutine = null;
        }
    }

    private IEnumerator RunIgniteSequence(PlayerController player)
    {
        _sequenceState = BonfireSequenceState.Igniting;

        if (logSequence)
        {
            Debug.Log($"[BonfireCheckpoint] Ignite sequence started -> {checkpoint.CheckpointId}");
        }

        bool triggerSucceeded = TryTriggerBonfireAnimation(player, PlayerAnimatorParams.BonfireIgniteTrigger, PlayerAnimatorParams.BonfireIgniteTriggerName);
        if (triggerSucceeded)
        {
            yield return WaitForAnimatorStateEnter(player, PlayerAnimatorStates.BonfireIgnite, stateEnterTimeout);
            yield return WaitForAnimatorStateExit(player, PlayerAnimatorStates.BonfireIgnite, stateExitTimeout);
        }

        CommitIgniteEffects(player);
    }

    private IEnumerator RunRestSequence(PlayerController player)
    {
        _sequenceState = BonfireSequenceState.StartingRest;

        if (logSequence)
        {
            Debug.Log($"[BonfireCheckpoint] Rest sequence started -> {checkpoint.CheckpointId}");
        }

        bool triggerSucceeded = TryTriggerBonfireAnimation(player, PlayerAnimatorParams.BonfireStartTrigger, PlayerAnimatorParams.BonfireStartTriggerName);
        if (triggerSucceeded)
        {
            yield return WaitForAnimatorStateEnter(player, PlayerAnimatorStates.BonfireStart, stateEnterTimeout);
            yield return WaitForAnimatorStateEnter(player, PlayerAnimatorStates.BonfireIdle, stateExitTimeout);
        }

        CommitRestEffects(player);
        _sequenceState = BonfireSequenceState.Resting;
        UIInteractionPrompt.Show(this, player.InteractionKey, GetRestExitLabel());

        while (_sequenceState == BonfireSequenceState.Resting && _sequencePlayer == player && player != null && !player.IsDead)
        {
            yield return null;
        }

        if (player == null || player.IsDead)
        {
            yield break;
        }

        _sequenceState = BonfireSequenceState.EndingRest;
        triggerSucceeded = TryTriggerBonfireAnimation(player, PlayerAnimatorParams.BonfireEndTrigger, PlayerAnimatorParams.BonfireEndTriggerName);
        if (triggerSucceeded)
        {
            yield return WaitForAnimatorStateEnter(player, PlayerAnimatorStates.BonfireEnd, stateEnterTimeout);
            yield return WaitForAnimatorStateExit(player, PlayerAnimatorStates.BonfireEnd, stateExitTimeout);
        }

        if (logSequence)
        {
            Debug.Log($"[BonfireCheckpoint] Rest sequence ended -> {checkpoint.CheckpointId}");
        }
    }

    private void CommitIgniteEffects(PlayerController player)
    {
        CheckpointManager.Instance.RegisterCheckpoint(checkpoint);
        ApplyRestEffects(player);
        QuestProgressionManager.Instance.CommitBonfireSave(checkpoint.CheckpointId, checkpoint.gameObject.scene.name);
        SetActivated(true);
        GameUiSfxPlayer.Play(UiSfxId.CheckpointRetry);

        if (logInteraction)
        {
            Debug.Log($"[BonfireCheckpoint] Ignite committed -> {checkpoint.CheckpointId}");
        }
    }

    private void CommitRestEffects(PlayerController player)
    {
        CheckpointManager.Instance.RegisterCheckpoint(checkpoint);
        ApplyRestEffects(player);
        QuestProgressionManager.Instance.CommitBonfireSave(checkpoint.CheckpointId, checkpoint.gameObject.scene.name);
        SetActivated(true);
        GameUiSfxPlayer.Play(UiSfxId.CheckpointRetry);

        if (logInteraction)
        {
            Debug.Log($"[BonfireCheckpoint] Rest committed -> {checkpoint.CheckpointId}");
        }
    }

    private void ApplyRestEffects(PlayerController player)
    {
        if (player == null || player.IsDead)
        {
            return;
        }

        if (restoreHealthOnInteract)
        {
            float recoverAmount = Mathf.Max(0f, player.MaxHp - player.CurrentHp);
            if (recoverAmount > 0f)
            {
                player.Heal(recoverAmount);
            }
        }

        if (refillStaminaOnInteract && player.Stamina != null)
        {
            player.Stamina.Refill();
            player.NotifyStaminaChanged();
        }

        if (refillFlasksOnInteract && player.Item != null)
        {
            player.Item.HandleRespawnRefill();
        }
    }

    private string GetIgniteInteractionLabel()
    {
        return string.IsNullOrWhiteSpace(igniteInteractionLabel) ? "화톳불을 점화한다" : igniteInteractionLabel;
    }

    private string GetActiveInteractionLabel()
    {
        return string.IsNullOrWhiteSpace(activeInteractionLabel) ? "화톳불에서 쉰다" : activeInteractionLabel;
    }

    private string GetRestExitLabel()
    {
        return string.IsNullOrWhiteSpace(restExitLabel) ? "휴식 종료" : restExitLabel;
    }

    private bool TryTriggerBonfireAnimation(PlayerController player, int triggerHash, string triggerName)
    {
        if (player?.AnimatorDriver == null)
        {
            return false;
        }

        player.AnimatorDriver.ResetTrigger(PlayerAnimatorParams.BonfireIgniteTrigger);
        player.AnimatorDriver.ResetTrigger(PlayerAnimatorParams.BonfireStartTrigger);
        player.AnimatorDriver.ResetTrigger(PlayerAnimatorParams.BonfireEndTrigger);
        return player.TrySetAnimatorTrigger(triggerHash, triggerName);
    }

    private IEnumerator WaitForAnimatorStateEnter(PlayerController player, string stateName, float timeout)
    {
        if (player?.AnimatorDriver == null)
        {
            yield break;
        }

        float expireAt = Time.time + Mathf.Max(0.05f, timeout);
        while (Time.time <= expireAt)
        {
            if (player.AnimatorDriver.IsCurrentState(stateName))
            {
                yield break;
            }

            yield return null;
        }

        if (logSequence)
        {
            Debug.LogWarning($"[BonfireCheckpoint] Timed out waiting for state enter: {stateName}");
        }
    }

    private IEnumerator WaitForAnimatorStateExit(PlayerController player, string stateName, float timeout)
    {
        if (player?.AnimatorDriver == null)
        {
            yield break;
        }

        float expireAt = Time.time + Mathf.Max(0.05f, timeout);
        while (Time.time <= expireAt)
        {
            if (!player.AnimatorDriver.IsCurrentState(stateName) && !player.AnimatorDriver.IsInTransition())
            {
                yield break;
            }

            yield return null;
        }

        if (logSequence)
        {
            Debug.LogWarning($"[BonfireCheckpoint] Timed out waiting for state exit: {stateName}");
        }
    }

    private void CancelInteractionSequence()
    {
        if (_interactionRoutine != null)
        {
            StopCoroutine(_interactionRoutine);
            _interactionRoutine = null;
        }

        UIInteractionPrompt.Hide(this);

        if (_sequencePlayer != null)
        {
            _sequencePlayer.EndBonfireInteractionLock();
            _sequencePlayer = null;
        }

        _sequenceState = BonfireSequenceState.None;
    }

    private void SyncActivationFromCheckpointState()
    {
        QuestProgressionManager questProgressionManager = _subscribedQuestProgressionManager;
        if (questProgressionManager == null)
        {
            questProgressionManager = Object.FindFirstObjectByType<QuestProgressionManager>(FindObjectsInactive.Include);
        }

        bool shouldBeActive = false;
        if (questProgressionManager != null)
        {
            shouldBeActive = questProgressionManager.IsBonfireActivated(checkpoint.CheckpointId) ||
                             string.Equals(questProgressionManager.CurrentCheckpointId, checkpoint.CheckpointId, System.StringComparison.Ordinal);
        }

        SetActivated(shouldBeActive);
    }

    private void SetActivated(bool value)
    {
        _isActivated = value;
        ApplyVisualState();
    }

    private void ApplyVisualState()
    {
        if (inactiveVisualRoot != null)
        {
            inactiveVisualRoot.SetActive(!_isActivated);
        }

        if (activeVisualRoot != null)
        {
            activeVisualRoot.SetActive(_isActivated);
        }

        if (flameVfx != null)
        {
            flameVfx.SetActive(_isActivated);
        }

        ApplyActivationTint();
    }

    private void SubscribeToQuestProgressionManager()
    {
        if (_subscribedQuestProgressionManager != null)
        {
            return;
        }

        _subscribedQuestProgressionManager = Object.FindFirstObjectByType<QuestProgressionManager>(FindObjectsInactive.Include);
        if (_subscribedQuestProgressionManager != null)
        {
            _subscribedQuestProgressionManager.StateChanged += HandleQuestProgressionStateChanged;
        }
    }

    private void UnsubscribeFromQuestProgressionManager()
    {
        if (_subscribedQuestProgressionManager == null)
        {
            return;
        }

        _subscribedQuestProgressionManager.StateChanged -= HandleQuestProgressionStateChanged;
        _subscribedQuestProgressionManager = null;
    }

    private void HandleQuestProgressionStateChanged()
    {
        SyncActivationFromCheckpointState();
    }

    private void ApplyActivationTint()
    {
        if (!useActivationTint || activationTintRenderers == null || activationTintRenderers.Length == 0)
        {
            return;
        }

        foreach (Renderer renderer in activationTintRenderers)
        {
            if (renderer == null)
            {
                continue;
            }

            if (_isActivated)
            {
                renderer.GetPropertyBlock(_propertyBlock);
                _propertyBlock.SetColor(BaseColorId, activationTintColor);
                _propertyBlock.SetColor(ColorId, activationTintColor);
                renderer.SetPropertyBlock(_propertyBlock);
                continue;
            }

            renderer.SetPropertyBlock(null);
        }
    }

    private void CacheActivationTintRenderers()
    {
        if (activationTintRenderers != null && activationTintRenderers.Length > 0)
        {
            return;
        }

        List<Renderer> renderers = new();
        AddRenderersFromRoot(renderers, inactiveVisualRoot != null ? inactiveVisualRoot.transform : null);
        AddRenderersFromRoot(renderers, activeVisualRoot != null ? activeVisualRoot.transform : null);

        if (renderers.Count == 0)
        {
            AddRenderersFromRoot(renderers, transform);
        }

        activationTintRenderers = renderers.ToArray();
    }

    private static void AddRenderersFromRoot(List<Renderer> renderers, Transform root)
    {
        if (root == null)
        {
            return;
        }

        Renderer[] foundRenderers = root.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer renderer in foundRenderers)
        {
            if (renderer == null || renderers.Contains(renderer))
            {
                continue;
            }

            renderers.Add(renderer);
        }
    }
}
