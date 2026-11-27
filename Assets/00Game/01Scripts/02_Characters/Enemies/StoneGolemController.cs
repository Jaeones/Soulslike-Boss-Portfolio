using System;
using System.Collections;
using System.Collections.Generic;
using Cinemachine;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

public class StoneGolemController : EnemyBase
{
    private const string AutoAttackHitboxObjectName = "StoneGolemAttackHitbox";
    private const string StoneGolemDisplayName = "Gate Guardian";
    private const string CorpseLootObjectName = "StoneGolemCorpseQuestItemLoot";
    private const string PersistentCorpseLootObjectName = "StoneGolemPersistentCorpseQuestItemLoot";

    [Serializable]
    public class StoneGolemAttackOption
    {
        [InspectorName("공격 ID")]
        public string attackId = "Attack_Close";

        [InspectorName("최소 거리")]
        [Min(0f)] public float minDistance = 0f;

        [InspectorName("최대 거리")]
        [Min(0f)] public float maxDistance = 3f;

        [InspectorName("선택 가중치")]
        [Min(0.01f)] public float selectionWeight = 1f;

        [InspectorName("쿨다운")]
        [Min(0f)] public float cooldown = 1.2f;

        [InspectorName("직전 반복 회피")]
        public bool avoidImmediateRepeat = true;

        [InspectorName("공격 정의")]
        public CombatAttackDefinition definition = new CombatAttackDefinition();

        public string ResolvedAttackId => string.IsNullOrWhiteSpace(attackId)
            ? definition.ResolvedAttackId
            : attackId;

        public void Normalize(int index)
        {
            if (definition == null)
            {
                definition = new CombatAttackDefinition();
            }

            if (string.IsNullOrWhiteSpace(attackId))
            {
                attackId = $"StoneGolemAttack_{index + 1}";
            }

            if (string.IsNullOrWhiteSpace(definition.attackId))
            {
                definition.attackId = attackId;
            }

            if (string.IsNullOrWhiteSpace(definition.animatorStateName))
            {
                definition.animatorStateName = attackId;
            }

            definition.Normalize(index);
            minDistance = Mathf.Max(0f, minDistance);
            maxDistance = Mathf.Max(minDistance, maxDistance);
            selectionWeight = Mathf.Max(0.01f, selectionWeight);
            cooldown = Mathf.Max(0f, cooldown);
        }

        public bool MatchesDistance(float distance)
        {
            return distance >= minDistance && distance <= maxDistance;
        }

        public CombatAttackDefinition CreateRuntimeAttack()
        {
            CombatAttackDefinition runtime = definition.CreateRuntimeCopy();
            runtime.attackId = ResolvedAttackId;
            runtime.Normalize();
            return runtime;
        }
    }

    [Header("Animator States")]
    [SerializeField] private string idleStateName = "mutant breathing idle";
    [SerializeField] private string moveStateName = "mutant walking";
    [SerializeField] private string alertStateName = "mutant roaring";
    [SerializeField] private string hitStateName = "mutant roaring";
    [SerializeField] private string groggyStateName = "mutant roaring2";
    [SerializeField] private string deathStateName = "mutant dying";

    [Header("Movement")]
    [SerializeField] private float alertDistance = 8f;
    [SerializeField] private float combatEnterDistance = 5.5f;
    [SerializeField] private float attackStoppingDistance = 2.8f;
    [SerializeField] private float moveSpeed = 2.2f;
    [SerializeField] private float rotationSpeed = 8f;
    [SerializeField] private float decisionInterval = 0.08f;
    [SerializeField] private float postAttackIdleDelay = 0.4f;
    [SerializeField] private float alertStateMinimumDuration = 0.7f;

    [Header("Combat Resources")]
    [SerializeField] private float maxStamina = 100f;
    [SerializeField] private float staminaRegenPerSecond = 20f;
    [SerializeField] private float staminaMoveRegenMultiplier = 0.45f;
    [SerializeField] private float attackRepeatLockDuration = 1.5f;

    [Header("Poise / Hit Reaction")]
    [SerializeField] private float maxPoise = 100f;
    [SerializeField] private float poiseRecoveryPerSecond = 10f;
    [SerializeField] private float poiseResetRatioAfterBreak = 0.6f;
    [SerializeField] private float lightHitReactDuration = 0.35f;
    [SerializeField] private float heavyHitReactDuration = 0.55f;
    [SerializeField] private float heavyHitPoiseThreshold = 28f;

    [Header("Combat Feedback")]
    public CinemachineImpulseSource impulseSource;
    [SerializeField] private GameObject stoneHitImpactEffectPrefab;
    [SerializeField] [Min(0.1f)] private float stoneHitImpactEffectScale = 3f;
    [SerializeField] private Color hitFlashColor = new Color(0.8f, 0.8f, 0.8f, 0.36f);
    [SerializeField] private float hitFlashDuration = 0.05f;

    [Header("Jump Attack Ground Impact")]
    [SerializeField] private Transform jumpAttackImpactAnchor;
    [SerializeField] private GameObject jumpAttackGroundImpactEffectPrefab;
    [SerializeField] [Min(0.1f)] private float jumpAttackGroundImpactScale = 5f;
    [SerializeField] [Range(0f, 1f)] private float jumpAttackGroundShakeIntensity = 0.28f;
    [SerializeField] [Min(0f)] private float jumpAttackGroundShakeDuration = 0.18f;
    [SerializeField] private LayerMask jumpAttackGroundMask = ~0;
    [SerializeField] private float jumpAttackGroundRayHeight = 2.5f;
    [SerializeField] private float jumpAttackGroundRayDistance = 5.5f;
    [SerializeField] private float jumpAttackGroundNormalOffset = 0.02f;

    [Header("Footstep Ground Impact")]
    [SerializeField] private Transform leftFootImpactAnchor;
    [SerializeField] private Transform rightFootImpactAnchor;
    [SerializeField] private GameObject footstepGroundImpactEffectPrefab;
    [SerializeField] [Min(0.1f)] private float footstepGroundImpactScale = 1.35f;
    [SerializeField] [Range(0f, 1f)] private float footstepGroundShakeIntensity = 0.08f;
    [SerializeField] [Min(0f)] private float footstepGroundShakeDuration = 0.08f;
    [SerializeField] private LayerMask footstepGroundMask = ~0;
    [SerializeField] private float footstepGroundRayHeight = 1.4f;
    [SerializeField] private float footstepGroundRayDistance = 3.2f;
    [SerializeField] private float footstepGroundNormalOffset = 0.01f;
    [SerializeField] private float footstepGroundMinInterval = 0.04f;

    [Header("Stone Golem SFX")]
    [SerializeField] private AudioSource stoneGolemSfxAudioSource;
    [SerializeField] private AudioClip[] footstepSfxClips;
    [SerializeField] private AudioClip jumpStartSfxClip;
    [SerializeField] private AudioClip jumpAttackSfxClip;
    [SerializeField] private AudioClip[] incomingHitSfxClips;
    [SerializeField] private AudioClip mutantRoaringSfxClip;
    [SerializeField] private AudioClip mutantRoaring2SfxClip;
    [SerializeField] [Range(0f, 1f)] private float footstepSfxVolume = 0.95f;
    [SerializeField] [Range(0f, 1f)] private float jumpStartSfxVolume = 1f;
    [SerializeField] [Range(0f, 1f)] private float jumpAttackSfxVolume = 1f;
    [SerializeField] [Range(0f, 1f)] private float incomingHitSfxVolume = 1f;
    [SerializeField] [Range(0f, 1f)] private float mutantRoaringSfxVolume = 1f;
    [SerializeField] [Range(0f, 1f)] private float mutantRoaring2SfxVolume = 1f;

    [Header("Corpse Loot")]
    [SerializeField] private bool enableCorpseQuestItemLoot = true;
    [SerializeField] private BossCorpseQuestItemInteractable corpseQuestItemInteractable;
    [SerializeField] private Material corpseSoulBillboardMaterial;
    [SerializeField] private Vector3 corpseLootLocalPosition = new Vector3(0f, 0.9f, 0.35f);
    [SerializeField] private float corpseLootTriggerRadius = 1.2f;

    [Header("Attack Runtime")]
    [SerializeField] private BossHitbox attackHitbox;
    [SerializeField] private List<StoneGolemAttackOption> attacks = new List<StoneGolemAttackOption>();

    [Header("Attack Query Preset")]
    [SerializeField] private Vector3 attackForwardHalfExtents = new Vector3(1.45f, 1.35f, 1.8f);
    [SerializeField] private float attackForwardOffset = 1.45f;
    [SerializeField] private Vector3 attackDashHalfExtents = new Vector3(1.35f, 1.2f, 1.65f);
    [SerializeField] private float attackDashForwardOffset = 1.3f;
    [SerializeField] private float attackSlamRadius = 2.1f;
    [SerializeField] private float jumpAttackRadius = 2.65f;
    [SerializeField] private float attackWhirlwindRadius = 2.2f;
    [SerializeField] private float attackGroundQueryVerticalOffset = 0.15f;

    private readonly Dictionary<string, float> _attackCooldowns = new Dictionary<string, float>(StringComparer.Ordinal);
    private StoneGolemAttackOption _queuedAttack;
    private string _lastAttackId;
    private float _lastAttackFinishedTime = float.NegativeInfinity;
    private float _currentStamina;
    private float _currentPoise;
    private Coroutine _attackDashRoutine;
    private bool _hasAlertedTarget;
    private bool _healthBarEncounterActive;
    private UIBossHealthBar _bossHealthBarUi;
    private BossCorpseQuestItemInteractable _persistentCorpseQuestItemInteractable;
    private float _lastFootstepGroundImpactTime = float.NegativeInfinity;

    protected override void Awake()
    {
        base.Awake();

        EnsureDefaultAttackSetup();
        NormalizeAttackOptions();

        _currentStamina = maxStamina;
        _currentPoise = maxPoise;

        if (agent != null)
        {
            agent.speed = moveSpeed;
            agent.stoppingDistance = attackStoppingDistance;
            if (anim != null && anim.applyRootMotion)
            {
                agent.updatePosition = false;
            }
        }

        EnsureAttackHitbox();
        EnsureCorpseQuestItemInteractable();
        ResolveStoneGolemSfxAudioSource();
        ResolveFootImpactAnchors();
        OnHpChanged += HandleHealthBarHpChanged;
        OnDied += HandleHealthBarOwnerDied;
    }

    protected override void Start()
    {
        currentState = State.Idle;
        base.Start();
        ResolveBossHealthBarUi();
        SetCorpseQuestItemLootActive(false);
        SetPersistentCorpseQuestItemLootActive(false);
        SyncWithQuestProgressionState();
    }

    private void OnDisable()
    {
        SetCorpseQuestItemLootActive(false);
        if (!IsDead)
        {
            EndHealthBarEncounter(cleared: false);
        }
    }

    private void OnDestroy()
    {
        SetCorpseQuestItemLootActive(false);
        OnHpChanged -= HandleHealthBarHpChanged;
        OnDied -= HandleHealthBarOwnerDied;
    }

    private void Update()
    {
        if (IsDead)
        {
            return;
        }

        RegenerateStamina();
        RecoverPoise();

        if (_hasAlertedTarget)
        {
            EnsureHealthBarEncounterStarted();
        }
    }

    private void OnAnimatorMove()
    {
        if (!IsRootMotionDrivingMovement())
        {
            return;
        }

        bool useFullAttackRootMotion = ShouldUseFullAttackRootMotion();
        Vector3 deltaPosition = anim.deltaPosition;

        Vector3 position = transform.position + deltaPosition;
        bool allowVerticalRootMotion = currentState == State.Action || currentState == State.Dead;

        if (!allowVerticalRootMotion)
        {
            if (agent != null && agent.enabled && agent.isOnNavMesh)
            {
                position.y = agent.nextPosition.y;
            }
            else
            {
                position.y = transform.position.y;
            }
        }

        transform.position = position;
        if (useFullAttackRootMotion)
        {
            transform.rotation *= anim.deltaRotation;
        }

        SyncAgentToTransform(allowVerticalRootMotion);
    }

    public void PlayFootstepSfx()
    {
        PlayRandomStoneGolemSfx(footstepSfxClips, footstepSfxVolume);
        PlayFootstepGroundImpactFeedback(null);
    }

    public void FootL()
    {
        PlayRandomStoneGolemSfx(footstepSfxClips, footstepSfxVolume);
        PlayFootstepGroundImpactFeedback(leftFootImpactAnchor);
    }

    public void FootR()
    {
        PlayRandomStoneGolemSfx(footstepSfxClips, footstepSfxVolume);
        PlayFootstepGroundImpactFeedback(rightFootImpactAnchor);
    }

    public void OnFootstep()
    {
        PlayFootstepSfx();
    }

    public void JumpStart()
    {
        PlayStoneGolemSfx(jumpStartSfxClip, jumpStartSfxVolume);
    }

    public void JumpAttack()
    {
        PlayStoneGolemSfx(jumpAttackSfxClip, jumpAttackSfxVolume);
        PlayJumpAttackGroundImpactFeedback();
    }

    private void OnValidate()
    {
        EnsureDefaultAttackSetup();
        attackStoppingDistance = Mathf.Max(0.5f, attackStoppingDistance);
        alertDistance = Mathf.Max(attackStoppingDistance, alertDistance);
        combatEnterDistance = Mathf.Max(attackStoppingDistance, combatEnterDistance);
        moveSpeed = Mathf.Max(0.1f, moveSpeed);
        rotationSpeed = Mathf.Max(0.1f, rotationSpeed);
        decisionInterval = Mathf.Max(0.02f, decisionInterval);
        postAttackIdleDelay = Mathf.Max(0f, postAttackIdleDelay);
        alertStateMinimumDuration = Mathf.Max(0f, alertStateMinimumDuration);
        maxStamina = Mathf.Max(1f, maxStamina);
        staminaRegenPerSecond = Mathf.Max(0f, staminaRegenPerSecond);
        staminaMoveRegenMultiplier = Mathf.Clamp01(staminaMoveRegenMultiplier);
        attackRepeatLockDuration = Mathf.Max(0f, attackRepeatLockDuration);
        maxPoise = Mathf.Max(1f, maxPoise);
        poiseRecoveryPerSecond = Mathf.Max(0f, poiseRecoveryPerSecond);
        poiseResetRatioAfterBreak = Mathf.Clamp01(poiseResetRatioAfterBreak);
        lightHitReactDuration = Mathf.Max(0.05f, lightHitReactDuration);
        heavyHitReactDuration = Mathf.Max(lightHitReactDuration, heavyHitReactDuration);
        heavyHitPoiseThreshold = Mathf.Max(0f, heavyHitPoiseThreshold);
        jumpAttackGroundImpactScale = Mathf.Max(0.1f, jumpAttackGroundImpactScale);
        jumpAttackGroundShakeIntensity = Mathf.Clamp01(jumpAttackGroundShakeIntensity);
        jumpAttackGroundShakeDuration = Mathf.Max(0f, jumpAttackGroundShakeDuration);
        jumpAttackGroundRayHeight = Mathf.Max(0f, jumpAttackGroundRayHeight);
        jumpAttackGroundRayDistance = Mathf.Max(0.1f, jumpAttackGroundRayDistance);
        jumpAttackGroundNormalOffset = Mathf.Max(0f, jumpAttackGroundNormalOffset);
        footstepGroundImpactScale = Mathf.Max(0.1f, footstepGroundImpactScale);
        footstepGroundShakeIntensity = Mathf.Clamp01(footstepGroundShakeIntensity);
        footstepGroundShakeDuration = Mathf.Max(0f, footstepGroundShakeDuration);
        footstepGroundRayHeight = Mathf.Max(0f, footstepGroundRayHeight);
        footstepGroundRayDistance = Mathf.Max(0.1f, footstepGroundRayDistance);
        footstepGroundNormalOffset = Mathf.Max(0f, footstepGroundNormalOffset);
        footstepGroundMinInterval = Mathf.Max(0f, footstepGroundMinInterval);
        if (!Application.isPlaying)
        {
            ResolveFootImpactAnchors();
        }
        attackForwardHalfExtents = new Vector3(
            Mathf.Max(0.1f, attackForwardHalfExtents.x),
            Mathf.Max(0.1f, attackForwardHalfExtents.y),
            Mathf.Max(0.1f, attackForwardHalfExtents.z));
        attackForwardOffset = Mathf.Max(0f, attackForwardOffset);
        attackDashHalfExtents = new Vector3(
            Mathf.Max(0.1f, attackDashHalfExtents.x),
            Mathf.Max(0.1f, attackDashHalfExtents.y),
            Mathf.Max(0.1f, attackDashHalfExtents.z));
        attackDashForwardOffset = Mathf.Max(0f, attackDashForwardOffset);
        attackSlamRadius = Mathf.Max(0.1f, attackSlamRadius);
        jumpAttackRadius = Mathf.Max(0.1f, jumpAttackRadius);
        attackWhirlwindRadius = Mathf.Max(0.1f, attackWhirlwindRadius);
        attackGroundQueryVerticalOffset = Mathf.Max(0f, attackGroundQueryVerticalOffset);
        NormalizeAttackOptions();
    }

    public override void TakeDamage(int amount, Vector3 hitPoint, Vector3 hitNormal)
    {
        if (IsDead)
        {
            return;
        }

        hp = Mathf.Max(0f, hp - amount);
        CallHpChanged();

        CombatAttackDefinition incomingAttack = CombatDamageContext.CurrentAttack;
        _hasAlertedTarget = true;
        EnsureHealthBarEncounterStarted();
        PlayIncomingHitFeedback(incomingAttack, hitPoint, hitNormal);

        if (hp <= 0f)
        {
            Die();
            return;
        }

        ApplyIncomingCombatPressure(amount, incomingAttack);
        ApplyIncomingPoiseDamage(amount, incomingAttack);

        if (ShouldEnterGroggyState())
        {
            StopCurrentAttackRuntime();
            currentState = State.Groggy;
            return;
        }

        if (CanEnterHitReaction() && ShouldTriggerHitReaction(amount, incomingAttack))
        {
            StopCurrentAttackRuntime();
            currentState = State.HitReact;
        }
    }

    protected override IEnumerator Intro()
    {
        StopNavigation();
        StopCurrentAttackRuntime();
        SetAnimatorSpeed(1f);

        if (!_hasAlertedTarget)
        {
            if (target == null || !IsTargetWithinAlertRange())
            {
                currentState = State.Idle;
                yield break;
            }

            _hasAlertedTarget = true;
        }

        if (!HasAnimatorState(alertStateName))
        {
            currentState = ResolvePostAlertState();
            yield break;
        }

        CrossFadeState(alertStateName, 0.08f);
        yield return null;

        float elapsed = 0f;
        while (currentState == State.Intro && !IsDead)
        {
            elapsed += Time.deltaTime;

            if (anim == null)
            {
                break;
            }

            AnimatorStateInfo stateInfo = anim.GetCurrentAnimatorStateInfo(0);
            bool finished = stateInfo.IsName(alertStateName) &&
                            elapsed >= alertStateMinimumDuration &&
                            stateInfo.normalizedTime >= 0.98f;

            if (finished || (elapsed >= alertStateMinimumDuration && !anim.IsInTransition(0) && !stateInfo.IsName(alertStateName)))
            {
                break;
            }

            yield return null;
        }

        if (!IsDead)
        {
            currentState = ResolvePostAlertState();
        }
    }

    protected override IEnumerator Idle()
    {
        StopNavigation();
        StopCurrentAttackRuntime();
        SetAnimatorSpeed(1f);
        CrossFadeState(idleStateName, 0.12f);

        WaitForSeconds wait = new WaitForSeconds(decisionInterval);
        while (currentState == State.Idle && !IsDead)
        {
            if (target == null)
            {
                yield return wait;
                continue;
            }

            if (!_hasAlertedTarget)
            {
                if (IsTargetWithinAlertRange())
                {
                    currentState = State.Intro;
                    yield break;
                }

                yield return wait;
                continue;
            }

            RotateTowardsTarget(rotationSpeed);
            float distanceToTarget = Vector3.Distance(transform.position, target.position);

            if (TryQueueAttack(distanceToTarget))
            {
                currentState = State.Action;
                yield break;
            }

            if (distanceToTarget > attackStoppingDistance + 0.35f)
            {
                currentState = State.Move;
                yield break;
            }

            yield return wait;
        }
    }

    protected override IEnumerator Move()
    {
        StopCurrentAttackRuntime();
        SetAnimatorSpeed(1f);
        ResumeNavigation();
        CrossFadeState(moveStateName, 0.1f);

        WaitForSeconds wait = new WaitForSeconds(decisionInterval);
        while (currentState == State.Move && !IsDead)
        {
            if (target == null)
            {
                currentState = State.Idle;
                yield break;
            }

            if (!_hasAlertedTarget)
            {
                currentState = State.Idle;
                yield break;
            }

            if (agent != null && agent.isOnNavMesh)
            {
                agent.speed = moveSpeed;
                agent.stoppingDistance = attackStoppingDistance;
                agent.SetDestination(target.position);
            }

            RotateTowardsTarget(rotationSpeed * 0.85f);
            float distanceToTarget = Vector3.Distance(transform.position, target.position);

            if (TryQueueAttack(distanceToTarget))
            {
                StopNavigation();
                currentState = State.Action;
                yield break;
            }

            if (distanceToTarget <= attackStoppingDistance + 0.2f)
            {
                StopNavigation();
                currentState = State.Idle;
                yield break;
            }

            yield return wait;
        }
    }

    protected override IEnumerator Action()
    {
        StopNavigation();

        StoneGolemAttackOption selectedAttack = _queuedAttack;
        _queuedAttack = null;

        if (selectedAttack == null)
        {
            float distanceToTarget = target != null ? Vector3.Distance(transform.position, target.position) : 0f;
            if (!TrySelectAttack(distanceToTarget, out selectedAttack))
            {
                currentState = State.Idle;
                yield break;
            }
        }

        CombatAttackDefinition runtimeAttack = selectedAttack.CreateRuntimeAttack();
        if (!TryConsumeStamina(runtimeAttack.staminaCost))
        {
            currentState = State.Move;
            yield break;
        }

        if (!HasAnimatorState(runtimeAttack.ResolvedAnimatorStateName))
        {
            Debug.LogWarning($"[StoneGolem] Animator state not found: {runtimeAttack.ResolvedAnimatorStateName}", this);
            currentState = State.Idle;
            yield break;
        }

        SetCooldown(selectedAttack);
        if (!ShouldKeepCurrentFacing(runtimeAttack))
        {
            RotateTowardsTarget(rotationSpeed * 2f, true);
        }
        attackHitbox?.BeginAttack(this, runtimeAttack);
        CrossFadeState(runtimeAttack.ResolvedAnimatorStateName, 0.08f);

        yield return null;

        bool hasTriggeredDash = false;
        float timeout = 0f;
        while (currentState == State.Action && !IsDead)
        {
            timeout += Time.deltaTime;
            AnimatorStateInfo stateInfo = anim != null ? anim.GetCurrentAnimatorStateInfo(0) : default;

            if (anim == null)
            {
                break;
            }

            if (stateInfo.IsName(runtimeAttack.ResolvedAnimatorStateName))
            {
                float normalizedTime = Mathf.Clamp01(stateInfo.normalizedTime);

                ApplyAttackAnimationSpeed(runtimeAttack, normalizedTime);
                UpdateAttackFacing(runtimeAttack, normalizedTime);
                attackHitbox?.UpdateAttackWindow(normalizedTime);
                attackHitbox?.PerformHit();

                if (!hasTriggeredDash &&
                    !IsRootMotionDrivingMovement() &&
                    runtimeAttack.dashDistance > 0f &&
                    normalizedTime >= runtimeAttack.impactNormalized)
                {
                    _attackDashRoutine = StartCoroutine(Co_AttackDash(runtimeAttack.dashDistance, runtimeAttack.dashDuration));
                    hasTriggeredDash = true;
                }

                if (normalizedTime >= Mathf.Max(0.98f, runtimeAttack.recoveryEndNormalized))
                {
                    break;
                }
            }
            else if (timeout > 0.3f && !anim.IsInTransition(0))
            {
                break;
            }

            yield return null;
        }

        StopCurrentAttackRuntime();
        _lastAttackId = runtimeAttack.ResolvedAttackId;
        _lastAttackFinishedTime = Time.time;

        if (postAttackIdleDelay > 0f && currentState == State.Action && !IsDead)
        {
            yield return new WaitForSeconds(postAttackIdleDelay * Mathf.Max(0.1f, runtimeAttack.postAttackWaitMultiplier));
        }

        if (currentState != State.Action || IsDead)
        {
            yield break;
        }

        float distanceAfterAttack = target != null ? Vector3.Distance(transform.position, target.position) : float.PositiveInfinity;
        currentState = distanceAfterAttack > attackStoppingDistance + 0.6f ? State.Move : State.Idle;
    }

    protected override IEnumerator HitReact()
    {
        StopNavigation();
        StopCurrentAttackRuntime();
        SetAnimatorSpeed(1f);

        bool heavyReaction = _currentPoise <= Mathf.Max(1f, maxPoise * poiseResetRatioAfterBreak);
        float duration = heavyReaction ? heavyHitReactDuration : lightHitReactDuration;
        CrossFadeState(hitStateName, 0.05f);

        float elapsed = 0f;
        while (currentState == State.HitReact && !IsDead && elapsed < duration)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        _currentPoise = Mathf.Clamp(maxPoise * poiseResetRatioAfterBreak, 0f, maxPoise);

        if (!IsDead)
        {
            float distanceToTarget = target != null ? Vector3.Distance(transform.position, target.position) : float.PositiveInfinity;
            currentState = distanceToTarget > attackStoppingDistance + 0.8f ? State.Move : State.Idle;
        }
    }

    protected override IEnumerator Groggy()
    {
        StopNavigation();
        StopCurrentAttackRuntime();
        SetAnimatorSpeed(1f);
        CrossFadeState(groggyStateName, 0.08f);

        float elapsed = 0f;
        while (currentState == State.Groggy && !IsDead && elapsed < groggyDuration)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        ResetGroggyGauge();
        _currentPoise = maxPoise;

        if (!IsDead)
        {
            currentState = State.Idle;
        }
    }

    protected override IEnumerator Dead()
    {
        StopNavigation();
        StopCurrentAttackRuntime();
        SetAnimatorSpeed(1f);
        CrossFadeState(deathStateName, 0.05f);

        if (agent != null)
        {
            agent.enabled = false;
        }

        QuestProgressionManager.Instance?.NotifyStoneGolemCleared();
        yield return WaitForDeathAnimationToFinish();
        TryEnableCorpseQuestItemLoot();
    }

    private void RegenerateStamina()
    {
        float regenMultiplier = currentState == State.Move ? staminaMoveRegenMultiplier : 1f;
        _currentStamina = Mathf.Min(maxStamina, _currentStamina + staminaRegenPerSecond * regenMultiplier * Time.deltaTime);
    }

    private void RecoverPoise()
    {
        if (currentState == State.Action || currentState == State.HitReact || currentState == State.Groggy)
        {
            return;
        }

        _currentPoise = Mathf.Min(maxPoise, _currentPoise + poiseRecoveryPerSecond * Time.deltaTime);
    }

    private void ApplyIncomingPoiseDamage(int amount, CombatAttackDefinition incomingAttack)
    {
        float poiseDamage = incomingAttack != null && incomingAttack.poiseDamage > 0f
            ? incomingAttack.poiseDamage
            : Mathf.Max(4f, amount * 0.2f);

        _currentPoise = Mathf.Max(0f, _currentPoise - poiseDamage);
    }

    private bool ShouldTriggerHitReaction(int amount, CombatAttackDefinition incomingAttack)
    {
        float poiseDamage = incomingAttack != null && incomingAttack.poiseDamage > 0f
            ? incomingAttack.poiseDamage
            : Mathf.Max(4f, amount * 0.2f);

        if (_currentPoise <= 0f)
        {
            return true;
        }

        if (incomingAttack != null && incomingAttack.isHeavyAttack)
        {
            return true;
        }

        return poiseDamage >= heavyHitPoiseThreshold;
    }

    private bool CanEnterHitReaction()
    {
        return currentState != State.Dead &&
               currentState != State.Groggy &&
               currentState != State.HitReact;
    }

    private bool TryConsumeStamina(float cost)
    {
        float resolvedCost = Mathf.Max(0f, cost);
        if (_currentStamina < resolvedCost)
        {
            return false;
        }

        _currentStamina -= resolvedCost;
        return true;
    }

    private bool TryQueueAttack(float distanceToTarget)
    {
        if (!TrySelectAttack(distanceToTarget, out StoneGolemAttackOption selectedAttack))
        {
            return false;
        }

        _queuedAttack = selectedAttack;
        return true;
    }

    private bool TrySelectAttack(float distanceToTarget, out StoneGolemAttackOption selectedAttack)
    {
        selectedAttack = null;
        if (target == null || attacks.Count == 0 || distanceToTarget > combatEnterDistance)
        {
            return false;
        }

        List<StoneGolemAttackOption> strictCandidates = GatherAttackCandidates(distanceToTarget, allowImmediateRepeat: false);
        List<StoneGolemAttackOption> relaxedCandidates = strictCandidates.Count > 0
            ? strictCandidates
            : GatherAttackCandidates(distanceToTarget, allowImmediateRepeat: true);

        if (relaxedCandidates.Count <= 0)
        {
            return false;
        }

        selectedAttack = PickWeightedAttack(relaxedCandidates);
        return selectedAttack != null;
    }

    private List<StoneGolemAttackOption> GatherAttackCandidates(float distanceToTarget, bool allowImmediateRepeat)
    {
        var candidates = new List<StoneGolemAttackOption>();
        for (int i = 0; i < attacks.Count; i++)
        {
            StoneGolemAttackOption attack = attacks[i];
            if (attack == null)
            {
                continue;
            }

            if (!attack.MatchesDistance(distanceToTarget))
            {
                continue;
            }

            CombatAttackDefinition definition = attack.definition;
            if (definition == null || _currentStamina < Mathf.Max(0f, definition.staminaCost))
            {
                continue;
            }

            if (IsOnCooldown(attack))
            {
                continue;
            }

            if (!allowImmediateRepeat &&
                attack.avoidImmediateRepeat &&
                string.Equals(attack.ResolvedAttackId, _lastAttackId, StringComparison.Ordinal) &&
                Time.time - _lastAttackFinishedTime < attackRepeatLockDuration)
            {
                continue;
            }

            candidates.Add(attack);
        }

        return candidates;
    }

    private StoneGolemAttackOption PickWeightedAttack(List<StoneGolemAttackOption> candidates)
    {
        if (candidates == null || candidates.Count == 0)
        {
            return null;
        }

        float totalWeight = 0f;
        for (int i = 0; i < candidates.Count; i++)
        {
            totalWeight += Mathf.Max(0.01f, candidates[i].selectionWeight);
        }

        float roll = UnityEngine.Random.Range(0f, totalWeight);
        for (int i = 0; i < candidates.Count; i++)
        {
            roll -= Mathf.Max(0.01f, candidates[i].selectionWeight);
            if (roll <= 0f)
            {
                return candidates[i];
            }
        }

        return candidates[candidates.Count - 1];
    }

    private bool IsOnCooldown(StoneGolemAttackOption attack)
    {
        if (attack == null || attack.cooldown <= 0f)
        {
            return false;
        }

        return _attackCooldowns.TryGetValue(attack.ResolvedAttackId, out float nextAvailableTime) &&
               Time.time < nextAvailableTime;
    }

    private void SetCooldown(StoneGolemAttackOption attack)
    {
        if (attack == null)
        {
            return;
        }

        _attackCooldowns[attack.ResolvedAttackId] = Time.time + attack.cooldown;
    }

    private void StopNavigation()
    {
        if (agent == null)
        {
            return;
        }

        if (agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.ResetPath();
            agent.velocity = Vector3.zero;
            SyncAgentToTransform(preserveVerticalPosition: true);
        }
    }

    private void ResumeNavigation()
    {
        if (agent == null)
        {
            return;
        }

        if (agent.isOnNavMesh)
        {
            SyncAgentToTransform(preserveVerticalPosition: true);
            agent.isStopped = false;
        }
    }

    private void StopCurrentAttackRuntime()
    {
        attackHitbox?.EndAttack();
        SetAnimatorSpeed(1f);

        if (_attackDashRoutine != null)
        {
            StopCoroutine(_attackDashRoutine);
            _attackDashRoutine = null;
        }
    }

    private IEnumerator Co_AttackDash(float distance, float duration)
    {
        float resolvedDuration = Mathf.Max(0.01f, duration);
        float elapsed = 0f;
        Vector3 forward = transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude <= 0.0001f)
        {
            yield break;
        }

        forward.Normalize();

        while (elapsed < resolvedDuration && currentState == State.Action && !IsDead)
        {
            elapsed += Time.deltaTime;
            Vector3 delta = forward * (distance / resolvedDuration) * Time.deltaTime;

            if (agent != null && agent.isOnNavMesh)
            {
                agent.Move(delta);
            }
            else
            {
                transform.position += delta;
            }

            yield return null;
        }

        _attackDashRoutine = null;
    }

    private void ApplyAttackAnimationSpeed(CombatAttackDefinition attack, float normalizedTime)
    {
        if (anim == null || attack == null)
        {
            return;
        }

        float clampedTime = Mathf.Clamp01(normalizedTime);
        float impactStart = Mathf.Clamp01(Mathf.Max(attack.startupStartNormalized, attack.impactNormalized));
        float activeEnd = Mathf.Clamp01(Mathf.Max(impactStart, attack.activeEndNormalized));

        if (clampedTime < impactStart)
        {
            float t = impactStart <= 0.0001f
                ? 1f
                : Mathf.InverseLerp(attack.startupStartNormalized, impactStart, clampedTime);
            SetAnimatorSpeed(Mathf.Lerp(attack.prepareStartSpeed, attack.prepareEndSpeed, t));
            return;
        }

        if (clampedTime <= activeEnd)
        {
            SetAnimatorSpeed(attack.impactSpeed);
            return;
        }

        SetAnimatorSpeed(attack.recoverySpeed);
    }

    private void RotateTowardsTarget(float speed, bool snapIfClose = false)
    {
        if (target == null)
        {
            return;
        }

        Vector3 direction = target.position - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(direction.normalized);
        if (snapIfClose || Quaternion.Angle(transform.rotation, targetRotation) < 1f)
        {
            transform.rotation = targetRotation;
            return;
        }

        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * Mathf.Max(0.1f, speed));
    }

    private void UpdateAttackFacing(CombatAttackDefinition attack, float normalizedTime)
    {
        // Stone Golem attacks should commit to the startup facing and let the
        // animation play without mid-swing target tracking.
    }

    private void CrossFadeState(string stateName, float duration)
    {
        if (anim == null || string.IsNullOrWhiteSpace(stateName))
        {
            return;
        }

        if (!HasAnimatorState(stateName))
        {
            return;
        }

        if (anim.GetCurrentAnimatorStateInfo(0).IsName(stateName))
        {
            return;
        }

        anim.CrossFadeInFixedTime(stateName, Mathf.Max(0f, duration));
        TryPlayStateEntrySfx(stateName);
    }

    private void TryPlayStateEntrySfx(string stateName)
    {
        if (MatchesAnimatorStateName(stateName, groggyStateName))
        {
            PlayStoneGolemSfx(mutantRoaring2SfxClip, mutantRoaring2SfxVolume);
            return;
        }

        if (MatchesAnimatorStateName(stateName, alertStateName) || MatchesAnimatorStateName(stateName, hitStateName))
        {
            PlayStoneGolemSfx(mutantRoaringSfxClip, mutantRoaringSfxVolume);
        }
    }

    private static bool MatchesAnimatorStateName(string stateName, string expectedStateName)
    {
        return !string.IsNullOrWhiteSpace(stateName) &&
               !string.IsNullOrWhiteSpace(expectedStateName) &&
               string.Equals(stateName.Trim(), expectedStateName.Trim(), StringComparison.Ordinal);
    }

    private void SetAnimatorSpeed(float speed)
    {
        if (anim == null)
        {
            return;
        }

        anim.speed = Mathf.Max(0.01f, speed);
    }

    private bool IsRootMotionDrivingMovement()
    {
        return anim != null && anim.applyRootMotion;
    }

    private bool ShouldUseFullAttackRootMotion()
    {
        if (anim == null || currentState != State.Action)
        {
            return false;
        }

        AnimatorStateInfo stateInfo = anim.GetCurrentAnimatorStateInfo(0);
        return stateInfo.IsName("mutant swiping");
    }

    private static bool ShouldKeepCurrentFacing(CombatAttackDefinition attack)
    {
        return attack != null && string.Equals(attack.ResolvedAnimatorStateName, "mutant swiping", StringComparison.Ordinal);
    }

    private bool IsTargetWithinAlertRange()
    {
        return target != null && Vector3.Distance(transform.position, target.position) <= alertDistance;
    }

    private State ResolvePostAlertState()
    {
        if (target == null)
        {
            return State.Idle;
        }

        float distanceToTarget = Vector3.Distance(transform.position, target.position);
        return distanceToTarget > attackStoppingDistance + 0.35f ? State.Move : State.Idle;
    }

    private void SyncAgentToTransform(bool preserveVerticalPosition)
    {
        if (agent == null || !agent.enabled || !agent.isOnNavMesh)
        {
            return;
        }

        Vector3 nextPosition = agent.nextPosition;
        nextPosition.x = transform.position.x;
        nextPosition.z = transform.position.z;
        if (!preserveVerticalPosition)
        {
            nextPosition.y = transform.position.y;
        }

        agent.nextPosition = nextPosition;
    }

    private bool HasAnimatorState(string stateName)
    {
        return anim != null &&
               !string.IsNullOrWhiteSpace(stateName) &&
               anim.HasState(0, Animator.StringToHash(stateName));
    }

    private void ResolveBossHealthBarUi()
    {
        if (_bossHealthBarUi == null)
        {
            _bossHealthBarUi = FindFirstObjectByType<UIBossHealthBar>(FindObjectsInactive.Include);
        }
    }

    private void EnsureCorpseQuestItemInteractable()
    {
        if (!enableCorpseQuestItemLoot)
        {
            return;
        }

        if (corpseQuestItemInteractable == null)
        {
            corpseQuestItemInteractable = GetComponentInChildren<BossCorpseQuestItemInteractable>(true);
        }

        if (corpseQuestItemInteractable == null)
        {
            Transform lootAnchor = FindChildTransformByName(CorpseLootObjectName);
            GameObject lootObject = lootAnchor != null ? lootAnchor.gameObject : new GameObject(CorpseLootObjectName);
            lootObject.layer = gameObject.layer;

            if (lootAnchor == null)
            {
                lootObject.transform.SetParent(transform, false);
                lootObject.transform.localPosition = corpseLootLocalPosition;
                lootObject.transform.localRotation = Quaternion.identity;
                lootObject.transform.localScale = Vector3.one;
            }

            SphereCollider trigger = lootObject.GetComponent<SphereCollider>();
            if (trigger == null)
            {
                trigger = lootObject.AddComponent<SphereCollider>();
            }

            trigger.isTrigger = true;
            trigger.radius = Mathf.Max(0.1f, corpseLootTriggerRadius);

            corpseQuestItemInteractable = lootObject.GetComponent<BossCorpseQuestItemInteractable>();
            if (corpseQuestItemInteractable == null)
            {
                corpseQuestItemInteractable = lootObject.AddComponent<BossCorpseQuestItemInteractable>();
            }
        }

        corpseQuestItemInteractable.ConfigureSoulVisuals(corpseSoulBillboardMaterial);

        SetCorpseQuestItemLootActive(false);
    }

    public void SyncProgressionState(bool stoneGolemCleared, bool questItemCollected)
    {
        if (!stoneGolemCleared)
        {
            SetCorpseQuestItemLootActive(false);
            SetPersistentCorpseQuestItemLootActive(false);

            if (!gameObject.activeSelf && !IsDead)
            {
                gameObject.SetActive(true);
            }

            return;
        }

        if (enableCorpseQuestItemLoot && !questItemCollected)
        {
            EnsurePersistentCorpseQuestItemInteractable();
            PositionPersistentCorpseQuestItemLoot();
            SetPersistentCorpseQuestItemLootActive(true);
            _persistentCorpseQuestItemInteractable?.ActivateLoot();
        }
        else
        {
            SetPersistentCorpseQuestItemLootActive(false);
        }

        if (gameObject.activeSelf)
        {
            gameObject.SetActive(false);
        }
    }

    private void SyncWithQuestProgressionState()
    {
        QuestProgressionManager progression = QuestProgressionManager.Instance;
        if (progression == null)
        {
            return;
        }

        SyncProgressionState(progression.StoneGolemCleared, progression.InjuredKnightQuestItemCollected);
    }

    private void EnsurePersistentCorpseQuestItemInteractable()
    {
        if (!enableCorpseQuestItemLoot)
        {
            return;
        }

        if (_persistentCorpseQuestItemInteractable == null)
        {
            Transform lootRoot = FindSceneRootTransformByName(PersistentCorpseLootObjectName);
            GameObject lootObject = lootRoot != null ? lootRoot.gameObject : new GameObject(PersistentCorpseLootObjectName);
            lootObject.layer = gameObject.layer;

            if (lootRoot == null)
            {
                Scene currentScene = gameObject.scene;
                if (currentScene.IsValid())
                {
                    SceneManager.MoveGameObjectToScene(lootObject, currentScene);
                }
            }

            SphereCollider trigger = lootObject.GetComponent<SphereCollider>();
            if (trigger == null)
            {
                trigger = lootObject.AddComponent<SphereCollider>();
            }

            trigger.isTrigger = true;
            trigger.radius = Mathf.Max(0.1f, corpseLootTriggerRadius);

            _persistentCorpseQuestItemInteractable = lootObject.GetComponent<BossCorpseQuestItemInteractable>();
            if (_persistentCorpseQuestItemInteractable == null)
            {
                _persistentCorpseQuestItemInteractable = lootObject.AddComponent<BossCorpseQuestItemInteractable>();
            }
        }

        _persistentCorpseQuestItemInteractable.ConfigureSoulVisuals(corpseSoulBillboardMaterial);
        SetPersistentCorpseQuestItemLootActive(false);
    }

    private Transform FindSceneRootTransformByName(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
        {
            return null;
        }

        Scene currentScene = gameObject.scene;
        if (!currentScene.IsValid() || !currentScene.isLoaded)
        {
            return null;
        }

        GameObject[] roots = currentScene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            GameObject root = roots[i];
            if (root != null && string.Equals(root.name, objectName, StringComparison.Ordinal))
            {
                return root.transform;
            }
        }

        return null;
    }

    private void PositionPersistentCorpseQuestItemLoot()
    {
        if (_persistentCorpseQuestItemInteractable == null)
        {
            return;
        }

        Transform lootTransform = _persistentCorpseQuestItemInteractable.transform;
        lootTransform.position = transform.TransformPoint(corpseLootLocalPosition);
        lootTransform.rotation = Quaternion.identity;
        lootTransform.localScale = Vector3.one;
    }

    private Transform FindChildTransformByName(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
        {
            return null;
        }

        Transform[] transforms = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform child = transforms[i];
            if (child != null && child != transform && string.Equals(child.name, objectName, StringComparison.Ordinal))
            {
                return child;
            }
        }

        return null;
    }

    private void SetCorpseQuestItemLootActive(bool active)
    {
        if (corpseQuestItemInteractable == null)
        {
            return;
        }

        if (corpseQuestItemInteractable.gameObject.activeSelf != active)
        {
            corpseQuestItemInteractable.gameObject.SetActive(active);
        }
    }

    private void SetPersistentCorpseQuestItemLootActive(bool active)
    {
        if (_persistentCorpseQuestItemInteractable == null)
        {
            return;
        }

        if (_persistentCorpseQuestItemInteractable.gameObject.activeSelf != active)
        {
            _persistentCorpseQuestItemInteractable.gameObject.SetActive(active);
        }
    }

    private bool TryEnableCorpseQuestItemLoot()
    {
        if (!enableCorpseQuestItemLoot)
        {
            return false;
        }

        EnsureCorpseQuestItemInteractable();
        if (corpseQuestItemInteractable == null || !corpseQuestItemInteractable.HasPendingLoot)
        {
            return false;
        }

        SetCorpseQuestItemLootActive(true);
        corpseQuestItemInteractable.ActivateLoot();
        return true;
    }

    private IEnumerator WaitForDeathAnimationToFinish()
    {
        if (anim == null || string.IsNullOrWhiteSpace(deathStateName))
        {
            yield return new WaitForSeconds(1f);
            yield break;
        }

        int stateHash = Animator.StringToHash(deathStateName);
        if (!anim.HasState(0, stateHash))
        {
            yield return new WaitForSeconds(1f);
            yield break;
        }

        float timeout = 0f;
        bool hasObservedDeathState = false;

        while (timeout < 6f)
        {
            AnimatorStateInfo stateInfo = anim.GetCurrentAnimatorStateInfo(0);
            if (stateInfo.IsName(deathStateName))
            {
                hasObservedDeathState = true;
                if (!anim.IsInTransition(0) && stateInfo.normalizedTime >= 0.98f)
                {
                    yield break;
                }
            }
            else if (hasObservedDeathState && !anim.IsInTransition(0))
            {
                yield break;
            }

            timeout += Time.deltaTime;
            yield return null;
        }
    }

    private void EnsureHealthBarEncounterStarted()
    {
        if (_healthBarEncounterActive)
        {
            return;
        }

        ResolveBossHealthBarUi();
        _bossHealthBarUi?.BindBoss(this, StoneGolemDisplayName);

        _healthBarEncounterActive = true;
        GameLoopEvents.RaiseBossFightStarted(this);
        GameLoopEvents.RaiseBossHPChanged(CurrentHp, MaxHp);
    }

    private void EndHealthBarEncounter(bool cleared)
    {
        if (!_healthBarEncounterActive)
        {
            return;
        }

        _healthBarEncounterActive = false;
        _bossHealthBarUi?.UnbindBoss();
        GameLoopEvents.RaiseBossFightEnded(this, cleared);
    }

    private void HandleHealthBarHpChanged(float currentHp, float maxHp)
    {
        if (!_healthBarEncounterActive)
        {
            return;
        }

        GameLoopEvents.RaiseBossHPChanged(currentHp, maxHp);
    }

    private void HandleHealthBarOwnerDied()
    {
        EndHealthBarEncounter(cleared: true);
    }

    private void NormalizeAttackOptions()
    {
        if (attacks == null)
        {
            return;
        }

        for (int i = 0; i < attacks.Count; i++)
        {
            StoneGolemAttackOption attack = attacks[i];
            attack?.Normalize(i);
            ApplyStoneGolemAttackDefaults(attack);
        }
    }

    private void EnsureAttackHitbox()
    {
        if (attackHitbox == null)
        {
            attackHitbox = GetComponentInChildren<BossHitbox>(true);
        }

        if (attackHitbox == null)
        {
            attackHitbox = CreateDedicatedAttackHitbox();
        }

        if (attackHitbox.gameObject == gameObject)
        {
            attackHitbox.SetHitCollider(EnsureDedicatedTriggerCollider(GetOrCreateDedicatedHitboxTransform()));
        }

        attackHitbox.Initialize(this);
        attackHitbox.ConfigureQueryShapes(
            attackForwardHalfExtents,
            attackForwardOffset,
            attackDashHalfExtents,
            attackDashForwardOffset,
            attackSlamRadius,
            jumpAttackRadius,
            attackWhirlwindRadius,
            attackGroundQueryVerticalOffset);
    }

    private BossHitbox CreateDedicatedAttackHitbox()
    {
        Transform hitboxTransform = GetOrCreateDedicatedHitboxTransform();
        BoxCollider triggerCollider = EnsureDedicatedTriggerCollider(hitboxTransform);
        BossHitbox createdHitbox = hitboxTransform.gameObject.AddComponent<BossHitbox>();
        createdHitbox.SetHitCollider(triggerCollider);
        return createdHitbox;
    }

    private Transform GetOrCreateDedicatedHitboxTransform()
    {
        Transform existing = transform.Find(AutoAttackHitboxObjectName);
        if (existing != null)
        {
            existing.gameObject.layer = gameObject.layer;
            return existing;
        }

        GameObject hitboxObject = new GameObject(AutoAttackHitboxObjectName);
        hitboxObject.transform.SetParent(transform, false);
        hitboxObject.layer = gameObject.layer;
        return hitboxObject.transform;
    }

    private BoxCollider EnsureDedicatedTriggerCollider(Transform targetTransform)
    {
        BoxCollider triggerCollider = targetTransform != null ? targetTransform.GetComponent<BoxCollider>() : null;
        if (triggerCollider == null && targetTransform != null)
        {
            triggerCollider = targetTransform.gameObject.AddComponent<BoxCollider>();
        }

        if (triggerCollider == null)
        {
            return null;
        }

        triggerCollider.isTrigger = true;
        triggerCollider.enabled = false;

        BoxCollider rootCollider = GetComponent<BoxCollider>();
        if (rootCollider != null)
        {
            triggerCollider.center = rootCollider.center;
            triggerCollider.size = rootCollider.size;
        }

        return triggerCollider;
    }

    private static void ApplyStoneGolemAttackDefaults(StoneGolemAttackOption attack)
    {
        if (attack == null || attack.definition == null)
        {
            return;
        }

        CombatAttackDefinition definition = attack.definition;
        string attackId = attack.ResolvedAttackId;

        if (definition.staminaDamage <= 0f)
        {
            if (string.Equals(attackId, "Swipe", StringComparison.Ordinal) ||
                string.Equals(attackId, "Swipe_Check", StringComparison.Ordinal))
            {
                definition.staminaDamage = 18f;
            }
            else if (string.Equals(attackId, "Swipe_Crush", StringComparison.Ordinal))
            {
                definition.staminaDamage = 26f;
            }
            else if (string.Equals(attackId, "JumpAttack", StringComparison.Ordinal))
            {
                definition.staminaDamage = 32f;
            }
            else
            {
                float fallbackDamage = Mathf.Max(6f, definition.damage * (definition.isHeavyAttack ? 0.85f : 0.55f));
                definition.staminaDamage = fallbackDamage;
            }
        }

        if (definition.playerHitReactionType == PlayerHitReactionType.DefaultDirectional)
        {
            if (string.Equals(attackId, "Swipe_Crush", StringComparison.Ordinal))
            {
                definition.playerHitReactionType = PlayerHitReactionType.Smash;
            }
            else if (string.Equals(attackId, "JumpAttack", StringComparison.Ordinal))
            {
                definition.playerHitReactionType = PlayerHitReactionType.Down;
            }
        }
    }

    private void EnsureDefaultAttackSetup()
    {
        if (attacks != null && attacks.Count > 0)
        {
            return;
        }

        attacks = new List<StoneGolemAttackOption>
        {
            CreateDefaultAttack(
                attackId: "Swipe",
                minDistance: 0f,
                maxDistance: 3.2f,
                cooldown: 1.2f,
                selectionWeight: 1.5f,
                damage: 24,
                staminaCost: 18f,
                groggyDamage: 14f,
                poiseDamage: 14f,
                impactNormalized: 0.3875f,
                activeStartNormalized: 0.3875f,
                activeEndNormalized: 0.5625f,
                recoveryEndNormalized: 1f,
                trackUntilNormalized: 0.22f,
                facingLockNormalized: 0.32f,
                isHeavyAttack: false,
                dashDistance: 0.12f,
                dashDuration: 0.08f,
                postAttackWaitMultiplier: 1.0f,
                animatorStateName: "mutant swiping",
                prepareStartSpeed: 0.78f,
                prepareEndSpeed: 0.92f,
                impactSpeed: 1.12f,
                recoverySpeed: 0.88f),
            CreateDefaultAttack(
                attackId: "JumpAttack",
                minDistance: 2.4f,
                maxDistance: 6.2f,
                cooldown: 2.8f,
                selectionWeight: 0.7f,
                damage: 36,
                staminaCost: 28f,
                groggyDamage: 24f,
                poiseDamage: 22f,
                impactNormalized: 0.1807f,
                activeStartNormalized: 0.6024f,
                activeEndNormalized: 0.6386f,
                recoveryEndNormalized: 1.0f,
                trackUntilNormalized: 0.14f,
                facingLockNormalized: 0.24f,
                isHeavyAttack: true,
                dashDistance: 2.6f,
                dashDuration: 0.22f,
                postAttackWaitMultiplier: 1.6f,
                animatorStateName: "JumpAttack",
                prepareStartSpeed: 0.68f,
                prepareEndSpeed: 0.84f,
                impactSpeed: 1.02f,
                recoverySpeed: 0.82f)
        };
    }

    private static StoneGolemAttackOption CreateDefaultAttack(
        string attackId,
        float minDistance,
        float maxDistance,
        float cooldown,
        float selectionWeight,
        int damage,
        float staminaCost,
        float groggyDamage,
        float poiseDamage,
        float impactNormalized,
        float activeStartNormalized,
        float activeEndNormalized,
        float recoveryEndNormalized,
        float trackUntilNormalized,
        float facingLockNormalized,
        bool isHeavyAttack,
        float dashDistance,
        float dashDuration,
        float postAttackWaitMultiplier,
        string animatorStateName = null,
        float prepareStartSpeed = 0.1f,
        float prepareEndSpeed = 0.8f,
        float impactSpeed = 1f,
        float recoverySpeed = 0.3f)
    {
        return new StoneGolemAttackOption
        {
            attackId = attackId,
            minDistance = minDistance,
            maxDistance = maxDistance,
            cooldown = cooldown,
            selectionWeight = selectionWeight,
            definition = new CombatAttackDefinition
            {
                attackId = attackId,
                attackName = attackId,
                animatorStateName = string.IsNullOrWhiteSpace(animatorStateName) ? attackId : animatorStateName,
                damage = damage,
                staminaCost = staminaCost,
                groggyDamage = groggyDamage,
                poiseDamage = poiseDamage,
                impactNormalized = impactNormalized,
                activeStartNormalized = activeStartNormalized,
                activeEndNormalized = activeEndNormalized,
                recoveryEndNormalized = recoveryEndNormalized,
                trackUntilNormalized = trackUntilNormalized,
                facingLockNormalized = facingLockNormalized,
                isHeavyAttack = isHeavyAttack,
                dashDistance = dashDistance,
                dashDuration = dashDuration,
                prepareStartSpeed = prepareStartSpeed,
                prepareEndSpeed = prepareEndSpeed,
                impactSpeed = impactSpeed,
                recoverySpeed = recoverySpeed,
                postAttackWaitMultiplier = postAttackWaitMultiplier
            }
        };
    }

    private void PlayIncomingHitFeedback(CombatAttackDefinition incomingAttack, Vector3 hitPoint, Vector3 hitNormal)
    {
        float hitStopDuration = ResolveCombatHitStopDuration(incomingAttack, 0.06f);
        float shakeDuration = ResolveCombatShakeDuration(incomingAttack, 0.08f, 0.12f);
        float shakeIntensity = ResolveCombatShakeIntensity(incomingAttack, 0.08f, 0.16f);

        PlayRandomStoneGolemSfx(incomingHitSfxClips, incomingHitSfxVolume);

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
            EffectManager.Instance.DoFlashEffect(targetRenderer, hitFlashColor, Mathf.Max(0.01f, hitFlashDuration));
        }

        SpawnCombatImpactEffect(stoneHitImpactEffectPrefab, hitPoint, hitNormal);
    }

    public void ApplyCameraShake(float intensity, float duration)
    {
        if (impulseSource != null)
        {
            impulseSource.GenerateImpulse(intensity);
        }
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

    private void ResolveStoneGolemSfxAudioSource()
    {
        if (stoneGolemSfxAudioSource == null)
        {
            stoneGolemSfxAudioSource = GetComponent<AudioSource>();
        }

        if (stoneGolemSfxAudioSource == null)
        {
            GameObject sfxRoot = new GameObject("StoneGolemSfxSource");
            sfxRoot.transform.SetParent(transform, false);
            stoneGolemSfxAudioSource = sfxRoot.AddComponent<AudioSource>();
        }

        stoneGolemSfxAudioSource.playOnAwake = false;
        stoneGolemSfxAudioSource.loop = false;
        stoneGolemSfxAudioSource.spatialBlend = 0f;
    }

    private void PlayStoneGolemSfx(AudioClip clip, float volume)
    {
        if (clip == null || stoneGolemSfxAudioSource == null)
        {
            return;
        }

        stoneGolemSfxAudioSource.pitch = 1f;
        stoneGolemSfxAudioSource.PlayOneShot(clip, volume);
    }

    private void PlayRandomStoneGolemSfx(AudioClip[] clips, float volume)
    {
        PlayStoneGolemSfx(GetRandomClip(clips), volume);
    }

    private void PlayJumpAttackGroundImpactFeedback()
    {
        if (jumpAttackGroundShakeIntensity > 0f)
        {
            ApplyCameraShake(jumpAttackGroundShakeIntensity, jumpAttackGroundShakeDuration);
        }

        GameObject impactPrefab = jumpAttackGroundImpactEffectPrefab != null
            ? jumpAttackGroundImpactEffectPrefab
            : stoneHitImpactEffectPrefab;
        if (impactPrefab == null)
        {
            return;
        }

        TryResolveGroundImpactPose(
            jumpAttackImpactAnchor != null ? jumpAttackImpactAnchor : (attackHitbox != null ? attackHitbox.transform : transform),
            jumpAttackGroundRayHeight,
            jumpAttackGroundRayDistance,
            jumpAttackGroundMask,
            out Vector3 impactPoint,
            out Vector3 impactNormal);
        SpawnGroundImpactEffect(
            impactPrefab,
            impactPoint + impactNormal * jumpAttackGroundNormalOffset,
            impactNormal,
            jumpAttackGroundImpactScale);
    }

    private void PlayFootstepGroundImpactFeedback(Transform preferredAnchor)
    {
        if (Time.time - _lastFootstepGroundImpactTime < footstepGroundMinInterval)
        {
            return;
        }

        _lastFootstepGroundImpactTime = Time.time;

        if (footstepGroundShakeIntensity > 0f)
        {
            ApplyCameraShake(footstepGroundShakeIntensity, footstepGroundShakeDuration);
        }

        GameObject impactPrefab = footstepGroundImpactEffectPrefab != null
            ? footstepGroundImpactEffectPrefab
            : stoneHitImpactEffectPrefab;
        if (impactPrefab == null)
        {
            return;
        }

        Transform anchor = preferredAnchor != null ? preferredAnchor : transform;
        TryResolveGroundImpactPose(
            anchor,
            footstepGroundRayHeight,
            footstepGroundRayDistance,
            footstepGroundMask,
            out Vector3 impactPoint,
            out Vector3 impactNormal);
        SpawnGroundImpactEffect(
            impactPrefab,
            impactPoint + impactNormal * footstepGroundNormalOffset,
            impactNormal,
            footstepGroundImpactScale);
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

        int selectedIndex = UnityEngine.Random.Range(0, validCount);
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

    private bool TryResolveGroundImpactPose(
        Transform anchor,
        float rayHeight,
        float rayDistance,
        LayerMask groundMask,
        out Vector3 impactPoint,
        out Vector3 impactNormal)
    {
        Vector3 anchorPosition = anchor != null ? anchor.position : transform.position;
        Vector3 rayOrigin = anchorPosition + Vector3.up * rayHeight;

        if (Physics.Raycast(
                rayOrigin,
                Vector3.down,
                out RaycastHit hit,
                rayDistance,
                groundMask,
                QueryTriggerInteraction.Ignore))
        {
            impactPoint = hit.point;
            impactNormal = hit.normal.sqrMagnitude > 0.0001f ? hit.normal.normalized : Vector3.up;
            return true;
        }

        impactPoint = anchorPosition;
        impactNormal = Vector3.up;
        return false;
    }

    private void ResolveFootImpactAnchors()
    {
        if (leftFootImpactAnchor == null)
        {
            leftFootImpactAnchor = FindChildRecursive(transform, "foot.l");
        }

        if (leftFootImpactAnchor == null)
        {
            leftFootImpactAnchor = FindChildRecursive(transform, "mixamorig:LeftFoot");
        }

        if (rightFootImpactAnchor == null)
        {
            rightFootImpactAnchor = FindChildRecursive(transform, "foot.r");
        }

        if (rightFootImpactAnchor == null)
        {
            rightFootImpactAnchor = FindChildRecursive(transform, "mixamorig:RightFoot");
        }
    }

    private static Transform FindChildRecursive(Transform root, string exactName)
    {
        if (root == null || string.IsNullOrWhiteSpace(exactName))
        {
            return null;
        }

        if (string.Equals(root.name, exactName, StringComparison.Ordinal))
        {
            return root;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindChildRecursive(root.GetChild(i), exactName);
            if (found != null)
            {
                return found;
            }
        }

        return null;
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
        effectInstance.transform.localScale = Vector3.one * Mathf.Max(0.1f, stoneHitImpactEffectScale);
    }

    private void SpawnGroundImpactEffect(GameObject effectPrefab, Vector3 impactPoint, Vector3 groundNormal, float scale)
    {
        if (effectPrefab == null)
        {
            return;
        }

        Vector3 up = groundNormal.sqrMagnitude > 0.0001f ? groundNormal.normalized : Vector3.up;
        Vector3 forward = Vector3.ProjectOnPlane(transform.forward, up);
        if (forward.sqrMagnitude <= 0.0001f)
        {
            forward = Vector3.ProjectOnPlane(transform.right, up);
        }

        if (forward.sqrMagnitude <= 0.0001f)
        {
            forward = Vector3.forward;
        }

        Quaternion spawnRotation = Quaternion.LookRotation(forward.normalized, up);
        GameObject effectInstance = Instantiate(effectPrefab, impactPoint, spawnRotation);
        effectInstance.transform.localScale = Vector3.one * Mathf.Max(0.1f, scale);
    }
}
