using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class PlayerHitReactionProfile
{
    public string stateName;
    [Range(0f, 1f)] public float recoveryNormalized = 0.9f;
    [Range(0f, 0.2f)] public float entryCrossFade = 0.03f;
    public bool allowRollBuffer = true;
}

public class PlayerController : CharacterBase
{
    private const string HandWeaponBoneName = "Bip001 R Hand";
    private const string BackWeaponBonePrimaryName = "Bip001 Spine2";
    private const string BackWeaponBoneSecondaryName = "Bip001 Spine1";
    private const string BackWeaponBoneFallbackName = "Bip001 Spine";
    private const float MovingStanceLocomotionCrossFadeDuration = 0.05f;
    private const float MovingStanceOverlayApplyNormalized = 0.4f;
    private const float LocomotionTransitionExitNormalized = 0.9f;
    private const float LocomotionTransitionFollowUpCrossFadeDuration = 0.05f;
    private const float HitMoveRecoveryIntentDuration = 0.2f;

    [Header("Debug")]
    [SerializeField] private bool enableGuardDebugLog = true;

    [Header("Data Settings")]
    public PlayerStatData statData;

    public StateMachine StateMachine { get; private set; }
    public PlayerIdleState IdleState { get; private set; }
    public PlayerMoveState MoveState { get; private set; }
    public PlayerRollState RollState { get; private set; }
    public PlayerAttackState AttackState { get; private set; }
    public PlayerParryState ParryState { get; private set; }
    public PlayerUseItemState UseItemState { get; private set; }
    public PlayerHitState HitState { get; private set; }
    public PlayerInputReader InputReader { get; private set; }
    public PlayerInputSnapshot InputSnapshot { get; private set; }
    public PlayerInteractionModule Interaction { get; private set; }
    public PlayerLocomotionMotor Locomotion { get; private set; }
    public PlayerStaminaModule Stamina { get; private set; }
    public PlayerStanceModule Stance { get; private set; }
    public PlayerCombatModule Combat { get; private set; }
    public PlayerAttackFacingModule AttackFacing { get; private set; }
    public PlayerParryModule Parry { get; private set; }
    public PlayerItemModule Item { get; private set; }
    public PlayerAnimatorDriver AnimatorDriver { get; private set; }
    public PlayerAnimatorLocomotionResolver AnimatorLocomotionResolver { get; private set; }

    public Vector2 InputVector { get; private set; }
    public Vector2 EffectiveLocomotionInput => ResolveEffectiveLocomotionInput();
    public bool HasEffectiveLocomotionInput => EffectiveLocomotionInput.sqrMagnitude > 0.0001f;
    public bool IsInvincible { get; set; }
    public bool IsParryWindowActive { get; private set; }
    public float CurrentStamina => Stamina != null ? Stamina.Current : 0f;
    public bool IsBattleStance => Stance != null && Stance.IsBattleStance;
    public bool IsUsingItem => Item != null && Item.IsUsingLegacyFlask;

    [Header("Combat Settings")]
    [SerializeField] private WeaponHitBox weaponHitBox;

    [Header("Stance Settings")]
    [SerializeField] private bool startInBattleStance = false;
    [SerializeField] private KeyCode battleStanceToggleKey = KeyCode.V;

    [Header("Hit Effect Settings")]
    [SerializeField] private Transform effectRoot;
    [SerializeField] private GameObject guardSuccessEffectPrefab;

    [Header("Hit Reaction Settings")]
    [SerializeField] private List<PlayerHitReactionProfile> hitReactionProfiles = new List<PlayerHitReactionProfile>();
    [SerializeField] private GameObject playerHitBloodEffectPrefab;

    [Header("Hit Knockback Settings")]
    [SerializeField] private bool enableHitKnockback = true;
    [SerializeField] [Min(0f)] private float defaultHitKnockbackDistance = 0.32f;
    [SerializeField] [Min(0.01f)] private float defaultHitKnockbackDuration = 0.08f;
    [SerializeField] [Min(0f)] private float strongHitKnockbackDistance = 0.46f;
    [SerializeField] [Min(0.01f)] private float strongHitKnockbackDuration = 0.1f;
    [SerializeField] [Min(0f)] private float launchHitKnockbackDistance = 0.6f;
    [SerializeField] [Min(0.01f)] private float launchHitKnockbackDuration = 0.12f;

    [Header("Footstep Audio Settings")]
    [SerializeField] private AudioSource footstepAudioSource;
    [SerializeField] private AudioClip[] footstepClips;
    [SerializeField] [Range(0f, 1f)] private float footstepVolume = 0.65f;
    [SerializeField] private Vector2 footstepPitchRange = new Vector2(0.95f, 1.05f);
    [SerializeField] private float footstepMinMoveSpeed = 0.15f;
    [SerializeField] private float footstepMinInterval = 0.08f;

    [Header("Player SFX Settings")]
    [SerializeField] private AudioSource playerSfxAudioSource;
    [SerializeField] private AudioClip[] attackClips;
    [SerializeField] private AudioClip rollClip;
    [SerializeField] private AudioClip[] incomingHitClips;
    [SerializeField] private AudioClip guardSuccessClip;
    [SerializeField] private AudioClip estusUseClip;
    [SerializeField] [Range(0f, 1f)] private float attackVolume = 0.9f;
    [SerializeField] [Range(0f, 1f)] private float rollVolume = 0.8f;
    [SerializeField] [Range(0f, 1f)] private float hitVolume = 0.9f;
    [SerializeField] [Range(0f, 1f)] private float guardSuccessVolume = 0.8f;
    [SerializeField] [Range(0f, 1f)] private float estusUseVolume = 0.85f;

    [Header("Attack Facing Settings")]
    [SerializeField] private float attackFacingTurnSpeed = 1080f;
    [Header("Attack Input Settings")]
    [SerializeField] [Min(0.05f)] private float attackHoldThreshold = 0.22f;

    public bool IsLockedOn { get; private set; }
    public Transform LockOnTarget { get; private set; }

    [Header("Flask Settings")]
    [SerializeField] private KeyCode flaskUseKey = KeyCode.R;
    [SerializeField] private KeyCode interactionKey = KeyCode.E;
    [SerializeField] private int maxFlaskCount = 3;
    [SerializeField] private float flaskHealAmount = 250f;
    [SerializeField] private float flaskUseDelay = 0.65f;
    [SerializeField] private float flaskAfterDelay = 0.35f;

    [Header("Estus Module Settings")]
    [SerializeField] private bool useEstusItemModule = true;
    [SerializeField] private PlayerEstusFlask estusFlask;

    [Header("Weapon Visual Settings")]
    [SerializeField] private GameObject handWeaponVisualObject;
    [SerializeField] private GameObject backWeaponVisualObject;
    [SerializeField] private string weaponVisualObjectName = "PlayerWeapon";

    public bool IsDead { get; private set; }
    public float CurrentHp => currentHp;
    public float MaxHp => maxHp;
    public float MaxStamina => Stamina != null ? Stamina.Max : (statData != null ? statData.maxStamina : 0f);
    public int CurrentFlaskCount => Item != null ? Item.CurrentFlaskCount : 0;
    public int MaxFlaskCount => Item != null ? Item.MaxFlaskCount : maxFlaskCount;
    public KeyCode InteractionKey => interactionKey;
    public bool IsBonfireInputLocked => _isBonfireInputLocked;
    public bool IsIntroInputLocked => IsIntroAnimationInputLocked();

    public event Action<float, float> OnPlayerHpChanged;
    public event Action<float, float> OnPlayerStaminaChanged;
    public event Action<int, int> OnFlaskChanged;
    public event Action OnPlayerDied;

    private bool _animatorMoveRunning;
    private bool _wasHitReactionAnimatorStateActive;
    private bool _pendingHeldMoveRecoveryAfterHit;
    private Vector2 _latchedHitMoveRecoveryInput;
    private bool _latchedHitMoveRecoverySprintHeld;
    private float _latchedHitMoveRecoveryExpireAt;
    private float _nextFootstepPlayableTime;
    private bool _hasPendingMovingBattleStance;
    private bool _pendingMovingBattleStanceValue;
    private bool _isBonfireInputLocked;
    private bool _useRollRootMotion;
    private Vector3 _pendingRollRootMotionDelta;
    private Quaternion _pendingRollRootMotionRotation = Quaternion.identity;
    private bool _hasPendingRollRootMotion;
    private bool _useAttackRootMotion;
    private Vector3 _pendingAttackRootMotionDelta;
    private Quaternion _pendingAttackRootMotionRotation = Quaternion.identity;
    private bool _hasPendingAttackRootMotion;
    private PlayerCam _playerCamera;
    private PlayerHitReactionProfile _currentHitReactionProfile;
    private bool _hasPendingHitKnockback;
    private Vector3 _pendingHitKnockbackDirection = Vector3.zero;
    private float _pendingHitKnockbackDistance;
    private float _pendingHitKnockbackDuration;
    private float _pendingHitKnockbackElapsed;

    protected override void Awake()
    {
        base.Awake();

        if (rb != null)
        {
            rb.interpolation = RigidbodyInterpolation.Interpolate;
        }

        InputReader = new PlayerInputReader(battleStanceToggleKey, flaskUseKey, interactionKey, attackHoldThreshold);
        Interaction = new PlayerInteractionModule(this, interactionKey);
        Locomotion = new PlayerLocomotionMotor(statData, rb, transform, col);
        Stamina = new PlayerStaminaModule(statData, HandleStaminaChanged);
        Stance = new PlayerStanceModule(HandleBattleStanceChanged);
        Combat = new PlayerCombatModule(this, weaponHitBox);
        AttackFacing = new PlayerAttackFacingModule(this, attackFacingTurnSpeed);
        Parry = new PlayerParryModule(this);
        Item = new PlayerItemModule(this, useEstusItemModule, maxFlaskCount, flaskHealAmount, flaskUseDelay, flaskAfterDelay);
        AnimatorDriver = new PlayerAnimatorDriver(Anim);
        AnimatorLocomotionResolver = new PlayerAnimatorLocomotionResolver();
        StateMachine = new StateMachine();
        IdleState = new PlayerIdleState(this, StateMachine, statData);
        MoveState = new PlayerMoveState(this, StateMachine, statData);
        RollState = new PlayerRollState(this, StateMachine, statData);
        AttackState = new PlayerAttackState(this, StateMachine, statData);
        ParryState = new PlayerParryState(this, StateMachine, statData);
        UseItemState = new PlayerUseItemState(this, StateMachine, statData);
        HitState = new PlayerHitState(this, StateMachine, statData);
        _playerCamera = GetComponent<PlayerCam>();
    }

    private void Start()
    {
        if (statData != null)
        {
            maxHp = statData.maxHP;
        }

        currentHp = maxHp;
        Stamina?.Initialize();

        if (estusFlask == null)
        {
            estusFlask = GetComponent<PlayerEstusFlask>();
        }

        ResolveEffectRoot();
        ResolveFootstepAudioSource();
        ResolvePlayerSfxAudioSource();
        EnsureAnimationEventBridge();
        SetRollRootMotionEnabled(false);

        Item?.Initialize(estusFlask);

        StateMachine.Initialize(IdleState);
        Stance?.Initialize(startInBattleStance);
        SyncWeaponVisualsToCurrentStance();
        OnPlayerHpChanged?.Invoke(currentHp, maxHp);
    }

    private void Update()
    {
        if (IsDead)
        {
            return;
        }

        if (Item != null && Item.IsUsingLegacyFlask)
        {
            UpdateAnimatorLocomotionSignals();
            RecoverStamina();
            return;
        }

        // 상태 클래스가 직접 Input을 읽지 않도록 매 프레임 입력 스냅샷을 갱신한다.
        if (InputReader != null)
        {
            InputSnapshot = InputReader.Capture();
            InputVector = InputSnapshot.Move;
            RefreshHitMoveRecoveryIntent();
        }
        else
        {
            InputSnapshot = default;
            InputVector = Vector2.zero;
        }

        if (_isBonfireInputLocked)
        {
            InputSnapshot = default;
            InputVector = Vector2.zero;
            SetVelocity(Vector3.zero);
            SetAnimatorMoveRunning(false);
            UpdateAnimatorLocomotionSignals();
            RecoverStamina();
            return;
        }

        if (IsIntroAnimationInputLocked())
        {
            InputSnapshot = default;
            InputVector = Vector2.zero;
            ClearHitMoveRecoveryIntent();
            ClearHorizontalVelocity();
            SetAnimatorMoveRunning(false);
            UpdateAnimatorLocomotionSignals();
            RecoverStamina();
            return;
        }

        TryApplyPendingMovingBattleStance();
        TryToggleBattleStanceInput();
        Interaction?.Tick(InputSnapshot, StateMachine.CurrentState);

        if (_isBonfireInputLocked)
        {
            InputSnapshot = default;
            InputVector = Vector2.zero;
            SetVelocity(Vector3.zero);
            SetAnimatorMoveRunning(false);
            UpdateAnimatorLocomotionSignals();
            RecoverStamina();
            return;
        }

        if (Item != null && Item.IsEstusModuleReady)
        {
            if (Item.TryUseEstusModuleInput(InputSnapshot, StateMachine.CurrentState))
            {
                UpdateAnimatorLocomotionSignals();
                RecoverStamina();
                return;
            }
        }
        else
        {
            Item?.TryUseLegacyFlaskInput(InputSnapshot, StateMachine.CurrentState);
        }

        StateMachine.CurrentState.Update();
        UpdateAnimatorLocomotionSignals();
        RecoverStamina();
    }

    private void FixedUpdate()
    {
        ApplyPendingRollRootMotion();
        ApplyPendingAttackRootMotion();
        ApplyPendingHitKnockback();

        if (IsDead ||
            _isBonfireInputLocked ||
            IsIntroAnimationInputLocked() ||
            (Item != null && Item.IsUsingLegacyFlask) ||
            IsHitReactionAnimatorStateActive())
        {
            return;
        }

        if (StateMachine.CurrentState == UseItemState && !IsMovingDrinkUpperActive())
        {
            return;
        }

        StateMachine.CurrentState.PhysicsUpdate();
    }

    public bool IsUsingRollRootMotion => _useRollRootMotion;
    public bool IsUsingAttackRootMotion => _useAttackRootMotion;

    private void LateUpdate()
    {
        if (IsDead || Combat == null)
        {
            return;
        }

        Combat.TickActiveHitScan();
    }

    public void SetVelocity(Vector3 velocity)
    {
        rb.velocity = velocity;
    }

    public void ClearHorizontalVelocity()
    {
        if (rb == null)
        {
            return;
        }

        Vector3 velocity = rb.velocity;
        velocity.x = 0f;
        velocity.z = 0f;
        rb.velocity = velocity;
    }

    public void SetRollRootMotionEnabled(bool enabled)
    {
        _useRollRootMotion = enabled;
        RefreshAnimatorRootMotionState();

        if (!enabled)
        {
            _pendingRollRootMotionDelta = Vector3.zero;
            _pendingRollRootMotionRotation = Quaternion.identity;
            _hasPendingRollRootMotion = false;
        }
    }

    public void SetAttackRootMotionEnabled(bool enabled)
    {
        _useAttackRootMotion = enabled;
        RefreshAnimatorRootMotionState();

        if (!enabled)
        {
            _pendingAttackRootMotionDelta = Vector3.zero;
            _pendingAttackRootMotionRotation = Quaternion.identity;
            _hasPendingAttackRootMotion = false;
        }
    }

    public void ApplyRollRootMotion()
    {
        if (!_useRollRootMotion || Anim == null || rb == null)
        {
            return;
        }

        Vector3 deltaPosition = Anim.deltaPosition;
        Quaternion deltaRotation = Anim.deltaRotation;

        _pendingRollRootMotionDelta += new Vector3(deltaPosition.x, 0f, deltaPosition.z);

        if (deltaRotation != Quaternion.identity)
        {
            _pendingRollRootMotionRotation *= deltaRotation;
        }

        _hasPendingRollRootMotion = true;
    }

    public void ApplyAttackRootMotion()
    {
        if (!_useAttackRootMotion || Anim == null || rb == null)
        {
            return;
        }

        Vector3 deltaPosition = Anim.deltaPosition;
        Quaternion deltaRotation = Anim.deltaRotation;

        _pendingAttackRootMotionDelta += new Vector3(deltaPosition.x, 0f, deltaPosition.z);

        if (deltaRotation != Quaternion.identity)
        {
            _pendingAttackRootMotionRotation *= deltaRotation;
        }

        _hasPendingAttackRootMotion = true;
    }

    public void MoveWithSpeed(Vector3 direction, float speed, bool rotateToMoveDirection)
    {
        if (Locomotion == null)
        {
            return;
        }

        // 기존 외부 호출 호환을 위해 컨트롤러 래퍼는 유지하고 실제 계산은 모터에 위임한다.
        Locomotion.ApplyVelocity(direction, speed, rotateToMoveDirection, Time.deltaTime);
    }

    private void ApplyPendingRollRootMotion()
    {
        if (!_useRollRootMotion || !_hasPendingRollRootMotion || rb == null)
        {
            return;
        }

        if (_pendingRollRootMotionDelta.sqrMagnitude > 0f)
        {
            rb.MovePosition(rb.position + _pendingRollRootMotionDelta);
        }

        if (_pendingRollRootMotionRotation != Quaternion.identity)
        {
            rb.MoveRotation(rb.rotation * _pendingRollRootMotionRotation);
        }

        _pendingRollRootMotionDelta = Vector3.zero;
        _pendingRollRootMotionRotation = Quaternion.identity;
        _hasPendingRollRootMotion = false;
    }

    private void ApplyPendingAttackRootMotion()
    {
        if (!_useAttackRootMotion || !_hasPendingAttackRootMotion || rb == null)
        {
            return;
        }

        if (_pendingAttackRootMotionDelta.sqrMagnitude > 0f)
        {
            rb.MovePosition(rb.position + _pendingAttackRootMotionDelta);
        }

        if (_pendingAttackRootMotionRotation != Quaternion.identity)
        {
            rb.MoveRotation(rb.rotation * _pendingAttackRootMotionRotation);
        }

        _pendingAttackRootMotionDelta = Vector3.zero;
        _pendingAttackRootMotionRotation = Quaternion.identity;
        _hasPendingAttackRootMotion = false;
    }

    public override void Move(Vector3 dir)
    {
        base.Move(dir);
    }

    public void SetLockOnTarget(Transform target)
    {
        bool shouldRefreshMovingLocomotion = target != null &&
                                             StateMachine != null &&
                                             StateMachine.CurrentState == MoveState &&
                                             !SupportsAnimatorDrivenLocomotion();

        LockOnTarget = target;
        IsLockedOn = target != null;
        if (IsLockedOn)
        {
            ForceBattleStance();
            if (shouldRefreshMovingLocomotion)
            {
                CrossFadeToMoveLocomotion(MovingStanceLocomotionCrossFadeDuration);
            }
        }
    }

    public void ClearLockOnTarget()
    {
        bool shouldRefreshMovingLocomotion = IsLockedOn &&
                                             StateMachine != null &&
                                             StateMachine.CurrentState == MoveState &&
                                             !SupportsAnimatorDrivenLocomotion();

        LockOnTarget = null;
        IsLockedOn = false;

        if (shouldRefreshMovingLocomotion)
        {
            CrossFadeToMoveLocomotion(MovingStanceLocomotionCrossFadeDuration);
        }
    }

    public void ForceUnlockCameraLockOn(bool silentLog = true)
    {
        if (_playerCamera == null)
        {
            _playerCamera = GetComponent<PlayerCam>();
        }

        if (_playerCamera != null)
        {
            _playerCamera.ForceUnlock(silentLog);
            return;
        }

        LockOnSystem lockOnSystem = GetComponent<LockOnSystem>();
        if (lockOnSystem != null)
        {
            lockOnSystem.ClearLockOn();
        }

        ClearLockOnTarget();
    }

    public void SyncCameraTargetsImmediate()
    {
        if (_playerCamera == null)
        {
            _playerCamera = GetComponent<PlayerCam>();
        }

        _playerCamera?.SyncCameraTargetsImmediate();
    }

    public bool CanStartAttack()
    {
        if (statData == null || !statData.HasAttackDefinitions)
        {
            return false;
        }

        string attackId = ResolveAttackEntryId();
        if (!string.IsNullOrWhiteSpace(attackId))
        {
            return CurrentStamina >= statData.GetAttackStaminaCost(attackId);
        }

        return CurrentStamina >= statData.GetAttackStaminaCost(0);
    }

    public bool HasAttackStartInput()
    {
        return InputSnapshot.AttackShortReleased || InputSnapshot.AttackLongPressTriggered;
    }

    public bool HasParryStartInput()
    {
        return InputSnapshot.ParryPressed;
    }

    public bool CanStartParry()
    {
        return statData != null &&
               Stamina != null &&
               CurrentStamina >= statData.parryStaminaCost;
    }

    public string ResolveAttackEntryId()
    {
        if (InputSnapshot.AttackLongPressTriggered)
        {
            return PlayerAnimatorStates.Attack3;
        }

        return null;
    }

    public bool TryUseStamina(float amount)
    {
        if (amount <= 0f)
        {
            return true;
        }

        if (Stamina == null)
        {
            return false;
        }

        return Stamina.TryUse(amount);
    }

    private void RecoverStamina()
    {
        if (Stamina == null)
        {
            return;
        }

        Stamina.Recover(Time.deltaTime, false);
    }

    public override void TakeDamage(int amount, Vector3 hitPoint, Vector3 hitNormal)
    {
        if (IsDead)
        {
            return;
        }

        if (IsInvincible)
        {
            Debug.Log("Invincible: ignored damage.");
            return;
        }

        CombatAttackDefinition incomingAttack = CombatDamageContext.CurrentAttack;

        PlayerParryHitResult parryResult = Parry != null
            ? Parry.HandleIncomingDamage(amount, hitPoint, hitNormal)
            : new PlayerParryHitResult(false, false);

        if (parryResult.ParryConsumedDamage)
        {
            return;
        }

        ApplyDirectDamage(amount, hitPoint, hitNormal);
        if (IsDead)
        {
            return;
        }

        if (Combat != null && Combat.IsSuperArmorActive)
        {
            PlayIncomingHitFeedback(incomingAttack, hitPoint, hitNormal, 0.04f, 0.08f, 0.18f);
            PlayRandomPlayerSfx(incomingHitClips, hitVolume);
            Debug.Log("Super armor absorbed hit interruption.");
            return;
        }

        CaptureHitMoveRecoveryIntent();
        HandleFullHitRuntimeCleanup();
        ClearHorizontalVelocity();
        SetAnimatorMoveRunning(false);
        QueueHitKnockback(incomingAttack, hitPoint, hitNormal);

        PlayHitReactionAnimation(hitPoint, hitNormal);

        PlayIncomingHitFeedback(incomingAttack, hitPoint, hitNormal, 0.1f, 0.2f, 0.5f);
        PlayRandomPlayerSfx(incomingHitClips, hitVolume);

        EnterHitState();
    }

    public void Hit()
    {
        Combat?.Hit();
    }

    public void Run()
    {
    }

    public void Roll()
    {
        PlayPlayerSfx(rollClip, rollVolume);
    }

    public void OnFootstepAnimationEvent(string footSide)
    {
        if (!CanPlayFootstepAudio())
        {
            return;
        }

        AudioClip clip = GetRandomFootstepClip();
        if (clip == null)
        {
            return;
        }

        // 좌우 발 구분은 추후 지면/이펙트 분리 때 확장하고, 현재는 공통 발소리만 재생한다.
        footstepAudioSource.pitch = UnityEngine.Random.Range(footstepPitchRange.x, footstepPitchRange.y);
        footstepAudioSource.PlayOneShot(clip, footstepVolume);
        _nextFootstepPlayableTime = Time.time + footstepMinInterval;
    }

    public void HitStart()
    {
        Combat?.OnAnimationHitStart();
    }

    public void HitEnd()
    {
        Combat?.OnAnimationHitEnd();
    }

    public void ResetCombatRuntimeState()
    {
        Combat?.ResetRuntimeState();
        AttackState?.ResetComboChainRuntime();
        ParryState?.ResetRuntimeState();
        SetParryWindowActive(false);
        SetAttackRootMotionEnabled(false);
        ResetAttackAnimatorRuntimeState();
        ClearPendingHitKnockback();
    }

    public void BeginAttackFacing(float trackUntilNormalized, float facingLockNormalized)
    {
        AttackFacing?.Begin(trackUntilNormalized, facingLockNormalized);
    }

    public void UpdateAttackFacing(float normalizedTime)
    {
        AttackFacing?.Update(normalizedTime);
    }

    public void EndAttackFacing()
    {
        AttackFacing?.End();
    }

    public void CrossFadeToLocomotion(float duration)
    {
        if (AnimatorDriver == null || AnimatorLocomotionResolver == null)
        {
            return;
        }

        string stateName = ResolveAvailableLocomotionStateName();
        if (string.IsNullOrEmpty(stateName))
        {
            return;
        }

        AnimatorDriver.TryCrossFadeToState(stateName, duration, 0, 0f);
    }

    public void CrossFadeToMoveLocomotion(float duration)
    {
        CrossFadeToMoveLocomotion(duration, ResolveAnimatorRunSignal());
    }

    public void CrossFadeToMoveLocomotion(float duration, bool isRunning)
    {
        if (AnimatorDriver == null || AnimatorLocomotionResolver == null)
        {
            return;
        }

        string stateName = AnimatorLocomotionResolver.ResolveMoveState(IsBattleStance, IsLockedOn, isRunning);
        if (AnimatorDriver.HasState(stateName))
        {
            AnimatorDriver.TryCrossFadeToState(stateName, duration, 0, 0f);
            return;
        }

        if (IsLockedOn && AnimatorDriver.HasState(PlayerAnimatorStates.RunLockOn))
        {
            AnimatorDriver.TryCrossFadeToState(PlayerAnimatorStates.RunLockOn, duration, 0, 0f);
            return;
        }

        if (AnimatorDriver.HasState(PlayerAnimatorStates.Run))
        {
            AnimatorDriver.TryCrossFadeToState(PlayerAnimatorStates.Run, duration, 0, 0f);
            return;
        }

        CrossFadeToLocomotion(duration);
    }

    public void CrossFadeToMoveEntryLocomotion(float duration, bool isRunning)
    {
        CrossFadeToMoveLocomotion(duration, isRunning);
    }

    public void SetBattleStance(bool isBattleStance)
    {
        CancelPendingMovingBattleStance();
        Stance?.SetBattleStance(isBattleStance);
    }

    // 공격/가드/락온처럼 전투 흐름에서 자세를 강제로 맞출 때 사용한다.
    public void ForceBattleStance()
    {
        CancelPendingMovingBattleStance();
        Stance?.ForceBattleStance();
    }

    public void SetGuarding(bool isGuarding)
    {
        if (!isGuarding)
        {
            SetParryWindowActive(false);
            AnimatorDriver?.TrySetGuard(false);
        }
    }

    public void BeginBonfireInteractionLock()
    {
        BeginExternalInteractionLock();
    }

    public void EndBonfireInteractionLock()
    {
        EndExternalInteractionLock();
    }

    public void BeginExternalInteractionLock()
    {
        _isBonfireInputLocked = true;
        CancelPendingMovingBattleStance();
        ClearHitMoveRecoveryIntent();
        SetGuarding(false);
        ResetCombatRuntimeState();
        EndAttackFacing();
        SetVelocity(Vector3.zero);
        SetAnimatorMoveRunning(false);
        InputSnapshot = default;
        InputVector = Vector2.zero;

        if (StateMachine != null && StateMachine.CurrentState != IdleState)
        {
            StateMachine.ChangeState(IdleState);
        }
    }

    public void EndExternalInteractionLock()
    {
        _isBonfireInputLocked = false;
        ClearHitMoveRecoveryIntent();
        SetAnimatorMoveRunning(false);
        InputSnapshot = default;
        InputVector = Vector2.zero;
        SetVelocity(Vector3.zero);
    }

    public void SetAnimatorMoveRunning(bool isRunning)
    {
        _animatorMoveRunning = isRunning;
    }

    public bool ShouldUseRunningLocomotionFromIntent()
    {
        if (!HasEffectiveLocomotionInput)
        {
            return false;
        }

        if (InputSnapshot.SprintHeld)
        {
            return CurrentStamina > 0f;
        }

        return HasLatchedHitMoveRecoveryIntent() && _latchedHitMoveRecoverySprintHeld && CurrentStamina > 0f;
    }

    public void RespawnFromCheckpoint(Vector3 position, Quaternion rotation)
    {
        StopAllCoroutines();
        Interaction?.ClearCandidates();
        ResetCombatRuntimeState();
        EndAttackFacing();

        transform.SetPositionAndRotation(position, rotation);
        SetVelocity(Vector3.zero);

        IsDead = false;
        IsInvincible = false;

        currentHp = maxHp;
        Stamina?.Refill();
        Item?.HandleRespawnRefill();

        ClearLockOnTarget();
        SetGuarding(false);
        StateMachine.ChangeState(IdleState);

        OnPlayerHpChanged?.Invoke(currentHp, maxHp);
        Debug.Log("Respawned at checkpoint.");
    }

    protected override void Die()
    {
        if (IsDead)
        {
            return;
        }

        IsDead = true;
        Interaction?.ClearCandidates();
        Item?.HandleDeath();

        SetVelocity(Vector3.zero);
        ResetCombatRuntimeState();
        EndAttackFacing();
        SetGuarding(false);
        ClearPendingHitKnockback();

        if (!TrySetAnimatorTrigger(PlayerAnimatorParams.DeadTrigger, PlayerAnimatorParams.DeadTriggerName))
        {
            Debug.LogWarning("Animator does not have tDead. Only death flow will run.");
        }

        Debug.Log("YOU DIED");
        OnPlayerDied?.Invoke();
        GameLoopEvents.RaisePlayerDied(this);
    }

    public PlayerEstusFlask GetEstusFlask()
    {
        return Item != null ? Item.EstusFlask : estusFlask;
    }

    public void Heal(float amount)
    {
        if (amount <= 0f || IsDead)
        {
            return;
        }

        currentHp = Mathf.Clamp(currentHp + amount, 0f, maxHp);
        OnPlayerHpChanged?.Invoke(currentHp, maxHp);
    }

    public void RevertHeal(float amount)
    {
        if (amount <= 0f || IsDead)
        {
            return;
        }

        currentHp = Mathf.Clamp(currentHp - amount, 0f, maxHp);
        OnPlayerHpChanged?.Invoke(currentHp, maxHp);
    }

    public void NotifyStaminaChanged()
    {
        OnPlayerStaminaChanged?.Invoke(CurrentStamina, MaxStamina);
    }

    public void PublishFlaskChanged(int current, int max)
    {
        OnFlaskChanged?.Invoke(current, max);
        GameLoopEvents.RaiseEstusChanged(current, max);
    }

    public void ApplyDirectDamage(int amount, Vector3 hitPoint, Vector3 hitNormal, bool notifyStamina = true)
    {
        base.TakeDamage(amount, hitPoint, hitNormal);
        OnPlayerHpChanged?.Invoke(currentHp, maxHp);
        if (notifyStamina)
        {
            OnPlayerStaminaChanged?.Invoke(CurrentStamina, MaxStamina);
        }
    }

    public void PlayAttackHitFeedback(CombatAttackDefinition attack, CombatHitResult hitResult)
    {
        if (!hitResult.HasHit)
        {
            return;
        }

        float hitStopDuration = ResolveCombatHitStopDuration(attack, attack != null && attack.isHeavyAttack ? 0.08f : 0.05f);
        float shakeDuration = ResolveCombatShakeDuration(attack, 0.06f, 0.1f);
        float shakeIntensity = ResolveCombatShakeIntensity(attack, 0.08f, 0.16f);

        if (hitStopDuration > 0f)
        {
            EffectManager.Instance.DoHitStop(hitStopDuration);
        }

        if (shakeIntensity > 0f)
        {
            EffectManager.Instance.DoCameraShake(shakeDuration, shakeIntensity);
        }
    }

    public void SetParryWindowActive(bool isActive)
    {
        IsParryWindowActive = isActive;
    }

    public void NotifySuccessfulParry(CombatAttackDefinition incomingAttack, Transform attacker, Vector3 hitPoint, Vector3 hitNormal)
    {
        SetParryWindowActive(false);
        PlayParrySuccessFeedback(incomingAttack);
        TryParryAttacker(attacker, incomingAttack, hitPoint, hitNormal);
    }

    public void PlayParrySuccessFeedback(CombatAttackDefinition incomingAttack = null)
    {
        float hitStopDuration = ResolveCombatHitStopDuration(incomingAttack, 0.05f);
        float shakeDuration = ResolveCombatShakeDuration(incomingAttack, 0.08f, 0.1f);
        float shakeIntensity = ResolveCombatShakeIntensity(incomingAttack, 0.12f, 0.16f);

        if (hitStopDuration > 0f)
        {
            EffectManager.Instance.DoHitStop(hitStopDuration);
        }

        if (shakeIntensity > 0f)
        {
            EffectManager.Instance.DoCameraShake(shakeDuration, shakeIntensity);
        }

        PlayGuardSuccessEffect();
        PlayPlayerSfx(guardSuccessClip, guardSuccessVolume);

        bool playedSuccessState = AnimatorDriver != null &&
                                  AnimatorDriver.TryCrossFadeToState(PlayerAnimatorStates.ParrySuccess, 0.02f, 0, 0f);
        if (!playedSuccessState)
        {
            TrySetAnimatorTrigger(PlayerAnimatorParams.GuardChipHitTrigger, PlayerAnimatorParams.GuardChipHitTriggerName);
        }
    }

    public void LogParryMessage(string message)
    {
        if (!enableGuardDebugLog)
        {
            return;
        }

        Debug.Log(message);
    }

    private bool TryParryAttacker(Transform attacker, CombatAttackDefinition incomingAttack, Vector3 hitPoint, Vector3 hitNormal)
    {
        if (attacker == null)
        {
            return false;
        }

        IParryResponder responder = attacker.GetComponentInParent<IParryResponder>();
        return responder != null && responder.OnParried(incomingAttack, transform, hitPoint, hitNormal);
    }

    public bool TrySetAnimatorTrigger(int triggerHash, string triggerName)
    {
        if (AnimatorDriver == null)
        {
            return false;
        }

        return AnimatorDriver.TrySetTrigger(triggerHash, triggerName);
    }

    public bool TrySetAnimatorTrigger(string triggerName)
    {
        return TrySetAnimatorTrigger(Animator.StringToHash(triggerName), triggerName);
    }

    public bool TriggerEstusDrinkAnimation(bool useUpperBodyDrink)
    {
        if (AnimatorDriver == null)
        {
            return false;
        }

        AnimatorDriver.ResetTrigger(PlayerAnimatorParams.DrinkTrigger);
        AnimatorDriver.ResetTrigger(PlayerAnimatorParams.DrinkUpperTrigger);

        if (useUpperBodyDrink)
        {
            return TrySetAnimatorTrigger(PlayerAnimatorParams.DrinkUpperTrigger, PlayerAnimatorParams.DrinkUpperTriggerName);
        }

        return TrySetAnimatorTrigger(PlayerAnimatorParams.DrinkTrigger, PlayerAnimatorParams.DrinkTriggerName);
    }

    public bool TrySetAnimatorBool(int boolHash, string boolName, bool value)
    {
        if (AnimatorDriver == null)
        {
            return false;
        }

        return AnimatorDriver.TrySetBool(boolHash, boolName, value);
    }

    public void SyncEstusCountFromModule()
    {
        Item?.SyncEstusCountFromModule();
    }

    public void OnEstusHealApplyAnimationEvent()
    {
        if (StateMachine.CurrentState == UseItemState && UseItemState != null)
        {
            UseItemState.OnEstusHealApplyAnimationEvent();
        }

        PlayPlayerSfx(estusUseClip, estusUseVolume);
    }

    public void ShowEstusVisualAnimationEvent()
    {
        estusFlask?.ShowHeldVisual();
    }

    public void HideEstusVisualAnimationEvent()
    {
        estusFlask?.HideHeldVisual();
    }

    public void ShowHandWeaponAnimationEvent()
    {
        ResolveWeaponVisualObjects();
        SetWeaponVisualStates(true, false);
    }

    public void ShowBackWeaponAnimationEvent()
    {
        ResolveWeaponVisualObjects();
        SetWeaponVisualStates(false, true);
    }

    private string GetPreferredLocomotionStateName()
    {
        if (AnimatorLocomotionResolver == null)
        {
            return IsBattleStance ? PlayerAnimatorStates.IdleB : PlayerAnimatorStates.IdleA;
        }

        return ResolveAvailableLocomotionStateName();
    }

    private string ResolveAvailableLocomotionStateName()
    {
        bool isRunning = ResolveAnimatorRunSignal();
        string preferredState = AnimatorLocomotionResolver.ResolvePreferredLocomotionState(IsBattleStance, IsLockedOn, InputVector, isRunning);
        if (AnimatorDriver != null && AnimatorDriver.HasState(preferredState))
        {
            return preferredState;
        }

        if (InputVector.sqrMagnitude > 0.0001f)
        {
            if (IsLockedOn && AnimatorDriver != null && AnimatorDriver.HasState(PlayerAnimatorStates.RunLockOn))
            {
                return PlayerAnimatorStates.RunLockOn;
            }

            string walkState = IsBattleStance ? PlayerAnimatorStates.WalkB : PlayerAnimatorStates.WalkA;
            string runState = IsBattleStance ? PlayerAnimatorStates.RunB : PlayerAnimatorStates.RunA;

            if (isRunning && AnimatorDriver != null && AnimatorDriver.HasState(runState))
            {
                return runState;
            }

            if (AnimatorDriver != null && AnimatorDriver.HasState(walkState))
            {
                return walkState;
            }

            if (AnimatorDriver != null && AnimatorDriver.HasState(runState))
            {
                return runState;
            }

            if (AnimatorDriver != null && AnimatorDriver.HasState(PlayerAnimatorStates.Run))
            {
                return PlayerAnimatorStates.Run;
            }
        }

        return IsBattleStance ? PlayerAnimatorStates.IdleB : PlayerAnimatorStates.IdleA;
    }

    private bool TryResolveFreeMoveDirection(Vector2 input, out Vector3 moveDirection)
    {
        moveDirection = Vector3.zero;

        Transform cameraTransform = Define.MainCam != null ? Define.MainCam.transform : null;
        if (cameraTransform == null)
        {
            return false;
        }

        Vector3 cameraForward = cameraTransform.forward;
        Vector3 cameraRight = cameraTransform.right;
        cameraForward.y = 0f;
        cameraRight.y = 0f;

        moveDirection = cameraForward.normalized * input.y + cameraRight.normalized * input.x;
        if (moveDirection.sqrMagnitude <= 0.0001f)
        {
            moveDirection = Vector3.zero;
            return false;
        }

        moveDirection.Normalize();
        return true;
    }

    private bool HasAnimatorTrigger(string triggerName)
    {
        return AnimatorDriver != null && AnimatorDriver.HasTrigger(triggerName);
    }

    public bool SupportsUpperBodyDrinkOverlay()
    {
        return HasAnimatorTrigger(PlayerAnimatorParams.DrinkUpperTriggerName) &&
               AnimatorDriver != null &&
               AnimatorDriver.HasState(PlayerAnimatorStates.DrinkUpper, 1);
    }

    private void ResetAttackAnimatorRuntimeState()
    {
        if (AnimatorDriver == null)
        {
            return;
        }

        AnimatorDriver.ResetTrigger(PlayerAnimatorParams.AttackTrigger);
        AnimatorDriver.TrySetBool(PlayerAnimatorParams.IsAttack, PlayerAnimatorParams.IsAttackName, false);
    }

    private void HandleFullHitRuntimeCleanup()
    {
        ResetCombatRuntimeState();
        EndAttackFacing();
        Item?.InterruptActiveUse();
    }

    private bool HasAnimatorBool(string boolName)
    {
        return AnimatorDriver != null && AnimatorDriver.HasBool(boolName);
    }

    public bool SupportsAnimatorDrivenLocomotion()
    {
        return HasAnimatorBool(PlayerAnimatorParams.IsMoveName);
    }

    private void RefreshAnimatorRootMotionState()
    {
        if (Anim == null)
        {
            return;
        }

        Anim.applyRootMotion = _useRollRootMotion || _useAttackRootMotion;
    }

    private void TryToggleBattleStanceInput()
    {
        if (!InputSnapshot.ToggleBattleStancePressed)
        {
            return;
        }

        if (StateMachine == null || Stance == null)
        {
            return;
        }

        bool isIdleState = StateMachine.CurrentState == IdleState;
        bool isMoveState = StateMachine.CurrentState == MoveState;
        bool isInCombatFlow = StateMachine.CurrentState == AttackState ||
                              StateMachine.CurrentState == ParryState ||
                              StateMachine.CurrentState == RollState ||
                              StateMachine.CurrentState == UseItemState;

        if (_hasPendingMovingBattleStance)
        {
            return;
        }

        if (!Stance.CanToggleManually(isIdleState || isMoveState, IsLockedOn, isInCombatFlow))
        {
            return;
        }

        bool wasBattleStance = IsBattleStance;
        bool targetBattleStance = !wasBattleStance;

        if (isMoveState)
        {
            QueuePendingMovingBattleStance(targetBattleStance);
            TriggerMovingStanceOverlay(wasBattleStance, targetBattleStance);
            return;
        }

        Stance.SetBattleStance(targetBattleStance);
    }

    private void TriggerMovingStanceOverlay(bool previousBattleStance, bool currentBattleStance)
    {
        if (previousBattleStance == currentBattleStance)
        {
            return;
        }

        if (currentBattleStance)
        {
            TrySetAnimatorTrigger(PlayerAnimatorParams.EquipTrigger, PlayerAnimatorParams.EquipTriggerName);
            return;
        }

        TrySetAnimatorTrigger(PlayerAnimatorParams.UnequipTrigger, PlayerAnimatorParams.UnequipTriggerName);
    }

    private void QueuePendingMovingBattleStance(bool targetBattleStance)
    {
        _hasPendingMovingBattleStance = true;
        _pendingMovingBattleStanceValue = targetBattleStance;
    }

    private void CancelPendingMovingBattleStance()
    {
        _hasPendingMovingBattleStance = false;
    }

    private void TryApplyPendingMovingBattleStance()
    {
        if (!_hasPendingMovingBattleStance || Stance == null)
        {
            return;
        }

        if (StateMachine == null || StateMachine.CurrentState != MoveState)
        {
            ApplyPendingMovingBattleStance();
            return;
        }

        if (AnimatorDriver == null || !AnimatorDriver.IsReady)
        {
            ApplyPendingMovingBattleStance();
            return;
        }

        bool isEquipActive = AnimatorDriver.IsCurrentState(PlayerAnimatorStates.EquipUpper, 1);
        bool isUnequipActive = AnimatorDriver.IsCurrentState(PlayerAnimatorStates.UnequipUpper, 1);
        if (isEquipActive || isUnequipActive)
        {
            if (AnimatorDriver.GetCurrentStateNormalizedTime(1) < MovingStanceOverlayApplyNormalized)
            {
                return;
            }

            ApplyPendingMovingBattleStance();
            return;
        }

        if (AnimatorDriver.IsInTransition(1))
        {
            return;
        }

        ApplyPendingMovingBattleStance();
    }

    private void ApplyPendingMovingBattleStance()
    {
        bool targetBattleStance = _pendingMovingBattleStanceValue;
        _hasPendingMovingBattleStance = false;

        Stance?.SetBattleStance(targetBattleStance);

        if (StateMachine != null &&
            StateMachine.CurrentState == MoveState &&
            !SupportsAnimatorDrivenLocomotion())
        {
            CrossFadeToMoveLocomotion(MovingStanceLocomotionCrossFadeDuration);
        }
    }

    // 현재 입력과 스태미나 기준으로 이동 가속 여부를 판단한다.
    private bool ShouldUseRunningLocomotion()
    {
        if (InputVector.sqrMagnitude <= 0.0001f)
        {
            return false;
        }

        return InputSnapshot.SprintHeld && CurrentStamina > 0f;
    }

    // 스태미나 변경 이벤트를 한곳에서 관리해 다른 시스템 연결 지점을 유지한다.
    private void UpdateAnimatorLocomotionSignals()
    {
        if (AnimatorDriver == null || !AnimatorDriver.IsReady)
        {
            return;
        }

        bool isHitReactionActive = IsHitReactionAnimatorStateActive();
        if (_wasHitReactionAnimatorStateActive && !isHitReactionActive && HasEffectiveLocomotionInput)
        {
            _pendingHeldMoveRecoveryAfterHit = true;
        }

        AnimatorDriver.SetLocomotionInput(ResolveAnimatorLocomotionInput(), 0.1f, Time.deltaTime);
        AnimatorDriver.TrySetMove(ResolveAnimatorMoveSignal());
        AnimatorDriver.TrySetBool(PlayerAnimatorParams.IsRun, PlayerAnimatorParams.IsRunName, ResolveAnimatorRunSignal());
        AnimatorDriver.TrySetLockOn(IsLockedOn);
        TryForceExitCompletedHitReaction();
        TryRecoverHeldMoveAfterHit();
        _wasHitReactionAnimatorStateActive = isHitReactionActive;
    }

    private bool ResolveAnimatorMoveSignal()
    {
        if (!HasEffectiveLocomotionInput)
        {
            return false;
        }

        return (StateMachine != null &&
                (StateMachine.CurrentState == MoveState || IsMovingDrinkUpperActive())) ||
               IsHitReactionAnimatorStateActive();
    }

    private Vector2 ResolveAnimatorLocomotionInput()
    {
        if (StateMachine != null && StateMachine.CurrentState == RollState)
        {
            if (IsLockedOn)
            {
                return HasEffectiveLocomotionInput ? EffectiveLocomotionInput : Vector2.down;
            }

            return Vector2.up;
        }

        return EffectiveLocomotionInput;
    }

    private bool ResolveAnimatorRunSignal()
    {
        if (!ResolveAnimatorMoveSignal())
        {
            return false;
        }

        if (IsHitReactionAnimatorStateActive())
        {
            return ShouldUseRunningLocomotionFromIntent();
        }

        return _animatorMoveRunning;
    }

    private bool IsMovingDrinkUpperActive()
    {
        return StateMachine != null &&
               StateMachine.CurrentState == UseItemState &&
               UseItemState != null &&
               UseItemState.UsesUpperBodyDrinkOverlay;
    }

    public bool IsHitReactionAnimatorStateActive()
    {
        return AnimatorDriver != null &&
               (AnimatorDriver.IsCurrentState(PlayerAnimatorStates.HitFront) ||
                AnimatorDriver.IsCurrentState(PlayerAnimatorStates.HitBack) ||
                AnimatorDriver.IsCurrentState(PlayerAnimatorStates.HitLeft) ||
                AnimatorDriver.IsCurrentState(PlayerAnimatorStates.HitRight) ||
                AnimatorDriver.IsCurrentState(PlayerAnimatorStates.HitSlam) ||
                AnimatorDriver.IsCurrentState(PlayerAnimatorStates.HitDown) ||
                AnimatorDriver.IsCurrentState(PlayerAnimatorStates.HitDash) ||
                AnimatorDriver.IsCurrentState(PlayerAnimatorStates.HitSmash) ||
                AnimatorDriver.IsCurrentState(PlayerAnimatorStates.HitSpear));
    }

    private void PlayHitReactionAnimation(Vector3 hitPoint, Vector3 hitNormal)
    {
        string hitReactionState = ResolveHitReactionState(hitPoint, hitNormal);
        if (string.IsNullOrWhiteSpace(hitReactionState))
        {
            hitReactionState = PlayerAnimatorStates.HitFront;
        }

        _currentHitReactionProfile = ResolveHitReactionProfile(hitReactionState);
        float crossFadeDuration = ResolveHitReactionCrossFade(_currentHitReactionProfile);

        if (AnimatorDriver != null &&
            AnimatorDriver.TryCrossFadeToState(hitReactionState, crossFadeDuration, 0, 0f))
        {
            return;
        }

        if (hitReactionState == PlayerAnimatorStates.HitFront)
        {
            TrySetAnimatorTrigger(PlayerAnimatorParams.HitFrontTrigger, PlayerAnimatorParams.HitFrontTriggerName);
            return;
        }

        TrySetAnimatorTrigger(PlayerAnimatorParams.HitSideBackTrigger, PlayerAnimatorParams.HitSideBackTriggerName);
    }

    private string ResolveHitReactionState(Vector3 hitPoint, Vector3 hitNormal)
    {
        string specialReactionState = ResolveBossSpecificHitReactionState();
        if (!string.IsNullOrWhiteSpace(specialReactionState))
        {
            return specialReactionState;
        }

        return ResolveDirectionalHitReactionState(hitPoint, hitNormal);
    }

    private string ResolveBossSpecificHitReactionState()
    {
        CombatAttackDefinition incomingAttack = CombatDamageContext.CurrentAttack;
        if (incomingAttack == null)
        {
            return null;
        }

        switch (incomingAttack.playerHitReactionType)
        {
            case PlayerHitReactionType.Slam:
            case PlayerHitReactionType.Down:
                return PlayerAnimatorStates.HitDown;
            case PlayerHitReactionType.Dash:
                return PlayerAnimatorStates.HitDash;
            case PlayerHitReactionType.Smash:
                return PlayerAnimatorStates.HitSmash;
            case PlayerHitReactionType.Spear:
                return PlayerAnimatorStates.HitSpear;
            default:
                return null;
        }
    }

    private string ResolveDirectionalHitReactionState(Vector3 hitPoint, Vector3 hitNormal)
    {
        Vector3 toAttacker = hitNormal.sqrMagnitude > 0.0001f
            ? -hitNormal
            : hitPoint - transform.position;

        toAttacker.y = 0f;
        if (toAttacker.sqrMagnitude <= 0.0001f)
        {
            return PlayerAnimatorStates.HitFront;
        }

        float signedAngle = Vector3.SignedAngle(transform.forward, toAttacker.normalized, Vector3.up);
        float absoluteAngle = Mathf.Abs(signedAngle);

        if (absoluteAngle <= 45f)
        {
            return PlayerAnimatorStates.HitFront;
        }

        if (absoluteAngle >= 135f)
        {
            return PlayerAnimatorStates.HitBack;
        }

        return signedAngle > 0f ? PlayerAnimatorStates.HitRight : PlayerAnimatorStates.HitLeft;
    }

    private PlayerHitReactionProfile ResolveHitReactionProfile(string stateName)
    {
        if (!string.IsNullOrWhiteSpace(stateName))
        {
            for (int i = 0; i < hitReactionProfiles.Count; i++)
            {
                PlayerHitReactionProfile profile = hitReactionProfiles[i];
                if (profile == null || string.IsNullOrWhiteSpace(profile.stateName))
                {
                    continue;
                }

                if (string.Equals(profile.stateName, stateName, StringComparison.Ordinal))
                {
                    return profile;
                }
            }
        }

        return CreateDefaultHitReactionProfile(stateName);
    }

    private static PlayerHitReactionProfile CreateDefaultHitReactionProfile(string stateName)
    {
        PlayerHitReactionProfile profile = new PlayerHitReactionProfile
        {
            stateName = stateName,
            recoveryNormalized = 0.9f,
            entryCrossFade = 0.03f,
            allowRollBuffer = true
        };

        switch (stateName)
        {
            case PlayerAnimatorStates.HitFront:
            case PlayerAnimatorStates.HitBack:
            case PlayerAnimatorStates.HitLeft:
            case PlayerAnimatorStates.HitRight:
                profile.recoveryNormalized = 0.78f;
                profile.entryCrossFade = 0.02f;
                break;
            case PlayerAnimatorStates.HitDash:
                profile.recoveryNormalized = 0.84f;
                profile.entryCrossFade = 0.02f;
                break;
            case PlayerAnimatorStates.HitSmash:
                profile.recoveryNormalized = 0.9f;
                profile.entryCrossFade = 0.02f;
                profile.allowRollBuffer = false;
                break;
            case PlayerAnimatorStates.HitSpear:
                profile.recoveryNormalized = 0.88f;
                profile.entryCrossFade = 0.02f;
                profile.allowRollBuffer = false;
                break;
            case PlayerAnimatorStates.HitSlam:
            case PlayerAnimatorStates.HitDown:
                profile.recoveryNormalized = 0.95f;
                profile.entryCrossFade = 0.02f;
                profile.allowRollBuffer = false;
                break;
        }

        return profile;
    }

    private static float ResolveHitReactionCrossFade(PlayerHitReactionProfile profile)
    {
        if (profile == null)
        {
            return 0.03f;
        }

        return Mathf.Clamp(profile.entryCrossFade, 0f, 0.2f);
    }

    private void TryRecoverHeldMoveAfterHit()
    {
        if (StateMachine == null ||
            StateMachine.CurrentState == HitState ||
            (StateMachine.CurrentState != IdleState && StateMachine.CurrentState != MoveState))
        {
            return;
        }

        if (!_pendingHeldMoveRecoveryAfterHit ||
            AnimatorDriver == null ||
            !HasEffectiveLocomotionInput)
        {
            if (!HasEffectiveLocomotionInput)
            {
                _pendingHeldMoveRecoveryAfterHit = false;
            }

            return;
        }

        if (IsHitReactionAnimatorStateActive() || AnimatorDriver.IsInTransition())
        {
            return;
        }

        _pendingHeldMoveRecoveryAfterHit = false;

        bool isRunning = InputSnapshot.SprintHeld && CurrentStamina > 0f;
        SetAnimatorMoveRunning(isRunning);

        if (StateMachine != null && StateMachine.CurrentState == IdleState)
        {
            StateMachine.ChangeState(MoveState);
        }

        // When the held move input survives a hit, re-enter locomotion once without requiring key release.
        if (!isRunning && !IsLockedOn)
        {
            CrossFadeToMoveEntryLocomotion(0.08f, false);
            return;
        }

        CrossFadeToMoveLocomotion(0.08f, isRunning);
    }

    private void TryForceExitCompletedHitReaction()
    {
        if (StateMachine == null ||
            StateMachine.CurrentState == HitState ||
            (StateMachine.CurrentState != IdleState && StateMachine.CurrentState != MoveState))
        {
            return;
        }

        if (AnimatorDriver == null ||
            !IsHitReactionAnimatorStateActive() ||
            AnimatorDriver.IsInTransition() ||
            AnimatorDriver.GetCurrentStateNormalizedTime() < 0.98f)
        {
            return;
        }

        bool isRunning = ShouldUseRunningLocomotionFromIntent();
        SetAnimatorMoveRunning(isRunning);

        if (HasEffectiveLocomotionInput)
        {
            if (StateMachine != null && StateMachine.CurrentState == IdleState)
            {
                StateMachine.ChangeState(MoveState);
            }

            if (!isRunning && !IsLockedOn)
            {
                CrossFadeToMoveEntryLocomotion(0.08f, false);
                return;
            }

            CrossFadeToMoveLocomotion(0.08f, isRunning);
            return;
        }

        if (StateMachine != null && StateMachine.CurrentState != IdleState)
        {
            StateMachine.ChangeState(IdleState);
        }

        CrossFadeToLocomotion(0.08f);
    }

    private Vector2 ResolveEffectiveLocomotionInput()
    {
        if (_isBonfireInputLocked || IsIntroAnimationInputLocked())
        {
            return Vector2.zero;
        }

        if (IsHitReactionAnimatorStateActive())
        {
            return Vector2.zero;
        }

        if (InputVector.sqrMagnitude > 0.0001f)
        {
            return InputVector;
        }

        return HasLatchedHitMoveRecoveryIntent() ? _latchedHitMoveRecoveryInput : Vector2.zero;
    }

    // Intro 연출 중에는 플레이어 능동 입력을 모두 멈추고, 중력/접지만 유지한다.
    private bool IsIntroAnimationInputLocked()
    {
        return AnimatorDriver != null &&
               AnimatorDriver.IsReady &&
               AnimatorDriver.IsCurrentState(PlayerAnimatorStates.Intro);
    }

    private void CaptureHitMoveRecoveryIntent()
    {
        if (InputVector.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        _latchedHitMoveRecoveryInput = InputVector;
        _latchedHitMoveRecoverySprintHeld = InputSnapshot.SprintHeld && CurrentStamina > 0f;
        _latchedHitMoveRecoveryExpireAt = Time.unscaledTime + HitMoveRecoveryIntentDuration;
    }

    private void RefreshHitMoveRecoveryIntent()
    {
        if (InputVector.sqrMagnitude > 0.0001f)
        {
            _latchedHitMoveRecoveryInput = InputVector;
            _latchedHitMoveRecoverySprintHeld = InputSnapshot.SprintHeld && CurrentStamina > 0f;
            _latchedHitMoveRecoveryExpireAt = Time.unscaledTime + HitMoveRecoveryIntentDuration;
            return;
        }

        if (!HasLatchedHitMoveRecoveryIntent())
        {
            _latchedHitMoveRecoveryInput = Vector2.zero;
            _latchedHitMoveRecoverySprintHeld = false;
            _latchedHitMoveRecoveryExpireAt = 0f;
        }
    }

    public void ClearHitMoveRecoveryIntent()
    {
        _latchedHitMoveRecoveryInput = Vector2.zero;
        _latchedHitMoveRecoverySprintHeld = false;
        _latchedHitMoveRecoveryExpireAt = 0f;
        _pendingHeldMoveRecoveryAfterHit = false;
    }

    private bool HasLatchedHitMoveRecoveryIntent()
    {
        return _latchedHitMoveRecoveryInput.sqrMagnitude > 0.0001f &&
               Time.unscaledTime <= _latchedHitMoveRecoveryExpireAt;
    }

    private void HandleStaminaChanged(float current, float max)
    {
        OnPlayerStaminaChanged?.Invoke(current, max);
    }

    // 전투 자세 변경 시 애니메이터 파라미터를 즉시 동기화한다.
    private void HandleBattleStanceChanged(bool isBattleStance)
    {
        AnimatorDriver?.TrySetBattle(isBattleStance);

        if (StateMachine == null || StateMachine.CurrentState == null)
        {
            SyncWeaponVisualsToCurrentStance();
            return;
        }

        bool isNeutralLocomotion = StateMachine.CurrentState == IdleState || StateMachine.CurrentState == MoveState;
        if (!isNeutralLocomotion)
        {
            SyncWeaponVisualsToCurrentStance();
        }
    }

    // 손/등 무기는 같은 이름 인스턴스를 둘 수 있어 본 기준으로 자동 분리한다.
    private void ResolveWeaponVisualObjects()
    {
        if (handWeaponVisualObject != null && backWeaponVisualObject != null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(weaponVisualObjectName))
        {
            return;
        }

        Transform[] transforms = GetComponentsInChildren<Transform>(true);
        foreach (Transform child in transforms)
        {
            if (child == null || child == transform || child.name != weaponVisualObjectName)
            {
                continue;
            }

            if (handWeaponVisualObject == null && HasAncestorName(child, HandWeaponBoneName))
            {
                handWeaponVisualObject = child.gameObject;
                continue;
            }

            if (backWeaponVisualObject == null && IsBackWeaponTransform(child))
            {
                backWeaponVisualObject = child.gameObject;
            }
        }
    }

    private void SyncWeaponVisualsToCurrentStance()
    {
        ResolveWeaponVisualObjects();
        SetWeaponVisualStates(IsBattleStance, !IsBattleStance);
    }

    private void SetWeaponVisualStates(bool showHandWeapon, bool showBackWeapon)
    {
        if (handWeaponVisualObject != null && handWeaponVisualObject.activeSelf != showHandWeapon)
        {
            handWeaponVisualObject.SetActive(showHandWeapon);
        }

        if (backWeaponVisualObject != null && backWeaponVisualObject.activeSelf != showBackWeapon)
        {
            backWeaponVisualObject.SetActive(showBackWeapon);
        }
    }

    private bool IsBackWeaponTransform(Transform target)
    {
        return HasAncestorName(target, BackWeaponBonePrimaryName) ||
               HasAncestorName(target, BackWeaponBoneSecondaryName) ||
               HasAncestorName(target, BackWeaponBoneFallbackName);
    }

    private bool HasAncestorName(Transform target, string ancestorName)
    {
        if (target == null || string.IsNullOrWhiteSpace(ancestorName))
        {
            return false;
        }

        Transform current = target.parent;
        while (current != null)
        {
            if (current.name == ancestorName)
            {
                return true;
            }

            current = current.parent;
        }

        return false;
    }

    private void InterruptActionByHit()
    {
        EnterHitState();
    }

    private bool ShouldInterruptCurrentStateByHit(IState currentState)
    {
        return currentState == AttackState ||
               currentState == RollState ||
               currentState == UseItemState ||
               currentState == ParryState;
    }

    public void EnterHitState()
    {
        if (StateMachine == null || HitState == null)
        {
            return;
        }

        SetGuarding(false);
        SetVelocity(Vector3.zero);
        StateMachine.ChangeState(HitState);
    }

    public float GetCurrentHitRecoveryNormalized()
    {
        if (_currentHitReactionProfile == null)
        {
            return 0.9f;
        }

        return Mathf.Clamp01(_currentHitReactionProfile.recoveryNormalized);
    }

    public bool CanBufferRollDuringCurrentHitReaction()
    {
        return _currentHitReactionProfile == null || _currentHitReactionProfile.allowRollBuffer;
    }

    public void ClearCurrentHitReactionProfile()
    {
        _currentHitReactionProfile = null;
    }

    public void RestoreLocomotionAfterHit()
    {
        ClearCurrentHitReactionProfile();
        ClearPendingHitKnockback();

        bool isRunning = ShouldUseRunningLocomotionFromIntent();
        SetAnimatorMoveRunning(isRunning);

        if (HasEffectiveLocomotionInput)
        {
            if (StateMachine != null && StateMachine.CurrentState != MoveState)
            {
                StateMachine.ChangeState(MoveState);
            }

            if (!isRunning && !IsLockedOn)
            {
                CrossFadeToMoveEntryLocomotion(0.08f, false);
                return;
            }

            CrossFadeToMoveLocomotion(0.08f, isRunning);
            return;
        }

        if (StateMachine != null && StateMachine.CurrentState != IdleState)
        {
            StateMachine.ChangeState(IdleState);
        }

        CrossFadeToLocomotion(0.08f);
    }

    private void QueueHitKnockback(CombatAttackDefinition incomingAttack, Vector3 hitPoint, Vector3 hitNormal)
    {
        if (!enableHitKnockback)
        {
            ClearPendingHitKnockback();
            return;
        }

        Vector3 knockbackDirection = ResolveHitKnockbackDirection(hitPoint, hitNormal);
        if (knockbackDirection.sqrMagnitude <= 0.0001f)
        {
            ClearPendingHitKnockback();
            return;
        }

        ResolveHitKnockbackPreset(incomingAttack, out float knockbackDistance, out float knockbackDuration);
        if (knockbackDistance <= 0f || knockbackDuration <= 0f)
        {
            ClearPendingHitKnockback();
            return;
        }

        _pendingHitKnockbackDirection = knockbackDirection;
        _pendingHitKnockbackDistance = knockbackDistance;
        _pendingHitKnockbackDuration = knockbackDuration;
        _pendingHitKnockbackElapsed = 0f;
        _hasPendingHitKnockback = true;
    }

    private void ApplyPendingHitKnockback()
    {
        if (!_hasPendingHitKnockback || rb == null)
        {
            return;
        }

        float duration = Mathf.Max(0.01f, _pendingHitKnockbackDuration);
        float startT = Mathf.Clamp01(_pendingHitKnockbackElapsed / duration);
        float nextElapsed = Mathf.Min(_pendingHitKnockbackElapsed + Time.fixedDeltaTime, duration);
        float endT = Mathf.Clamp01(nextElapsed / duration);
        float deltaWeight = EvaluateHitKnockbackCurve(endT) - EvaluateHitKnockbackCurve(startT);

        if (deltaWeight > 0f)
        {
            Vector3 delta = _pendingHitKnockbackDirection * (_pendingHitKnockbackDistance * deltaWeight);
            rb.MovePosition(rb.position + delta);
        }

        _pendingHitKnockbackElapsed = nextElapsed;
        if (_pendingHitKnockbackElapsed >= duration - 0.0001f)
        {
            ClearPendingHitKnockback();
        }
    }

    private void ClearPendingHitKnockback()
    {
        _hasPendingHitKnockback = false;
        _pendingHitKnockbackDirection = Vector3.zero;
        _pendingHitKnockbackDistance = 0f;
        _pendingHitKnockbackDuration = 0f;
        _pendingHitKnockbackElapsed = 0f;
    }

    private void ResolveHitKnockbackPreset(
        CombatAttackDefinition incomingAttack,
        out float knockbackDistance,
        out float knockbackDuration)
    {
        knockbackDistance = defaultHitKnockbackDistance;
        knockbackDuration = defaultHitKnockbackDuration;

        if (incomingAttack == null)
        {
            return;
        }

        if (incomingAttack.isHeavyAttack)
        {
            knockbackDistance = Mathf.Max(knockbackDistance, strongHitKnockbackDistance * 0.9f);
            knockbackDuration = Mathf.Max(knockbackDuration, strongHitKnockbackDuration);
        }

        switch (incomingAttack.playerHitReactionType)
        {
            case PlayerHitReactionType.Dash:
            case PlayerHitReactionType.Smash:
            case PlayerHitReactionType.Spear:
                knockbackDistance = strongHitKnockbackDistance;
                knockbackDuration = strongHitKnockbackDuration;
                break;
            case PlayerHitReactionType.Slam:
            case PlayerHitReactionType.Down:
                knockbackDistance = launchHitKnockbackDistance;
                knockbackDuration = launchHitKnockbackDuration;
                break;
        }
    }

    private Vector3 ResolveHitKnockbackDirection(Vector3 hitPoint, Vector3 hitNormal)
    {
        Vector3 toAttacker = hitNormal.sqrMagnitude > 0.0001f
            ? -hitNormal
            : hitPoint - transform.position;
        toAttacker.y = 0f;

        Vector3 awayFromAttacker = -toAttacker;
        awayFromAttacker.y = 0f;

        if (awayFromAttacker.sqrMagnitude <= 0.0001f)
        {
            awayFromAttacker = -transform.forward;
            awayFromAttacker.y = 0f;
        }

        return awayFromAttacker.sqrMagnitude > 0.0001f
            ? awayFromAttacker.normalized
            : Vector3.zero;
    }

    private static float EvaluateHitKnockbackCurve(float normalizedTime)
    {
        float t = Mathf.Clamp01(normalizedTime);
        float inverse = 1f - t;
        return 1f - inverse * inverse * inverse;
    }

    private void ResolveEffectRoot()
    {
        if (effectRoot != null)
        {
            return;
        }

        Transform[] transforms = GetComponentsInChildren<Transform>(true);
        foreach (Transform child in transforms)
        {
            if (child.name == "EffectRoot")
            {
                effectRoot = child;
                return;
            }
        }
    }

    private void ResolveFootstepAudioSource()
    {
        if (footstepAudioSource == null)
        {
            footstepAudioSource = GetComponent<AudioSource>();
        }

        if (footstepAudioSource == null)
        {
            footstepAudioSource = gameObject.AddComponent<AudioSource>();
        }

        footstepAudioSource.playOnAwake = false;
        footstepAudioSource.loop = false;
        footstepAudioSource.spatialBlend = 0f;
    }

    private void EnsureAnimationEventBridge()
    {
        if (Anim == null)
        {
            return;
        }

        if (Anim.GetComponent<PlayerAnimationEvents>() != null)
        {
            return;
        }

        // 이벤트를 받는 쪽은 Animator가 붙은 오브젝트여야 하므로 런타임에 브리지를 보장한다.
        Anim.gameObject.AddComponent<PlayerAnimationEvents>();
    }

    private void ResolvePlayerSfxAudioSource()
    {
        if (playerSfxAudioSource == null)
        {
            AudioSource[] audioSources = GetComponentsInChildren<AudioSource>(true);
            AudioSource fallbackSource = null;
            foreach (AudioSource source in audioSources)
            {
                if (source == null || source == footstepAudioSource)
                {
                    continue;
                }

                if (string.Equals(source.gameObject.name, "PlayerAudio", StringComparison.Ordinal))
                {
                    playerSfxAudioSource = source;
                    break;
                }

                if (fallbackSource == null)
                {
                    fallbackSource = source;
                }
            }

            if (playerSfxAudioSource == null)
            {
                playerSfxAudioSource = fallbackSource;
            }
        }

        if (playerSfxAudioSource == null)
        {
            GameObject sfxRoot = new GameObject("PlayerSfxSource");
            sfxRoot.transform.SetParent(transform, false);
            playerSfxAudioSource = sfxRoot.AddComponent<AudioSource>();
        }

        playerSfxAudioSource.playOnAwake = false;
        playerSfxAudioSource.loop = false;
        playerSfxAudioSource.spatialBlend = 0f;
        AutoLoadPlayerSfxIfNeeded();
    }

    private void AutoLoadPlayerSfxIfNeeded()
    {
        if (attackClips == null || attackClips.Length == 0)
        {
            attackClips = GameAudioResources.LoadClips(GameAudioCatalog.Player.AttackSwingPaths);
        }

        if (incomingHitClips == null || incomingHitClips.Length == 0)
        {
            incomingHitClips = GameAudioResources.LoadClips(GameAudioCatalog.Player.IncomingHitPaths);
        }

        if (guardSuccessClip == null)
        {
            guardSuccessClip = GameAudioResources.LoadClip(GameAudioCatalog.Player.GuardSuccess);
        }
    }

    // 이동 상태와 실제 수평 속도를 함께 확인해 불필요한 발소리 이벤트 재생을 막는다.
    private bool CanPlayFootstepAudio()
    {
        if (IsDead || footstepAudioSource == null)
        {
            return false;
        }

        if (footstepClips == null || footstepClips.Length == 0)
        {
            return false;
        }

        if (Time.time < _nextFootstepPlayableTime)
        {
            return false;
        }

        if (StateMachine == null || StateMachine.CurrentState != MoveState)
        {
            return false;
        }

        Vector3 horizontalVelocity = rb != null ? rb.velocity : Vector3.zero;
        horizontalVelocity.y = 0f;
        return horizontalVelocity.sqrMagnitude >= footstepMinMoveSpeed * footstepMinMoveSpeed;
    }

    private AudioClip GetRandomFootstepClip()
    {
        if (footstepClips == null || footstepClips.Length == 0)
        {
            return null;
        }

        int clipIndex = UnityEngine.Random.Range(0, footstepClips.Length);
        return footstepClips[clipIndex];
    }

    private void PlayPlayerSfx(AudioClip clip, float volume)
    {
        if (clip == null || playerSfxAudioSource == null)
        {
            return;
        }

        playerSfxAudioSource.pitch = 1f;
        playerSfxAudioSource.PlayOneShot(clip, volume);
    }

    public void PlayAttackSwingSfx()
    {
        PlayRandomPlayerSfx(attackClips, attackVolume);
    }

    private void PlayRandomPlayerSfx(AudioClip[] clips, float volume)
    {
        PlayPlayerSfx(GetRandomClip(clips), volume);
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

    private void PlayIncomingHitFeedback(CombatAttackDefinition incomingAttack, Vector3 hitPoint, Vector3 hitNormal, float fallbackHitStopDuration, float fallbackShakeDuration, float fallbackShakeIntensity)
    {
        float hitStopDuration = ResolveCombatHitStopDuration(incomingAttack, fallbackHitStopDuration);
        float shakeDuration = ResolveCombatShakeDuration(incomingAttack, fallbackShakeDuration, fallbackShakeDuration);
        float shakeIntensity = ResolveCombatShakeIntensity(incomingAttack, fallbackShakeIntensity, fallbackShakeIntensity);

        if (hitStopDuration > 0f)
        {
            EffectManager.Instance.DoHitStop(hitStopDuration);
        }

        if (shakeIntensity > 0f)
        {
            EffectManager.Instance.DoCameraShake(shakeDuration, shakeIntensity);
        }

        SpawnCombatImpactEffect(playerHitBloodEffectPrefab, hitPoint, hitNormal);
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

    private void PlayGuardSuccessEffect()
    {
        if (guardSuccessEffectPrefab == null)
        {
            return;
        }

        ResolveEffectRoot();
        Transform spawnRoot = effectRoot != null ? effectRoot : transform;
        GameObject effectInstance = Instantiate(guardSuccessEffectPrefab, spawnRoot);
        effectInstance.transform.localPosition = Vector3.zero;
        effectInstance.transform.localRotation = Quaternion.identity;
        effectInstance.transform.localScale = Vector3.one;
    }

}

