using System;
using UnityEngine;

public static class GameLoopEvents
{
    public static event Action<EnemyBase> BossFightStarted;
    public static event Action<EnemyBase, bool> BossFightEnded;
    public static event Action<PlayerController> PlayerDied;
    public static event Action<PlayerController, Transform> PlayerRespawned;
    public static event Action<float, float> BossHPChanged;
    public static event Action<int, int> EstusChanged;

    public static void RaiseBossFightStarted(EnemyBase boss)
    {
        BossFightStarted?.Invoke(boss);
    }

    public static void RaiseBossFightEnded(EnemyBase boss, bool cleared)
    {
        BossFightEnded?.Invoke(boss, cleared);
    }

    public static void RaisePlayerDied(PlayerController player)
    {
        PlayerDied?.Invoke(player);
    }

    public static void RaisePlayerRespawned(PlayerController player, Transform checkpoint)
    {
        PlayerRespawned?.Invoke(player, checkpoint);
    }

    public static void RaiseBossHPChanged(float current, float max)
    {
        BossHPChanged?.Invoke(current, max);
    }

    public static void RaiseEstusChanged(int current, int max)
    {
        EstusChanged?.Invoke(current, max);
    }
}
