using System.Collections;
using UnityEngine;

public class FogWallController : MonoBehaviour, IPlayerInteractable
{
    [Header("References")]
    [SerializeField] private GameObject visualRoot;
    [SerializeField] private Collider blockCollider;

    [Header("Scene Transition Interaction")]
    [SerializeField] private bool enableSceneTransitionInteraction;
    [SerializeField] private Transform interactionTransformOverride;
    [SerializeField] private int interactionPriority = 20;
    [SerializeField] private string interactionLabel = "Enter";
    [SerializeField] private string targetSceneName = TitleFlowConstants.BossSceneName;
    [SerializeField] [Min(0.05f)] private float interactionStateEnterTimeout = 0.2f;
    [SerializeField] [Min(0.05f)] private float interactionAnimationTimeout = 1.2f;

    private Coroutine _transitionSequence;
    private PlayerController _transitionSequencePlayer;
    private bool _keepPlayerLockedUntilDisable;

    public bool RequiresManualInteraction => enableSceneTransitionInteraction;
    public Object PromptOwner => this;
    public Transform InteractionTransform => interactionTransformOverride != null ? interactionTransformOverride : transform;

    private void Awake()
    {
        if (visualRoot == null)
        {
            visualRoot = gameObject;
        }

        if (blockCollider == null)
        {
            blockCollider = GetComponent<Collider>();
        }
    }

    private void OnEnable()
    {
        if (enableSceneTransitionInteraction)
        {
            PlayerInteractableRegistry.Register(this);
        }
    }

    private void OnDisable()
    {
        PlayerInteractableRegistry.Unregister(this);
        CancelTransitionSequence();
    }

    public void SetFogWallActive(bool active)
    {
        if (visualRoot != null)
        {
            visualRoot.SetActive(active);
        }

        if (blockCollider != null)
        {
            blockCollider.enabled = active;
        }
    }

    public void EnableWall()
    {
        SetFogWallActive(true);
    }

    public void DisableWall()
    {
        SetFogWallActive(false);
    }

    public bool CanInteract(PlayerController player)
    {
        if (!enableSceneTransitionInteraction || !isActiveAndEnabled || player == null || player.IsDead)
        {
            return false;
        }

        if (SimpleDialogueManager.IsOpen)
        {
            return false;
        }

        SceneFlowManager sceneFlowManager = SceneFlowManager.Instance;
        if (sceneFlowManager != null && sceneFlowManager.IsLoading)
        {
            return false;
        }

        return _transitionSequence == null;
    }

    public int GetInteractionPriority(PlayerController player)
    {
        return interactionPriority;
    }

    public string GetInteractionLabel(PlayerController player)
    {
        return string.IsNullOrWhiteSpace(interactionLabel) ? "Enter" : interactionLabel;
    }

    public void Interact(PlayerController player)
    {
        if (!CanInteract(player))
        {
            return;
        }

        if (_transitionSequence != null)
        {
            StopCoroutine(_transitionSequence);
        }

        _transitionSequence = StartCoroutine(CoTransitionAfterInteraction(player));
    }

    private IEnumerator CoTransitionAfterInteraction(PlayerController player)
    {
        if (player == null)
        {
            _transitionSequence = null;
            yield break;
        }

        _transitionSequencePlayer = player;
        _keepPlayerLockedUntilDisable = false;
        _transitionSequencePlayer.BeginExternalInteractionLock();
        UIInteractionPrompt.Hide(this);

        try
        {
            if (PlayerInteractionAnimationSequence.TryStart(player))
            {
                yield return PlayerInteractionAnimationSequence.WaitForCompletion(
                    player,
                    interactionStateEnterTimeout,
                    interactionAnimationTimeout);
            }

            SceneFlowManager sceneFlowManager = SceneFlowManager.Instance;
            if (sceneFlowManager == null || !sceneFlowManager.TryLoadScene(targetSceneName))
            {
                yield break;
            }

            _keepPlayerLockedUntilDisable = true;
        }
        finally
        {
            _transitionSequence = null;

            if (!_keepPlayerLockedUntilDisable && _transitionSequencePlayer != null)
            {
                _transitionSequencePlayer.EndExternalInteractionLock();
                _transitionSequencePlayer = null;
            }
        }
    }

    private void CancelTransitionSequence()
    {
        if (_transitionSequence != null)
        {
            StopCoroutine(_transitionSequence);
            _transitionSequence = null;
        }

        if (_transitionSequencePlayer != null)
        {
            _transitionSequencePlayer.EndExternalInteractionLock();
            _transitionSequencePlayer = null;
        }

        _keepPlayerLockedUntilDisable = false;
    }
}
