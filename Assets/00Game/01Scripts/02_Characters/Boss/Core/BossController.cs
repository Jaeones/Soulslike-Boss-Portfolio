using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;
using Cinemachine;
using TMPro;

public class BossController : EnemyBase, IDamageable
{
    private const string CutSceneCanvasName = "CutSceneCanvas";
    private const string CutSceneTextName = "CutSceneText";
    private const string CriticalStabPivotName = "StabPivot";

    private enum ZeroHpPhase2CutsceneShotId
    {
        KillConfirm,
        FallenConfession,
        ReviveRise,
        FinalStand
    }

    [System.Serializable]
    private struct ZeroHpPhase2CutsceneShotSettings
    {
        public Vector3 offset;
        public Vector3 aimOffset;
        public Vector3 eulerOffset;
        public float fieldOfView;
        public float blendTime;
        public CinemachineBlendDefinition.Style blendStyle;
    }

    private struct ZeroHpPhase2CutsceneCameraState
    {
        public Vector3 localPosition;
        public Quaternion localRotation;
        public float fieldOfView;
    }

    [Header("--- Components ---")]
    public BossHitbox bossHitbox;
    public NavMeshAgent Agent => agent;
    public Animator Anim => anim;
    public float IntroExhaustedAnimSpeed => introExhaustedAnimSpeed;

    [Header("--- Combat Data ---")]
    [SerializeField] private BossAttackLibrary attackLibrary;

    [Header("--- Zone & Intro Control (Auto/G Key) ---")]
    public GameObject zoneTriggerObj;
    public Transform introTriggerPoint;
    public float detectionDistance = 15.0f;
    public float triggerDistance = 5.0f;
    public CinemachineBlendListCamera introCam;
    public GameObject bossNameUI;
    public bool isIntroStarted = false;
    public float cameraBlendTime = 1.5f;
    public float introWaitTime = 3.0f;
    [SerializeField] [Range(0.1f, 2f)] private float introExhaustedAnimSpeed = 0.6f;
    private bool _isExternalEntranceControlActive;
    public bool IsExternalEntranceControlActive => _isExternalEntranceControlActive;

    [Header("--- Movement & Recovery ---")]
    public float attackStoppingDistance = 3.0f;
    public float moveSpeed = 6.0f;
    public float rotationSpeed = 20.0f;
    public float postAttackWaitTime = 2.5f;

    [Header("--- Root Motion Navigation ---")]
    [SerializeField] private bool useFullRootMotion = true;
    [SerializeField] private bool useRootMotionForMoveState = true;
    [SerializeField] private bool useRootMotionForActionState = true;
    [SerializeField] private bool useRootMotionForReactionStates = true;
    [SerializeField] private float rootMotionSteeringTurnMultiplier = 1.1f;
    [SerializeField] private LayerMask rootMotionGroundSnapMask = ~0;
    [SerializeField] private float rootMotionGroundSnapRayHeight = 1.5f;
    [SerializeField] private float rootMotionGroundSnapMaxDistance = 3.5f;
    [SerializeField] private float rootMotionGroundSnapOffset = 0.02f;

    [Header("--- Soulslike System ---")]
    public float stamina = 100f;
    public float maxStamina = 100f;
    public float staminaRegen = 20f;
    public float attackCost = 30f;
    public bool IsExhausted => stamina < 25f;

    [Header("--- Combat Stats ---")]
    public int currentPhase = 1;
    public float phase2Threshold = 0.5f;
    [SerializeField] private float phaseTransitionGroggyDuration = 1.2f;
    [SerializeField] private bool useZeroHpPhase2Transition = true;
    [SerializeField] private float phase1EncounterMaxHp = 2800f;
    [SerializeField] private float phase2EncounterMaxHp = 2800f;
    [SerializeField] private float zeroHpPhase2DownLoopDuration = 1.8f;
    [SerializeField] [Range(0.05f, 1f)] private float zeroHpPhase2RefillNormalized = 0.32f;
    [SerializeField] [Range(0.05f, 1f)] private float zeroHpPhase2RecoverNormalized = 0.73f;
    [SerializeField] private float zeroHpPhase2HitStopDuration = 0.16f;
    [SerializeField] [Range(0f, 1f)] private float zeroHpPhase2HitStopTimeScale = 0.05f;
    [SerializeField] private float zeroHpPhase2WakeShakeIntensity = 2.2f;
    [SerializeField] private float zeroHpPhase2WakeShakeDuration = 0.45f;
    [SerializeField] private float zeroHpPhase2PostRecoverDelay = 0f;
    [SerializeField] private float zeroHpPhase2CameraReturnDelay = 0.4f;
    [SerializeField] private float zeroHpPhase2CombatResumeDelay = 2.0f;
    [SerializeField] private float zeroHpPhase2PreCutsceneDelay = 1f;
    [SerializeField] private float zeroHpPhase2HudShowDelay = 1f;
    [SerializeField] private bool useEntranceCameraForZeroHpPhase2 = true;
    [SerializeField] private int zeroHpPhase2CameraPriority = 30;
    [SerializeField] private bool forceGroundSnapDuringZeroHpPhase2 = true;
    [SerializeField] private bool showZeroHpPhase2ShotPreviewGizmos = true;
    [SerializeField] [Range(0f, 1f)] private float zeroHpPhase2BgmDuckScale = 0.12f;
    [SerializeField] private float zeroHpPhase2BgmDuckDuration = 0.2f;
    [SerializeField] private float zeroHpPhase2BgmRestoreDuration = 0.5f;
    [SerializeField] private float zeroHpPhase2DownPulseInterval = 0.55f;
    [SerializeField] private float zeroHpPhase2DownPulseShakeIntensity = 0.2f;
    [SerializeField] private float zeroHpPhase2DownPulseFlashDuration = 0.08f;
    [SerializeField] private Color zeroHpPhase2DownPulseFlashColor = new Color(1f, 0.55f, 0.55f, 1f);
    [Header("Phase 2 Dialogue Cutscene")]
    [SerializeField] private bool useDialogueCutsceneForZeroHpPhase2 = true;
    [SerializeField] private string zeroHpPhase2DeathAnimation = "Phase1Die";
    [SerializeField] private string zeroHpPhase2ReviveAnimation = "Damage_Back_Down_StandUp";
    [SerializeField] [TextArea(2, 5)] private string[] zeroHpPhase2DialogueLines =
    {
        "……이제는 보이는가.",
        "빛이라 믿었던 것은 오래전에 식었고, 이름만 남았다.",
        "그날 태우지 못한 죄가, 아직도 내 피와 숨을 빌려 이 땅에 남아 있구나.",
        "오너라. 끝내 이름을 잃지 못한 것을, 여기서 멎게 하여라."
    };
    [SerializeField] private int zeroHpPhase2ReviveTriggerLineIndex = 2;
    [SerializeField] private float zeroHpPhase2DialogueStartDelay = 0.35f;
    [SerializeField] private float zeroHpPhase2DialogueAutoAdvanceDelay = 1.5f;
    [SerializeField] private Canvas zeroHpPhase2CutSceneCanvas;
    [SerializeField] private TMP_Text zeroHpPhase2CutSceneText;
    [SerializeField] private float zeroHpPhase2DialogueSecondsPerCharacter = 0.085f;
    [SerializeField] private float zeroHpPhase2DialogueMinLineDuration = 1.7f;
    [SerializeField] private float zeroHpPhase2DialogueMaxLineDuration = 4.5f;
    [Header("Phase 2 Dialogue Camera")]
    [SerializeField] private ZeroHpPhase2CutsceneShotSettings zeroHpPhase2KillConfirmShot = new ZeroHpPhase2CutsceneShotSettings
    {
        offset = new Vector3(0.9f, 0.45f, -4.6f),
        aimOffset = new Vector3(0.15f, 0.2f, 0.15f),
        eulerOffset = Vector3.zero,
        fieldOfView = 40f,
        blendTime = 0.15f,
        blendStyle = CinemachineBlendDefinition.Style.Cut
    };
    [SerializeField] private ZeroHpPhase2CutsceneShotSettings zeroHpPhase2FallenConfessionShot = new ZeroHpPhase2CutsceneShotSettings
    {
        offset = new Vector3(1.85f, 0.05f, -1.95f),
        aimOffset = new Vector3(0.05f, 0.1f, 0.2f),
        eulerOffset = new Vector3(-2f, -3f, 0f),
        fieldOfView = 32f,
        blendTime = 0.85f,
        blendStyle = CinemachineBlendDefinition.Style.EaseInOut
    };
    [SerializeField] private ZeroHpPhase2CutsceneShotSettings zeroHpPhase2ReviveRiseShot = new ZeroHpPhase2CutsceneShotSettings
    {
        offset = new Vector3(0.2f, -0.45f, -2.25f),
        aimOffset = new Vector3(0.05f, 0.2f, 0.3f),
        eulerOffset = new Vector3(-4f, 0f, 0f),
        fieldOfView = 29f,
        blendTime = 0.65f,
        blendStyle = CinemachineBlendDefinition.Style.EaseInOut
    };
    [SerializeField] private ZeroHpPhase2CutsceneShotSettings zeroHpPhase2FinalStandShot = new ZeroHpPhase2CutsceneShotSettings
    {
        offset = new Vector3(0f, 0.7f, -5.2f),
        aimOffset = new Vector3(0f, 0.25f, 0.35f),
        eulerOffset = new Vector3(-1f, 0f, 0f),
        fieldOfView = 36f,
        blendTime = 0.55f,
        blendStyle = CinemachineBlendDefinition.Style.EaseInOut
    };
    [Header("Phase 2 Revive FX")]
    [SerializeField] private GameObject phase2PreWakeCorePrefab;
    [SerializeField] private GameObject phase2GroundBurstPrefab;
    [SerializeField] private GameObject phase2BodyBurstPrefab;
    [SerializeField] private GameObject phase2WeaponIgnitePrefab;
    [SerializeField] private GameObject phase2ResidualAuraPrefab;
    [SerializeField] private GameObject phase2AmbientMotesPrefab;
    public float maxPoise = 100f;
    public float currentPoise;
    public float poiseRecoveryPerSecond = 18f;
    [SerializeField] private float lightHitReactDuration = 0.32f;
    [SerializeField] private float heavyHitReactDuration = 0.5f;
    [SerializeField] private float parryHitReactDuration = 0.42f;
    [SerializeField] [Range(0f, 1f)] private float poiseResetRatioAfterBreak = 0.35f;
    [SerializeField] private float poiseBreakGroggyBonus = 18f;
    [SerializeField] private float poiseBreakGroggyBonusMultiplier = 0.5f;
    [SerializeField] private float parryGroggyBonus = 0f;
    [SerializeField] [Range(0f, 1f)] private float parryGroggyBonusMultiplier = 0.3f;
    [SerializeField] private string parryHitReactAnimation = "Parryed";
    [SerializeField] private string lightHitReactAnimation = "Hit";
    [SerializeField] private string heavyHitReactAnimation = "Hit";
    public bool isSuperArmor = false;
    public float baseAttackSpeed = 1.0f;

    [Header("--- Weapon Visuals ---")]
    public GameObject backWeapon;
    public GameObject handWeapon;

    public bool isPhaseTransitioning = false;
    public bool isWaitingAfterAttack = false;
    private Queue<string> attackQueue = new Queue<string>();
    public CombatAttackDefinition CurrentAttack { get; private set; }

    [Header("--- States ---")]
    public BossIdleState idleState;
    public BossMoveState moveState;
    public BossActionState actionState;
    public BossHitReactState hitReactState;
    public BossGroggyState groggyState;
    public BossDeadState deadState;
    private BossState currentLogicState;
    private bool _stateTransitionLocked;
    private bool _hasPendingHitReaction;
    private float _pendingHitReactionDuration;
    private string _pendingHitReactionAnimationName;

    public CinemachineImpulseSource impulseSource;

    [Header("Boss SFX Settings")]
    [SerializeField] private AudioSource bossSfxAudioSource;
    [SerializeField] private AudioClip[] swingSfxClips;
    [SerializeField] private AudioClip[] groundImpactSfxClips;
    [SerializeField] private AudioClip[] incomingHitSfxClips;
    [SerializeField] private AudioClip[] footstepSfxClips;
    [SerializeField] private AudioClip[] parriedSfxClips;
    [SerializeField] [Range(0f, 1f)] private float swingSfxVolume = 0.9f;
    [SerializeField] [Range(0f, 1f)] private float groundImpactSfxVolume = 1f;
    [SerializeField] [Range(0f, 1f)] private float incomingHitSfxVolume = 0.95f;
    [SerializeField] [Range(0f, 1f)] private float footstepSfxVolume = 0.88f;
    [SerializeField] [Range(0f, 1f)] private float parriedSfxVolume = 1f;
    [SerializeField] private float footstepSfxMinInterval = 0.1f;

    [Header("Hit Effect Settings")]
    [SerializeField] private GameObject bossHitBloodEffectPrefab;

    [Header("Attack Trajectory Settings")]
    [SerializeField] private bool enableAttackTrajectory = true;
    [SerializeField] private float lightTrajectoryLifetime = 0.09f;
    [SerializeField] private float heavyTrajectoryLifetime = 0.12f;
    [SerializeField] private float lightTrajectoryWidthScale = 0.18f;
    [SerializeField] private float heavyTrajectoryWidthScale = 0.24f;
    [SerializeField] private float minTrajectoryWidth = 0.5f;
    [SerializeField] private float maxTrajectoryWidth = 3.0f;
    [SerializeField] private float trajectorySampleMinDistance = 0.04f;
    [SerializeField] private int trajectoryMaxSamples = 32;
    [SerializeField] private bool useSingleSidedTrajectory = true;
    [SerializeField] private Material attackTrajectoryMaterial;
    [SerializeField] private Color phase1LightTrajectoryColor = new Color(0.88f, 0.9f, 0.93f, 0.9f);
    [SerializeField] private Color phase1HeavyTrajectoryColor = new Color(0.97f, 0.97f, 0.98f, 0.96f);
    [SerializeField] private Color lightTrajectoryColor = new Color(0.72f, 0.84f, 1f, 0.82f);
    [SerializeField] private Color heavyTrajectoryColor = new Color(0.95f, 0.98f, 1f, 0.92f);
    [SerializeField] private bool enableAttackTrajectoryShell = true;
    [SerializeField] [Min(1f)] private float lightTrajectoryShellWidthMultiplier = 1.55f;
    [SerializeField] [Min(1f)] private float heavyTrajectoryShellWidthMultiplier = 1.8f;
    [SerializeField] [Min(1f)] private float lightTrajectoryShellLifetimeMultiplier = 1.25f;
    [SerializeField] [Min(1f)] private float heavyTrajectoryShellLifetimeMultiplier = 1.35f;
    [SerializeField] [Min(0.5f)] private float maxTrajectoryShellWidth = 5f;
    [SerializeField] private Color phase1LightTrajectoryShellColor = new Color(0.79f, 0.81f, 0.85f, 0.34f);
    [SerializeField] private Color phase1HeavyTrajectoryShellColor = new Color(0.9f, 0.92f, 0.95f, 0.46f);
    [SerializeField] private Color lightTrajectoryShellColor = new Color(0.28f, 0.97f, 1f, 0.42f);
    [SerializeField] private Color heavyTrajectoryShellColor = new Color(0.4f, 1f, 1f, 0.58f);

    [Header("Attack Settings")]
    public float skillCooldown = 5f;     // 스킬 재사용 대기시간
    private float lastSkillTime = -999f; // 마지막 스킬 사용 시간
    [Range(0, 100)] public int skillChance = 20; // 콤보 대신 스킬이 나갈 확률 (20%)

    [Header("Phase 1 Validation")]
    [SerializeField] private bool usePhase1ValidationPatternSet = true;
    [SerializeField] private bool disablePhase2DuringPhase1Validation = true;
    [SerializeField] private string phase1ApproachPattern = "Dash";
    [SerializeField] private string phase1QuickPattern = "Attack_3Combo_1";
    [SerializeField] private string phase1HeavyPattern = "Skill_J";
    [SerializeField] private float phase1ApproachDistanceThreshold = 4.8f;

    [Header("Phase 2 Validation")]
    [SerializeField] private bool usePhase2ValidationPatternSet = true;
    [SerializeField] private string phase2ApproachPattern = "Skill_H";
    [SerializeField] private string phase2QuickPattern = "Attack_4Combo_1";
    [SerializeField] private string phase2HeavyPattern = "Skill_G";
    [SerializeField] private float phase2ApproachDistanceThreshold = 6.2f;
    [SerializeField] private float phase2HealPunishDistance = 7.0f;
    [SerializeField] private float phase2PostAttackTempoMultiplier = 0.72f;
    private string _lastSelectedPatternId;
    private string _lastExecutedPatternId;
    private int _samePatternRepeatCount;

    [Header("Critical Strike")]
    [SerializeField] private string criticalPromptLabel = "치명타";
    [SerializeField] private float criticalInteractionRange = 2.4f;
    [SerializeField] private float criticalFacingAngle = 120f;
    [SerializeField] private float criticalApproachDistance = 1.6f;
    [SerializeField] private float criticalImpactDelay = 0.42f;
    [SerializeField] private float criticalRecoveryDelay = 0.38f;
    [SerializeField] private int criticalStrikeDamage = 450;
    [SerializeField] private float criticalStaminaDamage = 60f;
    [SerializeField] private float criticalGroggyDamage = 120f;
    [SerializeField] private float criticalPoiseDamage = 120f;
    [SerializeField] private float criticalCameraShakeIntensity = 0.2f;
    [SerializeField] private float criticalHitStopDuration = 0.12f;
    [SerializeField] [Range(0.1f, 1f)] private float criticalPresentationAnimSpeed = 0.5f;
    [SerializeField] private int criticalHitReactionStartFrame = 8;
    [SerializeField] private float criticalHitReactionSourceFps = 30f;
    [SerializeField] private string criticalHitReactionAnimation = "GroggyDamage";
    [SerializeField] private string criticalKnockDownLoopAnimation = "KnockDown_Front_Loop";
    [SerializeField] private string criticalGetUpAnimation = "KnockDown_Front_BW_Rolling_StandUp";
    [SerializeField] [Range(0.1f, 1f)] private float criticalGetUpIdleStartNormalized = 0.73f;
    [SerializeField] private float criticalReactionCrossFade = 0.03f;
    [SerializeField] private float criticalReactionTimeout = 6f;

    [Header("Groggy Animations")]
    [SerializeField] private string groggyAnimation = "Groggy";
    [SerializeField] private string phaseTransitionGroggyAnimation = "Groggy";
    private Coroutine _criticalStrikeRoutine;
    private Coroutine _criticalReactionRoutine;
    private bool _isCriticalPromptVisible;
    private bool _criticalStrikeConsumedThisGroggy;
    private float _combatResumeBlockedUntil;
    private PlayerController _criticalStrikePlayer;
    private bool _phase2TransitionPending;
    private bool _debugLockPhase1UntilReleased;
    private BossAttackTrajectoryRuntime _attackTrajectory;
    private bool _hasPlayedCurrentAttackSwingSfx;
    private bool _hasPlayedCurrentAttackGroundImpactSfx;
    private float _nextBossFootstepPlayableTime;
    private bool _hasTriggeredZeroHpPhase2Transition;
    private bool _isZeroHpPhase2TransitionActive;
    private bool _isZeroHpPhase2GroundClampActive;
    private GameObject _phase2WeaponIgniteFxInstance;
    private PlayerController _zeroHpPhase2LockedPlayer;
    private CinemachineVirtualCamera[] _zeroHpPhase2CutsceneShotCameras;
    private ZeroHpPhase2CutsceneCameraState[] _zeroHpPhase2CutsceneCameraStates;
    private bool[] _cachedZeroHpPhase2ShotBodyComponentEnabled;
    private bool[] _cachedZeroHpPhase2ShotAimComponentEnabled;
    private CinemachineBlendListCamera.Instruction[] _cachedZeroHpPhase2IntroCamInstructions;
    private bool _cachedZeroHpPhase2IntroCamLoop;
    private Transform _cachedZeroHpPhase2IntroCamLookAt;
    private Transform _cachedZeroHpPhase2IntroCamFollow;
    private bool _hasCachedZeroHpPhase2CutsceneRig;
    private int _zeroHpPhase2CutsceneCameraCursor = -1;
    private ICinemachineCamera _zeroHpPhase2LastCutsceneCamera;

    [Header("Debug Controls")]
    [SerializeField] private bool enableDebugStateHotkeys = true;
    [SerializeField] private KeyCode debugForcePhase1Key = KeyCode.F6;
    [SerializeField] private KeyCode debugForcePhase2Key = KeyCode.F7;
    [SerializeField] private KeyCode debugForceGroggyKey = KeyCode.F8;
    [SerializeField] private KeyCode debugForceSkillGKey = KeyCode.G;
    [SerializeField] private KeyCode debugForceSkillKKey = KeyCode.K;

    private Vector3 _spawnPosition;
    private Quaternion _spawnRotation;
    private int _initialPhase;
    private bool _rootMotionMovementEnabled;
    private bool _rootMotionRotationEnabled;

    protected override void Awake()
    {
        base.Awake();
        _spawnPosition = transform.position;
        _spawnRotation = transform.rotation;
        _initialPhase = currentPhase;
        ApplyEncounterHealthForPhase(currentPhase, true, false);
        ResolveCombatReferences();
        LoadAttackLibraryIfNeeded();
        idleState = new BossIdleState(this);
        moveState = new BossMoveState(this);
        actionState = new BossActionState(this);
        hitReactState = new BossHitReactState(this);
        groggyState = new BossGroggyState(this);
        deadState = new BossDeadState(this);

        currentPoise = maxPoise;

        if (agent != null)
        {
            agent.stoppingDistance = attackStoppingDistance;
            agent.updateRotation = false;
            agent.updatePosition = !UsesFullRootMotion;
            agent.acceleration = 35f;
            agent.speed = moveSpeed;
        }

        bossHitbox?.Initialize(this);
        ResolveBossSfxAudioSource();
        _attackTrajectory = new BossAttackTrajectoryRuntime(this);
    }

    protected override bool ShouldTreatZeroHpAsDead()
    {
        return !_isZeroHpPhase2TransitionActive && !CanTriggerZeroHpPhase2Transition();
    }

    private bool CanTriggerZeroHpPhase2Transition()
    {
        return useZeroHpPhase2Transition &&
               currentPhase == 1 &&
               !_hasTriggeredZeroHpPhase2Transition;
    }

    private float ResolveEncounterMaxHp(int phase)
    {
        float configuredMaxHp = phase >= 2 ? phase2EncounterMaxHp : phase1EncounterMaxHp;
        if (configuredMaxHp <= 0f)
        {
            configuredMaxHp = hp > 0f ? hp : 2800f;
        }

        return Mathf.Max(1f, configuredMaxHp);
    }

    private void ApplyEncounterHealthForPhase(int phase, bool restoreToFull, bool notify)
    {
        float maxHp = ResolveEncounterMaxHp(phase);
        float currentHp = restoreToFull ? maxHp : Mathf.Clamp(hp, 0f, maxHp);
        SetHealthValues(currentHp, maxHp, notify);
    }

    public bool IsCombatResumeDelayActive => Time.time < _combatResumeBlockedUntil;

    private void BeginCombatResumeDelay(float duration)
    {
        if (duration <= 0f)
        {
            return;
        }

        _combatResumeBlockedUntil = Mathf.Max(_combatResumeBlockedUntil, Time.time + duration);
    }

    private IEnumerator Co_WaitForZeroHpPhase2CameraReturn()
    {
        if (useEntranceCameraForZeroHpPhase2 && introCam != null)
        {
            DeactivateEntranceCamera();

            if (zeroHpPhase2CameraReturnDelay > 0f)
            {
                yield return new WaitForSeconds(zeroHpPhase2CameraReturnDelay);
            }
        }
    }

    private BossZoneTrigger ResolveBossZoneTrigger()
    {
        if (zoneTriggerObj == null)
        {
            return null;
        }

        BossZoneTrigger zoneTrigger = zoneTriggerObj.GetComponent<BossZoneTrigger>();
        if (zoneTrigger != null)
        {
            return zoneTrigger;
        }

        return zoneTriggerObj.GetComponentInParent<BossZoneTrigger>();
    }

    private Renderer ResolveBossPresentationRenderer()
    {
        Renderer targetRenderer = GetComponentInChildren<SkinnedMeshRenderer>();
        if (targetRenderer != null)
        {
            return targetRenderer;
        }

        return GetComponentInChildren<Renderer>();
    }

    private void FlashBossPresentation(Renderer targetRenderer, Color flashColor, float duration)
    {
        if (targetRenderer == null || EffectManager.Instance == null)
        {
            return;
        }

        EffectManager.Instance.DoFlashEffect(targetRenderer, flashColor, duration);
    }

    private Transform ResolvePhase2CoreAnchor()
    {
        Transform anchor = FindBossTransform("mixamorig:Spine2");
        if (anchor != null)
        {
            return anchor;
        }

        anchor = FindBossBone(HumanBodyBones.Chest);
        if (anchor != null)
        {
            return anchor;
        }

        anchor = FindBossBone(HumanBodyBones.Spine);
        if (anchor != null)
        {
            return anchor;
        }

        anchor = FindBossTransform("mixamorig:Hips");
        if (anchor != null)
        {
            return anchor;
        }

        return transform;
    }

    private Transform ResolvePhase2WeaponAnchor()
    {
        if (handWeapon != null)
        {
            return handWeapon.transform;
        }

        Transform anchor = FindBossTransform("Weapon_Root");
        if (anchor != null)
        {
            return anchor;
        }

        anchor = FindBossBone(HumanBodyBones.RightHand);
        if (anchor != null)
        {
            return anchor;
        }

        return transform;
    }

    private Transform ResolvePhase2GroundAnchor()
    {
        return transform;
    }

    private Transform FindBossBone(HumanBodyBones bone)
    {
        return anim != null ? anim.GetBoneTransform(bone) : null;
    }

    private Transform FindBossTransform(string nodeName)
    {
        if (string.IsNullOrWhiteSpace(nodeName))
        {
            return null;
        }

        Transform[] children = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i] != null && children[i].name == nodeName)
            {
                return children[i];
            }
        }

        return null;
    }

    private GameObject SpawnPhase2AttachedFx(GameObject prefab, Transform anchor)
    {
        if (prefab == null)
        {
            return null;
        }

        Transform parent = anchor != null ? anchor : transform;
        GameObject instance = Instantiate(prefab, parent);
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;
        instance.transform.localScale = Vector3.one;
        return instance;
    }

    private void SpawnPhase2WorldFx(GameObject prefab, Transform anchor)
    {
        if (prefab == null)
        {
            return;
        }

        Transform spawnAnchor = anchor != null ? anchor : transform;
        Instantiate(prefab, spawnAnchor.position, spawnAnchor.rotation);
    }

    private void PlayPhase2PreWakeCoreFx()
    {
        SpawnPhase2AttachedFx(phase2PreWakeCorePrefab, ResolvePhase2CoreAnchor());
    }

    private void PlayPhase2GroundBurstFx()
    {
        SpawnPhase2WorldFx(phase2GroundBurstPrefab, ResolvePhase2GroundAnchor());
    }

    private void PlayPhase2BodyBurstFx()
    {
        SpawnPhase2AttachedFx(phase2BodyBurstPrefab, ResolvePhase2CoreAnchor());
    }

    private void PlayPhase2ResidualAuraFx()
    {
        SpawnPhase2AttachedFx(phase2ResidualAuraPrefab, ResolvePhase2CoreAnchor());
    }

    private void PlayPhase2AmbientMotesFx()
    {
        SpawnPhase2AttachedFx(phase2AmbientMotesPrefab, ResolvePhase2CoreAnchor());
    }

    private void ResolveZeroHpPhase2CutSceneDialogueReferences()
    {
        if (zeroHpPhase2CutSceneCanvas == null)
        {
            Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < canvases.Length; i++)
            {
                Canvas candidate = canvases[i];
                if (candidate != null && string.Equals(candidate.name, CutSceneCanvasName, System.StringComparison.Ordinal))
                {
                    zeroHpPhase2CutSceneCanvas = candidate;
                    break;
                }
            }
        }

        if (zeroHpPhase2CutSceneText == null)
        {
            if (zeroHpPhase2CutSceneCanvas != null)
            {
                TMP_Text[] texts = zeroHpPhase2CutSceneCanvas.GetComponentsInChildren<TMP_Text>(true);
                for (int i = 0; i < texts.Length; i++)
                {
                    TMP_Text candidate = texts[i];
                    if (candidate != null && string.Equals(candidate.name, CutSceneTextName, System.StringComparison.Ordinal))
                    {
                        zeroHpPhase2CutSceneText = candidate;
                        break;
                    }
                }
            }

            if (zeroHpPhase2CutSceneText == null)
            {
                TMP_Text[] texts = FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                for (int i = 0; i < texts.Length; i++)
                {
                    TMP_Text candidate = texts[i];
                    if (candidate != null && string.Equals(candidate.name, CutSceneTextName, System.StringComparison.Ordinal))
                    {
                        zeroHpPhase2CutSceneText = candidate;
                        break;
                    }
                }
            }
        }
    }

    private void ShowZeroHpPhase2CutSceneDialogue(string line)
    {
        ResolveZeroHpPhase2CutSceneDialogueReferences();

        if (zeroHpPhase2CutSceneCanvas != null && !zeroHpPhase2CutSceneCanvas.gameObject.activeSelf)
        {
            zeroHpPhase2CutSceneCanvas.gameObject.SetActive(true);
        }

        if (zeroHpPhase2CutSceneText != null)
        {
            zeroHpPhase2CutSceneText.text = line ?? string.Empty;
            zeroHpPhase2CutSceneText.maxVisibleCharacters = 0;
        }
    }

    private void HideZeroHpPhase2CutSceneDialogue()
    {
        ResolveZeroHpPhase2CutSceneDialogueReferences();

        if (zeroHpPhase2CutSceneText != null)
        {
            zeroHpPhase2CutSceneText.text = string.Empty;
            zeroHpPhase2CutSceneText.maxVisibleCharacters = int.MaxValue;
        }

        if (zeroHpPhase2CutSceneCanvas != null && zeroHpPhase2CutSceneCanvas.gameObject.activeSelf)
        {
            zeroHpPhase2CutSceneCanvas.gameObject.SetActive(false);
        }
    }

    private float ResolveZeroHpPhase2DialogueLineDuration(string line)
    {
        int visibleCharCount = string.IsNullOrWhiteSpace(line) ? 0 : line.Trim().Length;
        float estimatedDuration = visibleCharCount * Mathf.Max(0.01f, zeroHpPhase2DialogueSecondsPerCharacter);
        return Mathf.Clamp(
            estimatedDuration,
            Mathf.Max(0.1f, zeroHpPhase2DialogueMinLineDuration),
            Mathf.Max(zeroHpPhase2DialogueMinLineDuration, zeroHpPhase2DialogueMaxLineDuration));
    }

    private IEnumerator Co_TypeZeroHpPhase2CutSceneDialogueLine(string line)
    {
        if (zeroHpPhase2CutSceneText == null)
        {
            yield return new WaitForSeconds(
                ResolveZeroHpPhase2DialogueLineDuration(line) +
                Mathf.Max(0f, zeroHpPhase2DialogueAutoAdvanceDelay));
            yield break;
        }

        zeroHpPhase2CutSceneText.text = line ?? string.Empty;
        zeroHpPhase2CutSceneText.ForceMeshUpdate();

        int totalVisibleCharacters = zeroHpPhase2CutSceneText.textInfo.characterCount;
        if (totalVisibleCharacters <= 0)
        {
            zeroHpPhase2CutSceneText.maxVisibleCharacters = int.MaxValue;
            yield return new WaitForSeconds(Mathf.Max(0.1f, zeroHpPhase2DialogueAutoAdvanceDelay));
            yield break;
        }

        float typingDuration = Mathf.Max(0.05f, ResolveZeroHpPhase2DialogueLineDuration(line));
        float elapsed = 0f;
        zeroHpPhase2CutSceneText.maxVisibleCharacters = 0;

        while (elapsed < typingDuration)
        {
            elapsed += Time.deltaTime;
            float normalized = Mathf.Clamp01(elapsed / typingDuration);
            int visibleCharacters = Mathf.Clamp(
                Mathf.FloorToInt(totalVisibleCharacters * normalized),
                0,
                totalVisibleCharacters);
            zeroHpPhase2CutSceneText.maxVisibleCharacters = visibleCharacters;
            yield return null;
        }

        zeroHpPhase2CutSceneText.maxVisibleCharacters = int.MaxValue;

        if (zeroHpPhase2DialogueAutoAdvanceDelay > 0f)
        {
            yield return new WaitForSeconds(zeroHpPhase2DialogueAutoAdvanceDelay);
        }
    }

    private void StartPhase2WeaponIgniteFx()
    {
        StopPhase2WeaponIgniteFx();
        _phase2WeaponIgniteFxInstance = SpawnPhase2AttachedFx(phase2WeaponIgnitePrefab, ResolvePhase2WeaponAnchor());
    }

    private void StopPhase2WeaponIgniteFx()
    {
        if (_phase2WeaponIgniteFxInstance == null)
        {
            return;
        }

        BossPhase2ParticleFx particleFx = _phase2WeaponIgniteFxInstance.GetComponent<BossPhase2ParticleFx>();
        if (particleFx != null)
        {
            particleFx.StopAndDestroy(0.12f);
        }
        else
        {
            Destroy(_phase2WeaponIgniteFxInstance);
        }

        _phase2WeaponIgniteFxInstance = null;
    }

    private void CleanupZeroHpPhase2Fx()
    {
        StopPhase2WeaponIgniteFx();
    }

    private void LockZeroHpPhase2CutscenePlayer(PlayerController targetPlayer)
    {
        if (targetPlayer == null)
        {
            return;
        }

        _zeroHpPhase2LockedPlayer = targetPlayer;
        targetPlayer.BeginExternalInteractionLock();
        targetPlayer.SetGuarding(false);
        targetPlayer.SetVelocity(Vector3.zero);
        targetPlayer.ForceUnlockCameraLockOn();
        targetPlayer.Interaction?.ClearCandidates();
    }

    private void ReleaseZeroHpPhase2CutscenePlayerLock()
    {
        if (_zeroHpPhase2LockedPlayer == null)
        {
            return;
        }

        _zeroHpPhase2LockedPlayer.EndExternalInteractionLock();
        _zeroHpPhase2LockedPlayer.CrossFadeToLocomotion(0.08f);
        _zeroHpPhase2LockedPlayer = null;
    }

    private void SetZeroHpPhase2GroundClampActive(bool active)
    {
        _isZeroHpPhase2GroundClampActive = forceGroundSnapDuringZeroHpPhase2 && active;
        if (_isZeroHpPhase2GroundClampActive)
        {
            ApplyGroundSnapIfNeeded();
        }
    }

    private CinemachineVirtualCamera[] GetZeroHpPhase2CutsceneCameras()
    {
        if (introCam == null)
        {
            return null;
        }

        if (_zeroHpPhase2CutsceneShotCameras != null && _zeroHpPhase2CutsceneShotCameras.Length > 0)
        {
            return _zeroHpPhase2CutsceneShotCameras;
        }

        CinemachineVirtualCamera[] shotCameras = introCam.GetComponentsInChildren<CinemachineVirtualCamera>(true);
        if (shotCameras == null || shotCameras.Length == 0)
        {
            return null;
        }

        System.Array.Sort(shotCameras, (left, right) => left.transform.GetSiblingIndex().CompareTo(right.transform.GetSiblingIndex()));
        _zeroHpPhase2CutsceneShotCameras = shotCameras;
        return _zeroHpPhase2CutsceneShotCameras;
    }

    private void CacheZeroHpPhase2CutsceneRig()
    {
        if (_hasCachedZeroHpPhase2CutsceneRig || introCam == null)
        {
            return;
        }

        CinemachineVirtualCamera[] shotCameras = GetZeroHpPhase2CutsceneCameras();
        if (shotCameras == null || shotCameras.Length == 0)
        {
            return;
        }

        _cachedZeroHpPhase2IntroCamInstructions = introCam.m_Instructions != null
            ? (CinemachineBlendListCamera.Instruction[])introCam.m_Instructions.Clone()
            : new CinemachineBlendListCamera.Instruction[0];
        _cachedZeroHpPhase2IntroCamLoop = introCam.m_Loop;
        _cachedZeroHpPhase2IntroCamLookAt = introCam.m_LookAt;
        _cachedZeroHpPhase2IntroCamFollow = introCam.m_Follow;
        _zeroHpPhase2CutsceneCameraStates = new ZeroHpPhase2CutsceneCameraState[shotCameras.Length];
        _cachedZeroHpPhase2ShotBodyComponentEnabled = new bool[shotCameras.Length];
        _cachedZeroHpPhase2ShotAimComponentEnabled = new bool[shotCameras.Length];
        for (int i = 0; i < shotCameras.Length; i++)
        {
            CinemachineVirtualCamera shotCamera = shotCameras[i];
            _zeroHpPhase2CutsceneCameraStates[i] = new ZeroHpPhase2CutsceneCameraState
            {
                localPosition = shotCamera.transform.localPosition,
                localRotation = shotCamera.transform.localRotation,
                fieldOfView = shotCamera.m_Lens.FieldOfView
            };

            CinemachineComponentBase bodyComponent = shotCamera.GetCinemachineComponent(CinemachineCore.Stage.Body);
            _cachedZeroHpPhase2ShotBodyComponentEnabled[i] = bodyComponent != null && bodyComponent.enabled;
            if (bodyComponent != null)
            {
                bodyComponent.enabled = false;
            }

            CinemachineComponentBase aimComponent = shotCamera.GetCinemachineComponent(CinemachineCore.Stage.Aim);
            _cachedZeroHpPhase2ShotAimComponentEnabled[i] = aimComponent != null && aimComponent.enabled;
            if (aimComponent != null)
            {
                aimComponent.enabled = false;
            }

            shotCamera.PreviousStateIsValid = false;
        }

        introCam.m_LookAt = null;
        introCam.m_Follow = null;
        introCam.PreviousStateIsValid = false;

        _hasCachedZeroHpPhase2CutsceneRig = true;
    }

    private void RestoreZeroHpPhase2CutsceneRig()
    {
        if (!_hasCachedZeroHpPhase2CutsceneRig || introCam == null)
        {
            _zeroHpPhase2CutsceneCameraCursor = -1;
            _zeroHpPhase2LastCutsceneCamera = null;
            return;
        }

        CinemachineVirtualCamera[] shotCameras = GetZeroHpPhase2CutsceneCameras();
        if (shotCameras != null && _zeroHpPhase2CutsceneCameraStates != null)
        {
            int restoreCount = Mathf.Min(shotCameras.Length, _zeroHpPhase2CutsceneCameraStates.Length);
            for (int i = 0; i < restoreCount; i++)
            {
                CinemachineVirtualCamera shotCamera = shotCameras[i];
                ZeroHpPhase2CutsceneCameraState cachedState = _zeroHpPhase2CutsceneCameraStates[i];
                shotCamera.transform.localPosition = cachedState.localPosition;
                shotCamera.transform.localRotation = cachedState.localRotation;
                LensSettings lens = shotCamera.m_Lens;
                lens.FieldOfView = cachedState.fieldOfView;
                shotCamera.m_Lens = lens;

                CinemachineComponentBase bodyComponent = shotCamera.GetCinemachineComponent(CinemachineCore.Stage.Body);
                if (bodyComponent != null && _cachedZeroHpPhase2ShotBodyComponentEnabled != null && i < _cachedZeroHpPhase2ShotBodyComponentEnabled.Length)
                {
                    bodyComponent.enabled = _cachedZeroHpPhase2ShotBodyComponentEnabled[i];
                }

                CinemachineComponentBase aimComponent = shotCamera.GetCinemachineComponent(CinemachineCore.Stage.Aim);
                if (aimComponent != null && _cachedZeroHpPhase2ShotAimComponentEnabled != null && i < _cachedZeroHpPhase2ShotAimComponentEnabled.Length)
                {
                    aimComponent.enabled = _cachedZeroHpPhase2ShotAimComponentEnabled[i];
                }

                shotCamera.PreviousStateIsValid = false;
            }
        }

        introCam.m_LookAt = _cachedZeroHpPhase2IntroCamLookAt;
        introCam.m_Follow = _cachedZeroHpPhase2IntroCamFollow;
        introCam.m_Instructions = _cachedZeroHpPhase2IntroCamInstructions ?? new CinemachineBlendListCamera.Instruction[0];
        introCam.m_Loop = _cachedZeroHpPhase2IntroCamLoop;
        introCam.LiveChild = null;
        introCam.PreviousStateIsValid = false;
        _zeroHpPhase2CutsceneCameraCursor = -1;
        _zeroHpPhase2LastCutsceneCamera = null;
    }

    private static Vector3 FlattenDirection(Vector3 direction, Vector3 fallback)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.0001f)
        {
            fallback.y = 0f;
            return fallback.sqrMagnitude > 0.0001f ? fallback.normalized : Vector3.forward;
        }

        return direction.normalized;
    }

    private ZeroHpPhase2CutsceneShotSettings ResolveZeroHpPhase2CutsceneShotSettings(ZeroHpPhase2CutsceneShotId shotId)
    {
        switch (shotId)
        {
            case ZeroHpPhase2CutsceneShotId.KillConfirm:
                return zeroHpPhase2KillConfirmShot;
            case ZeroHpPhase2CutsceneShotId.FallenConfession:
                return zeroHpPhase2FallenConfessionShot;
            case ZeroHpPhase2CutsceneShotId.ReviveRise:
                return zeroHpPhase2ReviveRiseShot;
            case ZeroHpPhase2CutsceneShotId.FinalStand:
                return zeroHpPhase2FinalStandShot;
            default:
                return zeroHpPhase2FinalStandShot;
        }
    }

    private string ResolveZeroHpPhase2CutsceneShotAnchorName(ZeroHpPhase2CutsceneShotId shotId)
    {
        switch (shotId)
        {
            case ZeroHpPhase2CutsceneShotId.KillConfirm:
                return "Phase2Shot_KillConfirm";
            case ZeroHpPhase2CutsceneShotId.FallenConfession:
                return "Phase2Shot_FallenConfession";
            case ZeroHpPhase2CutsceneShotId.ReviveRise:
                return "Phase2Shot_ReviveRise";
            case ZeroHpPhase2CutsceneShotId.FinalStand:
                return "Phase2Shot_FinalStand";
            default:
                return string.Empty;
        }
    }

    private Transform ResolveZeroHpPhase2CutsceneShotAnchor(ZeroHpPhase2CutsceneShotId shotId)
    {
        return FindBossTransform(ResolveZeroHpPhase2CutsceneShotAnchorName(shotId));
    }

    private void ResolveZeroHpPhase2CutsceneShotPose(
        ZeroHpPhase2CutsceneShotId shotId,
        PlayerController targetPlayer,
        out Vector3 worldPosition,
        out Vector3 lookAtPosition)
    {
        Transform focusAnchor = ResolvePhase2CoreAnchor();
        Vector3 anchorPosition = focusAnchor != null ? focusAnchor.position : transform.position + Vector3.up * 1.2f;
        Vector3 basisForward = FlattenDirection(transform.forward, Vector3.forward);
        if (shotId == ZeroHpPhase2CutsceneShotId.KillConfirm && targetPlayer != null)
        {
            basisForward = FlattenDirection(anchorPosition - targetPlayer.transform.position, basisForward);
        }

        Vector3 basisRight = Vector3.Cross(Vector3.up, basisForward);
        if (basisRight.sqrMagnitude <= 0.0001f)
        {
            basisRight = Vector3.right;
        }
        else
        {
            basisRight.Normalize();
        }

        ZeroHpPhase2CutsceneShotSettings shotSettings = ResolveZeroHpPhase2CutsceneShotSettings(shotId);
        Transform shotAnchor = ResolveZeroHpPhase2CutsceneShotAnchor(shotId);
        worldPosition = shotAnchor != null
            ? shotAnchor.position
            : anchorPosition +
              basisRight * shotSettings.offset.x +
              Vector3.up * shotSettings.offset.y +
              basisForward * shotSettings.offset.z;
        lookAtPosition =
            anchorPosition +
            basisRight * shotSettings.aimOffset.x +
            Vector3.up * shotSettings.aimOffset.y +
            basisForward * shotSettings.aimOffset.z;
    }

    private static Color ResolveZeroHpPhase2CutsceneShotGizmoColor(ZeroHpPhase2CutsceneShotId shotId)
    {
        switch (shotId)
        {
            case ZeroHpPhase2CutsceneShotId.KillConfirm:
                return new Color(1f, 0.25f, 0.25f, 1f);
            case ZeroHpPhase2CutsceneShotId.FallenConfession:
                return new Color(1f, 0.6f, 0.2f, 1f);
            case ZeroHpPhase2CutsceneShotId.ReviveRise:
                return new Color(1f, 0.9f, 0.2f, 1f);
            case ZeroHpPhase2CutsceneShotId.FinalStand:
                return new Color(0.95f, 0.95f, 1f, 1f);
            default:
                return Color.white;
        }
    }

    private void DrawZeroHpPhase2CutsceneShotGizmo(ZeroHpPhase2CutsceneShotId shotId)
    {
        ResolveZeroHpPhase2CutsceneShotPose(shotId, null, out Vector3 worldPosition, out Vector3 lookAtPosition);
        Vector3 lookDirection = lookAtPosition - worldPosition;
        if (lookDirection.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        Gizmos.color = ResolveZeroHpPhase2CutsceneShotGizmoColor(shotId);
        Gizmos.DrawWireSphere(worldPosition, 0.08f);
        Gizmos.DrawLine(worldPosition, lookAtPosition);
        Gizmos.DrawSphere(lookAtPosition, 0.05f);

        Vector3 arrowDirection = lookDirection.normalized * Mathf.Min(0.5f, lookDirection.magnitude * 0.3f);
        Vector3 arrowBase = lookAtPosition - arrowDirection;
        Vector3 arrowSide = Quaternion.AngleAxis(20f, Vector3.up) * -arrowDirection * 0.4f;
        Gizmos.DrawLine(lookAtPosition, arrowBase + arrowSide);
        arrowSide = Quaternion.AngleAxis(-20f, Vector3.up) * -arrowDirection * 0.4f;
        Gizmos.DrawLine(lookAtPosition, arrowBase + arrowSide);
    }

    private void OnDrawGizmos()
    {
        if (!showZeroHpPhase2ShotPreviewGizmos)
        {
            return;
        }

        DrawZeroHpPhase2CutsceneShotGizmo(ZeroHpPhase2CutsceneShotId.KillConfirm);
        DrawZeroHpPhase2CutsceneShotGizmo(ZeroHpPhase2CutsceneShotId.FallenConfession);
        DrawZeroHpPhase2CutsceneShotGizmo(ZeroHpPhase2CutsceneShotId.ReviveRise);
        DrawZeroHpPhase2CutsceneShotGizmo(ZeroHpPhase2CutsceneShotId.FinalStand);
    }

    private void ConfigureZeroHpPhase2CutsceneShotCamera(
        CinemachineVirtualCamera shotCamera,
        ZeroHpPhase2CutsceneShotId shotId,
        PlayerController targetPlayer)
    {
        if (shotCamera == null)
        {
            return;
        }

        ResolveZeroHpPhase2CutsceneShotPose(shotId, targetPlayer, out Vector3 worldPosition, out Vector3 lookAtPosition);
        ZeroHpPhase2CutsceneShotSettings shotSettings = ResolveZeroHpPhase2CutsceneShotSettings(shotId);
        Vector3 lookDirection = lookAtPosition - worldPosition;
        if (lookDirection.sqrMagnitude <= 0.0001f)
        {
            lookDirection = FlattenDirection(transform.forward, Vector3.forward);
        }

        shotCamera.Follow = null;
        shotCamera.LookAt = null;
        shotCamera.transform.position = worldPosition;
        shotCamera.transform.rotation = Quaternion.LookRotation(lookDirection.normalized, Vector3.up) * Quaternion.Euler(shotSettings.eulerOffset);

        LensSettings lens = shotCamera.m_Lens;
        lens.FieldOfView = Mathf.Max(1f, shotSettings.fieldOfView);
        shotCamera.m_Lens = lens;
        shotCamera.PreviousStateIsValid = false;
        shotCamera.CancelDamping();
    }

    private bool SwitchZeroHpPhase2CutsceneShot(
        ZeroHpPhase2CutsceneShotId shotId,
        PlayerController targetPlayer,
        bool forceCut = false)
    {
        if (!useEntranceCameraForZeroHpPhase2 || introCam == null)
        {
            return false;
        }

        CinemachineVirtualCamera[] shotCameras = GetZeroHpPhase2CutsceneCameras();
        if (shotCameras == null || shotCameras.Length == 0)
        {
            return false;
        }

        CacheZeroHpPhase2CutsceneRig();

        int nextIndex = shotCameras.Length == 1 ? 0 : (_zeroHpPhase2CutsceneCameraCursor + 1 + shotCameras.Length) % shotCameras.Length;
        if (shotCameras.Length > 1 && nextIndex == _zeroHpPhase2CutsceneCameraCursor)
        {
            nextIndex = (nextIndex + 1) % shotCameras.Length;
        }

        CinemachineVirtualCamera nextCamera = shotCameras[nextIndex];
        ConfigureZeroHpPhase2CutsceneShotCamera(nextCamera, shotId, targetPlayer);

        ZeroHpPhase2CutsceneShotSettings shotSettings = ResolveZeroHpPhase2CutsceneShotSettings(shotId);
        CinemachineBlendDefinition blendDefinition = new CinemachineBlendDefinition(
            forceCut ? CinemachineBlendDefinition.Style.Cut : shotSettings.blendStyle,
            forceCut ? 0f : Mathf.Max(0f, shotSettings.blendTime));

        introCam.m_Loop = false;
        introCam.m_Instructions = new[]
        {
            new CinemachineBlendListCamera.Instruction
            {
                m_VirtualCamera = nextCamera,
                m_Hold = 30f,
                m_Blend = blendDefinition
            }
        };

        bool wasCameraAlreadyActive = introCam.gameObject.activeSelf && introCam.Priority > 0;
        if (!wasCameraAlreadyActive)
        {
            ActivateEntranceCamera(zeroHpPhase2CameraPriority);
            _zeroHpPhase2CutsceneCameraCursor = nextIndex;
            _zeroHpPhase2LastCutsceneCamera = nextCamera;
            return true;
        }

        ICinemachineCamera previousCamera = _zeroHpPhase2LastCutsceneCamera ?? introCam.LiveChild;
        introCam.OnTransitionFromCamera(previousCamera, Vector3.up, 0f);
        _zeroHpPhase2CutsceneCameraCursor = nextIndex;
        _zeroHpPhase2LastCutsceneCamera = nextCamera;
        return true;
    }

    private string ResolveFirstExistingAnimationState(params string[] candidates)
    {
        if (anim == null || candidates == null)
        {
            return string.Empty;
        }

        for (int i = 0; i < candidates.Length; i++)
        {
            string candidate = candidates[i];
            if (string.IsNullOrWhiteSpace(candidate))
            {
                continue;
            }

            if (anim.HasState(0, Animator.StringToHash(candidate)))
            {
                return candidate;
            }
        }

        return string.Empty;
    }

    private string[] GetZeroHpPhase2DialogueSegment(int startIndex, int endExclusive)
    {
        if (zeroHpPhase2DialogueLines == null || zeroHpPhase2DialogueLines.Length == 0)
        {
            return new string[0];
        }

        int clampedStart = Mathf.Clamp(startIndex, 0, zeroHpPhase2DialogueLines.Length);
        int clampedEnd = Mathf.Clamp(endExclusive, clampedStart, zeroHpPhase2DialogueLines.Length);
        int count = clampedEnd - clampedStart;
        if (count <= 0)
        {
            return new string[0];
        }

        string[] lines = new string[count];
        for (int i = 0; i < count; i++)
        {
            lines[i] = zeroHpPhase2DialogueLines[clampedStart + i];
        }

        return lines;
    }

    private IEnumerator Co_PlayZeroHpPhase2Dialogue(PlayerController targetPlayer, string[] lines, System.Action<int> onLineShown = null)
    {
        if (lines == null || lines.Length == 0)
        {
            yield break;
        }

        ResolveZeroHpPhase2CutSceneDialogueReferences();

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i] ?? string.Empty;
            ShowZeroHpPhase2CutSceneDialogue(line);
            onLineShown?.Invoke(i);
            yield return StartCoroutine(Co_TypeZeroHpPhase2CutSceneDialogueLine(line));
        }

        HideZeroHpPhase2CutSceneDialogue();
    }

    private IEnumerator Co_PlayZeroHpPhase2ReviveAnimation(string reviveAnimationStateName, Renderer presentationRenderer)
    {
        StartPhase2WeaponIgniteFx();
        FlashBossPresentation(
            presentationRenderer,
            zeroHpPhase2DownPulseFlashColor,
            Mathf.Max(zeroHpPhase2DownPulseFlashDuration, 0.12f));

        if (!string.IsNullOrWhiteSpace(reviveAnimationStateName))
        {
            PlayAnim(reviveAnimationStateName, criticalReactionCrossFade);
            yield return StartCoroutine(Co_WaitForAnimatorStateCompletion(
                reviveAnimationStateName,
                Mathf.Max(criticalReactionTimeout, 6f)));
        }
    }

    protected override void Start()
    {
        base.Start();

        if (zoneTriggerObj != null && !HasExternalEntranceDirector()) zoneTriggerObj.SetActive(false);
        if (backWeapon) backWeapon.SetActive(true);
        if (handWeapon) handWeapon.SetActive(false);
        if (introCam) { introCam.gameObject.SetActive(false); introCam.Priority = 0; }
        if (bossNameUI) bossNameUI.SetActive(false);

        isIntroStarted = false;
        _hasTriggeredZeroHpPhase2Transition = currentPhase >= 2;
        _isZeroHpPhase2TransitionActive = false;
        SetZeroHpPhase2GroundClampActive(false);
        ApplyEncounterHealthForPhase(currentPhase, true, false);
        CancelAttackRuntimeState();
        StopAgent();
        StopAllCoroutines();

        currentState = State.Idle;
        currentLogicState = idleState;
        SetRootMotionMode(false, false);

        PlayAnim("PreintroIdle", 0.1f);
        SetAnimSpeed(baseAttackSpeed);
        CallHpChanged();
    }

    private void OnDisable()
    {
        _attackTrajectory?.Shutdown();
        SetZeroHpPhase2GroundClampActive(false);
        ReleaseZeroHpPhase2CutscenePlayerLock();
        RestoreZeroHpPhase2CutsceneRig();
        HideZeroHpPhase2CutSceneDialogue();
        CleanupZeroHpPhase2Fx();
    }

    private void OnDestroy()
    {
        _attackTrajectory?.Shutdown();
        SetZeroHpPhase2GroundClampActive(false);
        ReleaseZeroHpPhase2CutscenePlayerLock();
        RestoreZeroHpPhase2CutsceneRig();
        HideZeroHpPhase2CutSceneDialogue();
        CleanupZeroHpPhase2Fx();
    }

    void Update()
    {
        _attackTrajectory?.Tick();
        SyncAgentToTransform();

        if (_isZeroHpPhase2GroundClampActive)
        {
            ApplyGroundSnapIfNeeded();
        }

        if (IsDead) return;

        if (HandleDebugStateHotkeys())
        {
            return;
        }

        RefreshCriticalStrikePrompt();

        // 1. 인트로 전 거리 감지 및 자동/G키 처리
        if (!isIntroStarted)
        {
            HandlePreIntro();
            if (agent && agent.isOnNavMesh) agent.velocity = Vector3.zero;
            return;
        }

        // 2. 2페이즈 전환 실시간 체크
        bool shouldCheckPhase2 = !useZeroHpPhase2Transition &&
                                 currentPhase == 1 &&
                                 !isPhaseTransitioning &&
                                 !ShouldHoldPhase1ForValidation() &&
                                 !_debugLockPhase1UntilReleased;
        if (shouldCheckPhase2)
        {
            if ((hp / MaxHp) <= phase2Threshold)
            {
                if (ShouldDelayPhase2Transition())
                {
                    _phase2TransitionPending = true;
                    return;
                }

                BeginPhase2Transition();
                return;
            }
        }

        // 3. 특정 상태에서 에이전트 강제 정지 (걷기 방지)
        if (currentState == State.Idle || currentState == State.Intro || isWaitingAfterAttack || isPhaseTransitioning)
        {
            StopAgent();
        }

        // 스태미나 회복
        if (currentState != State.Action && !isWaitingAfterAttack && !isPhaseTransitioning)
        {
            stamina = Mathf.Min(maxStamina, stamina + staminaRegen * Time.deltaTime);
        }

        if (currentState != State.Action &&
            currentState != State.Groggy &&
            currentState != State.HitReact &&
            !isPhaseTransitioning)
        {
            currentPoise = Mathf.MoveTowards(currentPoise, maxPoise, poiseRecoveryPerSecond * Time.deltaTime);
        }
    }

    public override void TakeDamage(int amount, Vector3 hitPoint, Vector3 hitNormal)
    {
        if (IsDead || !isIntroStarted || isPhaseTransitioning || _isZeroHpPhase2TransitionActive)
        {
            return;
        }

        CombatAttackDefinition incomingAttack = CombatDamageContext.CurrentAttack;

        hp = Mathf.Max(0f, hp - amount);
        CallHpChanged();
        PlayIncomingHitFeedback(incomingAttack, hitPoint, hitNormal);

        if (hp <= 0f)
        {
            if (CanTriggerZeroHpPhase2Transition())
            {
                BeginZeroHpPhase2Transition();
                return;
            }

            Die();
            return;
        }

        ClearPendingHitReaction();
        ApplyIncomingCombatPressure(amount, CombatDamageContext.CurrentAttack);

        if (ShouldEnterGroggyState())
        {
            ClearPendingHitReaction();
            ForceImmediateStateTransition(groggyState, State.Groggy);
            return;
        }

        if (_hasPendingHitReaction)
        {
            ForceImmediateStateTransition(hitReactState, State.HitReact);
        }
    }

    public override bool OnParried(CombatAttackDefinition incomingAttack, Transform defender, Vector3 hitPoint, Vector3 hitNormal)
    {
        if (IsDead || !isIntroStarted)
        {
            return false;
        }

        ClearPendingHitReaction();
        CancelAttackRuntimeState();
        PlayRandomBossSfx(parriedSfxClips, parriedSfxVolume);

        if (maxGroggyGauge > 0f)
        {
            float groggyGain = Mathf.Max(parryGroggyBonus, maxGroggyGauge * parryGroggyBonusMultiplier);
            currentGroggyGauge = Mathf.Min(maxGroggyGauge, currentGroggyGauge + groggyGain);
        }

        currentPoise = 0f;

        if (ShouldEnterGroggyState())
        {
            ForceImmediateStateTransition(groggyState, State.Groggy);
            return true;
        }

        QueueParryHitReaction(incomingAttack);
        ForceImmediateStateTransition(hitReactState, State.HitReact);
        return true;
    }

    private void HandlePreIntro()
    {
        if (target == null) return;
        float dist = Vector3.Distance(transform.position, target.position);

        if (zoneTriggerObj != null && !zoneTriggerObj.activeSelf && dist <= detectionDistance)
            zoneTriggerObj.SetActive(true);

        if (_isExternalEntranceControlActive || HasExternalEntranceDirector())
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.G) || (CheckQuestStatus() && dist <= triggerDistance))
        {
            ExecuteIntroProcess();
        }
    }

    private bool HandleDebugStateHotkeys()
    {
        if (!enableDebugStateHotkeys)
        {
            return false;
        }

        if (Input.GetKeyDown(debugForcePhase1Key))
        {
            DebugForcePhase(1);
            return true;
        }

        if (Input.GetKeyDown(debugForcePhase2Key))
        {
            DebugForcePhase(2);
            return true;
        }

        if (Input.GetKeyDown(debugForceGroggyKey))
        {
            DebugForceGroggy();
            return true;
        }

        if (isIntroStarted && Input.GetKeyDown(debugForceSkillGKey))
        {
            DebugForceAttack("Skill_G", 2);
            return true;
        }

        if (isIntroStarted && Input.GetKeyDown(debugForceSkillKKey))
        {
            DebugForceAttack("Skill_K");
            return true;
        }

        return false;
    }

    [ContextMenu("Debug/Force Phase 1")]
    private void DebugForcePhase1FromContextMenu()
    {
        DebugForcePhase(1);
    }

    [ContextMenu("Debug/Force Phase 2")]
    private void DebugForcePhase2FromContextMenu()
    {
        DebugForcePhase(2);
    }

    [ContextMenu("Debug/Force Groggy")]
    private void DebugForceGroggyFromContextMenu()
    {
        DebugForceGroggy();
    }

    [ContextMenu("Debug/Force Skill G")]
    private void DebugForceSkillGFromContextMenu()
    {
        DebugForceAttack("Skill_G", 2);
    }

    [ContextMenu("Debug/Force Skill K")]
    private void DebugForceSkillKFromContextMenu()
    {
        DebugForceAttack("Skill_K");
    }

    private void DebugForcePhase(int phase)
    {
        DebugEnterCombatContext();

        currentPhase = Mathf.Clamp(phase, 1, 2);
        _phase2TransitionPending = false;
        _hasTriggeredZeroHpPhase2Transition = currentPhase >= 2;
        _isZeroHpPhase2TransitionActive = false;
        _debugLockPhase1UntilReleased = currentPhase == 1;
        currentPoise = maxPoise;
        stamina = maxStamina;
        ResetGroggyGauge();
        ResetPatternSelectionHistory();
        ApplyEncounterHealthForPhase(currentPhase, true, true);

        DebugTransitionToState(moveState, State.Move);
        Debug.Log($"[Boss Debug] Forced Phase {currentPhase}");
    }

    private void DebugForceGroggy()
    {
        DebugEnterCombatContext();
        _debugLockPhase1UntilReleased = false;
        currentPoise = 0f;
        ForceGroggyGaugeToMax();
        ResetPatternSelectionHistory();

        DebugTransitionToState(groggyState, State.Groggy);
        Debug.Log("[Boss Debug] Forced Groggy");
    }

    private void DebugForceAttack(string attackId, int minimumPhase = 0)
    {
        if (string.IsNullOrWhiteSpace(attackId))
        {
            return;
        }

        DebugEnterCombatContext();

        if (minimumPhase > 0)
        {
            currentPhase = Mathf.Max(currentPhase, minimumPhase);
            _debugLockPhase1UntilReleased = currentPhase == 1;
        }

        stamina = maxStamina;
        currentPoise = maxPoise;
        isWaitingAfterAttack = false;
        ResetPatternSelectionHistory();
        attackQueue.Clear();
        attackQueue.Enqueue(attackId);

        DebugStartActionStateWithQueuedAttack();
        Debug.Log($"[Boss Debug] Forced Attack {attackId} (Phase {currentPhase})");
    }

    private void DebugForceAttackSequence(string[] attackIds, int minimumPhase = 0)
    {
        if (attackIds == null || attackIds.Length <= 0)
        {
            return;
        }

        DebugEnterCombatContext();

        if (minimumPhase > 0)
        {
            currentPhase = Mathf.Max(currentPhase, minimumPhase);
            _debugLockPhase1UntilReleased = currentPhase == 1;
        }

        stamina = maxStamina;
        currentPoise = maxPoise;
        isWaitingAfterAttack = false;
        ResetPatternSelectionHistory();
        attackQueue.Clear();

        int queuedCount = 0;
        foreach (string attackId in attackIds)
        {
            if (string.IsNullOrWhiteSpace(attackId))
            {
                continue;
            }

            attackQueue.Enqueue(attackId);
            queuedCount++;
        }

        if (queuedCount <= 0)
        {
            return;
        }

        DebugStartActionStateWithQueuedAttack();
        Debug.Log($"[Boss Debug] Forced Sequence {string.Join(" -> ", attackIds)} (Phase {currentPhase})");
    }

    private void DebugEnterCombatContext()
    {
        _isExternalEntranceControlActive = false;
        isIntroStarted = true;
        isPhaseTransitioning = false;
        _stateTransitionLocked = false;
        ClearPendingHitReaction();

        AbortCriticalStrikePresentation();
        HideCriticalPrompt();
        CancelAttackRuntimeState();
        StopAllCoroutines();
        StopAgent();

        currentLogicState = null;

        if (zoneTriggerObj != null)
        {
            zoneTriggerObj.SetActive(true);
            zoneTriggerObj.SendMessage("StartBossFight", SendMessageOptions.DontRequireReceiver);
        }

        if (introCam != null)
        {
            introCam.gameObject.SetActive(false);
            introCam.Priority = 0;
        }

        if (bossNameUI != null)
        {
            bossNameUI.SetActive(true);
        }

        if (backWeapon != null)
        {
            backWeapon.SetActive(false);
        }

        if (handWeapon != null)
        {
            handWeapon.SetActive(true);
        }
    }

    public void BeginExternalEntranceControl()
    {
        _isExternalEntranceControlActive = true;
        isIntroStarted = false;
        isPhaseTransitioning = false;
        _stateTransitionLocked = false;
        ClearPendingHitReaction();
        AbortCriticalStrikePresentation();
        HideCriticalPrompt();
        CancelAttackRuntimeState();
        StopAgent();
        currentState = State.Idle;
        currentLogicState = idleState;
        SetRootMotionMode(false, false);
        SetAnimSpeed(baseAttackSpeed);
        PlayAnim("PreintroIdle", 0.05f);

        if (introCam != null)
        {
            introCam.gameObject.SetActive(false);
            introCam.Priority = 0;
        }

        if (bossNameUI != null)
        {
            bossNameUI.SetActive(false);
        }

        if (backWeapon != null)
        {
            backWeapon.SetActive(false);
        }

        if (handWeapon != null)
        {
            handWeapon.SetActive(false);
        }
    }


    public void ResetForEncounterRetry()
    {
        _isExternalEntranceControlActive = false;
        isIntroStarted = false;
        isPhaseTransitioning = false;
        isWaitingAfterAttack = false;
        isSuperArmor = false;
        _stateTransitionLocked = false;
        _phase2TransitionPending = false;
        _debugLockPhase1UntilReleased = false;
        _criticalStrikeConsumedThisGroggy = false;
        currentPhase = Mathf.Clamp(_initialPhase, 1, 2);
        _hasTriggeredZeroHpPhase2Transition = currentPhase >= 2;
        _isZeroHpPhase2TransitionActive = false;
        SetZeroHpPhase2GroundClampActive(false);
        stamina = maxStamina;
        currentPoise = maxPoise;
        lastSkillTime = -999f;
        ApplyEncounterHealthForPhase(currentPhase, true, false);

        if (target == null)
        {
            target = GameObject.FindWithTag("Player")?.transform;
        }

        HideCriticalPrompt();
        AbortCriticalStrikePresentation();
        CancelAttackRuntimeState();
        ClearPendingHitReaction();
        ResetGroggyGauge();
        ResetPatternSelectionHistory();
        StopAllCoroutines();
        StopAgent();
        RestoreSpawnTransform();

        currentState = State.Idle;
        currentLogicState = idleState;
        SetRootMotionMode(false, false);
        SetAnimSpeed(baseAttackSpeed);
        PlayAnim("PreintroIdle", 0.05f);

        Collider rootCollider = GetComponent<Collider>();
        if (rootCollider != null)
        {
            rootCollider.enabled = true;
        }

        bossHitbox?.SetHitBoxActive(false);

        if (zoneTriggerObj != null && !HasExternalEntranceDirector())
        {
            zoneTriggerObj.SetActive(false);
        }

        if (introCam != null)
        {
            introCam.Priority = 0;
            introCam.gameObject.SetActive(false);
        }

        if (bossNameUI != null)
        {
            bossNameUI.SetActive(false);
        }

        if (backWeapon != null)
        {
            backWeapon.SetActive(true);
        }

        if (handWeapon != null)
        {
            handWeapon.SetActive(false);
        }

        CallHpChanged();
    }

    public void ShowEntranceBackWeapon(bool visible)
    {
        if (backWeapon != null)
        {
            backWeapon.SetActive(visible);
        }
    }

    public void ShowEntranceHandWeapon(bool visible)
    {
        if (handWeapon != null)
        {
            handWeapon.SetActive(visible);
        }
    }

    public void ShowBossNameUI(bool visible)
    {
        if (bossNameUI != null)
        {
            bossNameUI.SetActive(visible);
        }
    }

    public void ActivateEntranceCamera(int priority = 25)
    {
        if (introCam == null)
        {
            return;
        }

        introCam.gameObject.SetActive(true);
        introCam.Priority = priority;
    }

    public void DeactivateEntranceCamera()
    {
        if (introCam == null)
        {
            return;
        }

        introCam.Priority = 0;
        introCam.gameObject.SetActive(false);
    }

    public void CompleteExternalEntranceControl()
    {
        _isExternalEntranceControlActive = false;
        isIntroStarted = true;
        SetRootMotionMode(false, false);
        SetAnimSpeed(baseAttackSpeed);
        ShowEntranceBackWeapon(false);
        ShowEntranceHandWeapon(true);

        if (IsDead)
        {
            return;
        }

        _stateTransitionLocked = false;
        currentLogicState = null;
        ChangeState(idleState, State.Idle);
    }

    private void RestoreSpawnTransform()
    {
        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.Warp(_spawnPosition);
            transform.rotation = _spawnRotation;
            return;
        }

        transform.SetPositionAndRotation(_spawnPosition, _spawnRotation);
    }

    public void AbortExternalEntranceControl()
    {
        _isExternalEntranceControlActive = false;
        DeactivateEntranceCamera();
        ShowBossNameUI(false);
        ShowEntranceBackWeapon(false);
        ShowEntranceHandWeapon(false);
        currentState = State.Idle;
        currentLogicState = idleState;
        SetRootMotionMode(false, false);
        SetAnimSpeed(baseAttackSpeed);
        PlayAnim("PreintroIdle", 0.1f);
    }

    private void DebugTransitionToState(BossState state, State enumState)
    {
        _stateTransitionLocked = false;
        currentLogicState = null;
        ChangeState(state, enumState);
    }

    private void DebugStartActionStateWithQueuedAttack()
    {
        _stateTransitionLocked = false;
        StopAllCoroutines();
        currentState = State.Action;
        currentLogicState = actionState;
        StartCoroutine(currentLogicState.Enter());
        StartCoroutine(Co_ReleaseStateTransitionLock());
    }

    public void StartBossIntro()
    {
        if (isIntroStarted || IsDead) return;
        if (CheckQuestStatus()) ExecuteIntroProcess();
    }

    private void ExecuteIntroProcess()
    {
        if (isIntroStarted) return;
        isIntroStarted = true;
        StartCoroutine(Intro());
    }

    protected override IEnumerator Intro()
    {
        currentState = State.Intro;
        StopAgent();

        if (zoneTriggerObj != null)
        {
            zoneTriggerObj.SetActive(true);
            yield return null;
            zoneTriggerObj.SendMessage("StartBossFight", SendMessageOptions.DontRequireReceiver);
        }

        yield return new WaitForSeconds(0.7f);

        if (backWeapon) backWeapon.SetActive(true);
        if (introCam != null) { introCam.gameObject.SetActive(true); introCam.Priority = 25; }

        yield return new WaitForSeconds(cameraBlendTime);
        SetRootMotionMode(ShouldUseIntroRootMotion(), false);
        SetAnimSpeed(introExhaustedAnimSpeed);
        PlayAnim("Exhausted", 0.2f);
        if (bossNameUI) bossNameUI.SetActive(true);
        yield return StartCoroutine(Co_WaitForAnimatorStateCompletion("Exhausted", Mathf.Max(introWaitTime, 8f)));
        SetRootMotionMode(false, false);
        SetAnimSpeed(baseAttackSpeed);

        if (introCam != null) introCam.Priority = 0;
        ChangeState(idleState, State.Idle);
    }

    private bool HasExternalEntranceDirector()
    {
        return zoneTriggerObj != null && zoneTriggerObj.GetComponent<BossEntranceDirector>() != null;
    }

    private bool CheckQuestStatus() => true;

    public void ApplyCameraShake(float intensity, float duration)
    {
        if (impulseSource != null)
        {
            // 시네머신 임펄스 발생 (진동)
            impulseSource.GenerateImpulse(intensity);
        }
    }

    private void PlayIncomingHitFeedback(CombatAttackDefinition incomingAttack, Vector3 hitPoint, Vector3 hitNormal)
    {
        float hitStopDuration = ResolveCombatHitStopDuration(incomingAttack, 0.06f);
        float shakeDuration = ResolveCombatShakeDuration(incomingAttack, 0.08f, 0.12f);
        float shakeIntensity = ResolveCombatShakeIntensity(incomingAttack, 0.08f, 0.16f);

        PlayRandomBossSfx(incomingHitSfxClips, incomingHitSfxVolume);

        if (hitStopDuration > 0f)
        {
            EffectManager.Instance.DoHitStop(hitStopDuration);
        }

        if (shakeIntensity > 0f)
        {
            if (impulseSource != null)
            {
                ApplyCameraShake(shakeIntensity, shakeDuration);
            }
            else
            {
                EffectManager.Instance.DoCameraShake(shakeDuration, shakeIntensity);
            }
        }

        Renderer targetRenderer = GetComponentInChildren<SkinnedMeshRenderer>();
        if (targetRenderer != null)
        {
            EffectManager.Instance.DoFlashEffect(targetRenderer, Color.white * 0.6f, 0.05f);
        }

        SpawnCombatImpactEffect(bossHitBloodEffectPrefab, hitPoint, hitNormal);
    }

    private static float ResolveCombatHitStopDuration(CombatAttackDefinition attack, float fallbackDuration)
    {
        if (attack == null)
        {
            return Mathf.Max(0f, fallbackDuration);
        }

        float baseDuration = attack.hitStopDuration > 0f ? attack.hitStopDuration : fallbackDuration;
        float multiplier = attack.hitStopMultiplier > 0f ? attack.hitStopMultiplier : 1f;
        return Mathf.Max(0f, baseDuration * multiplier);
    }

    private static float ResolveCombatShakeDuration(CombatAttackDefinition attack, float fallbackDuration, float heavyFallbackDuration)
    {
        if (attack == null)
        {
            return Mathf.Max(0f, fallbackDuration);
        }

        if (attack.cameraShakeDuration > 0f)
        {
            return attack.cameraShakeDuration;
        }

        return attack.isHeavyAttack ? Mathf.Max(0f, heavyFallbackDuration) : Mathf.Max(0f, fallbackDuration);
    }

    private static float ResolveCombatShakeIntensity(CombatAttackDefinition attack, float fallbackIntensity, float heavyFallbackIntensity)
    {
        if (attack == null)
        {
            return Mathf.Max(0f, fallbackIntensity);
        }

        if (attack.cameraShakeIntensity > 0f)
        {
            return attack.cameraShakeIntensity;
        }

        return attack.isHeavyAttack ? Mathf.Max(0f, heavyFallbackIntensity) : Mathf.Max(0f, fallbackIntensity);
    }

    private void SpawnCombatImpactEffect(GameObject effectPrefab, Vector3 hitPoint, Vector3 hitNormal)
    {
        if (effectPrefab == null)
        {
            return;
        }

        Vector3 spawnNormal = hitNormal.sqrMagnitude > 0.0001f ? hitNormal.normalized : transform.forward;
        Quaternion spawnRotation = Quaternion.LookRotation(spawnNormal, Vector3.up);
        GameObject effectInstance = Instantiate(effectPrefab, hitPoint, spawnRotation);
        effectInstance.transform.localScale = Vector3.one;
    }

    public bool TryChangeState(BossState newState, State enumState)
    {
        if (!isIntroStarted && enumState != State.Intro) return false;
        if (IsDead || isPhaseTransitioning) return false;
        if (_stateTransitionLocked) return false;
        if (currentState == enumState && currentLogicState == newState) return false;

        if (enumState != State.Groggy)
        {
            HideCriticalPrompt();
        }

        _stateTransitionLocked = true;
        CancelAttackRuntimeState();
        StopAllCoroutines();
        currentState = enumState;
        currentLogicState = newState;

        if (enumState == State.Action) PrepareAttackSequence();
        StartCoroutine(currentLogicState.Enter());
        StartCoroutine(Co_ReleaseStateTransitionLock());
        return true;
    }

    public void ChangeState(BossState newState, State enumState)
    {
        TryChangeState(newState, enumState);
    }

    public IEnumerator Co_WaitAfterAttack(float multiplier = 1.0f)
    {
        isWaitingAfterAttack = true;

        // 1. 방향 및 애니메이션 결정
        bool isLeft = Random.value > 0.5f;
        string walkAnim = isLeft ? "Walk_L90" : "Walk_R90";

        // [수정] 걷는 거리와 시간을 대폭 줄임
        // multiplier가 1.0(묵직함)일 때도 최대 1초 이상 걷지 않도록 Clamp를 겁니다.
        float maxWalkTime = 0.8f; // 1초 미만으로 설정하면 거리가 확실히 짧아집니다.
        float tempoMultiplier = ResolvePostAttackTempoMultiplier();
        float totalWaitTime = postAttackWaitTime * multiplier * tempoMultiplier;
        float waitTime = Mathf.Min(totalWaitTime, maxWalkTime);
        float elapsed = 0;

        // 속도를 낮추면 같은 시간 동안 이동하는 거리가 줄어듭니다.
        float currentMoveSpeed = moveSpeed * (multiplier >= 1.0f ? 0.2f : 0.35f);

        // 2. 애니메이션을 한 번만 실행 (발 굳음 방지)
        SetRootMotionMode(UsesFullRootMotion, false);
        PlayAnim(walkAnim, 0.2f);

        while (elapsed < waitTime)
        {
            if (IsDead || isPhaseTransitioning || currentState == State.Groggy) break;
            elapsed += Time.deltaTime;

            RotateToTargetWithOffset();

            if (target != null)
            {
                var stateInfo = anim.GetCurrentAnimatorStateInfo(0);

                // 애니메이션이 재생 중일 때만 이동
                if (!UsesFullRootMotion && stateInfo.IsName(walkAnim))
                {
                    Vector3 sideDir = isLeft ? -transform.right : transform.right;
                    if (agent.isOnNavMesh)
                    {
                        agent.Move(sideDir * currentMoveSpeed * Time.deltaTime);
                    }
                }
            }
            yield return null;
        }

        // 3. 루프가 끝나면 즉시 멈추고 Idle로 전환
        StopAgent();
        SetRootMotionMode(false, false);
        isWaitingAfterAttack = false;
        PlayAnim("Idle", 0.2f);

        // 만약 딜 타임(multiplier가 큼)이 더 남아있다면 Idle 상태로 마저 대기
        float remainingWait = totalWaitTime - elapsed;
        if (remainingWait > 0)
        {
            yield return new WaitForSeconds(remainingWait);
        }

        ChangeState(idleState, State.Idle);
    }

    public void RotateToTargetWithOffset()
    {
        if (target == null) return;
        Vector3 dir = (target.position - transform.position).normalized;
        dir.y = 0;
        if (dir != Vector3.zero)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), rotationSpeed * Time.deltaTime);
    }

    private void BeginZeroHpPhase2Transition()
    {
        if (!CanTriggerZeroHpPhase2Transition() || currentState == State.Dead || isPhaseTransitioning)
        {
            return;
        }

        _hasTriggeredZeroHpPhase2Transition = true;
        _phase2TransitionPending = false;
        StopAllCoroutines();
        StartCoroutine(TransitionToPhase2FromZeroHp());
    }

    private IEnumerator TransitionToPhase2FromZeroHp()
    {
        if (useDialogueCutsceneForZeroHpPhase2)
        {
            yield return StartCoroutine(TransitionToPhase2FromZeroHpDialogueCutscene());
            yield break;
        }

        yield return StartCoroutine(TransitionToPhase2FromZeroHpLegacy());
    }

    private IEnumerator TransitionToPhase2FromZeroHpDialogueCutscene()
    {
        BossZoneTrigger zoneTrigger = ResolveBossZoneTrigger();
        Renderer presentationRenderer = ResolveBossPresentationRenderer();
        PlayerController targetPlayer = ResolveTargetPlayer();

        CleanupZeroHpPhase2Fx();
        isPhaseTransitioning = true;
        _isZeroHpPhase2TransitionActive = true;
        SetZeroHpPhase2GroundClampActive(false);
        _debugLockPhase1UntilReleased = false;
        ClearPendingHitReaction();
        CancelAttackRuntimeState();
        HideCriticalPrompt();
        AbortCriticalStrikePresentation();
        ResetGroggyGauge();
        ResetPatternSelectionHistory();
        currentPoise = maxPoise;
        stamina = maxStamina;
        currentState = State.HitReact;
        currentLogicState = null;
        _stateTransitionLocked = true;
        StopAgent();
        bossHitbox?.SetHitBoxActive(false);
        ShowBossNameUI(false);
        zoneTrigger?.HideBossHud();
        SetAnimSpeed(1.0f);

        bool useReactionRootMotion = ShouldUseReactionRootMotion();
        SetRootMotionMode(useReactionRootMotion, useReactionRootMotion);

        if (EffectManager.Instance != null && zeroHpPhase2HitStopDuration > 0f)
        {
            EffectManager.Instance.DoHitStop(zeroHpPhase2HitStopDuration, Mathf.Clamp01(zeroHpPhase2HitStopTimeScale));
        }

        ApplyCameraShake(Mathf.Max(0.45f, criticalCameraShakeIntensity * 2f), 0.18f);

        string zeroHpDeathAnimationStateName = ResolveFirstExistingAnimationState(
            zeroHpPhase2DeathAnimation,
            criticalHitReactionAnimation,
            GroggyAnimationStateName);
        if (!string.IsNullOrWhiteSpace(zeroHpDeathAnimationStateName))
        {
            yield return StartCoroutine(Co_PlayTransitionAnimationAndWait(
                zeroHpDeathAnimationStateName,
                criticalReactionCrossFade,
                Mathf.Max(criticalReactionTimeout, 6f)));
        }

        if (zeroHpPhase2PreCutsceneDelay > 0f)
        {
            yield return new WaitForSeconds(zeroHpPhase2PreCutsceneDelay);
        }

        LockZeroHpPhase2CutscenePlayer(targetPlayer);
        zoneTrigger?.FadeBossBgmVolumeScale(zeroHpPhase2BgmDuckScale, zeroHpPhase2BgmDuckDuration);

        if (!SwitchZeroHpPhase2CutsceneShot(ZeroHpPhase2CutsceneShotId.KillConfirm, targetPlayer, true) &&
            useEntranceCameraForZeroHpPhase2 &&
            introCam != null)
        {
            ActivateEntranceCamera(zeroHpPhase2CameraPriority);
        }

        if (zeroHpPhase2DialogueStartDelay > 0f)
        {
            yield return new WaitForSeconds(zeroHpPhase2DialogueStartDelay);
        }

        SwitchZeroHpPhase2CutsceneShot(ZeroHpPhase2CutsceneShotId.FallenConfession, targetPlayer);

        int clampedReviveTriggerLineIndex = zeroHpPhase2DialogueLines == null || zeroHpPhase2DialogueLines.Length == 0
            ? -1
            : Mathf.Clamp(zeroHpPhase2ReviveTriggerLineIndex, 0, zeroHpPhase2DialogueLines.Length - 1);
        string[] preReviveLines = clampedReviveTriggerLineIndex >= 0
            ? GetZeroHpPhase2DialogueSegment(0, clampedReviveTriggerLineIndex + 1)
            : new string[0];
        string[] postReviveLines = clampedReviveTriggerLineIndex >= 0
            ? GetZeroHpPhase2DialogueSegment(clampedReviveTriggerLineIndex + 1, zeroHpPhase2DialogueLines.Length)
            : GetZeroHpPhase2DialogueSegment(0, zeroHpPhase2DialogueLines == null ? 0 : zeroHpPhase2DialogueLines.Length);

        string reviveAnimationStateName = ResolveFirstExistingAnimationState(
            zeroHpPhase2ReviveAnimation,
            criticalGetUpAnimation);
        Coroutine reviveAnimationCoroutine = null;
        bool reviveStarted = false;

        if (preReviveLines.Length > 0)
        {
            yield return StartCoroutine(Co_PlayZeroHpPhase2Dialogue(
                targetPlayer,
                preReviveLines,
                localLineIndex =>
                {
                    PlayPhase2PreWakeCoreFx();
                    FlashBossPresentation(
                        presentationRenderer,
                        zeroHpPhase2DownPulseFlashColor,
                        zeroHpPhase2DownPulseFlashDuration);

                    bool shouldStartRevive = !reviveStarted && localLineIndex >= preReviveLines.Length - 1;
                    if (!shouldStartRevive)
                    {
                        return;
                    }

                    SwitchZeroHpPhase2CutsceneShot(ZeroHpPhase2CutsceneShotId.ReviveRise, targetPlayer);
                    SetZeroHpPhase2GroundClampActive(false);
                    reviveStarted = true;
                    reviveAnimationCoroutine = StartCoroutine(Co_PlayZeroHpPhase2ReviveAnimation(
                        reviveAnimationStateName,
                        presentationRenderer));
                }));
        }

        if (!reviveStarted)
        {
            SetZeroHpPhase2GroundClampActive(false);
            reviveStarted = true;
            reviveAnimationCoroutine = StartCoroutine(Co_PlayZeroHpPhase2ReviveAnimation(
                reviveAnimationStateName,
                presentationRenderer));
        }

        if (reviveAnimationCoroutine != null)
        {
            yield return reviveAnimationCoroutine;
        }

        if (postReviveLines.Length > 0)
        {
            SwitchZeroHpPhase2CutsceneShot(ZeroHpPhase2CutsceneShotId.FinalStand, targetPlayer);
            yield return StartCoroutine(Co_PlayZeroHpPhase2Dialogue(targetPlayer, postReviveLines));
        }

        currentPhase = 2;
        ApplyEncounterHealthForPhase(currentPhase, true, true);
        ResetGroggyGauge();
        ResetPatternSelectionHistory();
        currentPoise = maxPoise;
        stamina = maxStamina;
        ShowBossNameUI(true);
        PlayPhase2GroundBurstFx();
        PlayPhase2BodyBurstFx();
        PlayPhase2AmbientMotesFx();
        FlashBossPresentation(
            presentationRenderer,
            zeroHpPhase2DownPulseFlashColor,
            Mathf.Max(zeroHpPhase2DownPulseFlashDuration, 0.12f));
        ApplyCameraShake(zeroHpPhase2WakeShakeIntensity, zeroHpPhase2WakeShakeDuration);
        zoneTrigger?.PlayPhase2BossBgm(zeroHpPhase2BgmRestoreDuration);

        PlayPhase2ResidualAuraFx();
        StopPhase2WeaponIgniteFx();

        if (zeroHpPhase2PostRecoverDelay > 0f)
        {
            yield return new WaitForSeconds(zeroHpPhase2PostRecoverDelay);
        }

        yield return StartCoroutine(Co_WaitForZeroHpPhase2CameraReturn());

        RestoreZeroHpPhase2CutsceneRig();
        CleanupZeroHpPhase2Fx();
        ShowBossNameUI(false);
        SetRootMotionMode(false, false);
        SetAnimSpeed(baseAttackSpeed);
        ReleaseZeroHpPhase2CutscenePlayerLock();
        SetZeroHpPhase2GroundClampActive(false);
        _isZeroHpPhase2TransitionActive = false;
        isPhaseTransitioning = false;
        _stateTransitionLocked = false;

        BeginCombatResumeDelay(zeroHpPhase2CombatResumeDelay);
        ChangeState(idleState, State.Idle);
        zoneTrigger?.ShowBossHudDelayed(zeroHpPhase2HudShowDelay);
    }

    private IEnumerator TransitionToPhase2FromZeroHpLegacy()
    {
        BossZoneTrigger zoneTrigger = ResolveBossZoneTrigger();
        Renderer presentationRenderer = ResolveBossPresentationRenderer();

        CleanupZeroHpPhase2Fx();
        isPhaseTransitioning = true;
        _isZeroHpPhase2TransitionActive = true;
        SetZeroHpPhase2GroundClampActive(false);
        _debugLockPhase1UntilReleased = false;
        ClearPendingHitReaction();
        CancelAttackRuntimeState();
        HideCriticalPrompt();
        AbortCriticalStrikePresentation();
        ResetGroggyGauge();
        ResetPatternSelectionHistory();
        currentPoise = maxPoise;
        stamina = maxStamina;
        currentState = State.HitReact;
        currentLogicState = null;
        _stateTransitionLocked = true;
        StopAgent();
        bossHitbox?.SetHitBoxActive(false);
        ShowBossNameUI(false);
        zoneTrigger?.HideBossHud();
        SetAnimSpeed(1.0f);

        bool useReactionRootMotion = ShouldUseReactionRootMotion();
        SetRootMotionMode(useReactionRootMotion, useReactionRootMotion);

        if (EffectManager.Instance != null && zeroHpPhase2HitStopDuration > 0f)
        {
            EffectManager.Instance.DoHitStop(zeroHpPhase2HitStopDuration, Mathf.Clamp01(zeroHpPhase2HitStopTimeScale));
        }

        ApplyCameraShake(Mathf.Max(0.45f, criticalCameraShakeIntensity * 2f), 0.18f);

        string zeroHpHitAnimationStateName = ResolveConfiguredAnimationStateName(criticalHitReactionAnimation, GroggyAnimationStateName);
        yield return StartCoroutine(Co_PlayTransitionAnimationAndWait(
            zeroHpHitAnimationStateName,
            criticalReactionCrossFade,
            Mathf.Max(criticalReactionTimeout, 6f)));

        if (!string.IsNullOrWhiteSpace(criticalKnockDownLoopAnimation) &&
            anim != null &&
            anim.HasState(0, Animator.StringToHash(criticalKnockDownLoopAnimation)))
        {
            PlayAnim(criticalKnockDownLoopAnimation, criticalReactionCrossFade);
        }

        if (zeroHpPhase2PreCutsceneDelay > 0f)
        {
            yield return new WaitForSeconds(zeroHpPhase2PreCutsceneDelay);
        }

        zoneTrigger?.FadeBossBgmVolumeScale(zeroHpPhase2BgmDuckScale, zeroHpPhase2BgmDuckDuration);

        if (useEntranceCameraForZeroHpPhase2 && introCam != null)
        {
            ActivateEntranceCamera(zeroHpPhase2CameraPriority);
        }

        if (zeroHpPhase2DownLoopDuration > 0f)
        {
            float downElapsed = 0f;
            float pulseInterval = Mathf.Max(0.05f, zeroHpPhase2DownPulseInterval);
            float nextPulseAt = pulseInterval;

            while (downElapsed < zeroHpPhase2DownLoopDuration)
            {
                if (zeroHpPhase2DownPulseInterval > 0f && downElapsed >= nextPulseAt)
                {
                    PlayPhase2PreWakeCoreFx();
                    FlashBossPresentation(presentationRenderer, zeroHpPhase2DownPulseFlashColor, zeroHpPhase2DownPulseFlashDuration);
                    if (zeroHpPhase2DownPulseShakeIntensity > 0f)
                    {
                        ApplyCameraShake(zeroHpPhase2DownPulseShakeIntensity, 0.1f);
                    }

                    nextPulseAt += pulseInterval;
                }

                downElapsed += Time.deltaTime;
                yield return null;
            }
        }

        bool hasGetUpAnimation = anim != null &&
                                 !string.IsNullOrWhiteSpace(criticalGetUpAnimation) &&
                                 anim.HasState(0, Animator.StringToHash(criticalGetUpAnimation));

        if (hasGetUpAnimation)
        {
            StartPhase2WeaponIgniteFx();
            FlashBossPresentation(presentationRenderer, zeroHpPhase2DownPulseFlashColor, zeroHpPhase2DownPulseFlashDuration);
            SetZeroHpPhase2GroundClampActive(false);
            PlayAnim(criticalGetUpAnimation, criticalReactionCrossFade);
            yield return StartCoroutine(Co_WaitForAnimatorStateCompletion(
                criticalGetUpAnimation,
                Mathf.Max(criticalReactionTimeout, 6f),
                Mathf.Clamp01(zeroHpPhase2RefillNormalized)));
        }
        else
        {
            SetZeroHpPhase2GroundClampActive(false);
        }

        currentPhase = 2;
        ApplyEncounterHealthForPhase(currentPhase, true, true);
        ResetGroggyGauge();
        ResetPatternSelectionHistory();
        currentPoise = maxPoise;
        stamina = maxStamina;
        ShowBossNameUI(true);
        PlayPhase2GroundBurstFx();
        PlayPhase2BodyBurstFx();
        PlayPhase2AmbientMotesFx();
        FlashBossPresentation(
            presentationRenderer,
            zeroHpPhase2DownPulseFlashColor,
            Mathf.Max(zeroHpPhase2DownPulseFlashDuration, 0.12f));
        ApplyCameraShake(zeroHpPhase2WakeShakeIntensity, zeroHpPhase2WakeShakeDuration);
        zoneTrigger?.PlayPhase2BossBgm(zeroHpPhase2BgmRestoreDuration);

        if (hasGetUpAnimation)
        {
            yield return StartCoroutine(Co_WaitForAnimatorStateCompletion(
                criticalGetUpAnimation,
                Mathf.Max(criticalReactionTimeout, 6f),
                Mathf.Clamp01(Mathf.Max(zeroHpPhase2RecoverNormalized, zeroHpPhase2RefillNormalized))));
        }

        PlayPhase2ResidualAuraFx();
        StopPhase2WeaponIgniteFx();

        if (zeroHpPhase2PostRecoverDelay > 0f)
        {
            yield return new WaitForSeconds(zeroHpPhase2PostRecoverDelay);
        }

        yield return StartCoroutine(Co_WaitForZeroHpPhase2CameraReturn());

        CleanupZeroHpPhase2Fx();
        ShowBossNameUI(false);
        SetRootMotionMode(false, false);
        SetAnimSpeed(baseAttackSpeed);
        SetZeroHpPhase2GroundClampActive(false);
        _isZeroHpPhase2TransitionActive = false;
        isPhaseTransitioning = false;
        _stateTransitionLocked = false;

        BeginCombatResumeDelay(zeroHpPhase2CombatResumeDelay);
        ChangeState(idleState, State.Idle);
        zoneTrigger?.ShowBossHudDelayed(zeroHpPhase2HudShowDelay);
    }

    private IEnumerator TransitionToPhase2()
    {
        isPhaseTransitioning = true;
        _phase2TransitionPending = false;
        ClearPendingHitReaction();
        currentPhase = 2;
        CancelAttackRuntimeState();
        HideCriticalPrompt();
        AbortCriticalStrikePresentation();
        ResetGroggyGauge();
        ResetPatternSelectionHistory();
        currentPoise = maxPoise;
        stamina = maxStamina;
        StopAgent();
        SetAnimSpeed(1.0f);
        bool useReactionRootMotion = ShouldUseReactionRootMotion();
        SetRootMotionMode(useReactionRootMotion, useReactionRootMotion);
        PlayAnim(PhaseTransitionGroggyAnimationStateName, 0.1f);
        yield return StartCoroutine(Co_WaitForAnimatorStateCompletion(
            PhaseTransitionGroggyAnimationStateName,
            Mathf.Max(phaseTransitionGroggyDuration, 8f)));


        // 에러 해결을 위해 정의한 메서드 사용
        ApplyCameraShake(2.0f, 0.5f);

        SetRootMotionMode(false, false);
        isPhaseTransitioning = false;
        BeginCombatResumeDelay(zeroHpPhase2CombatResumeDelay);
        ChangeState(idleState, State.Idle);
    }

    private IEnumerator Co_PlayTransitionAnimationAndWait(string stateName, float crossFadeDuration, float timeout, float completionNormalized = 0.98f)
    {
        if (anim == null || string.IsNullOrWhiteSpace(stateName))
        {
            yield break;
        }

        int stateHash = Animator.StringToHash(stateName);
        if (!anim.HasState(0, stateHash))
        {
            Debug.LogWarning($"[Boss Phase Transition] Missing state '{stateName}'.");
            yield break;
        }

        StopAgent();
        SetAnimSpeed(1.0f);
        PlayAnim(stateName, crossFadeDuration);
        yield return StartCoroutine(Co_WaitForAnimatorStateCompletion(stateName, timeout, completionNormalized));
    }

    public void PlayAnim(string name, float dur = 0.1f)
    {
        if (anim == null) return;

        // 애니메이터에 해당 이름의 상태(State)가 있는지 확인 (Layer 0 기준)
        if (anim.HasState(0, Animator.StringToHash(name)))
        {
            anim.CrossFadeInFixedTime(name, dur);
        }
        else
        {
            // 어떤 애니메이션 이름이 잘못되었는지 콘솔창에 노란색으로 표시합니다.
            Debug.LogWarning($"[애니메이터 설정 확인 필요] '{name}'이라는 이름의 State가 애니메이터 컨트롤러에 없습니다.");

            // 에러로 멈추는 것을 방지하기 위해 기본 Idle이라도 재생
            anim.CrossFadeInFixedTime("Idle", dur);
        }
    }

    public bool UsesFullRootMotion => useFullRootMotion && anim != null && anim.applyRootMotion;
    public bool ShouldUseMoveRootMotion() => UsesFullRootMotion && useRootMotionForMoveState;
    public bool ShouldUseActionRootMotion() => UsesFullRootMotion && useRootMotionForActionState;
    public bool ShouldUseReactionRootMotion() => UsesFullRootMotion && useRootMotionForReactionStates;
    public bool ShouldUseIntroRootMotion() => UsesFullRootMotion;
    public float RootMotionSteeringTurnMultiplier => rootMotionSteeringTurnMultiplier;
    public string GroggyAnimationStateName => ResolveConfiguredAnimationStateName(groggyAnimation, "Groggy");
    public string PhaseTransitionGroggyAnimationStateName => ResolveConfiguredAnimationStateName(
        phaseTransitionGroggyAnimation,
        GroggyAnimationStateName);

    public void SetRootMotionMode(bool enableMovement, bool enableRotation)
    {
        if (!UsesFullRootMotion)
        {
            _rootMotionMovementEnabled = false;
            _rootMotionRotationEnabled = false;
            return;
        }

        _rootMotionMovementEnabled = enableMovement;
        _rootMotionRotationEnabled = enableRotation;
        SyncAgentToTransform();
    }

    public void RotateTowardsDirection(Vector3 worldDirection, float turnMultiplier = 1f)
    {
        worldDirection.y = 0f;
        if (worldDirection.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(worldDirection.normalized, Vector3.up);
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRotation,
            Time.deltaTime * rotationSpeed * Mathf.Max(0f, turnMultiplier));
    }

    private bool ShouldApplyVerticalRootMotion()
    {
        if (_isZeroHpPhase2GroundClampActive)
        {
            return false;
        }

        return currentState != State.Move && !isWaitingAfterAttack;
    }

    private bool ShouldApplyGroundSnap()
    {
        if (_isZeroHpPhase2GroundClampActive)
        {
            return true;
        }

        return currentState == State.Move;
    }

    private void ApplyGroundSnapIfNeeded()
    {
        if (!ShouldApplyGroundSnap())
        {
            return;
        }

        float extraSnapHeight = _isZeroHpPhase2GroundClampActive
            ? rootMotionGroundSnapMaxDistance
            : 0f;
        Vector3 rayOrigin = transform.position + Vector3.up * Mathf.Max(0.1f, rootMotionGroundSnapRayHeight + extraSnapHeight);
        float rayDistance = Mathf.Max(0.1f, rootMotionGroundSnapRayHeight + rootMotionGroundSnapMaxDistance + extraSnapHeight);
        RaycastHit[] hits = Physics.RaycastAll(
            rayOrigin,
            Vector3.down,
            rayDistance,
            rootMotionGroundSnapMask,
            QueryTriggerInteraction.Ignore);

        RaycastHit? nearestHit = null;
        float nearestDistance = float.MaxValue;

        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit hit = hits[i];
            if (hit.collider == null)
            {
                continue;
            }

            Transform hitRoot = hit.collider.transform.root;
            if (hitRoot == transform)
            {
                continue;
            }

            if (hit.distance < nearestDistance)
            {
                nearestDistance = hit.distance;
                nearestHit = hit;
            }
        }

        if (!nearestHit.HasValue)
        {
            return;
        }

        Vector3 snappedPosition = transform.position;
        snappedPosition.y = nearestHit.Value.point.y + rootMotionGroundSnapOffset;
        transform.position = snappedPosition;
    }

    private void OnAnimatorMove()
    {
        if (!UsesFullRootMotion || anim == null)
        {
            return;
        }

        if (_rootMotionMovementEnabled)
        {
            Vector3 deltaPosition = anim.deltaPosition;
            if (!ShouldApplyVerticalRootMotion())
            {
                deltaPosition.y = 0f;
            }

            transform.position += deltaPosition;
            ApplyGroundSnapIfNeeded();
        }

        if (_rootMotionRotationEnabled)
        {
            transform.rotation *= anim.deltaRotation;
        }

        SyncAgentToTransform();
    }

    private void SyncAgentToTransform()
    {
        if (!UsesFullRootMotion || agent == null || !agent.enabled || !agent.isOnNavMesh)
        {
            return;
        }

        agent.nextPosition = transform.position;
    }

    public bool IsCurrentAnimState(string name)
    {
        if (anim == null || string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        if (anim.IsInTransition(0))
        {
            return false;
        }

        return anim.GetCurrentAnimatorStateInfo(0).IsName(name);
    }

    private static string ResolveConfiguredAnimationStateName(string configuredName, string fallbackName)
    {
        return string.IsNullOrWhiteSpace(configuredName) ? fallbackName : configuredName;
    }

    public void SetAnimSpeed(float speed) { if (anim) anim.speed = speed; }
    public void StopAgent() { if (agent && agent.isOnNavMesh) { agent.isStopped = true; agent.velocity = Vector3.zero; } }
    public void ResumeAgent() { if (agent && agent.isOnNavMesh) agent.isStopped = false; }

    protected override void Die()
    {
        if (currentState == State.Dead) return;
        HideCriticalPrompt();
        AbortCriticalStrikePresentation();
        CancelAttackRuntimeState();
        currentState = State.Dead;
        base.Die();
        StopAllCoroutines();
        StartCoroutine(deadState.Enter());
    }

    public void AE_EnableHitbox() { bossHitbox?.SetHitBoxActive(true); }
    public void AE_DisableHitbox() { bossHitbox?.SetHitBoxActive(false); }
    public void AE_SwapWeapon() { if (backWeapon) backWeapon.SetActive(false); if (handWeapon) handWeapon.SetActive(true); }

    public CombatAttackDefinition ResolveAttackDefinition(string motionName)
    {
        LoadAttackLibraryIfNeeded();
        return attackLibrary != null ? attackLibrary.Resolve(motionName) : BossAttackLibrary.CreateFallback(motionName);
    }

    public void BeginAttackExecution(CombatAttackDefinition attack)
    {
        ClearPendingHitReaction();
        CurrentAttack = attack;
        isSuperArmor = false;
        _hasPlayedCurrentAttackSwingSfx = false;
        _hasPlayedCurrentAttackGroundImpactSfx = false;
        bossHitbox?.BeginAttack(this, attack);
        _attackTrajectory?.BeginAttack(attack);
    }

    public void UpdateAttackExecution(float normalizedTime)
    {
        isSuperArmor = CurrentAttack != null && CurrentAttack.IsInSuperArmorWindow(normalizedTime);
        bossHitbox?.UpdateAttackWindow(normalizedTime);
        TryPlayCurrentAttackSwingSfx(normalizedTime);
        TryPlayCurrentAttackGroundImpactSfx(normalizedTime);
        _attackTrajectory?.UpdateAttack(CurrentAttack, normalizedTime);
    }

    public void TickAttackExecution()
    {
        bossHitbox?.PerformHit();
    }

    public void EndAttackExecution()
    {
        _attackTrajectory?.EndAttack();
        bossHitbox?.EndAttack();
        isSuperArmor = false;
        _hasPlayedCurrentAttackSwingSfx = false;
        _hasPlayedCurrentAttackGroundImpactSfx = false;
        CurrentAttack = null;
    }

    private void ResolveBossSfxAudioSource()
    {
        if (bossSfxAudioSource == null)
        {
            bossSfxAudioSource = GetComponent<AudioSource>();
        }

        if (bossSfxAudioSource == null)
        {
            GameObject sfxRoot = new GameObject("BossSfxSource");
            sfxRoot.transform.SetParent(transform, false);
            bossSfxAudioSource = sfxRoot.AddComponent<AudioSource>();
        }

        bossSfxAudioSource.playOnAwake = false;
        bossSfxAudioSource.loop = false;
        bossSfxAudioSource.spatialBlend = 0f;
        AutoLoadBossSfxIfNeeded();
    }

    private void AutoLoadBossSfxIfNeeded()
    {
        if (swingSfxClips == null || swingSfxClips.Length == 0)
        {
            swingSfxClips = GameAudioResources.LoadClips(GameAudioCatalog.Boss.SwingPaths);
        }

        if (groundImpactSfxClips == null || groundImpactSfxClips.Length == 0)
        {
            groundImpactSfxClips = GameAudioResources.LoadClips(GameAudioCatalog.Boss.GroundImpactPaths);
        }

        if (incomingHitSfxClips == null || incomingHitSfxClips.Length == 0)
        {
            incomingHitSfxClips = GameAudioResources.LoadClips(GameAudioCatalog.Boss.IncomingHitPaths);
        }

        if (footstepSfxClips == null || footstepSfxClips.Length == 0)
        {
            footstepSfxClips = GameAudioResources.LoadClips(GameAudioCatalog.Boss.FootstepPaths);
        }

        if (parriedSfxClips == null || parriedSfxClips.Length == 0)
        {
            parriedSfxClips = GameAudioResources.LoadClips(GameAudioCatalog.Boss.ParriedPaths);
        }
    }

    private void PlayBossSfx(AudioClip clip, float volume)
    {
        if (clip == null || bossSfxAudioSource == null)
        {
            return;
        }

        bossSfxAudioSource.pitch = 1f;
        bossSfxAudioSource.PlayOneShot(clip, volume);
    }

    private void PlayRandomBossSfx(AudioClip[] clips, float volume)
    {
        PlayBossSfx(GetRandomClip(clips), volume);
    }

    public void PlayFootstepSfx()
    {
        if (Time.time < _nextBossFootstepPlayableTime)
        {
            return;
        }

        _nextBossFootstepPlayableTime = Time.time + Mathf.Max(0.01f, footstepSfxMinInterval);
        PlayRandomBossSfx(footstepSfxClips, footstepSfxVolume);
    }

    public void FootL()
    {
        PlayFootstepSfx();
    }

    public void FootR()
    {
        PlayFootstepSfx();
    }

    public void OnFootstep()
    {
        PlayFootstepSfx();
    }

    private void TryPlayCurrentAttackSwingSfx(float normalizedTime)
    {
        if (_hasPlayedCurrentAttackSwingSfx || CurrentAttack == null)
        {
            return;
        }

        if (normalizedTime < ResolveAttackSwingStartNormalized(CurrentAttack))
        {
            return;
        }

        _hasPlayedCurrentAttackSwingSfx = true;
        PlayRandomBossSfx(swingSfxClips, swingSfxVolume);
    }

    private void TryPlayCurrentAttackGroundImpactSfx(float normalizedTime)
    {
        if (_hasPlayedCurrentAttackGroundImpactSfx || CurrentAttack == null)
        {
            return;
        }

        if (!ShouldPlayGroundImpactSfx(CurrentAttack))
        {
            return;
        }

        if (normalizedTime < ResolveGroundImpactNormalized(CurrentAttack))
        {
            return;
        }

        _hasPlayedCurrentAttackGroundImpactSfx = true;
        PlayRandomBossSfx(groundImpactSfxClips, groundImpactSfxVolume);
    }

    private static float ResolveAttackSwingStartNormalized(CombatAttackDefinition attack)
    {
        if (attack == null)
        {
            return 0f;
        }

        if (attack.activeStartNormalized > 0f)
        {
            return Mathf.Clamp01(attack.activeStartNormalized);
        }

        if (attack.impactNormalized > 0f)
        {
            return Mathf.Clamp01(attack.impactNormalized);
        }

        return 0f;
    }

    private static float ResolveGroundImpactNormalized(CombatAttackDefinition attack)
    {
        if (attack == null)
        {
            return 0f;
        }

        if (attack.impactNormalized > 0f)
        {
            return Mathf.Clamp01(attack.impactNormalized);
        }

        if (attack.activeEndNormalized > 0f)
        {
            return Mathf.Clamp01(attack.activeEndNormalized);
        }

        return ResolveAttackSwingStartNormalized(attack);
    }

    private static bool ShouldPlayGroundImpactSfx(CombatAttackDefinition attack)
    {
        if (attack == null)
        {
            return false;
        }

        string attackKey = !string.IsNullOrWhiteSpace(attack.attackId) ? attack.attackId : attack.attackName;
        return attackKey == "Skill_H" || attackKey == "Skill_J" || attackKey == "Skill_G";
    }

    private static AudioClip GetRandomClip(AudioClip[] clips)
    {
        if (clips == null || clips.Length == 0)
        {
            return null;
        }

        int validCount = 0;
        for (int i = 0; i < clips.Length; i++)
        {
            if (clips[i] != null)
            {
                validCount++;
            }
        }

        if (validCount == 0)
        {
            return null;
        }

        int selectedIndex = Random.Range(0, validCount);
        for (int i = 0; i < clips.Length; i++)
        {
            if (clips[i] == null)
            {
                continue;
            }

            if (selectedIndex == 0)
            {
                return clips[i];
            }

            selectedIndex--;
        }

        return null;
    }

    public void CancelAttackRuntimeState()
    {
        EndAttackExecution();
        attackQueue.Clear();
        isWaitingAfterAttack = false;
    }

    public void ResetGroggyRuntime()
    {
        ClearPendingHitReaction();
        ResetGroggyGauge();
        currentPoise = maxPoise;
        _criticalStrikeConsumedThisGroggy = false;
        CancelAttackRuntimeState();
    }

    public void BeginGroggyRuntime()
    {
        ClearPendingHitReaction();
        CancelAttackRuntimeState();
        StopAgent();
        SetAnimSpeed(1.0f);
        HideCriticalPrompt();
        AbortCriticalStrikePresentation();
        _criticalStrikeConsumedThisGroggy = false;
    }

    public void EndGroggyRuntime()
    {
        ResetGroggyGauge();
        currentPoise = maxPoise;
        _criticalStrikeConsumedThisGroggy = false;
        CancelAttackRuntimeState();
        ClearPendingHitReaction();
    }

    public bool TryConsumePendingPhase2Transition()
    {
        if (useZeroHpPhase2Transition)
        {
            _phase2TransitionPending = false;
            return false;
        }

        if (!_phase2TransitionPending)
        {
            return false;
        }

        if (IsDead || isPhaseTransitioning || currentPhase != 1)
        {
            return false;
        }

        if ((hp / MaxHp) > phase2Threshold)
        {
            _phase2TransitionPending = false;
            return false;
        }

        BeginPhase2Transition();
        return true;
    }

    public void BeginHitReactionRuntime()
    {
        CancelAttackRuntimeState();
        StopAgent();
        SetAnimSpeed(1.0f);
        HideCriticalPrompt();
    }

    public void EndHitReactionRuntime()
    {
        ClearPendingHitReaction();
    }

    public bool IsCriticalStrikeExecuting => _criticalStrikeRoutine != null;
    public bool HasConsumedCriticalStrikeThisGroggy => _criticalStrikeConsumedThisGroggy;
    public bool HasPendingPhase2Transition => _phase2TransitionPending;
    public float CurrentHitReactionDuration =>
        _pendingHitReactionDuration > 0f ? _pendingHitReactionDuration : Mathf.Max(0.05f, lightHitReactDuration);
    public string CurrentHitReactionAnimationName =>
        string.IsNullOrWhiteSpace(_pendingHitReactionAnimationName) ? lightHitReactAnimation : _pendingHitReactionAnimationName;

    public void DebugEnsureCombatContext()
    {
        DebugEnterCombatContext();
    }

    public void DebugForcePhaseState(int phase)
    {
        DebugForcePhase(phase);
    }

    public void DebugForceGroggyState()
    {
        DebugForceGroggy();
    }

    public void DebugForceAttackPattern(string attackId, int minimumPhase = 0)
    {
        DebugForceAttack(attackId, minimumPhase);
    }

    public void DebugForceAttackSequencePattern(string[] attackIds, int minimumPhase = 0)
    {
        DebugForceAttackSequence(attackIds, minimumPhase);
    }

    public void DebugSetHpRatio(float ratio)
    {
        if (MaxHp <= 0f || IsDead)
        {
            return;
        }

        float clampedRatio = Mathf.Clamp01(ratio);
        hp = Mathf.Clamp(MaxHp * clampedRatio, 1f, MaxHp);
        CallHpChanged();
    }

    public bool TryConsumeStamina(float amount)
    {
        if (amount <= 0f)
        {
            return true;
        }

        if (stamina < amount)
        {
            return false;
        }

        stamina = Mathf.Max(0f, stamina - amount);
        return true;
    }

    public float ResolveAttackStaminaCost(CombatAttackDefinition attack)
    {
        if (attack != null && attack.staminaCost > 0f)
        {
            return attack.staminaCost;
        }

        return Mathf.Max(0f, attackCost);
    }

    public float ResolveCombatActionEntryDistance()
    {
        float closeRangeThreshold = attackStoppingDistance + 0.2f;

        if (usePhase1ValidationPatternSet && currentPhase == 1)
        {
            return Mathf.Max(closeRangeThreshold, phase1ApproachDistanceThreshold + 0.2f);
        }

        if (usePhase2ValidationPatternSet && currentPhase == 2)
        {
            float actionEntryDistance = Mathf.Max(closeRangeThreshold, phase2ApproachDistanceThreshold + 0.2f);
            if (ShouldPunishHealing())
            {
                actionEntryDistance = Mathf.Max(actionEntryDistance, phase2HealPunishDistance);
            }

            return actionEntryDistance;
        }

        return attackStoppingDistance + 0.2f;
    }

    public float ResolveCombatActionAbortDistance()
    {
        float baseAbortDistance = attackStoppingDistance + 2.0f;
        return Mathf.Max(baseAbortDistance, ResolveCombatActionEntryDistance() + 0.25f);
    }

    public bool CanStartCombatAction(float distanceToTarget)
    {
        if (IsCombatResumeDelayActive)
        {
            return false;
        }

        if (usePhase1ValidationPatternSet && currentPhase == 1)
        {
            float approachCost = ResolveAttackStaminaCost(ResolveAttackDefinition(phase1ApproachPattern));
            float quickCost = ResolveAttackStaminaCost(ResolveAttackDefinition(phase1QuickPattern));
            float heavyCost = ResolveAttackStaminaCost(ResolveAttackDefinition(phase1HeavyPattern));
            float closeRangeThreshold = attackStoppingDistance + 0.2f;
            float actionEntryDistance = ResolveCombatActionEntryDistance();

            if (distanceToTarget > actionEntryDistance)
            {
                return false;
            }

            if (distanceToTarget > closeRangeThreshold)
            {
                return stamina >= approachCost;
            }

            float minimumCost = Mathf.Min(quickCost, heavyCost);
            return stamina >= minimumCost;
        }

        if (usePhase2ValidationPatternSet && currentPhase == 2)
        {
            float approachCost = ResolveAttackStaminaCost(ResolveAttackDefinition(phase2ApproachPattern));
            float quickCost = ResolveAttackStaminaCost(ResolveAttackDefinition(phase2QuickPattern));
            float heavyCost = ResolveAttackStaminaCost(ResolveAttackDefinition(phase2HeavyPattern));
            float closeRangeThreshold = attackStoppingDistance + 0.2f;
            float actionEntryDistance = ResolveCombatActionEntryDistance();

            if (distanceToTarget > actionEntryDistance)
            {
                return false;
            }

            if (distanceToTarget > closeRangeThreshold)
            {
                return stamina >= approachCost;
            }

            float minimumCost = Mathf.Min(quickCost, heavyCost);
            return stamina >= minimumCost;
        }

        if (distanceToTarget > attackStoppingDistance + 0.2f)
        {
            return false;
        }

        return stamina >= attackCost;
    }

    private void PrepareAttackSequence()
    {
        attackQueue.Clear();
        if (target == null) return;

        if (usePhase1ValidationPatternSet && currentPhase == 1)
        {
            PreparePhase1ValidationSequence();
            return;
        }

        if (usePhase2ValidationPatternSet && currentPhase == 2)
        {
            PreparePhase2ValidationSequence();
            return;
        }

        float d = Vector3.Distance(transform.position, target.position);
        float rnd = Random.value;

        // --- [핵심 추가] 스킬 사용 가능 여부 판단 ---
        // 쿨타임이 지났고, 설정된 확률(skillChance)을 통과했는가?
        bool canUseSkill = (Time.time >= lastSkillTime + skillCooldown) && (Random.Range(0, 100) < skillChance);

        // 1. 원거리 상황: 거리 좁히기
        if (d > 7.0f)
        {
            // 스킬 사용 가능할 때만 특수 진입(돌진/점프) 사용
            if (canUseSkill)
            {
                if (rnd < 0.5f)
                {
                    TryEnqueuePattern("Skill_H"); // 돌진
                    TryEnqueuePattern("Skill_A");
                }
                else
                {
                    TryEnqueuePattern("Skill_J"); // 점프 공격
                    TryEnqueuePattern("Skill_K");
                }
                lastSkillTime = Time.time; // 스킬 사용 시점 기록
            }
            else
            {
                // 스킬 불가능하면 대시로 접근 후 기본 콤보
                TryEnqueuePattern("Dash");
                EnqueueCombo("Attack_4Combo", 4);
            }
        }
        // 2. 근거리 상황
        else
        {
            if (stamina >= 30f)
            {
                // 스킬 사용 가능 조건이면 스킬 위주 패턴 (case 2, 3, 5, 7 등)
                if (canUseSkill)
                {
                    int p = Random.Range(0, 4);
                    switch (p)
                    {
                        case 0: // 스킬 난사
                            TryEnqueuePattern("Skill_B");
                            TryEnqueuePattern("Skill_D");
                            TryEnqueuePattern("Skill_K");
                            break;
                        case 1: // 견제기 연계
                            TryEnqueuePattern("Attack_3Combo_1");
                            TryEnqueuePattern("Skill_E");
                            break;
                        case 2: // 찌르기 위주
                            TryEnqueuePattern("Skill_B");
                            TryEnqueuePattern("Skill_C");
                            TryEnqueuePattern("Skill_A");
                            break;
                        case 3: // 돌진 급발진
                            TryEnqueuePattern("Skill_H");
                            TryEnqueuePattern("Skill_K");
                            break;
                    }
                    lastSkillTime = Time.time; // 스킬 사용 시점 기록
                    Debug.Log("<color=cyan>[AI] 특수 스킬 패턴 발동!</color>");
                }
                else
                {
                    // 스킬 불가 또는 확률 미달 시 무조건 기본 콤보 위주 (case 0, 1, 4, 6 등)
                    int p = Random.Range(0, 4);
                    switch (p)
                    {
                        case 0: EnqueueCombo("Attack_4Combo", 4); break;
                        case 1: // 콤보 연계
                            TryEnqueuePattern("Attack_3Combo_1");
                            TryEnqueuePattern("Attack_4Combo_2");
                            TryEnqueuePattern("Attack_4Combo_3");
                            break;
                        case 2: // 3단 콤보
                            EnqueueCombo("Attack_3Combo", 3);
                            break;
                        case 3: // 섞인 콤보
                            TryEnqueuePattern("Attack_4Combo_1");
                            TryEnqueuePattern("Attack_3Combo_2");
                            break;
                    }
                }
            }
            else // 스태미나 부족 시
            {
                TryEnqueuePattern("Attack_3Combo_1");
            }
        }

        // 3. 궁극기(Skill_G) 처리 - 이것도 스킬이므로 쿨타임/확률 체크 영향 하에 둡니다.
        if (canUseSkill) // 이미 위에서 스킬을 썼다면 lastSkillTime이 갱신되어 여기서 중복 체크될 수 있으니 주의
        {
            float ultChance = (currentPhase == 2) ? 0.25f : 0.12f;
            if (Random.value < ultChance && stamina > 20f)
            {
                TryEnqueuePattern("Skill_G");
                lastSkillTime = Time.time;
            }
        }
    }

    private void EnqueueCombo(string baseName, int count)
    {
        for (int i = 1; i <= count; i++)
        {
            TryEnqueuePattern($"{baseName}_{i}");
        }
    }

    private void PreparePhase1ValidationSequence()
    {
        if (!TryPrepareLibraryDrivenSequence(1))
        {
            Debug.Log("[Boss AI] Phase1 library-driven pattern skipped: recovering stamina.");
        }
    }

    private void PreparePhase2ValidationSequence()
    {
        if (!TryPrepareLibraryDrivenSequence(2))
        {
            Debug.Log("[Boss AI] Phase2 library-driven pattern skipped: recovering stamina.");
        }
    }

    public int GetAttackQueueCount() => attackQueue.Count;
    public string GetNextAttackInQueue() => (attackQueue.Count > 0) ? attackQueue.Dequeue() : null;
    public string PeekNextAttackInQueue() => attackQueue.Count > 0 ? attackQueue.Peek() : null;
    public void RecordExecutedPattern(string patternId)
    {
        if (string.IsNullOrWhiteSpace(patternId))
        {
            return;
        }

        _lastExecutedPatternId = patternId;
    }

    private void ResolveCombatReferences()
    {
        if (bossHitbox == null)
        {
            bossHitbox = GetComponentInChildren<BossHitbox>(true);
        }
    }

    private void LoadAttackLibraryIfNeeded()
    {
        if (attackLibrary == null)
        {
            attackLibrary = Resources.Load<BossAttackLibrary>("Combat/BossAttackLibrary");
        }
    }

        private IEnumerator Co_ReleaseStateTransitionLock()
    {
        yield return null;
        _stateTransitionLocked = false;
    }

    private bool ShouldHoldPhase1ForValidation()
    {
        return usePhase1ValidationPatternSet &&
               disablePhase2DuringPhase1Validation &&
               !usePhase2ValidationPatternSet;
    }

    private bool ShouldDelayPhase2Transition()
    {
        return currentState == State.Groggy || IsCriticalStrikeExecuting;
    }

    private void BeginPhase2Transition()
    {
        if (IsDead || isPhaseTransitioning || currentPhase != 1)
        {
            _phase2TransitionPending = false;
            return;
        }

        _phase2TransitionPending = false;
        StopAllCoroutines();
        StartCoroutine(TransitionToPhase2());
    }

    private float ResolvePostAttackTempoMultiplier()
    {
        if (currentPhase == 2 && usePhase2ValidationPatternSet)
        {
            return Mathf.Max(0.1f, phase2PostAttackTempoMultiplier);
        }

        return 1f;
    }

    private bool ShouldPunishHealing()
    {
        PlayerController player = ResolveTargetPlayer();
        return player != null && player.IsUsingItem;
    }

    private bool ShouldAvoidImmediateRepeat(string patternId)
    {
        return IsImmediateRepeat(patternId);
    }

    private bool TryPrepareLibraryDrivenSequence(int phase)
    {
        if (target == null)
        {
            return false;
        }

        List<string[]> patternOptions = BuildLibraryPatternOptions(phase);
        if (patternOptions.Count == 0)
        {
            return false;
        }

        float distance = Vector3.Distance(transform.position, target.position);
        bool preferHeavyPattern = phase >= 2 && Random.value < 0.4f;

        if (phase >= 2 && ShouldPunishHealing())
        {
            if (TryQueueRandomPatternOption(patternOptions, IsGapCloseOrLongRangeSequence, distance, "Phase2 heal punish"))
            {
                return true;
            }
        }

        float approachThreshold = phase >= 2 ? phase2ApproachDistanceThreshold : phase1ApproachDistanceThreshold;
        if (distance > approachThreshold)
        {
            if (TryQueueRandomPatternOption(patternOptions, IsGapCloserSequence, distance, $"Phase{phase} gap close"))
            {
                return true;
            }
        }

        if (preferHeavyPattern &&
            TryQueueRandomPatternOption(patternOptions, IsHeavySequence, distance, $"Phase{phase} heavy"))
        {
            return true;
        }

        if (Random.value < 0.35f &&
            TryQueueRandomPatternOption(patternOptions, IsComboSequence, distance, $"Phase{phase} combo"))
        {
            return true;
        }

        if (TryQueueRandomPatternOption(patternOptions, IsGeneralCombatSequence, distance, $"Phase{phase} general"))
        {
            return true;
        }

        return TryQueueRandomPatternOption(patternOptions, _ => true, distance, $"Phase{phase} fallback");
    }

    private List<string[]> BuildLibraryPatternOptions(int phase)
    {
        List<string[]> options = new List<string[]>();
        List<string> attackIds = GetRegisteredBossAttackIds();
        if (attackIds.Count == 0)
        {
            return options;
        }

        bool hasThreeCombo = attackIds.Contains("Attack_3Combo_1") &&
                             attackIds.Contains("Attack_3Combo_2") &&
                             attackIds.Contains("Attack_3Combo_3");
        if (hasThreeCombo)
        {
            options.Add(new[] { "Attack_3Combo_1", "Attack_3Combo_2", "Attack_3Combo_3" });
        }

        bool hasFourCombo = attackIds.Contains("Attack_4Combo_1") &&
                            attackIds.Contains("Attack_4Combo_2") &&
                            attackIds.Contains("Attack_4Combo_3") &&
                            attackIds.Contains("Attack_4Combo_4");
        if (hasFourCombo)
        {
            options.Add(new[] { "Attack_4Combo_1", "Attack_4Combo_2", "Attack_4Combo_3", "Attack_4Combo_4" });
        }

        foreach (string attackId in attackIds)
        {
            if (string.IsNullOrWhiteSpace(attackId))
            {
                continue;
            }

            if (phase <= 1 && string.Equals(attackId, "Skill_G", System.StringComparison.Ordinal))
            {
                continue;
            }

            bool isThreeComboPart = attackId.StartsWith("Attack_3Combo_", System.StringComparison.Ordinal);
            bool isFourComboPart = attackId.StartsWith("Attack_4Combo_", System.StringComparison.Ordinal);
            if ((isThreeComboPart && hasThreeCombo) || (isFourComboPart && hasFourCombo))
            {
                continue;
            }

            options.Add(new[] { attackId });
        }

        return options;
    }

    private List<string> GetRegisteredBossAttackIds()
    {
        LoadAttackLibraryIfNeeded();
        return attackLibrary != null ? attackLibrary.GetRegisteredAttackIds() : new List<string>();
    }

    private bool TryQueueRandomPatternOption(List<string[]> patternOptions, System.Predicate<string[]> predicate, float distanceToTarget, string reason)
    {
        List<string[]> candidates = new List<string[]>();
        foreach (string[] patternOption in patternOptions)
        {
            if (patternOption == null || patternOption.Length == 0)
            {
                continue;
            }

            string firstPatternId = patternOption[0];
            if (string.IsNullOrWhiteSpace(firstPatternId) || ShouldAvoidImmediateRepeat(firstPatternId))
            {
                continue;
            }

            CombatAttackDefinition firstAttack = ResolveAttackDefinition(firstPatternId);
            if (firstAttack == null || stamina < ResolveAttackStaminaCost(firstAttack))
            {
                continue;
            }

            if (predicate != null && !predicate(patternOption))
            {
                continue;
            }

            if (!IsSequenceAllowedAtDistance(patternOption, distanceToTarget))
            {
                continue;
            }

            candidates.Add(patternOption);
        }

        if (candidates.Count <= 0)
        {
            return false;
        }

        string[] selectedOption = candidates[Random.Range(0, candidates.Count)];
        return QueueAttackSequence(selectedOption, reason);
    }

    private bool QueueAttackSequence(string[] patternIds, string reason)
    {
        if (patternIds == null || patternIds.Length == 0)
        {
            return false;
        }

        string primaryPatternId = patternIds[0];
        if (string.IsNullOrWhiteSpace(primaryPatternId) || ShouldAvoidImmediateRepeat(primaryPatternId))
        {
            return false;
        }

        int enqueuedCount = 0;
        foreach (string patternId in patternIds)
        {
            if (string.IsNullOrWhiteSpace(patternId))
            {
                continue;
            }

            attackQueue.Enqueue(patternId);
            enqueuedCount++;
        }

        if (enqueuedCount <= 0)
        {
            return false;
        }

        RecordSelectedPattern(primaryPatternId);
        Debug.Log($"[Boss AI] {reason}: {string.Join(" -> ", patternIds)}");
        return true;
    }

    private bool IsSequenceAllowedAtDistance(string[] patternIds, float distanceToTarget)
    {
        float closeRangeThreshold = attackStoppingDistance + 0.2f;
        if (distanceToTarget <= closeRangeThreshold)
        {
            return true;
        }

        if (distanceToTarget > ResolveCombatActionEntryDistance())
        {
            return false;
        }

        return IsGapCloseOrLongRangeSequence(patternIds);
    }

    private bool IsComboSequence(string[] patternIds)
    {
        string primaryPatternId = GetPrimaryPatternId(patternIds);
        return !string.IsNullOrWhiteSpace(primaryPatternId) &&
               primaryPatternId.StartsWith("Attack_", System.StringComparison.Ordinal);
    }

    private bool IsGapCloserSequence(string[] patternIds)
    {
        string primaryPatternId = GetPrimaryPatternId(patternIds);
        if (string.IsNullOrWhiteSpace(primaryPatternId))
        {
            return false;
        }

        if (primaryPatternId == "Dash" ||
            primaryPatternId == "Skill_H" ||
            primaryPatternId == "Skill_J")
        {
            return true;
        }

        CombatAttackDefinition attack = ResolveAttackDefinition(primaryPatternId);
        return attack != null && attack.dashDistance >= 2.4f;
    }

    private bool IsGapCloseOrLongRangeSequence(string[] patternIds)
    {
        if (IsGapCloserSequence(patternIds))
        {
            return true;
        }

        string primaryPatternId = GetPrimaryPatternId(patternIds);
        CombatAttackDefinition attack = ResolveAttackDefinition(primaryPatternId);
        return attack != null && attack.dashDistance >= 1.0f;
    }

    private bool IsHeavySequence(string[] patternIds)
    {
        string primaryPatternId = GetPrimaryPatternId(patternIds);
        if (string.IsNullOrWhiteSpace(primaryPatternId))
        {
            return false;
        }

        if (primaryPatternId == "Skill_G")
        {
            return true;
        }

        CombatAttackDefinition attack = ResolveAttackDefinition(primaryPatternId);
        return attack != null && attack.isHeavyAttack;
    }

    private bool IsGeneralCombatSequence(string[] patternIds)
    {
        return !IsGapCloserSequence(patternIds);
    }

    private string GetPrimaryPatternId(string[] patternIds)
    {
        if (patternIds == null || patternIds.Length == 0)
        {
            return null;
        }

        return patternIds[0];
    }

    private void QueueSelectedPattern(string patternId, string reason)
    {
        if (string.IsNullOrWhiteSpace(patternId))
        {
            return;
        }

        if (!TryEnqueuePattern(patternId))
        {
            Debug.Log($"[Boss AI] skipped repeated pattern: {patternId}");
            return;
        }

        RecordSelectedPattern(patternId);
        Debug.Log($"[Boss AI] {reason}: {patternId}");
    }

    private void RecordSelectedPattern(string patternId)
    {
        if (string.IsNullOrWhiteSpace(patternId))
        {
            return;
        }

        if (string.Equals(_lastSelectedPatternId, patternId, System.StringComparison.Ordinal))
        {
            _samePatternRepeatCount++;
            return;
        }

        _lastSelectedPatternId = patternId;
        _samePatternRepeatCount = 1;
    }

    private void ResetPatternSelectionHistory()
    {
        _lastSelectedPatternId = null;
        _lastExecutedPatternId = null;
        _samePatternRepeatCount = 0;
    }

    private bool TryEnqueuePattern(string patternId)
    {
        if (string.IsNullOrWhiteSpace(patternId))
        {
            return false;
        }

        if (IsImmediateRepeat(patternId))
        {
            return false;
        }

        attackQueue.Enqueue(patternId);
        return true;
    }

    private bool IsImmediateRepeat(string patternId)
    {
        if (string.IsNullOrWhiteSpace(patternId))
        {
            return false;
        }

        string comparisonTarget = GetLastQueuedPatternId();
        if (string.IsNullOrWhiteSpace(comparisonTarget))
        {
            comparisonTarget = _lastExecutedPatternId;
        }

        return !string.IsNullOrWhiteSpace(comparisonTarget) &&
               string.Equals(comparisonTarget, patternId, System.StringComparison.Ordinal);
    }

    private string GetLastQueuedPatternId()
    {
        if (attackQueue.Count <= 0)
        {
            return null;
        }

        string[] queuedPatterns = attackQueue.ToArray();
        return queuedPatterns[queuedPatterns.Length - 1];
    }

    protected override void ApplyIncomingCombatPressure(int amount, CombatAttackDefinition incomingAttack)
    {
        base.ApplyIncomingCombatPressure(amount, incomingAttack);

        if (maxPoise <= 0f || currentState == State.Groggy || currentState == State.Dead)
        {
            return;
        }

        float poiseDamage = incomingAttack != null && incomingAttack.poiseDamage > 0f
            ? incomingAttack.poiseDamage
            : Mathf.Max(4f, amount * 0.15f);

        currentPoise = Mathf.Max(0f, currentPoise - poiseDamage);
        if (currentPoise <= 0f)
        {
            ApplyPoiseBreakGroggyBonus(amount, incomingAttack);

            if (!isSuperArmor && CanEnterHitReactionState())
            {
                QueueHitReaction(incomingAttack, poiseDamage);
            }

            currentPoise = Mathf.Clamp(maxPoise * poiseResetRatioAfterBreak, 0f, maxPoise);
        }
    }

    private void ApplyPoiseBreakGroggyBonus(int amount, CombatAttackDefinition incomingAttack)
    {
        float groggyBonus = poiseBreakGroggyBonus;

        if (incomingAttack != null && incomingAttack.groggyDamage > 0f)
        {
            groggyBonus = Mathf.Max(groggyBonus, incomingAttack.groggyDamage * poiseBreakGroggyBonusMultiplier);
        }
        else
        {
            groggyBonus = Mathf.Max(groggyBonus, amount * poiseBreakGroggyBonusMultiplier);
        }

        currentGroggyGauge = Mathf.Min(maxGroggyGauge, currentGroggyGauge + groggyBonus);
    }

    private bool CanEnterHitReactionState()
    {
        return currentState != State.Dead &&
               currentState != State.Groggy &&
               currentState != State.HitReact &&
               !isPhaseTransitioning;
    }

    private void QueueHitReaction(CombatAttackDefinition incomingAttack, float poiseDamage)
    {
        bool isHeavyReaction = (incomingAttack != null && incomingAttack.isHeavyAttack) ||
                               poiseDamage >= Mathf.Max(18f, maxPoise * 0.3f);

        float duration = isHeavyReaction ? heavyHitReactDuration : lightHitReactDuration;
        string animationName = isHeavyReaction ? heavyHitReactAnimation : lightHitReactAnimation;

        _hasPendingHitReaction = true;
        _pendingHitReactionDuration = Mathf.Max(_pendingHitReactionDuration, duration);
        _pendingHitReactionAnimationName = animationName;
    }

    private void QueueParryHitReaction(CombatAttackDefinition incomingAttack)
    {
        string animationName = ResolveParryHitReactionAnimationName();
        if (string.IsNullOrWhiteSpace(animationName))
        {
            QueueHitReaction(incomingAttack, Mathf.Max(24f, maxPoise));
            return;
        }

        _hasPendingHitReaction = true;
        _pendingHitReactionDuration = Mathf.Max(_pendingHitReactionDuration, Mathf.Max(0.05f, parryHitReactDuration));
        _pendingHitReactionAnimationName = animationName;
    }

    private string ResolveParryHitReactionAnimationName()
    {
        if (anim != null && !string.IsNullOrWhiteSpace(parryHitReactAnimation))
        {
            int stateHash = Animator.StringToHash(parryHitReactAnimation);
            if (anim.HasState(0, stateHash))
            {
                return parryHitReactAnimation;
            }
        }

        return null;
    }

    private void ClearPendingHitReaction()
    {
        _hasPendingHitReaction = false;
        _pendingHitReactionDuration = 0f;
        _pendingHitReactionAnimationName = null;
    }

    private void ForceImmediateStateTransition(BossState newState, State enumState)
    {
        _stateTransitionLocked = false;
        currentLogicState = null;
        TryChangeState(newState, enumState);
    }

    private void RefreshCriticalStrikePrompt()
    {
        if (currentState != State.Groggy || IsCriticalStrikeExecuting || IsDead)
        {
            HideCriticalPrompt();
            return;
        }

        PlayerController player = ResolveTargetPlayer();
        if (!CanOfferCriticalStrike(player))
        {
            HideCriticalPrompt();
            return;
        }

        if (!_isCriticalPromptVisible)
        {
            UIInteractionPrompt.Show(this, player.InteractionKey, criticalPromptLabel);
            _isCriticalPromptVisible = true;
        }

        if (Input.GetKeyDown(player.InteractionKey))
        {
            StartCriticalStrike(player);
        }
    }

    private void StartCriticalStrike(PlayerController player)
    {
        if (!CanOfferCriticalStrike(player) || _criticalStrikeRoutine != null)
        {
            return;
        }

        _criticalStrikeConsumedThisGroggy = true;
        HideCriticalPrompt();
        _criticalStrikeRoutine = StartCoroutine(Co_CriticalStrike(player));
    }

    private IEnumerator Co_CriticalStrike(PlayerController player)
    {
        if (player == null)
        {
            _criticalStrikeRoutine = null;
            yield break;
        }

        string playedCriticalAttackStateName = BeginCriticalPresentation(player);
        bool hasAppliedCriticalDamage = false;
        float presentationSpeed = ResolveCriticalPresentationAnimSpeed();
        float hitReactionDelay = ResolveCriticalHitReactionDelay(presentationSpeed);
        float elapsed = 0f;

        if (hitReactionDelay > 0f)
        {
            yield return new WaitForSeconds(hitReactionDelay);
            elapsed += hitReactionDelay;
        }

        if (!IsDead && currentState == State.Groggy)
        {
            PlayCriticalHitReactionPresentation();
            StartCriticalReactionSequence();
        }

        float remainingImpactDelay = Mathf.Max(0f, (criticalImpactDelay / presentationSpeed) - elapsed);
        if (remainingImpactDelay > 0f)
        {
            yield return new WaitForSeconds(remainingImpactDelay);
        }

        if (!IsDead && currentState == State.Groggy)
        {
            ApplyCriticalStrikeDamage(player);
            hasAppliedCriticalDamage = true;
        }

        float minimumPlayerReleaseAt = Time.time + (criticalRecoveryDelay / presentationSpeed);
        yield return Co_WaitForPlayerCriticalAnimationCompletion(player, playedCriticalAttackStateName, minimumPlayerReleaseAt);

        EndCriticalPresentation(player);

        if (hasAppliedCriticalDamage && _criticalReactionRoutine != null)
        {
            yield return _criticalReactionRoutine;
        }
        else
        {
            SetAnimSpeed(1.0f);
        }

        _criticalStrikeRoutine = null;
    }

    private string BeginCriticalPresentation(PlayerController player)
    {
        _criticalStrikePlayer = player;
        player.BeginExternalInteractionLock();
        player.ForceBattleStance();
        player.SetGuarding(false);
        player.SetVelocity(Vector3.zero);

        StopAgent();
        Vector3 playerForward = player.transform.forward;
        playerForward.y = 0f;
        if (playerForward.sqrMagnitude <= 0.0001f)
        {
            playerForward = transform.position - player.transform.position;
            playerForward.y = 0f;
        }

        if (playerForward.sqrMagnitude <= 0.0001f)
        {
            playerForward = -transform.forward;
            playerForward.y = 0f;
        }

        playerForward.Normalize();

        Vector3 targetBossPosition = ResolveCriticalPresentationBossPosition(player, playerForward);
        transform.position = targetBossPosition;
        SyncAgentToTransform();

        RotateBossTowardPlayer(player.transform);
        player.SyncCameraTargetsImmediate();
        ApplyCriticalPresentationPlaybackSpeed(player);
        PlayAnim(GroggyAnimationStateName, 0.05f);

        if (player.AnimatorDriver != null)
        {
            string criticalAttackStateName = ResolveCriticalAttackAnimatorStateName(player);
            if (player.AnimatorDriver.TryCrossFadeToState(criticalAttackStateName, 0.05f, 0, 0f))
            {
                player.SetAttackRootMotionEnabled(string.Equals(
                    criticalAttackStateName,
                    PlayerAnimatorStates.GroggyAttack,
                    System.StringComparison.Ordinal));
                return criticalAttackStateName;
            }

            if (player.AnimatorDriver.TryCrossFadeToState(PlayerAnimatorStates.Attack3, 0.05f, 0, 0f))
            {
                player.SetAttackRootMotionEnabled(false);
                return PlayerAnimatorStates.Attack3;
            }

            player.SetAttackRootMotionEnabled(false);
            player.CrossFadeToLocomotion(0.05f);
        }

        return null;
    }

    private Vector3 ResolveCriticalPresentationBossPosition(PlayerController player, Vector3 playerForward)
    {
        Transform stabPivot = FindChildTransformByName(player != null ? player.transform : null, CriticalStabPivotName);
        if (stabPivot != null)
        {
            Vector3 pivotPosition = stabPivot.position;
            pivotPosition.y = transform.position.y;
            return pivotPosition;
        }

        Vector3 fallbackPosition = player.transform.position + playerForward * Mathf.Max(0.1f, criticalApproachDistance);
        fallbackPosition.y = transform.position.y;
        return fallbackPosition;
    }

    private static Transform FindChildTransformByName(Transform root, string childName)
    {
        if (root == null || string.IsNullOrWhiteSpace(childName))
        {
            return null;
        }

        Transform[] childTransforms = root.GetComponentsInChildren<Transform>(true);
        foreach (Transform child in childTransforms)
        {
            if (child != null && string.Equals(child.name, childName, System.StringComparison.Ordinal))
            {
                return child;
            }
        }

        return null;
    }

    private void EndCriticalPresentation(PlayerController player)
    {
        if (player == null)
        {
            _criticalStrikePlayer = null;
            return;
        }

        ResetCriticalPresentationPlaybackSpeed(player);
        player.SetAttackRootMotionEnabled(false);
        player.EndExternalInteractionLock();
        player.CrossFadeToLocomotion(0.08f);
        _criticalStrikePlayer = null;
    }

    private void ApplyCriticalStrikeDamage(PlayerController player)
    {
        CombatAttackDefinition criticalAttack = CreateCriticalAttackDefinition(player);
        Vector3 hitPoint = transform.position + Vector3.up * 1.2f;
        Vector3 hitNormal = (transform.position - player.transform.position);
        hitNormal.y = 0f;
        if (hitNormal.sqrMagnitude <= 0.0001f)
        {
            hitNormal = transform.forward;
        }

        using (CombatDamageContext.Push(criticalAttack, player.transform))
        {
            TakeDamage(criticalAttack.damage, hitPoint, hitNormal.normalized);
        }

        ApplyCameraShake(criticalCameraShakeIntensity, 0.12f);
        EffectManager.Instance.DoHitStop(criticalHitStopDuration);
        EffectManager.Instance.DoCameraShake(criticalCameraShakeIntensity, 0.18f);
        UIInteractionPrompt.ShowNotice("Critical", 1.1f);
    }

    private CombatAttackDefinition CreateCriticalAttackDefinition(PlayerController player)
    {
        CombatAttackDefinition criticalAttack = player != null && player.statData != null
            ? player.statData.CreateAttackDefinition(PlayerAnimatorStates.GroggyAttack)
            : null;

        if (criticalAttack == null)
        {
            criticalAttack = new CombatAttackDefinition
            {
                attackId = PlayerAnimatorStates.GroggyAttack,
                attackName = "GroggyAttack",
                animatorStateName = PlayerAnimatorStates.GroggyAttack,
                impactNormalized = 0.5f,
                activeStartNormalized = 0.4f,
                activeEndNormalized = 0.6f,
                recoveryEndNormalized = 1f
            };
        }

        criticalAttack.attackId = PlayerAnimatorStates.GroggyAttack;
        if (string.IsNullOrWhiteSpace(criticalAttack.attackName))
        {
            criticalAttack.attackName = "GroggyAttack";
        }

        criticalAttack.animatorStateName = string.IsNullOrWhiteSpace(criticalAttack.animatorStateName)
            ? PlayerAnimatorStates.GroggyAttack
            : criticalAttack.animatorStateName;
        criticalAttack.damage = Mathf.Max(criticalAttack.damage, Mathf.Max(1, criticalStrikeDamage));
        criticalAttack.staminaDamage = Mathf.Max(criticalAttack.staminaDamage, Mathf.Max(0f, criticalStaminaDamage));
        criticalAttack.groggyDamage = Mathf.Max(criticalAttack.groggyDamage, Mathf.Max(0f, criticalGroggyDamage));
        criticalAttack.poiseDamage = Mathf.Max(criticalAttack.poiseDamage, Mathf.Max(0f, criticalPoiseDamage));
        criticalAttack.isHeavyAttack = true;
        criticalAttack.cameraShakeIntensity = Mathf.Max(criticalAttack.cameraShakeIntensity, criticalCameraShakeIntensity);
        criticalAttack.hitStopDuration = Mathf.Max(criticalAttack.hitStopDuration, criticalHitStopDuration);
        criticalAttack.hitStopMultiplier = Mathf.Max(criticalAttack.hitStopMultiplier, 1.4f);
        criticalAttack.Normalize();
        return criticalAttack;
    }

    private string ResolveCriticalAttackAnimatorStateName(PlayerController player)
    {
        if (player != null && player.statData != null)
        {
            string stateName = player.statData.GetAttackAnimatorStateName(PlayerAnimatorStates.GroggyAttack);
            if (!string.IsNullOrWhiteSpace(stateName))
            {
                return stateName;
            }
        }

        return PlayerAnimatorStates.GroggyAttack;
    }

    private IEnumerator Co_PlayCriticalReactionSequence()
    {
        try
        {
            AnimatorStateInfo currentStateInfo = anim != null ? anim.GetCurrentAnimatorStateInfo(0) : default;
            bool hasAlreadyAdvancedPastReaction =
                anim != null &&
                !anim.IsInTransition(0) &&
                (currentStateInfo.IsName(criticalKnockDownLoopAnimation) ||
                 currentStateInfo.IsName(criticalGetUpAnimation) ||
                 currentStateInfo.IsName("Idle"));

            if (!hasAlreadyAdvancedPastReaction)
            {
                yield return Co_WaitForBossAnimationCompletion(criticalHitReactionAnimation);
            }

            if (IsDead || currentState != State.Groggy || isPhaseTransitioning)
            {
                SetAnimSpeed(1.0f);
                yield break;
            }

            SetAnimSpeed(1.0f);
            if (!string.IsNullOrWhiteSpace(criticalKnockDownLoopAnimation))
            {
                yield return Co_WaitForBossAutoFollowupCompletion(criticalHitReactionAnimation, criticalKnockDownLoopAnimation);

                if (IsDead || currentState != State.Groggy || isPhaseTransitioning)
                {
                    yield break;
                }

                yield return Co_WaitForBossAutoFollowupCompletion(criticalKnockDownLoopAnimation, criticalGetUpAnimation);
            }
            else
            {
                yield return Co_WaitForBossAutoFollowupCompletion(criticalHitReactionAnimation, criticalGetUpAnimation);
            }

            if (IsDead || currentState != State.Groggy || isPhaseTransitioning)
            {
                yield break;
            }

            if (!IsCurrentAnimState("Idle"))
            {
                PlayAnim("Idle", 0.05f);
            }
        }
        finally
        {
            _criticalReactionRoutine = null;
        }
    }

    private void PlayCriticalHitReactionPresentation()
    {
        if (anim == null)
        {
            return;
        }

        StopAgent();
        SetAnimSpeed(ResolveCriticalPresentationAnimSpeed());

        int stateHash = Animator.StringToHash(criticalHitReactionAnimation);
        if (anim.HasState(0, stateHash))
        {
            PlayAnim(criticalHitReactionAnimation, criticalReactionCrossFade);
            return;
        }

        PlayAnim(GroggyAnimationStateName, 0.05f);
    }

    private IEnumerator Co_PlayBossAnimationAndWaitForCompletion(string stateName)
    {
        if (anim == null || string.IsNullOrWhiteSpace(stateName))
        {
            yield break;
        }

        int stateHash = Animator.StringToHash(stateName);
        if (!anim.HasState(0, stateHash))
        {
            Debug.LogWarning($"[Boss Critical] Missing state '{stateName}'.");
            yield break;
        }

        StopAgent();
        SetAnimSpeed(1.0f);
        PlayAnim(stateName, criticalReactionCrossFade);

        float timeout = Mathf.Max(0.5f, criticalReactionTimeout);
        float elapsed = 0f;
        bool hasObservedTargetState = false;

        while (elapsed < timeout)
        {
            if (IsDead || currentState != State.Groggy || isPhaseTransitioning)
            {
                yield break;
            }

            AnimatorStateInfo stateInfo = anim.GetCurrentAnimatorStateInfo(0);
            if (stateInfo.IsName(stateName))
            {
                hasObservedTargetState = true;
                if (!anim.IsInTransition(0) && stateInfo.normalizedTime >= 0.98f)
                {
                    yield break;
                }
            }
            else if (hasObservedTargetState && !anim.IsInTransition(0))
            {
                yield break;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    private IEnumerator Co_WaitForAnimatorStateCompletion(string stateName, float timeout, float completionNormalized = 0.98f)
    {
        if (anim == null || string.IsNullOrWhiteSpace(stateName))
        {
            yield break;
        }

        int stateHash = Animator.StringToHash(stateName);
        if (!anim.HasState(0, stateHash))
        {
            Debug.LogWarning($"[Boss Intro] Missing state '{stateName}'.");
            yield break;
        }

        float elapsed = 0f;
        bool hasObservedTargetState = false;

        while (elapsed < timeout)
        {
            if (IsDead)
            {
                yield break;
            }

            AnimatorStateInfo stateInfo = anim.GetCurrentAnimatorStateInfo(0);
            if (stateInfo.IsName(stateName))
            {
                hasObservedTargetState = true;
                if (!anim.IsInTransition(0) && stateInfo.normalizedTime >= completionNormalized)
                {
                    yield break;
                }
            }
            else if (hasObservedTargetState && !anim.IsInTransition(0))
            {
                yield break;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    private IEnumerator Co_WaitForBossAutoFollowupCompletion(string previousStateName, string followupStateName)
    {
        if (anim == null || string.IsNullOrWhiteSpace(followupStateName))
        {
            yield break;
        }

        float timeout = Mathf.Max(0.5f, criticalReactionTimeout);
        float completionNormalized = ResolveFollowupCompletionNormalized(followupStateName);
        float elapsed = 0f;
        bool hasEnteredFollowupState = false;

        while (elapsed < timeout)
        {
            if (IsDead || currentState != State.Groggy || isPhaseTransitioning)
            {
                yield break;
            }

            AnimatorStateInfo stateInfo = anim.GetCurrentAnimatorStateInfo(0);
            if (stateInfo.IsName(followupStateName))
            {
                hasEnteredFollowupState = true;
                if (!anim.IsInTransition(0) && stateInfo.normalizedTime >= completionNormalized)
                {
                    yield break;
                }
            }
            else if (hasEnteredFollowupState && !anim.IsInTransition(0))
            {
                yield break;
            }
            else if (!hasEnteredFollowupState &&
                     !string.IsNullOrWhiteSpace(previousStateName) &&
                     !stateInfo.IsName(previousStateName) &&
                     !anim.IsInTransition(0))
            {
                yield break;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    private float ResolveFollowupCompletionNormalized(string followupStateName)
    {
        if (!string.IsNullOrWhiteSpace(criticalGetUpAnimation) &&
            string.Equals(followupStateName, criticalGetUpAnimation, System.StringComparison.Ordinal))
        {
            return Mathf.Clamp01(criticalGetUpIdleStartNormalized);
        }

        return 0.98f;
    }

    private void StartCriticalReactionSequence()
    {
        if (_criticalReactionRoutine != null)
        {
            return;
        }

        _criticalReactionRoutine = StartCoroutine(Co_PlayCriticalReactionSequence());
    }

    private void ApplyCriticalPresentationPlaybackSpeed(PlayerController player)
    {
        float speed = ResolveCriticalPresentationAnimSpeed();
        SetAnimSpeed(speed);

        if (player != null && player.Anim != null)
        {
            player.Anim.speed = speed;
        }
    }

    private void ResetCriticalPresentationPlaybackSpeed(PlayerController player)
    {
        if (player != null && player.Anim != null)
        {
            player.Anim.speed = 1.0f;
        }
    }

    private float ResolveCriticalPresentationAnimSpeed()
    {
        return Mathf.Clamp(criticalPresentationAnimSpeed, 0.1f, 1.0f);
    }

    private float ResolveCriticalHitReactionDelay(float presentationSpeed)
    {
        float fps = Mathf.Max(1f, criticalHitReactionSourceFps);
        float clipSeconds = Mathf.Max(0f, criticalHitReactionStartFrame) / fps;
        return clipSeconds / Mathf.Max(0.1f, presentationSpeed);
    }

    private IEnumerator Co_WaitForPlayerCriticalAnimationCompletion(PlayerController player, string stateName, float minimumReleaseTime)
    {
        while (Time.time < minimumReleaseTime)
        {
            if (player == null || player.IsDead)
            {
                yield break;
            }

            yield return null;
        }

        if (player == null || player.IsDead || player.AnimatorDriver == null || string.IsNullOrWhiteSpace(stateName))
        {
            yield break;
        }

        float timeout = Mathf.Max(0.5f, criticalReactionTimeout);
        float elapsed = 0f;
        bool hasObservedTargetState = false;

        while (elapsed < timeout)
        {
            if (player == null || player.IsDead)
            {
                yield break;
            }

            if (player.AnimatorDriver.IsCurrentState(stateName))
            {
                hasObservedTargetState = true;
                if (!player.AnimatorDriver.IsInTransition() &&
                    player.AnimatorDriver.GetCurrentStateNormalizedTime() >= 0.98f)
                {
                    yield break;
                }
            }
            else if (hasObservedTargetState && !player.AnimatorDriver.IsInTransition())
            {
                yield break;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    private IEnumerator Co_WaitForBossAnimationCompletion(string stateName)
    {
        if (anim == null || string.IsNullOrWhiteSpace(stateName))
        {
            yield break;
        }

        int stateHash = Animator.StringToHash(stateName);
        if (!anim.HasState(0, stateHash))
        {
            yield break;
        }

        float timeout = Mathf.Max(0.5f, criticalReactionTimeout);
        float elapsed = 0f;
        bool hasObservedTargetState = false;

        while (elapsed < timeout)
        {
            if (IsDead || currentState != State.Groggy || isPhaseTransitioning)
            {
                yield break;
            }

            AnimatorStateInfo stateInfo = anim.GetCurrentAnimatorStateInfo(0);
            if (stateInfo.IsName(stateName))
            {
                hasObservedTargetState = true;
                if (!anim.IsInTransition(0) && stateInfo.normalizedTime >= 0.98f)
                {
                    yield break;
                }
            }
            else if (hasObservedTargetState && !anim.IsInTransition(0))
            {
                yield break;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    private bool CanOfferCriticalStrike(PlayerController player)
    {
        if (_criticalStrikeConsumedThisGroggy || player == null || player.IsDead || player.IsBonfireInputLocked || player.IsIntroInputLocked)
        {
            return false;
        }

        if (player.StateMachine == null)
        {
            return false;
        }

        IState currentPlayerState = player.StateMachine.CurrentState;
        bool isNeutralState = currentPlayerState == player.IdleState ||
                              currentPlayerState == player.MoveState;
        if (!isNeutralState)
        {
            return false;
        }

        Vector3 toPlayer = player.transform.position - transform.position;
        toPlayer.y = 0f;
        if (toPlayer.sqrMagnitude > criticalInteractionRange * criticalInteractionRange)
        {
            return false;
        }

        if (toPlayer.sqrMagnitude <= 0.0001f)
        {
            return true;
        }

        float angle = Vector3.Angle(transform.forward, toPlayer.normalized);
        return angle <= criticalFacingAngle * 0.5f;
    }

    private PlayerController ResolveTargetPlayer()
    {
        return target != null ? target.GetComponent<PlayerController>() : null;
    }

    private void RotateBossTowardPlayer(Transform playerTransform)
    {
        if (playerTransform == null)
        {
            return;
        }

        Vector3 dir = playerTransform.position - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        transform.rotation = Quaternion.LookRotation(dir.normalized);
    }

    private void HideCriticalPrompt()
    {
        if (!_isCriticalPromptVisible)
        {
            return;
        }

        UIInteractionPrompt.Hide(this);
        _isCriticalPromptVisible = false;
    }

    private void AbortCriticalStrikePresentation()
    {
        if (_criticalReactionRoutine != null)
        {
            StopCoroutine(_criticalReactionRoutine);
            _criticalReactionRoutine = null;
        }

        if (_criticalStrikePlayer != null)
        {
            EndCriticalPresentation(_criticalStrikePlayer);
        }

        _criticalStrikeRoutine = null;
    }

    private void ResolveTrajectoryColors(bool isHeavy, out Color coreColor, out Color shellColor)
    {
        if (currentPhase <= 1)
        {
            coreColor = isHeavy ? phase1HeavyTrajectoryColor : phase1LightTrajectoryColor;
            shellColor = isHeavy ? phase1HeavyTrajectoryShellColor : phase1LightTrajectoryShellColor;
            return;
        }

        coreColor = isHeavy ? heavyTrajectoryColor : lightTrajectoryColor;
        shellColor = isHeavy ? heavyTrajectoryShellColor : lightTrajectoryShellColor;
    }

    protected override IEnumerator Idle() => idleState.Enter();
    protected override IEnumerator Move() => moveState.Enter();
    protected override IEnumerator Action() => actionState.Enter();
    protected override IEnumerator HitReact() => hitReactState.Enter();
    protected override IEnumerator Groggy() => groggyState.Enter();
    protected override IEnumerator Dead() => deadState.Enter();

    private sealed class BossAttackTrajectoryRuntime
    {
        private struct RibbonSample
        {
            public Vector3 BasePoint;
            public Vector3 TipPoint;
            public float Time;

            public RibbonSample(Vector3 basePoint, Vector3 tipPoint, float time)
            {
                BasePoint = basePoint;
                TipPoint = tipPoint;
                Time = time;
            }
        }

        private sealed class RibbonLayer
        {
            public readonly string Name;
            public GameObject RootObject;
            public MeshFilter MeshFilter;
            public MeshRenderer MeshRenderer;
            public Mesh Mesh;
            public Material RuntimeMaterial;

            public RibbonLayer(string name)
            {
                Name = name;
            }
        }

        private readonly BossController _owner;
        private readonly List<RibbonSample> _samples = new List<RibbonSample>(24);
        private readonly RibbonLayer _coreLayer = new RibbonLayer("BossAttackTrajectoryCore");
        private readonly RibbonLayer _shellLayer = new RibbonLayer("BossAttackTrajectoryShell");
        private GameObject _ribbonRootObject;
        private CombatAttackDefinition _currentAttack;
        private bool _isTrajectoryAttack;
        private bool _isEmitting;
        private float _coreLifetime;
        private float _shellLifetime;
        private float _retainedLifetime;
        private float _referenceWeaponLength;
        private float _coreSpan;
        private float _shellSpan;
        private Color _coreColor;
        private Color _shellColor;

        public BossAttackTrajectoryRuntime(BossController owner)
        {
            _owner = owner;
        }

        public void BeginAttack(CombatAttackDefinition attack)
        {
            _currentAttack = attack;
            _isTrajectoryAttack = _owner.enableAttackTrajectory && ShouldUseWeaponTrajectory(attack);
            _isEmitting = false;

            if (!_isTrajectoryAttack)
            {
                DisableEmission(clearTrail: true);
                return;
            }

            EnsureRenderer();
            ConfigureRenderer(attack);
            ClearSamples();
            TryAppendSample(force: true);
            _isEmitting = true;
        }

        public void UpdateAttack(CombatAttackDefinition attack, float normalizedTime)
        {
            if (!_isTrajectoryAttack)
            {
                return;
            }

            _currentAttack = attack;
        }

        public void EndAttack()
        {
            DisableEmission(clearTrail: false);
            _currentAttack = null;
            _isTrajectoryAttack = false;
        }

        public void Tick()
        {
            if (_coreLayer.Mesh == null)
            {
                return;
            }

            if (_isEmitting)
            {
                TryAppendSample(force: false);
            }

            PruneExpiredSamples(Time.time);
            RebuildMeshes(Time.time);
        }

        public void Shutdown()
        {
            DisableEmission(clearTrail: false);

            DestroyLayerResources(_coreLayer);
            DestroyLayerResources(_shellLayer);

            if (_ribbonRootObject != null)
            {
                Object.Destroy(_ribbonRootObject);
                _ribbonRootObject = null;
            }

            _currentAttack = null;
            _isTrajectoryAttack = false;
            _isEmitting = false;
            _samples.Clear();
        }

        private void EnsureRenderer()
        {
            if (_ribbonRootObject == null)
            {
                _ribbonRootObject = new GameObject("BossAttackTrajectory");
                _ribbonRootObject.transform.SetParent(_owner.transform, false);
            }

            EnsureLayer(_shellLayer, isShell: true);
            EnsureLayer(_coreLayer, isShell: false);
        }

        private void EnsureLayer(RibbonLayer layer, bool isShell)
        {
            if (layer.RootObject == null)
            {
                layer.RootObject = new GameObject(layer.Name);
                layer.RootObject.transform.SetParent(_ribbonRootObject.transform, false);
            }

            if (layer.MeshFilter == null)
            {
                layer.MeshFilter = layer.RootObject.GetComponent<MeshFilter>();
                if (layer.MeshFilter == null)
                {
                    layer.MeshFilter = layer.RootObject.AddComponent<MeshFilter>();
                }
            }

            if (layer.MeshRenderer == null)
            {
                layer.MeshRenderer = layer.RootObject.GetComponent<MeshRenderer>();
                if (layer.MeshRenderer == null)
                {
                    layer.MeshRenderer = layer.RootObject.AddComponent<MeshRenderer>();
                }
            }

            if (layer.Mesh == null)
            {
                layer.Mesh = new Mesh
                {
                    name = isShell ? "BossAttackTrajectoryShellMesh" : "BossAttackTrajectoryCoreMesh"
                };
                layer.Mesh.MarkDynamic();
            }

            if (layer.RuntimeMaterial == null)
            {
                layer.RuntimeMaterial = CreateRuntimeTrailMaterial(isShell);
            }

            layer.MeshFilter.sharedMesh = layer.Mesh;
            layer.MeshRenderer.sharedMaterial = layer.RuntimeMaterial;
            layer.MeshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            layer.MeshRenderer.receiveShadows = false;
            layer.MeshRenderer.enabled = false;
        }

        private void ConfigureRenderer(CombatAttackDefinition attack)
        {
            float weaponLength = ResolveWeaponLength();
            _referenceWeaponLength = weaponLength;
            bool isHeavy = attack != null && attack.isHeavyAttack;
            float widthScale = isHeavy ? _owner.heavyTrajectoryWidthScale : _owner.lightTrajectoryWidthScale;
            _coreSpan = Mathf.Clamp(
                weaponLength * Mathf.Max(1.01f, 1f + widthScale),
                _owner.minTrajectoryWidth,
                _owner.maxTrajectoryWidth);
            _coreLifetime = Mathf.Max(0.01f, isHeavy ? _owner.heavyTrajectoryLifetime : _owner.lightTrajectoryLifetime);
            _owner.ResolveTrajectoryColors(isHeavy, out _coreColor, out _shellColor);

            if (_owner.enableAttackTrajectoryShell)
            {
                float shellWidthMultiplier = isHeavy
                    ? _owner.heavyTrajectoryShellWidthMultiplier
                    : _owner.lightTrajectoryShellWidthMultiplier;
                float shellLifetimeMultiplier = isHeavy
                    ? _owner.heavyTrajectoryShellLifetimeMultiplier
                    : _owner.lightTrajectoryShellLifetimeMultiplier;

                _shellSpan = Mathf.Clamp(
                    _coreSpan * Mathf.Max(1.05f, shellWidthMultiplier),
                    Mathf.Max(_coreSpan * 1.08f, _owner.minTrajectoryWidth),
                    Mathf.Max(_coreSpan * 1.08f, _owner.maxTrajectoryShellWidth));
                _shellLifetime = Mathf.Max(_coreLifetime, _coreLifetime * Mathf.Max(1f, shellLifetimeMultiplier));
            }
            else
            {
                _shellSpan = _coreSpan;
                _shellLifetime = _coreLifetime;
                _shellColor = _coreColor;
            }

            _retainedLifetime = Mathf.Max(_coreLifetime, _shellLifetime);
            ConfigureLayerMaterial(_coreLayer.RuntimeMaterial, _coreColor, isHeavy, isShell: false);
            ConfigureLayerMaterial(_shellLayer.RuntimeMaterial, _shellColor, isHeavy, isShell: true);
        }

        private void ConfigureLayerMaterial(Material material, Color layerColor, bool isHeavy, bool isShell)
        {
            if (material == null)
            {
                return;
            }

            Color accentColor = isShell
                ? Color.Lerp(layerColor, Color.white, isHeavy ? 0.58f : 0.46f)
                : Color.Lerp(layerColor, Color.white, isHeavy ? 0.28f : 0.18f);
            Color baseColor = isShell
                ? Color.Lerp(layerColor, Color.black, isHeavy ? 0.48f : 0.54f)
                : Color.Lerp(layerColor, Color.black, isHeavy ? 0.78f : 0.72f);

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", new Color(baseColor.r, baseColor.g, baseColor.b, 1f));
            }

            if (material.HasProperty("_AccentColor"))
            {
                material.SetColor("_AccentColor", new Color(accentColor.r, accentColor.g, accentColor.b, 1f));
            }

            if (material.HasProperty("_Mode"))
            {
                material.SetFloat("_Mode", 0f);
            }

            if (material.HasProperty("_Opacity"))
            {
                material.SetFloat("_Opacity", isShell
                    ? (isHeavy ? 1.72f : 1.54f)
                    : (isHeavy ? 1.45f : 1.18f));
            }

            if (material.HasProperty("_NoiseStrength"))
            {
                material.SetFloat("_NoiseStrength", isShell
                    ? (isHeavy ? 0.24f : 0.18f)
                    : (isHeavy ? 0.32f : 0.22f));
            }

            if (material.HasProperty("_NoiseTexStrength"))
            {
                material.SetFloat("_NoiseTexStrength", isShell
                    ? (isHeavy ? 0.95f : 0.88f)
                    : (isHeavy ? 0.82f : 0.7f));
            }

            if (material.HasProperty("_DetailStrength"))
            {
                material.SetFloat("_DetailStrength", isShell
                    ? (isHeavy ? 0.62f : 0.5f)
                    : (isHeavy ? 0.58f : 0.44f));
            }

            if (material.HasProperty("_DetailScale"))
            {
                material.SetFloat("_DetailScale", isShell
                    ? (isHeavy ? 2.1f : 1.85f)
                    : (isHeavy ? 1.7f : 1.45f));
            }

            if (material.HasProperty("_BandWidth"))
            {
                material.SetFloat("_BandWidth", isShell
                    ? (isHeavy ? 0.46f : 0.4f)
                    : (isHeavy ? 0.34f : 0.28f));
            }

            if (material.HasProperty("_BandFeather"))
            {
                material.SetFloat("_BandFeather", isShell
                    ? (isHeavy ? 0.28f : 0.24f)
                    : (isHeavy ? 0.18f : 0.14f));
            }

            if (material.HasProperty("_BodyIntensity"))
            {
                material.SetFloat("_BodyIntensity", isShell
                    ? (isHeavy ? 1.52f : 1.34f)
                    : (isHeavy ? 1.08f : 0.92f));
            }

            if (material.HasProperty("_CoreWidth"))
            {
                material.SetFloat("_CoreWidth", isShell
                    ? (isHeavy ? 0.08f : 0.065f)
                    : (isHeavy ? 0.055f : 0.045f));
            }

            if (material.HasProperty("_CoreFeather"))
            {
                material.SetFloat("_CoreFeather", isShell
                    ? (isHeavy ? 0.08f : 0.06f)
                    : (isHeavy ? 0.04f : 0.03f));
            }

            if (material.HasProperty("_CoreIntensity"))
            {
                material.SetFloat("_CoreIntensity", isShell
                    ? (isHeavy ? 0.34f : 0.24f)
                    : (isHeavy ? 0.9f : 0.72f));
            }

            if (material.HasProperty("_SecondaryOffset"))
            {
                material.SetFloat("_SecondaryOffset", isShell
                    ? (isHeavy ? 0.48f : 0.44f)
                    : (isHeavy ? 0.34f : 0.3f));
            }

            if (material.HasProperty("_SecondaryWidth"))
            {
                material.SetFloat("_SecondaryWidth", isShell
                    ? (isHeavy ? 0.1f : 0.08f)
                    : (isHeavy ? 0.05f : 0.04f));
            }

            if (material.HasProperty("_SecondaryFeather"))
            {
                material.SetFloat("_SecondaryFeather", isShell
                    ? (isHeavy ? 0.12f : 0.1f)
                    : (isHeavy ? 0.06f : 0.045f));
            }

            if (material.HasProperty("_SecondaryIntensity"))
            {
                material.SetFloat("_SecondaryIntensity", isShell
                    ? (isHeavy ? 0.22f : 0.16f)
                    : (isHeavy ? 0.16f : 0.12f));
            }

            if (material.HasProperty("_HeadGlow"))
            {
                material.SetFloat("_HeadGlow", isShell
                    ? (isHeavy ? 0.56f : 0.42f)
                    : (isHeavy ? 0.36f : 0.24f));
            }

            if (material.HasProperty("_TailFade"))
            {
                material.SetFloat("_TailFade", isShell
                    ? (isHeavy ? 0.07f : 0.08f)
                    : (isHeavy ? 0.1f : 0.12f));
            }

            if (material.HasProperty("_RingRadius"))
            {
                material.SetFloat("_RingRadius", isShell
                    ? 0.58f
                    : 0.5f);
            }

            if (material.HasProperty("_RingWidth"))
            {
                material.SetFloat("_RingWidth", isShell
                    ? 0.08f
                    : 0.05f);
            }

            if (material.HasProperty("_RingIntensity"))
            {
                material.SetFloat("_RingIntensity", 0f);
            }

            if (material.HasProperty("_FlowSpeedX"))
            {
                material.SetFloat("_FlowSpeedX", isShell
                    ? (isHeavy ? 0.72f : 0.58f)
                    : (isHeavy ? 0.56f : 0.42f));
            }

            if (material.HasProperty("_FlowSpeedY"))
            {
                material.SetFloat("_FlowSpeedY", isShell
                    ? (isHeavy ? 0.26f : 0.18f)
                    : (isHeavy ? 0.18f : 0.12f));
            }
        }

        private bool TryAppendSample(bool force)
        {
            if (_coreLayer.Mesh == null || _owner.bossHitbox == null)
            {
                return false;
            }

            if (!_owner.bossHitbox.TryGetWeaponTrailSample(out Vector3 rawBasePoint, out Vector3 rawTipPoint))
            {
                return false;
            }

            Vector3 center = (rawBasePoint + rawTipPoint) * 0.5f;
            if ((rawTipPoint - rawBasePoint).sqrMagnitude <= 0.0001f)
            {
                return false;
            }

            float sampleTime = Time.time;

            if (!force && _samples.Count > 0)
            {
                RibbonSample lastSample = _samples[_samples.Count - 1];
                Vector3 lastCenter = (lastSample.BasePoint + lastSample.TipPoint) * 0.5f;
                float centerDistance = Vector3.Distance(lastCenter, center);
                if (centerDistance < _owner.trajectorySampleMinDistance)
                {
                    return false;
                }

                int intermediateSampleCount = ResolveIntermediateSampleCount(centerDistance);
                AppendIntermediateSamples(lastSample, rawBasePoint, rawTipPoint, sampleTime, intermediateSampleCount);
            }

            AppendRibbonSample(rawBasePoint, rawTipPoint, sampleTime);

            return true;
        }

        private float ResolveWeaponLength()
        {
            if (_owner.bossHitbox != null &&
                _owner.bossHitbox.TryGetWeaponTrailSample(out Vector3 basePoint, out Vector3 tipPoint))
            {
                return Mathf.Max(0.1f, Vector3.Distance(basePoint, tipPoint));
            }

            return 1f;
        }

        private void DisableEmission(bool clearTrail)
        {
            _isEmitting = false;
            if (clearTrail)
            {
                ClearSamples();
                DisableLayer(_coreLayer);
                DisableLayer(_shellLayer);
            }
        }

        private int ResolveIntermediateSampleCount(float centerDistance)
        {
            float minDistance = Mathf.Max(0.001f, _owner.trajectorySampleMinDistance);
            int desiredSegments = Mathf.CeilToInt(centerDistance / minDistance);
            return Mathf.Clamp(desiredSegments - 1, 0, 4);
        }

        private void AppendIntermediateSamples(
            RibbonSample startSample,
            Vector3 endBasePoint,
            Vector3 endTipPoint,
            float endTime,
            int intermediateSampleCount)
        {
            if (intermediateSampleCount <= 0)
            {
                return;
            }

            for (int i = 1; i <= intermediateSampleCount; i++)
            {
                float t = i / (float)(intermediateSampleCount + 1);
                Vector3 basePoint = Vector3.Lerp(startSample.BasePoint, endBasePoint, t);
                Vector3 tipPoint = Vector3.Lerp(startSample.TipPoint, endTipPoint, t);
                float sampleTime = Mathf.Lerp(startSample.Time, endTime, t);
                AppendRibbonSample(basePoint, tipPoint, sampleTime);
            }
        }

        private void AppendRibbonSample(Vector3 basePoint, Vector3 tipPoint, float sampleTime)
        {
            _samples.Add(new RibbonSample(basePoint, tipPoint, sampleTime));
            int maxSamples = Mathf.Max(2, _owner.trajectoryMaxSamples);
            while (_samples.Count > maxSamples)
            {
                _samples.RemoveAt(0);
            }
        }

        private void DisableLayer(RibbonLayer layer)
        {
            if (layer.MeshRenderer != null)
            {
                layer.MeshRenderer.enabled = false;
            }

            if (layer.Mesh != null)
            {
                layer.Mesh.Clear();
            }
        }

        private void PruneExpiredSamples(float currentTime)
        {
            if (_samples.Count == 0)
            {
                return;
            }

            float cutoff = currentTime - _retainedLifetime;
            while (_samples.Count > 0 && _samples[0].Time < cutoff)
            {
                _samples.RemoveAt(0);
            }
        }

        private void RebuildMeshes(float currentTime)
        {
            float normalizedReferenceLength = Mathf.Max(0.01f, _referenceWeaponLength);
            float shellSpanScale = Mathf.Max(0.01f, _shellSpan / normalizedReferenceLength);
            RebuildLayerMesh(
                _shellLayer,
                currentTime,
                shellSpanScale,
                _shellLifetime,
                _shellColor,
                forceDoubleSided: true,
                isShell: true,
                enabled: _owner.enableAttackTrajectoryShell);

            float coreSpanScale = Mathf.Max(0.01f, _coreSpan / normalizedReferenceLength);
            RebuildLayerMesh(
                _coreLayer,
                currentTime,
                coreSpanScale,
                _coreLifetime,
                _coreColor,
                forceDoubleSided: !_owner.useSingleSidedTrajectory,
                isShell: false,
                enabled: true);
        }

        private void RebuildLayerMesh(
            RibbonLayer layer,
            float currentTime,
            float spanScale,
            float lifetime,
            Color color,
            bool forceDoubleSided,
            bool isShell,
            bool enabled)
        {
            if (!enabled || layer.Mesh == null)
            {
                DisableLayer(layer);
                return;
            }

            if (_samples.Count < 2 || spanScale <= 0.0001f)
            {
                DisableLayer(layer);
                return;
            }

            int sampleCount = _samples.Count;
            Vector3[] vertices = new Vector3[sampleCount * 2];
            Color[] colors = new Color[sampleCount * 2];
            Vector2[] uvs = new Vector2[sampleCount * 2];
            Vector3[] worldBasePoints = new Vector3[sampleCount];
            Vector3[] worldTipPoints = new Vector3[sampleCount];
            bool useDoubleSided = forceDoubleSided;
            int trianglesPerSegment = useDoubleSided ? 12 : 6;
            int[] triangles = new int[(sampleCount - 1) * trianglesPerSegment];

            float widthFadeFloor = isShell ? 0.42f : 0.18f;
            float widthFadeExponent = isShell ? 0.24f : 0.38f;
            float endTaperScale = isShell ? 0.94f : 0.85f;
            float endTaperBias = isShell ? 0.28f : 0.15f;
            float brightnessMin = isShell ? 0.96f : 0.7f;
            float brightnessMax = isShell ? 1.42f : 1.18f;
            float alphaTailAttenuation = isShell ? 0.12f : 0.35f;

            for (int i = 0; i < sampleCount; i++)
            {
                RibbonSample sample = _samples[i];
                int vertexIndex = i * 2;
                float age01 = lifetime > 0.0001f
                    ? Mathf.Clamp01((currentTime - sample.Time) / lifetime)
                    : 1f;
                float alpha = Mathf.Clamp01(color.a * (1f - age01) * (1f - age01 * alphaTailAttenuation));
                float u = sampleCount > 1 ? (float)i / (sampleCount - 1) : 0f;
                float widthFade = Mathf.Lerp(widthFadeFloor, 1f, Mathf.Pow(1f - age01, widthFadeExponent));
                float endTaper = Mathf.Clamp01(Mathf.Sin(u * Mathf.PI) * endTaperScale + endTaperBias);
                float widthFactor = widthFade * endTaper * spanScale;
                Vector3 center = (sample.BasePoint + sample.TipPoint) * 0.5f;
                Vector3 basePoint = Vector3.Lerp(center, sample.BasePoint, widthFactor);
                Vector3 tipPoint = Vector3.Lerp(center, sample.TipPoint, widthFactor);
                float brightness = Mathf.Lerp(brightnessMin, brightnessMax, Mathf.Clamp01(1f - age01 * 0.9f));
                Color sampleColor = new Color(
                    Mathf.Clamp01(color.r * brightness),
                    Mathf.Clamp01(color.g * brightness),
                    Mathf.Clamp01(color.b * brightness),
                    alpha);

                worldBasePoints[i] = basePoint;
                worldTipPoints[i] = tipPoint;
                vertices[vertexIndex] = _owner.transform.InverseTransformPoint(basePoint);
                vertices[vertexIndex + 1] = _owner.transform.InverseTransformPoint(tipPoint);
                colors[vertexIndex] = sampleColor;
                colors[vertexIndex + 1] = sampleColor;
                uvs[vertexIndex] = new Vector2(u, 0f);
                uvs[vertexIndex + 1] = new Vector2(u, 1f);
            }

            Camera referenceCamera = ResolveReferenceCamera();
            int triangleIndex = 0;
            for (int i = 0; i < sampleCount - 1; i++)
            {
                int current = i * 2;
                int next = current + 2;

                if (!useDoubleSided)
                {
                    bool flipWinding = ShouldFlipSegmentWinding(
                        referenceCamera,
                        worldBasePoints[i],
                        worldTipPoints[i],
                        worldBasePoints[i + 1],
                        worldTipPoints[i + 1]);

                    if (flipWinding)
                    {
                        triangles[triangleIndex++] = current;
                        triangles[triangleIndex++] = current + 1;
                        triangles[triangleIndex++] = next;
                        triangles[triangleIndex++] = current + 1;
                        triangles[triangleIndex++] = next + 1;
                        triangles[triangleIndex++] = next;
                    }
                    else
                    {
                        triangles[triangleIndex++] = current;
                        triangles[triangleIndex++] = next;
                        triangles[triangleIndex++] = current + 1;
                        triangles[triangleIndex++] = current + 1;
                        triangles[triangleIndex++] = next;
                        triangles[triangleIndex++] = next + 1;
                    }

                    continue;
                }

                triangles[triangleIndex++] = current;
                triangles[triangleIndex++] = next;
                triangles[triangleIndex++] = current + 1;
                triangles[triangleIndex++] = current + 1;
                triangles[triangleIndex++] = next;
                triangles[triangleIndex++] = next + 1;

                triangles[triangleIndex++] = current + 1;
                triangles[triangleIndex++] = next;
                triangles[triangleIndex++] = current;
                triangles[triangleIndex++] = next + 1;
                triangles[triangleIndex++] = next;
                triangles[triangleIndex++] = current + 1;
            }

            layer.Mesh.Clear();
            layer.Mesh.vertices = vertices;
            layer.Mesh.colors = colors;
            layer.Mesh.uv = uvs;
            layer.Mesh.triangles = triangles;
            layer.Mesh.RecalculateBounds();

            if (layer.MeshRenderer != null)
            {
                layer.MeshRenderer.enabled = true;
            }
        }

        private void ClearSamples()
        {
            _samples.Clear();
            if (_coreLayer.Mesh != null)
            {
                _coreLayer.Mesh.Clear();
            }

            if (_shellLayer.Mesh != null)
            {
                _shellLayer.Mesh.Clear();
            }
        }

        private static Camera ResolveReferenceCamera()
        {
            if (Camera.main != null)
            {
                return Camera.main;
            }

            return Camera.current;
        }

        private static bool ShouldFlipSegmentWinding(
            Camera referenceCamera,
            Vector3 currentBase,
            Vector3 currentTip,
            Vector3 nextBase,
            Vector3 nextTip)
        {
            if (referenceCamera != null &&
                TryGetClockwiseWinding(referenceCamera, currentBase, nextBase, currentTip, out bool isClockwise))
            {
                return !isClockwise;
            }

            if (referenceCamera != null &&
                TryGetClockwiseWinding(referenceCamera, currentTip, nextBase, nextTip, out bool secondTriangleClockwise))
            {
                return !secondTriangleClockwise;
            }

            return false;
        }

        private static bool TryGetClockwiseWinding(
            Camera referenceCamera,
            Vector3 a,
            Vector3 b,
            Vector3 c,
            out bool isClockwise)
        {
            isClockwise = false;
            if (referenceCamera == null)
            {
                return false;
            }

            Vector3 viewA = referenceCamera.WorldToViewportPoint(a);
            Vector3 viewB = referenceCamera.WorldToViewportPoint(b);
            Vector3 viewC = referenceCamera.WorldToViewportPoint(c);
            if (viewA.z <= 0f || viewB.z <= 0f || viewC.z <= 0f)
            {
                return false;
            }

            float signedArea =
                (viewB.x - viewA.x) * (viewC.y - viewA.y) -
                (viewB.y - viewA.y) * (viewC.x - viewA.x);
            if (Mathf.Abs(signedArea) <= 0.000001f)
            {
                return false;
            }

            isClockwise = signedArea < 0f;
            return true;
        }

        private static bool ShouldUseWeaponTrajectory(CombatAttackDefinition attack)
        {
            if (attack == null)
            {
                return false;
            }

            string attackId = attack.ResolvedAttackId;
            if (string.IsNullOrWhiteSpace(attackId))
            {
                return false;
            }

            return true;
        }

        private Material CreateRuntimeTrailMaterial(bool isShell)
        {
            Material material = null;
            if (_owner.attackTrajectoryMaterial != null)
            {
                material = new Material(_owner.attackTrajectoryMaterial);
            }
            else
            {
                Shader shader = Shader.Find("Custom/VFX/BossAttackPulse");
                if (shader == null)
                {
                    shader = Shader.Find("Sprites/Default");
                }

                if (shader != null)
                {
                    material = new Material(shader);
                }
            }

            if (material == null)
            {
                return null;
            }

            material.name = isShell ? "BossAttackTrajectoryShell_Runtime" : "BossAttackTrajectoryCore_Runtime";
            material.renderQueue = isShell
                ? Mathf.Max(0, material.renderQueue - 1)
                : Mathf.Max(0, material.renderQueue + 1);

            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", Color.white);
            }

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", Color.white);
            }

            if (material.HasProperty("_Mode"))
            {
                material.SetFloat("_Mode", 0f);
            }

            if (material.HasProperty("_BodyIntensity"))
            {
                material.SetFloat("_BodyIntensity", isShell ? 1.2f : 0.8f);
            }

            if (material.HasProperty("_RingIntensity"))
            {
                material.SetFloat("_RingIntensity", 0f);
            }

            return material;
        }

        private static void DestroyLayerResources(RibbonLayer layer)
        {
            if (layer.Mesh != null)
            {
                Object.Destroy(layer.Mesh);
                layer.Mesh = null;
            }

            if (layer.RuntimeMaterial != null)
            {
                Object.Destroy(layer.RuntimeMaterial);
                layer.RuntimeMaterial = null;
            }

            layer.MeshFilter = null;
            layer.MeshRenderer = null;
            layer.RootObject = null;
        }
    }
}
