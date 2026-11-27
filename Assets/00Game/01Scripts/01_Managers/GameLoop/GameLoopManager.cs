using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameLoopManager : Singleton<GameLoopManager>
{
    private const int StartupCheckpointSpawnRetryFrames = 30;

    [Header("李몄“")]
    [SerializeField] private PlayerController player;
    [SerializeField] private UIYouDied youDiedUI;

    [Header("?щ쭩 ?곗텧")]
    [SerializeField] private float youDiedHoldTime = 2.2f;

    private PlayerRespawnFlow _respawnFlow;
    private bool _startupCheckpointSpawnApplied;
    private bool _startupCheckpointSpawnResolved;

    public bool HasResolvedStartupCheckpointSpawn => _startupCheckpointSpawnResolved;

    protected override void Awake()
    {
        DetachFromSceneParentIfNeeded();
        base.Awake();
    }

    private void OnEnable()
    {
        GameLoopEvents.PlayerDied += HandlePlayerDiedEvent;
    }

    private void OnDisable()
    {
        GameLoopEvents.PlayerDied -= HandlePlayerDiedEvent;
    }

    private void Start()
    {
        if (player == null)
        {
            player = FindObjectOfType<PlayerController>();
        }

        ValidateSceneReferences(true);

        if (_respawnFlow == null)
        {
            _respawnFlow = new PlayerRespawnFlow(this, player, youDiedUI, youDiedHoldTime);
        }

        _respawnFlow.SetPlayer(player);
        _respawnFlow.HideOverlayImmediate();

        if (!_startupCheckpointSpawnApplied)
        {
            StartCoroutine(ApplyStartupCheckpointSpawn());
        }
    }

    [ContextMenu("Validate Scene References")]
    private void ValidateSceneReferencesContext()
    {
        ValidateSceneReferences(false);
    }

    private void HandlePlayerDiedEvent(PlayerController deadPlayer)
    {
        if (deadPlayer != null)
        {
            player = deadPlayer;
        }

        if (_respawnFlow == null)
        {
            _respawnFlow = new PlayerRespawnFlow(this, player, youDiedUI, youDiedHoldTime);
        }

        _respawnFlow.HandlePlayerDied(deadPlayer);
    }

    private void ValidateSceneReferences(bool logOnlyOnce)
    {
        SceneReferenceValidator.Entry[] entries =
        {
            SceneReferenceValidator.Required(nameof(youDiedUI), youDiedUI, "YOU DIED UI"),
            SceneReferenceValidator.Recommended("UIPlayerStatusBars(scene)", FindFirstObjectByType<UIPlayerStatusBars>(FindObjectsInactive.Include), "GameLoopUICanvas 湲곗?蹂??뺤씤"),
            SceneReferenceValidator.Recommended("UIEstusFlask(scene)", FindFirstObjectByType<UIEstusFlask>(FindObjectsInactive.Include), "GameLoopUICanvas 湲곗?蹂??뺤씤"),
            SceneReferenceValidator.Recommended("UIBossHealthBar(scene)", FindFirstObjectByType<UIBossHealthBar>(FindObjectsInactive.Include), "GameLoopUICanvas 湲곗?蹂??뺤씤"),
            SceneReferenceValidator.RuntimeBound(nameof(player), player, "Start() ?먮뒗 PlayerDied ?대깽?몄뿉???뺣낫")
        };

        if (logOnlyOnce)
        {
            SceneReferenceValidator.ValidateOnce(this, nameof(GameLoopManager), entries);
            return;
        }

        SceneReferenceValidator.Validate(this, nameof(GameLoopManager), entries);
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

    private IEnumerator ApplyStartupCheckpointSpawn()
    {
        _startupCheckpointSpawnApplied = true;

        if (QuestProgressionManager.Instance == null ||
            string.IsNullOrWhiteSpace(QuestProgressionManager.Instance.CurrentCheckpointId))
        {
            _startupCheckpointSpawnResolved = true;
            yield break;
        }

        string savedCheckpointSceneName = QuestProgressionManager.Instance.CurrentCheckpointSceneName;
        string activeSceneName = SceneManager.GetActiveScene().name;
        if (!string.IsNullOrWhiteSpace(savedCheckpointSceneName) &&
            !string.Equals(savedCheckpointSceneName, activeSceneName, System.StringComparison.Ordinal))
        {
            _startupCheckpointSpawnResolved = true;
            yield break;
        }

        for (int frame = 0; frame < StartupCheckpointSpawnRetryFrames; frame++)
        {
            if (player == null)
            {
                player = FindObjectOfType<PlayerController>();
            }

            if (player != null &&
                CheckpointManager.Instance != null &&
                CheckpointManager.Instance.RespawnPlayer(player))
            {
                _startupCheckpointSpawnResolved = true;
                yield break;
            }

            yield return null;
        }

        _startupCheckpointSpawnResolved = true;
    }
}
