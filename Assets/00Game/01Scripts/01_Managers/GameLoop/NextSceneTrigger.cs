using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(Collider))]
public sealed class NextSceneTrigger : MonoBehaviour
{
    [SerializeField] private string targetSceneName = "Boss";
    [SerializeField] private bool triggerOnce = true;
    [SerializeField] private bool logTransition;

    private bool _hasTriggered;

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
        if (_hasTriggered && triggerOnce)
        {
            return;
        }

        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player == null || string.IsNullOrWhiteSpace(targetSceneName))
        {
            return;
        }

        if (logTransition)
        {
            Debug.Log($"[NextSceneTrigger] {name} -> {targetSceneName}");
        }

        _hasTriggered = true;
        SceneManager.LoadScene(targetSceneName);
    }
}
