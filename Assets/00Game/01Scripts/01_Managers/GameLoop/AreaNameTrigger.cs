using UnityEngine;

[RequireComponent(typeof(Collider))]
public sealed class AreaNameTrigger : MonoBehaviour
{
    [Header("Area")]
    [SerializeField] private string areaName = "지역명";
    [SerializeField] private bool triggerOnce = true;
    [SerializeField] private float holdDuration = 2.4f;
    [SerializeField] private float fadeDuration = 0.6f;

    [Header("Debug")]
    [SerializeField] private bool logTrigger;

    private Collider _triggerCollider;
    private bool _hasTriggered;
    private bool _playerInside;

    private void Awake()
    {
        _triggerCollider = GetComponent<Collider>();
    }

    private System.Collections.IEnumerator Start()
    {
        yield return new WaitForFixedUpdate();
        TryShowForInitialOverlap();
    }

    private void Reset()
    {
        Collider triggerCollider = GetComponent<Collider>();
        if (triggerCollider != null)
        {
            triggerCollider.isTrigger = true;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        TryShow(other);
    }

    private void OnTriggerStay(Collider other)
    {
        if (_playerInside)
        {
            return;
        }

        TryShow(other);
    }

    private void OnTriggerExit(Collider other)
    {
        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player == null)
        {
            return;
        }

        _playerInside = false;
    }

    private void TryShow(Collider other)
    {
        if (_hasTriggered && triggerOnce)
        {
            return;
        }

        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player == null || string.IsNullOrWhiteSpace(areaName))
        {
            return;
        }

        if (_playerInside)
        {
            return;
        }

        _playerInside = true;
        UIAreaTitle.Show(areaName, holdDuration, fadeDuration);

        if (logTrigger)
        {
            Debug.Log($"[AreaNameTrigger] {name} -> {areaName}");
        }

        if (triggerOnce)
        {
            _hasTriggered = true;
        }
    }

    private void TryShowForInitialOverlap()
    {
        if (_hasTriggered && triggerOnce)
        {
            return;
        }

        if (_triggerCollider == null || !_triggerCollider.enabled || !_triggerCollider.gameObject.activeInHierarchy)
        {
            return;
        }

        PlayerController player = FindFirstObjectByType<PlayerController>(FindObjectsInactive.Exclude);
        if (player == null || !IsPlayerOverlapping(player))
        {
            return;
        }

        Collider playerCollider = player.GetComponentInChildren<Collider>();
        if (playerCollider == null)
        {
            return;
        }

        TryShow(playerCollider);
    }

    private bool IsPlayerOverlapping(PlayerController player)
    {
        Collider[] playerColliders = player.GetComponentsInChildren<Collider>();
        foreach (Collider playerCollider in playerColliders)
        {
            if (playerCollider == null || !playerCollider.enabled || playerCollider.isTrigger)
            {
                continue;
            }

            if (_triggerCollider.bounds.Intersects(playerCollider.bounds))
            {
                return true;
            }
        }

        return false;
    }
}
