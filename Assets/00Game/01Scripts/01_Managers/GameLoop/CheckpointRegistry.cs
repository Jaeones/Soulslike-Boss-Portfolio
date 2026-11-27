using UnityEngine;

public sealed class CheckpointRegistry
{
    private Checkpoint _defaultCheckpoint;
    private Checkpoint _currentCheckpoint;

    public CheckpointRegistry(Checkpoint defaultCheckpoint)
    {
        SetDefaultCheckpoint(defaultCheckpoint);
    }

    public Checkpoint CurrentCheckpoint => _currentCheckpoint;
    public Checkpoint DefaultCheckpoint => _defaultCheckpoint;

    public void SetDefaultCheckpoint(Checkpoint checkpoint)
    {
        _defaultCheckpoint = checkpoint;

        if (_currentCheckpoint == null && _defaultCheckpoint != null)
        {
            _currentCheckpoint = _defaultCheckpoint;
        }
    }

    public bool RegisterCheckpoint(Checkpoint checkpoint)
    {
        if (checkpoint == null || _currentCheckpoint == checkpoint)
        {
            return false;
        }

        _currentCheckpoint = checkpoint;
        Debug.Log($"체크포인트 갱신: {checkpoint.name}");
        return true;
    }

    public bool TryGetRespawnData(
        Transform fallback,
        out Transform checkpointTransform,
        out Vector3 position,
        out Quaternion rotation)
    {
        Transform respawnTransform = null;
        if (_currentCheckpoint != null)
        {
            respawnTransform = _currentCheckpoint.RespawnPoint;
        }
        else if (_defaultCheckpoint != null)
        {
            respawnTransform = _defaultCheckpoint.RespawnPoint;
        }

        checkpointTransform = respawnTransform;
        if (respawnTransform != null)
        {
            position = respawnTransform.position;
            rotation = respawnTransform.rotation;
            return true;
        }

        if (fallback != null)
        {
            position = fallback.position;
            rotation = fallback.rotation;
            return true;
        }

        position = Vector3.zero;
        rotation = Quaternion.identity;
        return false;
    }
}
