#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using UnityEngine;

public sealed class QuestProgressDebugPanel : MonoBehaviour
{
    private const string RuntimeObjectName = "QuestProgressDebugPanel";
    private const KeyCode ToggleKey = KeyCode.F8;

    private static QuestProgressDebugPanel _instance;

    private Rect _windowRect = new Rect(24f, 24f, 420f, 470f);
    private bool _visible;
    private string _sceneName = string.Empty;
    private string _checkpointId = string.Empty;
    private int _stepIndex;
    private bool _goalKnown;
    private bool _sealAccepted;
    private bool _bossFightStarted;
    private bool _bossCleared;
    private bool _stoneGolemCleared;
    private bool _endingClaimed;
    private bool _rearGateOpened;
    private List<string> _activatedBonfireIds = new();
    private string _activatedBonfireIdsSummary = string.Empty;
    private string _statusMessage = string.Empty;
    private Vector2 _scrollPosition;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (_instance != null)
        {
            return;
        }

        GameObject host = new GameObject(RuntimeObjectName);
        _instance = host.AddComponent<QuestProgressDebugPanel>();
        DontDestroyOnLoad(host);
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);
        PullFromManagers();
    }

    private void Update()
    {
        if (Input.GetKeyDown(ToggleKey))
        {
            _visible = !_visible;
            if (_visible)
            {
                PullFromManagers();
            }
        }
    }

    private void OnGUI()
    {
        if (!_visible)
        {
            return;
        }

        GUI.depth = -1000;
        _windowRect = GUI.Window(GetInstanceID(), _windowRect, DrawWindow, "Quest Progress Debug");
    }

    private void DrawWindow(int windowId)
    {
        BellInteractable bell = FindFirstObjectByType<BellInteractable>(FindObjectsInactive.Exclude);
        PlayerController player = FindFirstObjectByType<PlayerController>(FindObjectsInactive.Exclude);

        _scrollPosition = GUILayout.BeginScrollView(_scrollPosition, false, true);

        GUILayout.Label($"Save Path: {SaveGameService.Instance.SavePath}");
        GUILayout.Label($"Has Save: {SaveGameService.Instance.HasSave}");
        GUILayout.Space(8f);

        GUILayout.Label("Scene Name");
        _sceneName = GUILayout.TextField(_sceneName ?? string.Empty);

        GUILayout.Label("Checkpoint Id");
        _checkpointId = GUILayout.TextField(_checkpointId ?? string.Empty);

        QuestProgressStep[] steps = (QuestProgressStep[])System.Enum.GetValues(typeof(QuestProgressStep));
        string[] stepNames = System.Array.ConvertAll(steps, step => step.ToString());
        GUILayout.Label($"Quest Step: {stepNames[_stepIndex]}");
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("<", GUILayout.Width(36f)))
        {
            _stepIndex = (_stepIndex - 1 + stepNames.Length) % stepNames.Length;
        }

        if (GUILayout.Button(">", GUILayout.Width(36f)))
        {
            _stepIndex = (_stepIndex + 1) % stepNames.Length;
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(6f);
        _goalKnown = GUILayout.Toggle(_goalKnown, "goalKnown");
        _sealAccepted = GUILayout.Toggle(_sealAccepted, "sealAccepted");
        _bossFightStarted = GUILayout.Toggle(_bossFightStarted, "bossFightStarted");
        _bossCleared = GUILayout.Toggle(_bossCleared, "bossCleared");
        _stoneGolemCleared = GUILayout.Toggle(_stoneGolemCleared, "stoneGolemCleared");
        _endingClaimed = GUILayout.Toggle(_endingClaimed, "endingClaimed");
        _rearGateOpened = GUILayout.Toggle(_rearGateOpened, "rearGateOpened");

        GUILayout.Space(8f);
        GUILayout.Label($"Activated Bonfires: {_activatedBonfireIds.Count}");
        GUILayout.TextArea(string.IsNullOrWhiteSpace(_activatedBonfireIdsSummary) ? "(none)" : _activatedBonfireIdsSummary, GUILayout.MinHeight(72f));

        GUILayout.Space(10f);
        if (GUILayout.Button("Refresh From Managers"))
        {
            PullFromManagers();
            _statusMessage = "현재 런타임 상태를 다시 읽었습니다.";
        }

        if (GUILayout.Button("Apply And Save"))
        {
            ApplyToManagers();
            _statusMessage = "입력한 상태를 저장하고 런타임에 다시 적용했습니다.";
        }

        if (GUILayout.Button("Reload From Disk"))
        {
            SaveGameService.Instance.DebugReloadFromDisk();
            QuestProgressionManager.Instance.ReloadFromSave();
            PullFromManagers();
            _statusMessage = "디스크 저장 데이터를 다시 불러왔습니다.";
        }

        if (GUILayout.Button("Delete Save File"))
        {
            SaveGameService.Instance.DebugDeleteSave();
            QuestProgressionManager.Instance.DebugResetProgress(persist: false);
            PullFromManagers();
            _statusMessage = "저장 파일을 삭제하고 런타임 상태를 기본값으로 되돌렸습니다.";
        }

        GUILayout.Space(8f);
        if (GUILayout.Button("Preset: Story Start"))
        {
            ApplyPreset(QuestProgressStep.Arrival, checkpointId: "Checkpoint_Start", goalKnown: false, sealAccepted: false, bossFightStarted: false, bossCleared: false, stoneGolemCleared: false, endingClaimed: false, rearGateOpened: false);
        }

        if (GUILayout.Button("Preset: Goal Known"))
        {
            ApplyPreset(QuestProgressStep.Discovery, checkpointId: _checkpointId, goalKnown: true, sealAccepted: false, bossFightStarted: false, bossCleared: false, stoneGolemCleared: false, endingClaimed: false, rearGateOpened: false);
        }

        if (GUILayout.Button("Preset: Seal Accepted"))
        {
            ApplyPreset(QuestProgressStep.Opened, checkpointId: _checkpointId, goalKnown: true, sealAccepted: true, bossFightStarted: false, bossCleared: false, stoneGolemCleared: true, endingClaimed: false, rearGateOpened: false);
        }

        if (GUILayout.Button("Preset: Boss Cleared"))
        {
            ApplyPreset(QuestProgressStep.Cleared, checkpointId: _checkpointId, goalKnown: true, sealAccepted: true, bossFightStarted: false, bossCleared: true, stoneGolemCleared: true, endingClaimed: false, rearGateOpened: true);
        }

        GUILayout.Space(12f);
        DrawBellDebugSection(bell, player);

        GUILayout.Space(10f);
        GUILayout.Label($"Status: {_statusMessage}");

        GUILayout.EndScrollView();
        GUI.DragWindow(new Rect(0f, 0f, 10000f, 24f));
    }

    private void DrawBellDebugSection(BellInteractable bell, PlayerController player)
    {
        GUILayout.Label("Bell Debug");
        GUILayout.Label($"Bell Found: {GetPresenceLabel(bell != null)}");
        GUILayout.Label($"Player Found: {GetPresenceLabel(player != null)}");

        GUI.enabled = bell != null;
        if (GUILayout.Button("Bell: Trigger Sequence"))
        {
            if (player == null)
            {
                _statusMessage = "Bell test failed: PlayerController not found.";
            }
            else if (bell.DebugTriggerBellSequence(player, prepareQuestState: true))
            {
                PullFromManagers();
                _statusMessage = "Bell test sequence started.";
            }
            else
            {
                _statusMessage = "Bell test failed: QuestProgressionManager not found.";
            }
        }

        if (GUILayout.Button("Bell: Prepare Quest State"))
        {
            QuestProgressionManager progression = QuestProgressionManager.Instance;
            if (progression == null)
            {
                _statusMessage = "Bell debug failed: QuestProgressionManager not found.";
            }
            else
            {
                if (!progression.InjuredKnightQuestItemCollected)
                {
                    progression.MarkInjuredKnightQuestItemCollected(markGoalKnown: true);
                }

                if (!progression.BellPepperInstalled)
                {
                    progression.InstallBellPepper(markGoalKnown: true);
                }

                PullFromManagers();
                _statusMessage = "Bell quest state prepared.";
            }
        }
        GUI.enabled = true;
    }

    private void PullFromManagers()
    {
        QuestProgressSaveData snapshot = QuestProgressionManager.Instance.GetSnapshot();
        _sceneName = string.IsNullOrWhiteSpace(SaveGameService.Instance.CurrentSceneName)
            ? TitleFlowConstants.GameSceneName
            : SaveGameService.Instance.CurrentSceneName;
        _checkpointId = snapshot.currentCheckpointId ?? string.Empty;
        _stepIndex = (int)snapshot.GetStepOrDefault();
        _goalKnown = snapshot.goalKnown;
        _sealAccepted = snapshot.sealAccepted;
        _bossFightStarted = snapshot.bossFightStarted;
        _bossCleared = snapshot.bossCleared;
        _stoneGolemCleared = snapshot.stoneGolemCleared;
        _endingClaimed = snapshot.endingClaimed;
        _rearGateOpened = snapshot.rearGateOpened;
        _activatedBonfireIds = snapshot.activatedBonfireIds != null
            ? new List<string>(snapshot.activatedBonfireIds)
            : new List<string>();
        _activatedBonfireIdsSummary = _activatedBonfireIds.Count > 0
            ? string.Join("\n", _activatedBonfireIds)
            : string.Empty;
    }

    private void ApplyToManagers()
    {
        QuestProgressSaveData snapshot = new QuestProgressSaveData
        {
            currentCheckpointId = _checkpointId ?? string.Empty,
            activatedBonfireIds = new List<string>(_activatedBonfireIds),
            goalKnown = _goalKnown,
            sealAccepted = _sealAccepted,
            bossFightStarted = _bossFightStarted,
            bossCleared = _bossCleared,
            stoneGolemCleared = _stoneGolemCleared,
            endingClaimed = _endingClaimed,
            rearGateOpened = _rearGateOpened
        };

        QuestProgressStep[] steps = (QuestProgressStep[])System.Enum.GetValues(typeof(QuestProgressStep));
        snapshot.SetStep(steps[Mathf.Clamp(_stepIndex, 0, steps.Length - 1)]);

        SaveGameService.Instance.DebugSetSceneName(
            string.IsNullOrWhiteSpace(_sceneName) ? TitleFlowConstants.GameSceneName : _sceneName,
            hasStartedGame: true);
        QuestProgressionManager.Instance.DebugApplySnapshot(snapshot, persist: true);
    }

    private void ApplyPreset(
        QuestProgressStep step,
        string checkpointId,
        bool goalKnown,
        bool sealAccepted,
        bool bossFightStarted,
        bool bossCleared,
        bool stoneGolemCleared,
        bool endingClaimed,
        bool rearGateOpened)
    {
        _stepIndex = (int)step;
        _checkpointId = checkpointId ?? string.Empty;
        _goalKnown = goalKnown;
        _sealAccepted = sealAccepted;
        _bossFightStarted = bossFightStarted;
        _bossCleared = bossCleared;
        _stoneGolemCleared = stoneGolemCleared;
        _endingClaimed = endingClaimed;
        _rearGateOpened = rearGateOpened;
        ApplyToManagers();
        _statusMessage = $"프리셋 적용: {step}";
    }

    private static string GetPresenceLabel(bool present)
    {
        return present ? "Yes" : "No";
    }
}
#endif
