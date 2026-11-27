using System;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public sealed class BossTalkInteractable : MonoBehaviour, IPlayerInteractable
{
    [Serializable]
    private sealed class DialogueSet
    {
        [TextArea(2, 5)]
        public string[] lines;
    }

    [Header("References")]
    [SerializeField] private BossEntranceDirector entranceDirector;
    [SerializeField] private BossZoneTrigger zoneTrigger;
    [SerializeField] private BossController boss;

    [Header("Interaction")]
    [SerializeField] private KeyCode interactionKey = KeyCode.E;
    [SerializeField] private int interactionPriority = 40;
    [SerializeField] private string interactionLabel = "Talk";

    [Header("Dialogue")]
    [SerializeField] private string speakerName = "Nameless King";
    [SerializeField] private DialogueSet introDialogue = new DialogueSet
    {
        lines = new[]
        {
            "So you finally crossed the bridge.",
            "If you would claim this throne, prove it in steel."
        }
    };

    [Header("Behavior")]
    [SerializeField] private bool enableManualInteraction = true;
    [SerializeField] private bool disableAfterIntroSeen = true;
    [SerializeField] private bool requireEncounterAvailability = true;
#if UNITY_EDITOR
    [SerializeField] private bool allowRepeatInteractionInEditor = true;
#endif

    private PlayerController _interactingPlayer;

    public bool RequiresManualInteraction => enableManualInteraction;
    public UnityEngine.Object PromptOwner => this;
    public Transform InteractionTransform => boss != null ? boss.transform : transform;

    private void Reset()
    {
        Collider triggerCollider = GetComponent<Collider>();
        if (triggerCollider != null)
        {
            triggerCollider.isTrigger = true;
        }

        entranceDirector = GetComponent<BossEntranceDirector>();
        zoneTrigger = GetComponent<BossZoneTrigger>();
        if (zoneTrigger != null)
        {
            boss = zoneTrigger.BossProxy;
        }
    }

    private void Awake()
    {
        if (entranceDirector == null)
        {
            entranceDirector = GetComponent<BossEntranceDirector>();
        }

        if (zoneTrigger == null)
        {
            zoneTrigger = GetComponent<BossZoneTrigger>();
        }

        if (boss == null && zoneTrigger != null)
        {
            boss = zoneTrigger.BossProxy;
        }
    }

    private void OnEnable()
    {
        if (!enableManualInteraction)
        {
            return;
        }

        PlayerInteractableRegistry.Register(this);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!enableManualInteraction)
        {
            return;
        }

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
        if (!enableManualInteraction)
        {
            return;
        }

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
        if (enableManualInteraction)
        {
            PlayerInteractableRegistry.Unregister(this);
        }

        if (_interactingPlayer != null)
        {
            _interactingPlayer.Interaction?.UnregisterCandidate(this);
            _interactingPlayer = null;
        }
    }

    public bool CanInteract(PlayerController player)
    {
        if (!enableManualInteraction)
        {
            return false;
        }

        if (!CanStartEncounter(player))
        {
            return false;
        }

        if (interactionKey != KeyCode.None && interactionKey != player.InteractionKey)
        {
            return false;
        }

        return true;
    }

    public bool TryStartEncounterFromTrigger(PlayerController player)
    {
        if (!CanStartEncounter(player))
        {
            return false;
        }

        _interactingPlayer = player;
        return StartEncounter(player);
    }

    private bool CanStartEncounter(PlayerController player)
    {
        if (!isActiveAndEnabled || player == null || player.IsDead || SimpleDialogueManager.IsOpen)
        {
            return false;
        }

        if (entranceDirector == null || zoneTrigger == null)
        {
            return false;
        }

        if (zoneTrigger.IsFightStarted || zoneTrigger.IsEntranceSequenceActive)
        {
            return false;
        }

        QuestProgressionManager progression = QuestProgressionManager.Instance;
        if (progression != null)
        {
            if (progression.BossCleared)
            {
                return false;
            }

            if (ShouldBlockAfterIntroSeen(progression))
            {
                return false;
            }

            if (requireEncounterAvailability && !progression.CanStartBossFight(zoneTrigger.RequireSealAcceptance))
            {
                return false;
            }
        }

        return true;
    }

    private bool ShouldBlockAfterIntroSeen(QuestProgressionManager progression)
    {
        if (!disableAfterIntroSeen || progression == null || !progression.BossIntroSeen)
        {
            return false;
        }

#if UNITY_EDITOR
        if (allowRepeatInteractionInEditor)
        {
            return false;
        }
#endif

        return true;
    }

    public int GetInteractionPriority(PlayerController player)
    {
        return interactionPriority;
    }

    public string GetInteractionLabel(PlayerController player)
    {
        return string.IsNullOrWhiteSpace(interactionLabel) ? "Talk" : interactionLabel;
    }

    public void Interact(PlayerController player)
    {
        if (!CanInteract(player))
        {
            return;
        }

        _interactingPlayer = player;
        StartEncounter(player);
    }

    private bool StartEncounter(PlayerController player)
    {
        string[] lines = ResolveLines(
            introDialogue,
            "So you finally crossed the bridge.");

        if (entranceDirector != null && entranceDirector.UseDialogueCutsceneSequence)
        {
            zoneTrigger?.AuthorizeManualEncounterStart();
            if (!entranceDirector.TryStartDialogueCutsceneEncounter(player, lines))
            {
                Debug.LogWarning("[BossTalkInteractable] Failed to start dialogue cutscene encounter.");
                return false;
            }

            return true;
        }

        DialogueRequest request = new DialogueRequest(
            speakerName,
            lines,
            onCompleted: () => HandleDialogueCompleted(player));

        SimpleDialogueManager.TryBegin(player, request);
        return true;
    }

    private void HandleDialogueCompleted(PlayerController player)
    {
        if (player == null || entranceDirector == null || zoneTrigger == null)
        {
            return;
        }

        zoneTrigger.AuthorizeManualEncounterStart();
        if (!entranceDirector.TryStartScriptedEncounter(player, forceIntro: true))
        {
            Debug.LogWarning("[BossTalkInteractable] Failed to start scripted boss encounter.");
        }
    }

    private static string[] ResolveLines(DialogueSet dialogueSet, string fallback)
    {
        if (dialogueSet?.lines != null && dialogueSet.lines.Length > 0)
        {
            return dialogueSet.lines;
        }

        return new[] { fallback };
    }
}
