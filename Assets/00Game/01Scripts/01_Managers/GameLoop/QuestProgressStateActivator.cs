using UnityEngine;

public sealed class QuestProgressStateActivator : MonoBehaviour
{
    public enum RequiredState
    {
        GoalKnown = 0,
        SealAccepted = 1,
        BossCleared = 2,
        EndingClaimed = 3,
        RearGateOpened = 4
    }

    [SerializeField] private RequiredState requiredState = RequiredState.BossCleared;
    [SerializeField] private bool invert;
    [SerializeField] private GameObject targetObject;
    [SerializeField] private Collider[] collidersToToggle;

    private void OnEnable()
    {
        QuestProgressionManager progression = QuestProgressionManager.Instance;
        progression.StateChanged += HandleStateChanged;
        ApplyState(progression);
    }

    private void Start()
    {
        ApplyState(QuestProgressionManager.Instance);
    }

    private void OnDisable()
    {
        QuestProgressionManager progression = UnityEngine.Object.FindFirstObjectByType<QuestProgressionManager>(FindObjectsInactive.Include);
        if (progression != null)
        {
            progression.StateChanged -= HandleStateChanged;
        }
    }

    private void HandleStateChanged()
    {
        ApplyState(QuestProgressionManager.Instance);
    }

    private void ApplyState(QuestProgressionManager progression)
    {
        if (progression == null)
        {
            return;
        }

        bool isActive = requiredState switch
        {
            RequiredState.GoalKnown => progression.GoalKnown,
            RequiredState.SealAccepted => progression.SealAccepted,
            RequiredState.BossCleared => progression.BossCleared,
            RequiredState.EndingClaimed => progression.EndingClaimed,
            RequiredState.RearGateOpened => progression.RearGateOpened,
            _ => false
        };

        if (invert)
        {
            isActive = !isActive;
        }

        GameObject target = targetObject != null ? targetObject : gameObject;
        target.SetActive(isActive);

        if (collidersToToggle == null)
        {
            return;
        }

        foreach (Collider targetCollider in collidersToToggle)
        {
            if (targetCollider != null)
            {
                targetCollider.enabled = isActive;
            }
        }
    }
}
