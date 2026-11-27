using System;
using UnityEngine;

public sealed class BossFightFlowCoordinator
{
    private readonly GameObject _ownerObject;
    private readonly FogWallController _fogWall;
    private readonly UIBossHealthBar _bossHealthBar;
    private readonly AudioClip _bossBgmClip;
    private readonly bool _stopBossBgmOnFightEnd;
    private readonly bool _openFogWallOnFightEnd;

    private EnemyBase _bossEnemy;
    private AudioSource _bossBgmSource;
    private bool _isFightStarted;
    private bool _isBossCleared;
    private bool _isBossEventBound;

    public BossFightFlowCoordinator(
        GameObject ownerObject,
        EnemyBase bossEnemy,
        FogWallController fogWall,
        UIBossHealthBar bossHealthBar,
        AudioClip bossBgmClip,
        AudioSource bossBgmSource,
        bool stopBossBgmOnFightEnd,
        bool openFogWallOnFightEnd)
    {
        _ownerObject = ownerObject;
        _bossEnemy = bossEnemy;
        _fogWall = fogWall;
        _bossHealthBar = bossHealthBar;
        _bossBgmClip = bossBgmClip;
        _bossBgmSource = bossBgmSource;
        _stopBossBgmOnFightEnd = stopBossBgmOnFightEnd;
        _openFogWallOnFightEnd = openFogWallOnFightEnd;
    }

    public event Action<bool> FightEnded;

    public bool IsFightStarted => _isFightStarted;
    public bool IsBossCleared => _isBossCleared;
    public AudioSource BossBgmSource => _bossBgmSource;

    public void SyncBoss(EnemyBase bossEnemy)
    {
        if (_isBossEventBound && _bossEnemy != bossEnemy)
        {
            UnbindBossEvents();
        }

        _bossEnemy = bossEnemy;
    }

    public void InitializeSceneState()
    {
        EnsureBossBgmSource();

        if (_fogWall != null)
        {
            _fogWall.DisableWall();
        }
    }

    public void CleanupOnDisable()
    {
        UnbindBossEvents();

        if (!_isFightStarted)
        {
            StopBossBgm();
        }
    }

    public bool TryStartFight(PlayerController player)
    {
        if (player == null)
        {
            Debug.LogWarning("보스전 시작 실패: Player 참조가 없습니다.");
            return false;
        }

        if (_bossEnemy == null)
        {
            Debug.LogWarning("보스전 시작 실패: 보스 참조(EnemyBase)가 없습니다.");
            return false;
        }

        _isFightStarted = true;
        BindBossEvents();
        PlayBossBgm();

        if (_fogWall != null)
        {
            _fogWall.EnableWall();
        }

        if (_bossHealthBar != null)
        {
            _bossHealthBar.BindBoss(_bossEnemy);
        }

        GameLoopEvents.RaiseBossFightStarted(_bossEnemy);
        GameLoopEvents.RaiseBossHPChanged(_bossEnemy.CurrentHp, _bossEnemy.MaxHp);

        Debug.Log("보스 존 입장: 안개벽 활성 + 보스 UI 활성 + 보스 BGM 재생");
        return true;
    }

    public void HandlePlayerDied(bool endFightWhenPlayerDies)
    {
        if (!_isFightStarted || !endFightWhenPlayerDies)
        {
            return;
        }

        EndBossFight(false);
    }

    private void BindBossEvents()
    {
        if (_isBossEventBound || _bossEnemy == null)
        {
            return;
        }

        _bossEnemy.OnHpChanged += HandleBossHpChanged;
        _bossEnemy.OnDied += HandleBossDied;
        _isBossEventBound = true;
    }

    private void UnbindBossEvents()
    {
        if (!_isBossEventBound)
        {
            return;
        }

        if (_bossEnemy != null)
        {
            _bossEnemy.OnHpChanged -= HandleBossHpChanged;
            _bossEnemy.OnDied -= HandleBossDied;
        }

        _isBossEventBound = false;
    }

    private void HandleBossHpChanged(float current, float max)
    {
        GameLoopEvents.RaiseBossHPChanged(current, max);
    }

    private void HandleBossDied()
    {
        EndBossFight(true);
    }

    private void EndBossFight(bool cleared)
    {
        if (!_isFightStarted && !(cleared && !_isBossCleared))
        {
            return;
        }

        _isFightStarted = false;
        _isBossCleared = _isBossCleared || cleared;

        UnbindBossEvents();

        if (_stopBossBgmOnFightEnd)
        {
            StopBossBgm();
        }

        if (_openFogWallOnFightEnd && _fogWall != null)
        {
            _fogWall.DisableWall();
        }

        if (_bossHealthBar != null)
        {
            _bossHealthBar.UnbindBoss();
        }

        GameLoopEvents.RaiseBossFightEnded(_bossEnemy, cleared);
        Debug.Log(cleared ? "보스 처치: 보스전 종료 처리" : "플레이어 사망: 보스전 종료 처리");
        FightEnded?.Invoke(cleared);
    }

    private void EnsureBossBgmSource()
    {
        if (_bossBgmSource == null && _ownerObject != null)
        {
            _bossBgmSource = _ownerObject.GetComponent<AudioSource>();
        }

        if (_bossBgmSource == null && _ownerObject != null)
        {
            _bossBgmSource = _ownerObject.AddComponent<AudioSource>();
        }

        if (_bossBgmSource == null)
        {
            return;
        }

        _bossBgmSource.playOnAwake = false;
        _bossBgmSource.loop = true;
        _bossBgmSource.spatialBlend = 0f;
    }

    private void PlayBossBgm()
    {
        if (_bossBgmClip == null)
        {
            return;
        }

        EnsureBossBgmSource();
        if (_bossBgmSource == null)
        {
            return;
        }

        _bossBgmSource.clip = _bossBgmClip;
        if (!_bossBgmSource.isPlaying)
        {
            _bossBgmSource.Play();
        }
    }

    private void StopBossBgm()
    {
        if (_bossBgmSource != null && _bossBgmSource.isPlaying)
        {
            _bossBgmSource.Stop();
        }
    }
}
