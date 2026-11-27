using UnityEngine;

[RequireComponent(typeof(Collider))]
public sealed class QuestProgressionTrigger : MonoBehaviour, IPlayerInteractable
{
    public enum TriggerAction
    {
        DiscoverGoal = 0,
        ConfirmApproach = 1,
        AcceptSeal = 2,
        ClaimEnding = 3
    }

    [Header("Progression")]
    [SerializeField] private TriggerAction triggerAction = TriggerAction.DiscoverGoal;
    [SerializeField] private bool requiresManualInput = true;
    [SerializeField] private KeyCode interactionKey = KeyCode.E;
    [SerializeField] private bool triggerOnce = true;
    [SerializeField] private bool markGoalKnownWhenAcceptingSeal = true;
    [SerializeField] private int interactionPriority;

    [Header("Presentation")]
    [SerializeField] private GameObject disableTargetOnTrigger;
    [SerializeField] private string interactionLabelOverride;
    [SerializeField] private bool logTrigger;

    private PlayerController _interactingPlayer;
    private bool _hasTriggered;

    public bool RequiresManualInteraction => requiresManualInput;
    public Object PromptOwner => this;
    public Transform InteractionTransform => transform;

    private void Reset()
    {
        Collider triggerCollider = GetComponent<Collider>();
        if (triggerCollider != null)
        {
            triggerCollider.isTrigger = true;
        }
    }

    private void OnEnable()
    {
        PlayerInteractableRegistry.Register(this);
        QuestProgressionManager progression = QuestProgressionManager.Instance;
        progression.StateChanged += HandleProgressionStateChanged;
        SyncFromProgressionState(progression);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_hasTriggered)
        {
            return;
        }

        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player == null)
        {
            return;
        }

        _interactingPlayer = player;

        if (!requiresManualInput)
        {
            Interact(player);
            return;
        }

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

        PlayerController player = _interactingPlayer;
        if (player != null)
        {
            player.Interaction?.UnregisterCandidate(this);
        }

        _interactingPlayer = null;

        QuestProgressionManager progression = Object.FindFirstObjectByType<QuestProgressionManager>(FindObjectsInactive.Include);
        if (progression != null)
        {
            progression.StateChanged -= HandleProgressionStateChanged;
        }
    }

    public bool CanInteract(PlayerController player)
    {
        if (_hasTriggered || !isActiveAndEnabled || player == null)
        {
            return false;
        }

        bool isSupportedInteractionKey = interactionKey == KeyCode.None || interactionKey == player.InteractionKey;
        return isSupportedInteractionKey && CanExecute(QuestProgressionManager.Instance);
    }

    public int GetInteractionPriority(PlayerController player)
    {
        return interactionPriority;
    }

    public string GetInteractionLabel(PlayerController player)
    {
        if (!string.IsNullOrWhiteSpace(interactionLabelOverride))
        {
            return interactionLabelOverride;
        }

        switch (triggerAction)
        {
            case TriggerAction.DiscoverGoal:
                return "Inspect";
            case TriggerAction.ConfirmApproach:
                return "Confirm";
            case TriggerAction.AcceptSeal:
                return "Accept seal";
            case TriggerAction.ClaimEnding:
                return "Claim ending";
            default:
                return "Interact";
        }
    }

    public void Interact(PlayerController player)
    {
        if (!CanInteract(player))
        {
            return;
        }

        _interactingPlayer = player;

        QuestProgressionManager progression = QuestProgressionManager.Instance;

        switch (triggerAction)
        {
            case TriggerAction.DiscoverGoal:
                progression.MarkGoalKnown();
                break;
            case TriggerAction.ConfirmApproach:
                progression.MarkApproachConfirmed();
                break;
            case TriggerAction.AcceptSeal:
                if (markGoalKnownWhenAcceptingSeal)
                {
                    progression.MarkGoalKnown();
                }

                if (!progression.AcceptSeal())
                {
                    return;
                }
                break;
            case TriggerAction.ClaimEnding:
                progression.MarkEndingClaimed();
                break;
        }

        if (logTrigger)
        {
            Debug.Log($"[QuestProgressionTrigger] {name} -> {triggerAction}");
        }

        if (triggerOnce)
        {
            _hasTriggered = true;
            DisableTrigger();
        }
    }

    private void DisableTrigger()
    {
        PlayerController player = _interactingPlayer;
        if (player != null)
        {
            player.Interaction?.UnregisterCandidate(this);
        }

        _interactingPlayer = null;

        Collider triggerCollider = GetComponent<Collider>();
        if (triggerCollider != null)
        {
            triggerCollider.enabled = false;
        }

        if (disableTargetOnTrigger != null)
        {
            disableTargetOnTrigger.SetActive(false);
        }
    }

    private void HandleProgressionStateChanged()
    {
        SyncFromProgressionState(QuestProgressionManager.Instance);
    }

    private void SyncFromProgressionState(QuestProgressionManager progression)
    {
        if (!triggerOnce || _hasTriggered || progression == null || !IsSatisfied(progression))
        {
            return;
        }

        _hasTriggered = true;
        DisableTrigger();
    }

    private bool CanExecute(QuestProgressionManager progression)
    {
        if (progression == null)
        {
            return false;
        }

        switch (triggerAction)
        {
            case TriggerAction.DiscoverGoal:
                return !progression.GoalKnown && !progression.BossCleared;
            case TriggerAction.ConfirmApproach:
                return progression.CurrentStep < QuestProgressStep.Approach && !progression.BossCleared;
            case TriggerAction.AcceptSeal:
                return progression.BellPepperInstalled &&
                       !progression.SealAccepted &&
                       !progression.BossCleared;
            case TriggerAction.ClaimEnding:
                return progression.BossCleared && !progression.EndingClaimed;
            default:
                return false;
        }
    }

    private bool IsSatisfied(QuestProgressionManager progression)
    {
        if (progression == null)
        {
            return false;
        }

        switch (triggerAction)
        {
            case TriggerAction.DiscoverGoal:
                return progression.GoalKnown || progression.BossCleared;
            case TriggerAction.ConfirmApproach:
                return progression.CurrentStep >= QuestProgressStep.Approach || progression.BossCleared;
            case TriggerAction.AcceptSeal:
                return progression.SealAccepted || progression.BossCleared;
            case TriggerAction.ClaimEnding:
                return progression.EndingClaimed;
            default:
                return false;
        }
    }
}
