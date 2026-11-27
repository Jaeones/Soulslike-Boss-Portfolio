using UnityEngine;

[RequireComponent(typeof(Collider))]
public class Checkpoint : MonoBehaviour
{
    [Header("Checkpoint Settings")]
    [SerializeField] private string checkpointId;
    [SerializeField] private Transform respawnPoint;
    [SerializeField] private bool autoRegisterOnTrigger = true;
    [SerializeField] private bool useOwnTransformWhenRespawnPointMissing = true;

    public string CheckpointId => string.IsNullOrWhiteSpace(checkpointId) ? gameObject.name : checkpointId;
    public bool AutoRegisterOnTrigger => autoRegisterOnTrigger;

    public Transform RespawnPoint
    {
        get
        {
            if (respawnPoint != null)
            {
                return respawnPoint;
            }

            return useOwnTransformWhenRespawnPointMissing ? transform : null;
        }
    }

    private void Reset()
    {
        if (string.IsNullOrWhiteSpace(checkpointId))
        {
            checkpointId = gameObject.name;
        }

        Collider triggerCol = GetComponent<Collider>();
        if (triggerCol != null)
        {
            triggerCol.isTrigger = true;
        }
    }

    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(checkpointId))
        {
            checkpointId = gameObject.name;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!autoRegisterOnTrigger)
        {
            return;
        }

        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player == null || CheckpointManager.Instance == null)
        {
            return;
        }

        CheckpointManager.Instance.RegisterCheckpoint(this);
    }
}
