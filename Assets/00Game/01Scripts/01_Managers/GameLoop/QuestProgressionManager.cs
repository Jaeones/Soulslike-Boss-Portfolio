using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class QuestProgressionManager : Singleton<QuestProgressionManager>
{
    [Header("Debug")]
    [SerializeField] private bool logStateChanges;

    private QuestProgressSaveData _state = QuestProgressSaveData.CreateDefault();

    public event Action StateChanged;

    public QuestProgressStep CurrentStep => _state.GetStepOrDefault();
    public string CurrentCheckpointId => _state.currentCheckpointId;
    public string CurrentCheckpointSceneName => _state.currentCheckpointSceneName;
    public IReadOnlyList<string> ActivatedBonfireIds => _state.activatedBonfireIds;
    public bool GoalKnown => _state.goalKnown;
    public bool SealAccepted => _state.sealAccepted;
    public bool BossFightStarted => _state.bossFightStarted;
    public bool BossIntroSeen => _state.bossIntroSeen;
    public bool BossCleared => _state.bossCleared;
    public bool StoneGolemCleared => _state.stoneGolemCleared;
    public bool EndingClaimed => _state.endingClaimed;
    public bool RearGateOpened => _state.rearGateOpened;
    public bool BellPepperInstalled => _state.bellPepperInstalled;
    public InjuredKnightQuestState CurrentInjuredKnightQuestState => _state.GetInjuredKnightQuestStateOrDefault();
    public bool InjuredKnightQuestItemCollected => _state.injuredKnightQuestItemCollected;

    protected override void Awake()
    {
        base.Awake();

        if (Instance != this)
        {
            return;
        }

        LoadFromSave();
    }

    private void OnEnable()
    {
        if (Instance != this)
        {
            return;
        }

        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void Start()
    {
        if (Instance != this)
        {
            return;
        }

        ApplySceneState();
    }

    private void OnDisable()
    {
        if (Instance != this)
        {
            return;
        }

        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    public QuestProgressSaveData GetSnapshot()
    {
        return _state.Clone();
    }

    public void ReloadFromSave()
    {
        LoadFromSave();
        ApplySceneState();
    }

    public void RegisterCheckpoint(string checkpointId, string checkpointSceneName = null)
    {
        if (string.IsNullOrWhiteSpace(checkpointId))
        {
            return;
        }

        string resolvedSceneName = string.IsNullOrWhiteSpace(checkpointSceneName)
            ? SceneManager.GetActiveScene().name
            : checkpointSceneName;

        if (string.Equals(_state.currentCheckpointId, checkpointId, StringComparison.Ordinal) &&
            string.Equals(_state.currentCheckpointSceneName, resolvedSceneName, StringComparison.Ordinal))
        {
            return;
        }

        _state.currentCheckpointId = checkpointId;
        _state.currentCheckpointSceneName = resolvedSceneName;
        PromoteStep(QuestProgressStep.Arrival);
        PersistState();
    }

    public bool IsBonfireActivated(string checkpointId)
    {
        if (string.IsNullOrWhiteSpace(checkpointId) || _state.activatedBonfireIds == null)
        {
            return false;
        }

        return _state.activatedBonfireIds.Contains(checkpointId);
    }

    public bool MarkBonfireActivated(string checkpointId)
    {
        if (string.IsNullOrWhiteSpace(checkpointId))
        {
            return false;
        }

        _state.activatedBonfireIds ??= new List<string>();
        if (_state.activatedBonfireIds.Contains(checkpointId))
        {
            return false;
        }

        _state.activatedBonfireIds.Add(checkpointId);
        PersistState();
        return true;
    }

    public void CommitBonfireSave(string checkpointId, string checkpointSceneName = null)
    {
        if (string.IsNullOrWhiteSpace(checkpointId))
        {
            return;
        }

        string resolvedSceneName = string.IsNullOrWhiteSpace(checkpointSceneName)
            ? SceneManager.GetActiveScene().name
            : checkpointSceneName;

        _state.currentCheckpointId = checkpointId;
        _state.currentCheckpointSceneName = resolvedSceneName;
        _state.activatedBonfireIds ??= new List<string>();
        if (!_state.activatedBonfireIds.Contains(checkpointId))
        {
            _state.activatedBonfireIds.Add(checkpointId);
        }

        PromoteStep(QuestProgressStep.Arrival);
        PersistState(saveToDisk: true, sceneNameOverride: resolvedSceneName);
    }

    public void MarkGoalKnown()
    {
        if (_state.goalKnown)
        {
            return;
        }

        _state.goalKnown = true;
        PromoteStep(QuestProgressStep.Discovery);
        PersistState();
    }

    public void MarkApproachConfirmed()
    {
        if (CurrentStep >= QuestProgressStep.Approach)
        {
            return;
        }

        PromoteStep(QuestProgressStep.Approach);
        PersistState();
    }

    public bool AcceptSeal()
    {
        if (_state.bossCleared || !_state.bellPepperInstalled)
        {
            return false;
        }

        if (!_state.goalKnown)
        {
            _state.goalKnown = true;
        }

        if (_state.sealAccepted)
        {
            return true;
        }

        _state.sealAccepted = true;
        PromoteStep(QuestProgressStep.Opened);
        PersistState();
        return true;
    }

    public bool InstallBellPepper(bool markGoalKnown = false)
    {
        bool changed = false;

        if (markGoalKnown && !_state.goalKnown)
        {
            _state.goalKnown = true;
            PromoteStep(QuestProgressStep.Discovery);
            changed = true;
        }

        if (!_state.injuredKnightQuestItemCollected)
        {
            if (changed)
            {
                PersistState();
            }

            return false;
        }

        if (_state.bellPepperInstalled)
        {
            if (changed)
            {
                PersistState();
            }

            return false;
        }

        _state.bellPepperInstalled = true;
        changed = true;

        PersistState();
        return true;
    }

    public bool MarkInjuredKnightQuestItemCollected(bool markGoalKnown = false)
    {
        bool changed = false;

        if (markGoalKnown && !_state.goalKnown)
        {
            _state.goalKnown = true;
            PromoteStep(QuestProgressStep.Discovery);
            changed = true;
        }

        if (_state.injuredKnightQuestItemCollected)
        {
            if (!_state.stoneGolemCleared)
            {
                _state.stoneGolemCleared = true;
                changed = true;
            }

            if (changed)
            {
                PersistState();
            }

            return false;
        }

        _state.injuredKnightQuestItemCollected = true;
        _state.stoneGolemCleared = true;
        changed = true;

        PersistState();
        return true;
    }

    public bool CanStartBossFight(bool requireSealAcceptance)
    {
        if (_state.bossCleared)
        {
            return false;
        }

        return !requireSealAcceptance || _state.sealAccepted;
    }

    public void NotifyBossFightStarted()
    {
        if (_state.bossCleared || _state.bossFightStarted)
        {
            return;
        }

        _state.bossFightStarted = true;
        PromoteStep(QuestProgressStep.Encounter);
        PersistState();
    }

    public void MarkBossIntroSeen()
    {
        if (_state.bossIntroSeen)
        {
            return;
        }

        _state.bossIntroSeen = true;
        PersistState();
    }

    public void NotifyBossFightEnded(bool cleared)
    {
        bool changed = false;

        if (_state.bossFightStarted)
        {
            _state.bossFightStarted = false;
            changed = true;
        }

        if (cleared && !_state.bossCleared)
        {
            _state.bossCleared = true;
            _state.rearGateOpened = true;
            PromoteStep(QuestProgressStep.Cleared);
            changed = true;
        }

        if (changed)
        {
            PersistState(saveToDisk: cleared);
        }
    }

    public bool NotifyStoneGolemCleared()
    {
        if (_state.stoneGolemCleared)
        {
            return false;
        }

        _state.stoneGolemCleared = true;
        PersistState(saveToDisk: true);
        return true;
    }

    public void MarkEndingClaimed()
    {
        if (!_state.bossCleared || _state.endingClaimed)
        {
            return;
        }

        _state.endingClaimed = true;
        _state.rearGateOpened = true;
        PromoteStep(QuestProgressStep.Ending);
        PersistState();
    }

    public void SetRearGateOpened(bool opened)
    {
        if (_state.rearGateOpened == opened)
        {
            return;
        }

        _state.rearGateOpened = opened;
        PersistState();
    }

    public void MarkInjuredKnightQuestOffered()
    {
        if (CurrentInjuredKnightQuestState != InjuredKnightQuestState.None)
        {
            return;
        }

        _state.SetInjuredKnightQuestState(InjuredKnightQuestState.Offered);
        PersistState();
    }

    public void AcceptInjuredKnightQuest(bool markGoalKnown = false)
    {
        if (markGoalKnown)
        {
            MarkGoalKnown();
        }

        if (CurrentInjuredKnightQuestState == InjuredKnightQuestState.Accepted)
        {
            return;
        }

        _state.SetInjuredKnightQuestState(InjuredKnightQuestState.Accepted);
        PersistState();
    }

    public void DeclineInjuredKnightQuest()
    {
        if (CurrentInjuredKnightQuestState == InjuredKnightQuestState.Declined)
        {
            return;
        }

        _state.SetInjuredKnightQuestState(InjuredKnightQuestState.Declined);
        PersistState();
    }

    public void CompleteInjuredKnightQuest()
    {
        if (CurrentInjuredKnightQuestState == InjuredKnightQuestState.Completed)
        {
            return;
        }

        _state.SetInjuredKnightQuestState(InjuredKnightQuestState.Completed);
        PersistState();
    }

    public void DebugApplySnapshot(QuestProgressSaveData snapshot, bool persist = true)
    {
        _state = snapshot != null ? snapshot.Clone() : QuestProgressSaveData.CreateDefault();
        NormalizeState();

        if (persist)
        {
            SaveGameService.Instance.SaveQuestProgress(_state);
        }

        ApplySceneState();

        if (logStateChanges)
        {
            Debug.Log($"[QuestProgressionManager][DebugApply] step={_state.questStep}, checkpoint={_state.currentCheckpointId}, goalKnown={_state.goalKnown}, sealAccepted={_state.sealAccepted}, bossCleared={_state.bossCleared}, stoneGolemCleared={_state.stoneGolemCleared}, endingClaimed={_state.endingClaimed}");
        }
    }

    public void DebugResetProgress(bool persist = true)
    {
        DebugApplySnapshot(QuestProgressSaveData.CreateDefault(), persist);
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (string.IsNullOrWhiteSpace(scene.name))
        {
            return;
        }

        LoadFromSave();
        ApplySceneState();
    }

    private void LoadFromSave()
    {
        _state = SaveGameService.Instance.GetQuestProgressCopy();
        NormalizeState();
    }

    private void NormalizeState()
    {
        _state ??= QuestProgressSaveData.CreateDefault();
        _state.activatedBonfireIds ??= new List<string>();
        _state.SetInjuredKnightQuestState(_state.GetInjuredKnightQuestStateOrDefault());

        QuestProgressStep currentStep = _state.GetStepOrDefault();
        _state.SetStep(currentStep);
        _state.bossFightStarted = false;

        if (_state.bellPepperInstalled || _state.sealAccepted)
        {
            _state.injuredKnightQuestItemCollected = true;
            _state.bellPepperInstalled = true;
            _state.stoneGolemCleared = true;
        }

        if (_state.endingClaimed)
        {
            _state.bossCleared = true;
            _state.stoneGolemCleared = true;
            _state.rearGateOpened = true;
            PromoteStep(QuestProgressStep.Ending);
            return;
        }

        if (_state.bossCleared)
        {
            _state.stoneGolemCleared = true;
            _state.rearGateOpened = true;
            PromoteStep(QuestProgressStep.Cleared);
        }
    }

    private void ApplySceneState()
    {
        RestoreCheckpoint();
        SyncBossZoneState();
        SyncStoneGolemState();
        StateChanged?.Invoke();
    }

    private void RestoreCheckpoint()
    {
        if (string.IsNullOrWhiteSpace(_state.currentCheckpointId))
        {
            return;
        }

        string activeSceneName = SceneManager.GetActiveScene().name;
        if (!string.IsNullOrWhiteSpace(_state.currentCheckpointSceneName) &&
            !string.Equals(_state.currentCheckpointSceneName, activeSceneName, StringComparison.Ordinal))
        {
            return;
        }

        CheckpointManager checkpointManager = UnityEngine.Object.FindFirstObjectByType<CheckpointManager>(FindObjectsInactive.Include);
        if (checkpointManager == null)
        {
            return;
        }

        checkpointManager.TryRestoreCheckpointById(_state.currentCheckpointId);
    }

    private void SyncBossZoneState()
    {
        BossZoneTrigger[] triggers = UnityEngine.Object.FindObjectsByType<BossZoneTrigger>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (BossZoneTrigger trigger in triggers)
        {
            if (trigger != null)
            {
                trigger.SyncProgressionState(_state.bossCleared);
            }
        }
    }

    private void SyncStoneGolemState()
    {
        StoneGolemController[] stoneGolems = UnityEngine.Object.FindObjectsByType<StoneGolemController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (StoneGolemController stoneGolem in stoneGolems)
        {
            if (stoneGolem != null)
            {
                stoneGolem.SyncProgressionState(_state.stoneGolemCleared, _state.injuredKnightQuestItemCollected);
            }
        }
    }

    private void PromoteStep(QuestProgressStep targetStep)
    {
        if (targetStep <= CurrentStep)
        {
            return;
        }

        _state.SetStep(targetStep);
    }

    private void PersistState(bool saveToDisk = false, string sceneNameOverride = null)
    {
        NormalizeState();

        if (saveToDisk)
        {
            string sceneName = string.IsNullOrWhiteSpace(sceneNameOverride)
                ? SceneManager.GetActiveScene().name
                : sceneNameOverride;
            SaveGameService.Instance.SaveGame(sceneName, _state);
        }

        if (logStateChanges)
        {
            Debug.Log($"[QuestProgressionManager] step={_state.questStep}, checkpoint={_state.currentCheckpointId}, goalKnown={_state.goalKnown}, sealAccepted={_state.sealAccepted}, bossCleared={_state.bossCleared}, stoneGolemCleared={_state.stoneGolemCleared}, endingClaimed={_state.endingClaimed}");
        }

        StateChanged?.Invoke();
    }
}
