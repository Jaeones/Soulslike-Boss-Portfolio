using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class PlayerRespawnFlow
{
    private readonly MonoBehaviour coroutineHost;
    private readonly UIYouDied youDiedUI;
    private readonly float youDiedHoldTime;

    private PlayerController player;
    private bool isRunning;

    public bool IsRunning => isRunning;

    public PlayerRespawnFlow(MonoBehaviour coroutineHost, PlayerController player, UIYouDied youDiedUI, float youDiedHoldTime)
    {
        this.coroutineHost = coroutineHost;
        this.player = player;
        this.youDiedUI = youDiedUI;
        this.youDiedHoldTime = youDiedHoldTime;
    }

    public void SetPlayer(PlayerController targetPlayer)
    {
        if (targetPlayer != null)
        {
            player = targetPlayer;
        }
    }

    public void HideOverlayImmediate()
    {
        if (youDiedUI != null)
        {
            youDiedUI.HideImmediate();
        }
    }

    public void HandlePlayerDied(PlayerController deadPlayer)
    {
        if (isRunning || coroutineHost == null)
        {
            return;
        }

        SetPlayer(deadPlayer);
        coroutineHost.StartCoroutine(RunRespawnSequence());
    }

    private IEnumerator RunRespawnSequence()
    {
        isRunning = true;

        // Use real time so the respawn sequence keeps running even if gameplay time is paused.
        Time.timeScale = 1f;

        if (youDiedUI != null)
        {
            yield return youDiedUI.PlayShow();
        }

        yield return new WaitForSecondsRealtime(Mathf.Max(0.1f, youDiedHoldTime));

        bool hasRespawned = TryRespawnPlayer();
        if (!hasRespawned)
        {
            Debug.LogWarning("Checkpoint respawn failed: no valid player position is available.");
        }
        else
        {
            GameUiSfxPlayer.Play(UiSfxId.CheckpointRetry);
        }

        if (youDiedUI != null)
        {
            yield return youDiedUI.PlayHide();
        }

        isRunning = false;
    }

    private bool TryRespawnPlayer()
    {
        if (player == null)
        {
            return false;
        }

        if (TryLoadCheckpointSceneForRespawn())
        {
            return true;
        }

        if (CheckpointManager.Instance == null)
        {
            Debug.LogWarning("Respawn failed: CheckpointManager is missing.");
            return false;
        }

        return CheckpointManager.Instance.RespawnPlayer(player);
    }

    private bool TryLoadCheckpointSceneForRespawn()
    {
        if (QuestProgressionManager.Instance == null)
        {
            return false;
        }

        string checkpointSceneName = TitleFlowConstants.NormalizeSceneName(QuestProgressionManager.Instance.CurrentCheckpointSceneName);
        if (string.IsNullOrWhiteSpace(checkpointSceneName))
        {
            return false;
        }

        string activeSceneName = TitleFlowConstants.NormalizeSceneName(SceneManager.GetActiveScene().name);
        if (string.Equals(checkpointSceneName, activeSceneName, System.StringComparison.Ordinal))
        {
            return false;
        }

        if (!SceneFlowManager.Instance.TryLoadRespawnAtSavedCheckpoint(checkpointSceneName))
        {
            SceneManager.LoadScene(checkpointSceneName);
        }

        return true;
    }
}
