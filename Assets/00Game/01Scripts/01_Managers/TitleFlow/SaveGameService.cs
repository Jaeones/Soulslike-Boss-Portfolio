using System;
using System.IO;
using UnityEngine;

public sealed class SaveGameService : Singleton<SaveGameService>
{
    private const int CurrentVersion = 2;
    private const string SaveFileName = "savegame.json";

    [Serializable]
    private sealed class SaveGameData
    {
        public int version = CurrentVersion;
        public string sceneName = TitleFlowConstants.GameSceneName;
        public string savedAtUtc = string.Empty;
        public bool hasStartedGame;
        public QuestProgressSaveData questProgress = QuestProgressSaveData.CreateDefault();
    }

    private SaveGameData _currentData = CreateDefaultData();

    public bool HasSave => _currentData != null && _currentData.hasStartedGame && !string.IsNullOrWhiteSpace(_currentData.sceneName);
    public string CurrentSceneName => HasSave ? _currentData.sceneName : string.Empty;
    public string SavePath => Path.Combine(Application.persistentDataPath, SaveFileName);

    protected override void Awake()
    {
        base.Awake();

        if (Instance != this)
        {
            return;
        }

        LoadFromDisk();
    }

    public void CreateNewGame(string sceneName)
    {
        _currentData = CreateDefaultData();
        string normalizedSceneName = TitleFlowConstants.NormalizeSceneName(sceneName);
        _currentData.sceneName = string.IsNullOrWhiteSpace(normalizedSceneName) ? TitleFlowConstants.GameSceneName : normalizedSceneName;
        _currentData.hasStartedGame = false;
        _currentData.savedAtUtc = string.Empty;
        SaveToDisk();
    }

    public void SaveCurrentScene(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            return;
        }

        if (_currentData == null)
        {
            _currentData = CreateDefaultData();
        }

        _currentData.sceneName = TitleFlowConstants.NormalizeSceneName(sceneName);
        _currentData.hasStartedGame = true;
        _currentData.savedAtUtc = DateTime.UtcNow.ToString("O");
        SaveToDisk();
    }

    public bool TryGetContinueScene(out string sceneName)
    {
        sceneName = string.Empty;
        if (!HasSave)
        {
            return false;
        }

        string checkpointSceneName = _currentData?.questProgress != null
            ? TitleFlowConstants.NormalizeSceneName(_currentData.questProgress.currentCheckpointSceneName)
            : string.Empty;

        sceneName = !string.IsNullOrWhiteSpace(checkpointSceneName)
            ? checkpointSceneName
            : TitleFlowConstants.NormalizeSceneName(CurrentSceneName);

        return !string.IsNullOrWhiteSpace(sceneName);
    }

    public void SaveGame(string sceneName, QuestProgressSaveData progressData)
    {
        if (_currentData == null)
        {
            _currentData = CreateDefaultData();
        }

        string normalizedSceneName = TitleFlowConstants.NormalizeSceneName(sceneName);
        _currentData.sceneName = string.IsNullOrWhiteSpace(normalizedSceneName) ? TitleFlowConstants.GameSceneName : normalizedSceneName;
        _currentData.questProgress = progressData != null
            ? progressData.Clone()
            : QuestProgressSaveData.CreateDefault();
        _currentData.hasStartedGame = true;
        _currentData.savedAtUtc = DateTime.UtcNow.ToString("O");
        SaveToDisk();
    }

    public QuestProgressSaveData GetQuestProgressCopy()
    {
        if (_currentData == null || _currentData.questProgress == null)
        {
            return QuestProgressSaveData.CreateDefault();
        }

        return _currentData.questProgress.Clone();
    }

    public void SaveQuestProgress(QuestProgressSaveData progressData)
    {
        if (_currentData == null)
        {
            _currentData = CreateDefaultData();
        }

        _currentData.questProgress = progressData != null
            ? progressData.Clone()
            : QuestProgressSaveData.CreateDefault();
        _currentData.hasStartedGame = true;
        _currentData.savedAtUtc = DateTime.UtcNow.ToString("O");
        SaveToDisk();
    }

    public void DebugSetSceneName(string sceneName, bool hasStartedGame = true)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            return;
        }

        if (_currentData == null)
        {
            _currentData = CreateDefaultData();
        }

        _currentData.sceneName = TitleFlowConstants.NormalizeSceneName(sceneName);
        _currentData.hasStartedGame = hasStartedGame;
        _currentData.savedAtUtc = DateTime.UtcNow.ToString("O");
        SaveToDisk();
    }

    public void DebugReloadFromDisk()
    {
        LoadFromDisk();
    }

    public void DebugDeleteSave()
    {
        try
        {
            if (File.Exists(SavePath))
            {
                File.Delete(SavePath);
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[SaveGameService] 디버그 저장 삭제 실패. path={SavePath} error={ex.Message}");
        }

        _currentData = CreateDefaultData();
    }

    private void LoadFromDisk()
    {
        if (!File.Exists(SavePath))
        {
            _currentData = CreateDefaultData();
            return;
        }

        try
        {
            string json = File.ReadAllText(SavePath);
            SaveGameData loaded = JsonUtility.FromJson<SaveGameData>(json);

            if (loaded == null || loaded.version <= 0)
            {
                Debug.LogWarning($"[SaveGameService] 저장 데이터가 비어 있어 기본값으로 대체합니다. path={SavePath}");
                _currentData = CreateDefaultData();
                return;
            }

            loaded.sceneName = NormalizeOrDefaultSceneName(loaded.sceneName);

            if (loaded.questProgress == null)
            {
                loaded.questProgress = QuestProgressSaveData.CreateDefault();
            }

            loaded.questProgress.currentCheckpointSceneName = TitleFlowConstants.NormalizeSceneName(loaded.questProgress.currentCheckpointSceneName);

            _currentData = loaded;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[SaveGameService] 저장 데이터를 읽지 못해 기본값으로 대체합니다. path={SavePath} error={ex.Message}");
            _currentData = CreateDefaultData();
        }
    }

    private void SaveToDisk()
    {
        try
        {
            string directory = Path.GetDirectoryName(SavePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string json = JsonUtility.ToJson(_currentData, true);
            File.WriteAllText(SavePath, json);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[SaveGameService] 저장 데이터를 기록하지 못했습니다. path={SavePath} error={ex.Message}");
        }
    }

    private static SaveGameData CreateDefaultData()
    {
        return new SaveGameData
        {
            version = CurrentVersion,
            sceneName = TitleFlowConstants.GameSceneName,
            savedAtUtc = string.Empty,
            hasStartedGame = false,
            questProgress = QuestProgressSaveData.CreateDefault()
        };
    }

    private static string NormalizeOrDefaultSceneName(string sceneName)
    {
        string normalizedSceneName = TitleFlowConstants.NormalizeSceneName(sceneName);
        return string.IsNullOrWhiteSpace(normalizedSceneName) ? TitleFlowConstants.GameSceneName : normalizedSceneName;
    }
}
