using UnityEngine;
using UnityEngine.SceneManagement;

public class CheckpointManager : Singleton<CheckpointManager>
{
    public event System.Action<Checkpoint> CurrentCheckpointChanged;

    [Header("Default Checkpoint")]
    [SerializeField] private Checkpoint defaultCheckpoint;

    private CheckpointRegistry _registry;

    public Checkpoint CurrentCheckpoint => _registry != null ? _registry.CurrentCheckpoint : null;

    protected override void Awake()
    {
        DetachFromSceneParentIfNeeded();
        base.Awake();
        ResolveDefaultCheckpointFallback();
        _registry = new CheckpointRegistry(defaultCheckpoint);
        // Force quest progression bootstrap during scene load so checkpoint restore
        // is ready immediately after Continue instead of waiting for later access.
        _ = QuestProgressionManager.Instance;

        ValidateSceneReferences(true);
    }

    [ContextMenu("Validate Scene References")]
    private void ValidateSceneReferencesContext()
    {
        ValidateSceneReferences(false);
    }

    public void SetDefaultCheckpoint(Checkpoint checkpoint)
    {
        Checkpoint previousCheckpoint = CurrentCheckpoint;
        defaultCheckpoint = checkpoint;
        _registry.SetDefaultCheckpoint(checkpoint);
        NotifyCheckpointChangedIfNeeded(previousCheckpoint);
    }

    public void RegisterCheckpoint(Checkpoint checkpoint)
    {
        if (checkpoint == null)
        {
            return;
        }

        Checkpoint previousCheckpoint = CurrentCheckpoint;
        bool changed = _registry.RegisterCheckpoint(checkpoint);
        QuestProgressionManager.Instance.RegisterCheckpoint(checkpoint.CheckpointId, checkpoint.gameObject.scene.name);

        if (changed)
        {
            NotifyCheckpointChangedIfNeeded(previousCheckpoint);
        }
    }

    public bool TryRestoreCheckpointById(string checkpointId)
    {
        if (string.IsNullOrWhiteSpace(checkpointId))
        {
            return false;
        }

        Checkpoint[] checkpoints = UnityEngine.Object.FindObjectsByType<Checkpoint>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Checkpoint checkpoint in checkpoints)
        {
            if (checkpoint == null || !string.Equals(checkpoint.CheckpointId, checkpointId, System.StringComparison.Ordinal))
            {
                continue;
            }

            RegisterCheckpoint(checkpoint);
            return true;
        }

        return false;
    }

    public bool TryGetRespawnPose(Transform fallback, out Vector3 position, out Quaternion rotation)
    {
        return _registry.TryGetRespawnData(fallback, out _, out position, out rotation);
    }

    public bool RespawnPlayer(PlayerController player)
    {
        if (player == null)
        {
            return false;
        }

        if (!_registry.TryGetRespawnData(player.transform, out Transform checkpointTransform, out Vector3 spawnPos, out Quaternion spawnRot))
        {
            Debug.LogWarning("Respawn failed: no valid checkpoint is available.");
            return false;
        }

        player.RespawnFromCheckpoint(spawnPos, spawnRot);
        GameLoopEvents.RaisePlayerRespawned(player, checkpointTransform);
        return true;
    }

    private void ValidateSceneReferences(bool logOnlyOnce)
    {
        SceneReferenceValidator.Entry[] entries =
        {
            SceneReferenceValidator.Required(nameof(defaultCheckpoint), defaultCheckpoint, "기본 체크포인트")
        };

        if (logOnlyOnce)
        {
            SceneReferenceValidator.ValidateOnce(this, nameof(CheckpointManager), entries);
            return;
        }

        SceneReferenceValidator.Validate(this, nameof(CheckpointManager), entries);
    }

    private void DetachFromSceneParentIfNeeded()
    {
        if (transform.parent == null)
        {
            return;
        }

        // Singleton base applies DontDestroyOnLoad, so the manager must live on a root object.
        transform.SetParent(null, true);
    }

    private void NotifyCheckpointChangedIfNeeded(Checkpoint previousCheckpoint)
    {
        if (previousCheckpoint == CurrentCheckpoint)
        {
            return;
        }

        CurrentCheckpointChanged?.Invoke(CurrentCheckpoint);
    }

    private void ResolveDefaultCheckpointFallback()
    {
        if (defaultCheckpoint != null)
        {
            return;
        }

        Checkpoint[] checkpoints = UnityEngine.Object.FindObjectsByType<Checkpoint>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (checkpoints == null || checkpoints.Length == 0)
        {
            return;
        }

        string savedCheckpointId = QuestProgressionManager.Instance != null
            ? QuestProgressionManager.Instance.CurrentCheckpointId
            : string.Empty;
        string savedCheckpointSceneName = QuestProgressionManager.Instance != null
            ? QuestProgressionManager.Instance.CurrentCheckpointSceneName
            : string.Empty;
        string activeSceneName = SceneManager.GetActiveScene().name;

        if (!string.IsNullOrWhiteSpace(savedCheckpointId) &&
            (string.IsNullOrWhiteSpace(savedCheckpointSceneName) ||
             string.Equals(savedCheckpointSceneName, activeSceneName, System.StringComparison.Ordinal)))
        {
            foreach (Checkpoint checkpoint in checkpoints)
            {
                if (checkpoint != null &&
                    string.Equals(checkpoint.CheckpointId, savedCheckpointId, System.StringComparison.Ordinal))
                {
                    defaultCheckpoint = checkpoint;
                    return;
                }
            }
        }

        Checkpoint selectedCheckpoint = null;
        foreach (Checkpoint checkpoint in checkpoints)
        {
            if (checkpoint == null)
            {
                continue;
            }

            if (selectedCheckpoint == null ||
                string.CompareOrdinal(checkpoint.CheckpointId, selectedCheckpoint.CheckpointId) < 0)
            {
                selectedCheckpoint = checkpoint;
            }
        }

        defaultCheckpoint = selectedCheckpoint;
    }
}
