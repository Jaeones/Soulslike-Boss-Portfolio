#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;

public sealed class BossBattleDebugPanel : MonoBehaviour
{
    private const string RuntimeObjectName = "BossBattleDebugPanel";
    private const KeyCode ToggleKey = KeyCode.F9;

    private static BossBattleDebugPanel _instance;

    private Rect _windowRect = new Rect(460f, 24f, 360f, 430f);
    private Vector2 _scrollPosition;
    private bool _visible;
    private string _statusMessage = "Idle";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (_instance != null)
        {
            return;
        }

        GameObject host = new GameObject(RuntimeObjectName);
        _instance = host.AddComponent<BossBattleDebugPanel>();
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
    }

    private void Update()
    {
        if (Input.GetKeyDown(ToggleKey))
        {
            _visible = !_visible;
        }
    }

    private void OnGUI()
    {
        if (!_visible)
        {
            return;
        }

        GUI.depth = -1001;
        _windowRect = GUI.Window(GetInstanceID(), _windowRect, DrawWindow, "Boss Battle Debug");
    }

    private void DrawWindow(int windowId)
    {
        BossController boss = FindFirstObjectByType<BossController>(FindObjectsInactive.Exclude);
        PlayerController player = FindFirstObjectByType<PlayerController>(FindObjectsInactive.Exclude);

        _scrollPosition = GUILayout.BeginScrollView(_scrollPosition, false, true);

        GUILayout.Label($"Toggle: {ToggleKey}");
        GUILayout.Space(4f);

        if (boss == null)
        {
            GUILayout.Label("Boss: not found");
            GUILayout.Label("Status: place an active BossController in the scene.");
            GUILayout.EndScrollView();
            GUI.DragWindow(new Rect(0f, 0f, 10000f, 24f));
            return;
        }

        DrawBossSection(boss);
        GUILayout.Space(10f);
        DrawBossControlSection(boss);
        GUILayout.Space(10f);
        DrawPlayerSection(player);
        GUILayout.Space(10f);
        GUILayout.Label($"Status: {_statusMessage}");

        GUILayout.EndScrollView();
        GUI.DragWindow(new Rect(0f, 0f, 10000f, 24f));
    }

    private void DrawBossSection(BossController boss)
    {
        float hpRatio = boss.MaxHp > 0f ? boss.CurrentHp / boss.MaxHp : 0f;

        GUILayout.Label($"Boss State: {boss.currentState}");
        GUILayout.Label($"Phase: {boss.currentPhase}");
        GUILayout.Label($"Intro Started: {boss.isIntroStarted}");
        GUILayout.Label($"Transitioning: {boss.isPhaseTransitioning}");
        GUILayout.Label($"Phase2 Pending: {boss.HasPendingPhase2Transition}");
        GUILayout.Label($"Critical Running: {boss.IsCriticalStrikeExecuting}");
        GUILayout.Label($"Critical Consumed: {boss.HasConsumedCriticalStrikeThisGroggy}");
        GUILayout.Label($"HP: {boss.CurrentHp:0}/{boss.MaxHp:0} ({hpRatio:P0})");
        GUILayout.Label($"Groggy: {boss.CurrentGroggyGauge:0.0}/{boss.maxGroggyGauge:0.0}");
        GUILayout.Label($"Poise: {boss.currentPoise:0.0}/{boss.maxPoise:0.0}");
        GUILayout.Label($"Stamina: {boss.stamina:0.0}/{boss.maxStamina:0.0}");
        GUILayout.Label($"Current Attack: {ResolveAttackLabel(boss)}");
        GUILayout.Label($"Next Attack: {ResolveQueuedAttackLabel(boss)}");
        GUILayout.Label($"Queued Count: {boss.GetAttackQueueCount()}");
    }

    private void DrawBossControlSection(BossController boss)
    {
        GUILayout.Label("Controls");

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Enter Combat"))
        {
            boss.DebugEnsureCombatContext();
            SetStatus("Entered combat context.");
        }

        if (GUILayout.Button("Phase 1"))
        {
            boss.DebugForcePhaseState(1);
            SetStatus("Forced phase 1.");
        }

        if (GUILayout.Button("Phase 2"))
        {
            boss.DebugForcePhaseState(2);
            SetStatus("Forced phase 2.");
        }
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Groggy"))
        {
            boss.DebugForceGroggyState();
            SetStatus("Forced groggy.");
        }

        if (GUILayout.Button("Skill G"))
        {
            boss.DebugForceAttackPattern("Skill_G", 2);
            SetStatus("Forced Skill_G.");
        }

        if (GUILayout.Button("Skill K"))
        {
            boss.DebugForceAttackPattern("Skill_K");
            SetStatus("Forced Skill_K.");
        }
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Combo 3"))
        {
            boss.DebugForceAttackSequencePattern(new[] { "Attack_3Combo_1", "Attack_3Combo_2", "Attack_3Combo_3" });
            SetStatus("Forced Attack_3Combo sequence.");
        }

        if (GUILayout.Button("Combo 4"))
        {
            boss.DebugForceAttackSequencePattern(new[] { "Attack_4Combo_1", "Attack_4Combo_2", "Attack_4Combo_3", "Attack_4Combo_4" }, 2);
            SetStatus("Forced Attack_4Combo sequence.");
        }
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("HP 100%"))
        {
            boss.DebugSetHpRatio(1.0f);
            SetStatus("Boss HP set to 100%.");
        }

        if (GUILayout.Button("HP 60%"))
        {
            boss.DebugSetHpRatio(0.6f);
            SetStatus("Boss HP set to 60%.");
        }

        if (GUILayout.Button("HP 50%"))
        {
            boss.DebugSetHpRatio(0.5f);
            SetStatus("Boss HP set to 50%.");
        }
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("HP 49%"))
        {
            boss.DebugSetHpRatio(0.49f);
            SetStatus("Boss HP set to 49%.");
        }

        if (GUILayout.Button("HP 25%"))
        {
            boss.DebugSetHpRatio(0.25f);
            SetStatus("Boss HP set to 25%.");
        }

        if (GUILayout.Button("Pending Test"))
        {
            boss.DebugForcePhaseState(1);
            boss.DebugForceGroggyState();
            boss.DebugSetHpRatio(0.49f);
            SetStatus("Prepared pending phase transition test.");
        }
        GUILayout.EndHorizontal();
    }

    private void DrawPlayerSection(PlayerController player)
    {
        if (player == null)
        {
            GUILayout.Label("Player: not found");
            return;
        }

        string stateName = player.StateMachine?.CurrentState != null
            ? player.StateMachine.CurrentState.GetType().Name
            : "(none)";

        GUILayout.Label("Player");
        GUILayout.Label($"State: {stateName}");
        GUILayout.Label($"HP: {player.CurrentHp:0}/{player.MaxHp:0}");
        GUILayout.Label($"Stamina: {player.CurrentStamina:0.0}/{player.MaxStamina:0.0}");
        GUILayout.Label($"Battle Stance: {player.IsBattleStance}");
        GUILayout.Label($"Parry Active: {player.IsParryWindowActive}");
    }

    private static string ResolveAttackLabel(BossController boss)
    {
        if (boss.CurrentAttack == null)
        {
            return "(none)";
        }

        if (!string.IsNullOrWhiteSpace(boss.CurrentAttack.attackId))
        {
            return boss.CurrentAttack.attackId;
        }

        return string.IsNullOrWhiteSpace(boss.CurrentAttack.attackName)
            ? "(unnamed)"
            : boss.CurrentAttack.attackName;
    }

    private static string ResolveQueuedAttackLabel(BossController boss)
    {
        string nextAttack = boss.PeekNextAttackInQueue();
        return string.IsNullOrWhiteSpace(nextAttack)
            ? "(none)"
            : nextAttack;
    }

    private void SetStatus(string message)
    {
        _statusMessage = message;
    }
}
#endif
