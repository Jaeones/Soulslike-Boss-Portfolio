using System;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public sealed class InjuredKnightInteractable : MonoBehaviour, IPlayerInteractable
{
    [Serializable]
    private sealed class DialogueSet
    {
        [TextArea(2, 5)]
        public string[] lines;
    }

    [Header("Interaction")]
    [SerializeField] private KeyCode interactionKey = KeyCode.E;
    [SerializeField] private int interactionPriority = 25;
    [SerializeField] private string interactionLabel = "대화하기";

    [Header("Dialogue")]
    [SerializeField] private string speakerName = "부상당한 기사";
    [SerializeField] private DialogueSet offeredDialogue = new DialogueSet
    {
        lines = new[]
        {
            "아직도 이곳에 들어오는 인간이 있군",
            "어짜피 죽을 목숨 선행 쌓는 마음으로 하나 알려주지"
        }
    };
    [SerializeField] private DialogueSet acceptedDialogue = new DialogueSet
    {
        lines = new[]
        {
            "성에서 도망치기 위해 놓고온 추가 하나 있어",
            "성안으로 들어가고 싶다면 필요할거야"
        }
    };
    [SerializeField] private DialogueSet declinedDialogue = new DialogueSet
    {
        lines = new[]
        {
            "그래 성안에 들어가지 않는게 현명한 생각이지...",
            "(숨을 거둔 듯하다)"
        }
    };
    [SerializeField] private DialogueSet completedDialogue = new DialogueSet
    {
        lines = new[]
        {
            "You ended it... truly?",
            "Then I can finally rest for a moment."
        }
    };

    [Header("Choice Labels")]
    [SerializeField, TextArea(1, 3)] private string choiceText = "이야기를 듣겠습니까?";
    [SerializeField] private string acceptLabel = "이야기를 듣는다";
    [SerializeField] private string declineLabel = "무시한다";
    [SerializeField] private string acceptNotice = "";
    [SerializeField] private string declineNotice = "";
    [SerializeField] private bool markGoalKnownOnAccept = true;

    private PlayerController _interactingPlayer;

    public bool RequiresManualInteraction => true;
    public UnityEngine.Object PromptOwner => this;
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

        if (_interactingPlayer != null)
        {
            _interactingPlayer.Interaction?.UnregisterCandidate(this);
            _interactingPlayer = null;
        }
    }

    public bool CanInteract(PlayerController player)
    {
        QuestProgressionManager progression = QuestProgressionManager.Instance;
        if (progression != null && progression.CurrentInjuredKnightQuestState == InjuredKnightQuestState.Declined)
        {
            return false;
        }

        return isActiveAndEnabled &&
               player != null &&
               !player.IsDead &&
               !SimpleDialogueManager.IsOpen &&
               (interactionKey == KeyCode.None || interactionKey == player.InteractionKey);
    }

    public int GetInteractionPriority(PlayerController player)
    {
        return interactionPriority;
    }

    public string GetInteractionLabel(PlayerController player)
    {
        return string.IsNullOrWhiteSpace(interactionLabel) ? "대화하기" : interactionLabel;
    }

    public void Interact(PlayerController player)
    {
        if (!CanInteract(player))
        {
            return;
        }

        _interactingPlayer = player;
        QuestProgressionManager progression = QuestProgressionManager.Instance;
        InjuredKnightQuestState state = progression != null
            ? progression.CurrentInjuredKnightQuestState
            : InjuredKnightQuestState.None;

        if (progression != null && state == InjuredKnightQuestState.None)
        {
            progression.MarkInjuredKnightQuestOffered();
            state = progression.CurrentInjuredKnightQuestState;
        }

        DialogueRequest request = BuildRequest(state, progression);
        if (request == null)
        {
            return;
        }

        SimpleDialogueManager.TryBegin(player, request);
    }

    private DialogueRequest BuildRequest(InjuredKnightQuestState state, QuestProgressionManager progression)
    {
        switch (state)
        {
            case InjuredKnightQuestState.None:
            case InjuredKnightQuestState.Offered:
                return new DialogueRequest(
                    speakerName,
                    ResolveLines(offeredDialogue, "어짜피 죽을 목숨 선행 쌓는 마음으로 하나 알려주지"),
                    new[]
                    {
                        new DialogueChoiceOption(
                            string.IsNullOrWhiteSpace(acceptLabel) ? "이야기를 듣는다" : acceptLabel,
                            () => progression?.AcceptInjuredKnightQuest(markGoalKnownOnAccept),
                            acceptNotice,
                            () => BuildAcceptedRequest()),
                        new DialogueChoiceOption(
                            string.IsNullOrWhiteSpace(declineLabel) ? "무시한다" : declineLabel,
                            () => progression?.DeclineInjuredKnightQuest(),
                            declineNotice,
                            () => BuildDeclinedRequest())
                    },
                    string.IsNullOrWhiteSpace(choiceText) ? "이야기를 듣겠습니까?" : choiceText);

            case InjuredKnightQuestState.Accepted:
                return BuildAcceptedRequest();

            case InjuredKnightQuestState.Declined:
                return BuildDeclinedRequest();

            case InjuredKnightQuestState.Completed:
                return new DialogueRequest(
                    speakerName,
                    ResolveLines(completedDialogue, "You ended it... thank you."));

            case InjuredKnightQuestState.Failed:
                return new DialogueRequest(
                    speakerName,
                    new[] { "It is too late now..." });

            default:
                return null;
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

    private DialogueRequest BuildAcceptedRequest()
    {
        return new DialogueRequest(
            speakerName,
            ResolveLines(acceptedDialogue, "성안으로 들어가고 싶다면 필요할거야"));
    }

    private DialogueRequest BuildDeclinedRequest()
    {
        return new DialogueRequest(
            speakerName,
            ResolveLines(declinedDialogue, "(숨을 거둔 듯하다)"));
    }
}
