using UnityEngine;

[RequireComponent(typeof(Collider))]
public sealed class BossIntroPointTrigger : MonoBehaviour
{
    [SerializeField] private BossTalkInteractable talkInteractable;
    [SerializeField] private bool triggerOncePerSession = true;

    private bool _hasTriggered;

    private void Reset()
    {
        Collider triggerCollider = GetComponent<Collider>();
        if (triggerCollider != null)
        {
            triggerCollider.isTrigger = true;
        }
    }

    private void Awake()
    {
        Collider triggerCollider = GetComponent<Collider>();
        if (triggerCollider != null && !triggerCollider.isTrigger)
        {
            triggerCollider.isTrigger = true;
        }

        ResolveReferences();
    }

    private void OnEnable()
    {
        _hasTriggered = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (triggerOncePerSession && _hasTriggered)
        {
            return;
        }

        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player == null)
        {
            return;
        }

        ResolveReferences();
        if (talkInteractable == null)
        {
            Debug.LogWarning("[BossIntroPointTrigger] BossTalkInteractable reference is missing.");
            return;
        }

        if (talkInteractable.TryStartEncounterFromTrigger(player))
        {
            _hasTriggered = true;
        }
    }

    private void ResolveReferences()
    {
        if (talkInteractable != null)
        {
            return;
        }

        BossTalkInteractable[] interactables = FindObjectsByType<BossTalkInteractable>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        for (int i = 0; i < interactables.Length; i++)
        {
            BossTalkInteractable candidate = interactables[i];
            if (candidate != null && candidate.isActiveAndEnabled)
            {
                talkInteractable = candidate;
                return;
            }
        }
    }
}
