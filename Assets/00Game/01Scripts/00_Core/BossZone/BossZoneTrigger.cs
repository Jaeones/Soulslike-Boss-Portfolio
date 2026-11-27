using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Collider))]
public class BossZoneTrigger : MonoBehaviour
{
    private const string PreferredFogWallObjectName = "FX_Environment_FogWall_Entrance_Loop";

    [Header("참조")]
    [SerializeField] private PlayerController player;
    [SerializeField] private EnemyBase bossEnemy;
    [SerializeField] private BossController bossProxy;
    [SerializeField] private FogWallController fogWall;
    [SerializeField] private UIBossHealthBar bossHealthBar;

    [Header("오디오")]
    [SerializeField] private AudioClip bossBgmClip;
    [SerializeField] private AudioClip bossPhase2BgmClip;
    [SerializeField] private AudioSource bossBgmSource;
    [SerializeField] private bool stopBossBgmOnFightEnd = true;
    [SerializeField] [Range(0f, 1f)] private float bossBgmBaseVolume = 1f;

    [Header("정책")]
    [SerializeField] private bool endFightWhenPlayerDies = true;
    [SerializeField] private bool disableTriggerAfterBossClear = true;
    [SerializeField] private bool openFogWallOnFightEnd = true;
    [SerializeField] private bool requireSealAcceptance;
    [SerializeField] private bool requireManualFirstEncounterStart;
    [SerializeField] private bool markGoalKnownWhenBlocked = true;
    [SerializeField] private float fightStartHudShowDelay = 1f;
#if UNITY_EDITOR
    [SerializeField] private bool requireManualStartInEditor = true;
#endif

    private bool _isFightStarted;
    private bool _isBossCleared;
    private bool _isBossEventBound;
    private bool _isEntranceSequenceActive;
    private bool _isBossPresentationActive;
    private bool _manualEncounterAuthorized;
    private BossEntranceDirector _entranceDirector;
    private Coroutine _bossBgmFadeRoutine;
    private Coroutine _bossHudShowRoutine;
    private bool _hasCapturedBossBgmBaseVolume;

    public PlayerController Player => player;
    public EnemyBase BossEnemy => bossEnemy;
    public BossController BossProxy => bossProxy;
    public FogWallController FogWall => fogWall;
    public bool RequireSealAcceptance => requireSealAcceptance;
    public bool IsFightStarted => _isFightStarted;
    public bool IsBossCleared => _isBossCleared;
    public bool IsEntranceSequenceActive => _isEntranceSequenceActive;

    private void Reset()
    {
        Collider triggerCol = GetComponent<Collider>();
        if (triggerCol != null)
        {
            triggerCol.isTrigger = true;
        }
    }

    private void Awake()
    {
        if (bossEnemy == null)
        {
            bossEnemy = GetComponentInChildren<EnemyBase>();
        }

        if (bossProxy == null)
        {
            bossProxy = bossEnemy as BossController;
        }

        if (bossProxy == null)
        {
            bossProxy = GetComponentInChildren<BossController>(true);
        }

        _entranceDirector = GetComponent<BossEntranceDirector>();
        fogWall = ResolvePreferredFogWallController();

        EnsureBossBgmSource();

        // 씬 초기 상태가 잘못 세팅되어도 보스전 시작 전에 벽이 비활성 상태가 되도록 보정
        if (fogWall != null)
        {
            fogWall.DisableWall();
        }
    }

    private void Start()
    {
        // 트리거 박스가 너무 작으면 진입 판정이 거의 발생하지 않음
        BoxCollider box = GetComponent<BoxCollider>();
        if (box != null && box.size.sqrMagnitude <= 1.1f)
        {
            Debug.LogWarning("BossZoneTrigger 콜라이더 크기가 매우 작습니다. 입구를 충분히 덮도록 Size를 키워주세요.");
        }
    }

    private void OnEnable()
    {
        GameLoopEvents.PlayerDied += HandlePlayerDied;
    }

    private void OnDisable()
    {
        GameLoopEvents.PlayerDied -= HandlePlayerDied;
        UnbindBossEvents();
        StopBossHudShowRoutine();

        if (!_isFightStarted)
        {
            CancelBossFightPresentation();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        PlayerController enteredPlayer = other.GetComponentInParent<PlayerController>();
        TryHandleEncounterStart(enteredPlayer, allowEntranceDirector: true);
    }

    public bool TryHandleSceneEntry(PlayerController enteredPlayer)
    {
        return TryHandleEncounterStart(enteredPlayer, allowEntranceDirector: true);
    }

    public bool TryBeginEntranceSequence(PlayerController enteredPlayer)
    {
        if (_isBossCleared || _isFightStarted || _isEntranceSequenceActive)
        {
            return false;
        }

        if (!CanStartEncounter(enteredPlayer, markBlockedGoalKnown: false))
        {
            return false;
        }

        _manualEncounterAuthorized = true;
        _isEntranceSequenceActive = true;
        return true;
    }

    public void CancelEntranceSequence()
    {
        _isEntranceSequenceActive = false;
    }

    public bool StartBossFight()
    {
        return BeginBossFightPresentation() && FinalizeBossFightStart();
    }

    public bool BeginBossFightPresentation()
    {
        return BeginBossFightPresentationInternal(playBossBgm: true);
    }

    public bool BeginBossFightPresentationWithoutBgm()
    {
        return BeginBossFightPresentationInternal(playBossBgm: false);
    }

    private bool BeginBossFightPresentationInternal(bool playBossBgm)
    {
        if (player == null)
        {
            Debug.LogWarning("보스전 시작 실패: Player 참조가 없습니다.");
            _isEntranceSequenceActive = false;
            return false;
        }

        if (bossEnemy == null)
        {
            Debug.LogWarning("보스전 시작 실패: 보스 참조(EnemyBase)가 없습니다.");
            _isEntranceSequenceActive = false;
            return false;
        }

        if (_isBossPresentationActive || _isFightStarted)
        {
            _isEntranceSequenceActive = false;
            return true;
        }

        _manualEncounterAuthorized = true;
        _isBossPresentationActive = true;
        _isEntranceSequenceActive = false;
        if (playBossBgm)
        {
            PlayBossBgm();
        }

        if (fogWall != null)
        {
            fogWall.EnableWall();
        }

        return true;
    }

    public bool FinalizeBossFightStart()
    {
        if (player == null)
        {
            Debug.LogWarning("보스전 최종 시작 실패: Player 참조가 없습니다.");
            _isEntranceSequenceActive = false;
            return false;
        }

        if (bossEnemy == null)
        {
            Debug.LogWarning("보스전 최종 시작 실패: 보스 참조(EnemyBase)가 없습니다.");
            _isEntranceSequenceActive = false;
            return false;
        }

        if (_isFightStarted)
        {
            _isEntranceSequenceActive = false;
            return true;
        }

        if (!_isBossPresentationActive && !BeginBossFightPresentation())
        {
            return false;
        }

        _isFightStarted = true;
        _isEntranceSequenceActive = false;
        BindBossEvents();

        if (bossHealthBar != null)
        {
            bossHealthBar.BindBoss(bossEnemy);
        }

        GameLoopEvents.RaiseBossFightStarted(bossEnemy);
        GameLoopEvents.RaiseBossHPChanged(bossEnemy.CurrentHp, bossEnemy.MaxHp);
        QuestProgressionManager.Instance.NotifyBossFightStarted();

        HideBossHud();
        ShowBossHudDelayed(fightStartHudShowDelay);

        Debug.Log("보스 존 입장: 안개벽 활성 + 보스 UI 활성 + 보스 BGM 재생");
        return true;
    }

    public bool StartBossFight(PlayerController enteredPlayer)
    {
        if (enteredPlayer != null)
        {
            player = enteredPlayer;
        }

        return StartBossFight();
    }

    public bool BeginBossFightPresentation(PlayerController enteredPlayer)
    {
        if (enteredPlayer != null)
        {
            player = enteredPlayer;
        }

        return BeginBossFightPresentation();
    }

    public bool BeginBossFightPresentationWithoutBgm(PlayerController enteredPlayer)
    {
        if (enteredPlayer != null)
        {
            player = enteredPlayer;
        }

        return BeginBossFightPresentationWithoutBgm();
    }

    public bool FinalizeBossFightStart(PlayerController enteredPlayer)
    {
        if (enteredPlayer != null)
        {
            player = enteredPlayer;
        }

        return FinalizeBossFightStart();
    }

    public void CancelBossFightPresentation()
    {
        if (_isFightStarted || !_isBossPresentationActive)
        {
            return;
        }

        _isBossPresentationActive = false;
        _isEntranceSequenceActive = false;
        StopBossBgm();

        if (fogWall != null)
        {
            fogWall.DisableWall();
        }
    }

    public void PlayPresentationBossBgm()
    {
        if (!_isBossPresentationActive && !_isFightStarted)
        {
            return;
        }

        PlayBossBgm();
    }

    public void SetBossHudVisible(bool visible)
    {
        StopBossHudShowRoutine();

        if (bossHealthBar == null)
        {
            return;
        }

        if (visible && bossEnemy != null)
        {
            bossHealthBar.BindBoss(bossEnemy);
            GameLoopEvents.RaiseBossHPChanged(bossEnemy.CurrentHp, bossEnemy.MaxHp);
        }

        bossHealthBar.SetVisible(visible);
    }

    public void HideBossHud()
    {
        SetBossHudVisible(false);
    }

    public void ShowBossHud()
    {
        SetBossHudVisible(true);
    }

    public void ShowBossHudDelayed(float delay)
    {
        StopBossHudShowRoutine();

        if (bossHealthBar == null)
        {
            return;
        }

        if (delay <= 0f)
        {
            ShowBossHud();
            return;
        }

        _bossHudShowRoutine = StartCoroutine(Co_ShowBossHudDelayed(delay));
    }

    public void AuthorizeManualEncounterStart()
    {
        _manualEncounterAuthorized = true;
    }

    public bool CanStartEncounter(PlayerController enteredPlayer, bool markBlockedGoalKnown = true)
    {
        if (_isBossCleared || _isFightStarted)
        {
            return false;
        }

        if (enteredPlayer == null && player == null)
        {
            return false;
        }

        if (enteredPlayer != null)
        {
            player = enteredPlayer;
        }

        QuestProgressionManager progression = QuestProgressionManager.Instance;
        if (RequiresManualAuthorization(progression))
        {
            return false;
        }

        if (progression == null || !progression.CanStartBossFight(requireSealAcceptance))
        {
            if (progression != null && markBlockedGoalKnown && markGoalKnownWhenBlocked)
            {
                progression.MarkGoalKnown();
            }

            Debug.Log("보스전 시작 차단: 선행 진행 조건이 아직 충족되지 않았습니다.");
            return false;
        }

        return true;
    }

    private bool TryHandleEncounterStart(PlayerController enteredPlayer, bool allowEntranceDirector)
    {
        if (!CanStartEncounter(enteredPlayer))
        {
            return false;
        }

        if (allowEntranceDirector &&
            _entranceDirector != null &&
            _entranceDirector.isActiveAndEnabled &&
            _entranceDirector.TryHandleTriggerEnter(enteredPlayer))
        {
            return true;
        }

        return StartBossFight();
    }

    private void BindBossEvents()
    {
        if (_isBossEventBound)
        {
            return;
        }

        if (bossEnemy != null)
        {
            bossEnemy.OnHpChanged += HandleBossHpChanged;
            bossEnemy.OnDied += HandleBossDied;
            _isBossEventBound = true;
        }
    }

    private void UnbindBossEvents()
    {
        if (!_isBossEventBound)
        {
            return;
        }

        if (bossEnemy != null)
        {
            bossEnemy.OnHpChanged -= HandleBossHpChanged;
            bossEnemy.OnDied -= HandleBossDied;
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

    private void HandlePlayerDied(PlayerController deadPlayer)
    {
        if (!_isFightStarted || !endFightWhenPlayerDies)
        {
            return;
        }

        EndBossFight(false);
    }

    private void EndBossFight(bool cleared)
    {
        if (!_isFightStarted && !(cleared && !_isBossCleared))
        {
            return;
        }

        _isFightStarted = false;
        _isBossCleared = _isBossCleared || cleared;
        _isEntranceSequenceActive = false;
        _isBossPresentationActive = false;

        UnbindBossEvents();
        StopBossHudShowRoutine();
        HideBossHud();

        if (stopBossBgmOnFightEnd)
        {
            StopBossBgm();
        }

        if (openFogWallOnFightEnd && fogWall != null)
        {
            fogWall.DisableWall();
        }

        if (bossHealthBar != null)
        {
            bossHealthBar.UnbindBoss();
        }

        GameLoopEvents.RaiseBossFightEnded(bossEnemy, cleared);
        QuestProgressionManager.Instance.NotifyBossFightEnded(cleared);

        if (!cleared)
        {
            bossProxy?.ResetForEncounterRetry();
        }

        Debug.Log(cleared ? "보스 처치: 보스전 종료 처리" : "플레이어 사망: 보스전 종료 처리");

        if (cleared && disableTriggerAfterBossClear)
        {
            Collider triggerCol = GetComponent<Collider>();
            if (triggerCol != null)
            {
                triggerCol.enabled = false;
            }
        }
    }

    private void EnsureBossBgmSource()
    {
        if (bossBgmSource == null)
        {
            bossBgmSource = GetComponent<AudioSource>();
        }

        if (bossBgmSource == null)
        {
            bossBgmSource = gameObject.AddComponent<AudioSource>();
        }

        bossBgmSource.playOnAwake = false;
        bossBgmSource.loop = true;
        bossBgmSource.spatialBlend = 0f;

        if (!_hasCapturedBossBgmBaseVolume)
        {
            float capturedVolume = bossBgmSource.volume;
            if (capturedVolume > 0f)
            {
                bossBgmBaseVolume = Mathf.Clamp01(capturedVolume);
            }

            if (bossBgmBaseVolume <= 0f)
            {
                bossBgmBaseVolume = 1f;
            }

            _hasCapturedBossBgmBaseVolume = true;
        }
    }

    private FogWallController ResolvePreferredFogWallController()
    {
        FogWallController[] fogWalls = FindObjectsByType<FogWallController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < fogWalls.Length; i++)
        {
            FogWallController candidate = fogWalls[i];
            if (candidate == null || candidate.gameObject == null)
            {
                continue;
            }

            if (string.Equals(candidate.gameObject.name, PreferredFogWallObjectName, System.StringComparison.Ordinal))
            {
                return candidate;
            }
        }

        return fogWall;
    }

    private bool RequiresManualAuthorization(QuestProgressionManager progression)
    {
        if (!requireManualFirstEncounterStart || _manualEncounterAuthorized)
        {
            return false;
        }

#if UNITY_EDITOR
        if (requireManualStartInEditor)
        {
            return true;
        }
#endif

        return progression == null || !progression.BossIntroSeen;
    }

    private void PlayBossBgm()
    {
        if (bossBgmClip == null)
        {
            return;
        }

        EnsureBossBgmSource();
        StopBossBgmFade();
        if (bossBgmSource.clip != bossBgmClip && bossBgmSource.isPlaying)
        {
            bossBgmSource.Stop();
        }

        bossBgmSource.clip = bossBgmClip;
        bossBgmSource.volume = bossBgmBaseVolume;
        if (!bossBgmSource.isPlaying)
        {
            bossBgmSource.Play();
        }
    }

    private void StopBossBgm()
    {
        StopBossBgmFade();
        if (bossBgmSource != null && bossBgmSource.isPlaying)
        {
            bossBgmSource.Stop();
        }

        if (bossBgmSource != null)
        {
            bossBgmSource.volume = bossBgmBaseVolume;
        }
    }

    public void FadeBossBgmVolumeScale(float targetScale, float duration)
    {
        if (bossBgmClip == null)
        {
            return;
        }

        EnsureBossBgmSource();
        StartBossBgmFade(Mathf.Clamp01(targetScale) * bossBgmBaseVolume, duration);
    }

    public void RestoreBossBgmVolume(float duration)
    {
        if (bossBgmClip == null)
        {
            return;
        }

        EnsureBossBgmSource();
        bossBgmSource.clip = bossBgmClip;
        if (!bossBgmSource.isPlaying)
        {
            bossBgmSource.Play();
        }

        StartBossBgmFade(bossBgmBaseVolume, duration);
    }

    public void PlayPhase2BossBgm(float fadeDuration)
    {
        if (bossPhase2BgmClip == null)
        {
            RestoreBossBgmVolume(fadeDuration);
            return;
        }

        EnsureBossBgmSource();
        StopBossBgmFade();

        if (bossBgmSource.clip != bossPhase2BgmClip && bossBgmSource.isPlaying)
        {
            bossBgmSource.Stop();
        }

        bossBgmSource.clip = bossPhase2BgmClip;
        bossBgmSource.volume = fadeDuration > 0f ? 0f : bossBgmBaseVolume;

        if (!bossBgmSource.isPlaying)
        {
            bossBgmSource.Play();
        }

        StartBossBgmFade(bossBgmBaseVolume, fadeDuration);
    }

    private void StartBossBgmFade(float targetVolume, float duration)
    {
        EnsureBossBgmSource();
        StopBossBgmFade();

        if (duration <= 0f)
        {
            bossBgmSource.volume = targetVolume;
            return;
        }

        _bossBgmFadeRoutine = StartCoroutine(Co_FadeBossBgmVolume(targetVolume, duration));
    }

    private void StopBossBgmFade()
    {
        if (_bossBgmFadeRoutine == null)
        {
            return;
        }

        StopCoroutine(_bossBgmFadeRoutine);
        _bossBgmFadeRoutine = null;
    }

    private void StopBossHudShowRoutine()
    {
        if (_bossHudShowRoutine == null)
        {
            return;
        }

        StopCoroutine(_bossHudShowRoutine);
        _bossHudShowRoutine = null;
    }

    private IEnumerator Co_ShowBossHudDelayed(float delay)
    {
        yield return new WaitForSeconds(delay);
        _bossHudShowRoutine = null;
        ShowBossHud();
    }

    private IEnumerator Co_FadeBossBgmVolume(float targetVolume, float duration)
    {
        if (bossBgmSource == null)
        {
            _bossBgmFadeRoutine = null;
            yield break;
        }

        float startVolume = bossBgmSource.volume;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            bossBgmSource.volume = Mathf.Lerp(startVolume, targetVolume, t);
            yield return null;
        }

        bossBgmSource.volume = targetVolume;
        _bossBgmFadeRoutine = null;
    }

    public void SyncProgressionState(bool bossCleared)
    {
        _isFightStarted = false;
        _isBossCleared = bossCleared;
        _isEntranceSequenceActive = false;
        _isBossPresentationActive = false;
        _manualEncounterAuthorized = false;

        UnbindBossEvents();
        StopBossBgm();

        if (fogWall != null)
        {
            fogWall.DisableWall();
        }

        if (bossHealthBar != null)
        {
            bossHealthBar.UnbindBoss();
        }

        if (bossEnemy != null && bossEnemy.gameObject.activeSelf == bossCleared)
        {
            bossEnemy.gameObject.SetActive(!bossCleared);
        }

        Collider triggerCol = GetComponent<Collider>();
        if (triggerCol != null)
        {
            triggerCol.enabled = !(bossCleared && disableTriggerAfterBossClear);
        }
    }

}
