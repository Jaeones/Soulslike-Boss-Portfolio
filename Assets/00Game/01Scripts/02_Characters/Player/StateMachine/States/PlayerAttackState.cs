using UnityEngine;

public class PlayerAttackState : PlayerState
{
    private const int DefaultAttackIndex = 0;

    private bool hasObservedCurrentAttackState;
    private bool hasQueuedFollowUpAttack;
    private float lastResolvedNormalizedTime;
    private CombatAttackDefinition currentAttack;
    private string attackEntryId;
    private string queuedFollowUpAttackId;

    public PlayerAttackState(PlayerController player, StateMachine stateMachine, PlayerStatData statData)
        : base(player, stateMachine, statData)
    {
        animBoolName = PlayerAnimatorParams.IsAttackName;
        animBoolHash = PlayerAnimatorParams.IsAttack;
    }

    public override void Enter()
    {
        player.SetGuarding(false);
        player.ForceBattleStance();
        attackEntryId = player.ResolveAttackEntryId();
        StartAttack();
    }

    public override void Update()
    {
        base.Update();
        PlayerInputSnapshot input = player.InputSnapshot;

        if (statData == null || currentAttack == null)
        {
            TransitionToRecoveryState();
            return;
        }

        if (!TryResolveAttackNormalizedTime(out float normalizedTime))
        {
            return;
        }

        TryQueueNextComboAttack(normalizedTime, input);
        player.UpdateAttackFacing(normalizedTime);
        player.Combat?.UpdateAttackWindow(normalizedTime);

        if (CanRollCancelIntoRecovery(currentAttack, normalizedTime) &&
            input.RollPressed &&
            player.CurrentStamina >= statData.rollCost)
        {
            stateMachine.ChangeState(player.RollState);
            return;
        }

        if (normalizedTime < ResolveAttackEndNormalized(currentAttack))
        {
            return;
        }

        TransitionToRecoveryState();
    }

    public override void Exit()
    {
        player.ResetCombatRuntimeState();
        player.EndAttackFacing();
        player.SetAttackRootMotionEnabled(false);

        attackEntryId = null;
        queuedFollowUpAttackId = null;
        hasQueuedFollowUpAttack = false;
        currentAttack = null;
        hasObservedCurrentAttackState = false;
        lastResolvedNormalizedTime = 0f;
    }

    public void ResetComboChainRuntime()
    {
        attackEntryId = null;
        queuedFollowUpAttackId = null;
        hasQueuedFollowUpAttack = false;
        currentAttack = null;
        startTime = 0f;
        hasObservedCurrentAttackState = false;
        lastResolvedNormalizedTime = 0f;
    }

    private void StartAttack()
    {
        if (statData == null || !statData.HasAttackDefinitions)
        {
            TransitionToRecoveryState();
            return;
        }

        currentAttack = HasDirectAttackEntry
            ? statData.CreateAttackDefinition(attackEntryId)
            : statData.CreateAttackDefinition(DefaultAttackIndex);
        if (currentAttack == null)
        {
            TransitionToRecoveryState();
            return;
        }

        if (player.Stamina == null || !player.Stamina.TryUse(currentAttack.staminaCost))
        {
            Debug.Log("Not enough stamina to attack.");
            currentAttack = null;
            TransitionToRecoveryState();
            return;
        }

        player.Combat?.SetCurrentAttackDefinition(currentAttack);
        if (!TryPlayAttackAnimation())
        {
            Debug.LogWarning($"{GetAttackAnimationStateName()} state is missing. Ending attack.");
            player.Combat?.ClearCurrentAttackInfo();
            currentAttack = null;
            TransitionToRecoveryState();
            return;
        }

        player.SetVelocity(Vector3.zero);
        player.SetAttackRootMotionEnabled(ShouldUseAttackRootMotion());
        player.BeginAttackFacing(currentAttack.trackUntilNormalized, currentAttack.facingLockNormalized);

        Debug.Log($"Attack {(HasDirectAttackEntry ? attackEntryId : "1")}: {currentAttack.ResolvedAttackName}, Damage: {currentAttack.damage}");

        queuedFollowUpAttackId = null;
        hasQueuedFollowUpAttack = false;
        hasObservedCurrentAttackState = false;
        lastResolvedNormalizedTime = 0f;
    }

    private bool CanRollCancelIntoRecovery(CombatAttackDefinition attack, float normalizedTime)
    {
        return normalizedTime >= ResolveRollCancelStartNormalized(attack);
    }

    private bool TryPlayAttackAnimation()
    {
        string stateName = GetAttackAnimationStateName();
        if (string.IsNullOrEmpty(stateName))
        {
            return false;
        }

        player.AnimatorDriver.ResetTrigger(PlayerAnimatorParams.AttackTrigger);

        if (player.AnimatorDriver.HasState(stateName))
        {
            return player.AnimatorDriver.TryCrossFadeToState(stateName, 0.04f, 0, 0f);
        }

        player.TrySetAnimatorTrigger(PlayerAnimatorParams.AttackTrigger, PlayerAnimatorParams.AttackTriggerName);
        return false;
    }

    private float ResolveRollCancelStartNormalized(CombatAttackDefinition attack)
    {
        return Mathf.Clamp01(attack.rollCancelStartNormalized);
    }

    private float ResolveRecoveryEndNormalized(CombatAttackDefinition attack)
    {
        return Mathf.Clamp01(attack.recoveryEndNormalized);
    }

    private float ResolveMinimumAttackEndNormalized(CombatAttackDefinition attack)
    {
        if (attack.activeEndNormalized > attack.activeStartNormalized)
        {
            return Mathf.Clamp01(attack.activeEndNormalized);
        }

        return 0.05f;
    }

    private string GetAttackAnimationStateName()
    {
        if (currentAttack != null && !string.IsNullOrEmpty(currentAttack.ResolvedAnimatorStateName))
        {
            return currentAttack.ResolvedAnimatorStateName;
        }

        if (HasDirectAttackEntry && statData != null)
        {
            string stateName = statData.GetAttackAnimatorStateName(attackEntryId);
            if (!string.IsNullOrEmpty(stateName))
            {
                return stateName;
            }
        }

        if (statData != null)
        {
            string stateName = statData.GetAttackAnimatorStateName(DefaultAttackIndex);
            if (!string.IsNullOrEmpty(stateName))
            {
                return stateName;
            }
        }

        return PlayerAnimatorStates.Attack1;
    }

    private bool TryResolveAttackNormalizedTime(out float normalizedTime)
    {
        normalizedTime = 0f;
        string currentAttackStateName = GetAttackAnimationStateName();
        if (player.AnimatorDriver != null &&
            !string.IsNullOrEmpty(currentAttackStateName) &&
            player.AnimatorDriver.IsCurrentState(currentAttackStateName))
        {
            hasObservedCurrentAttackState = true;
            lastResolvedNormalizedTime = Mathf.Clamp01(player.AnimatorDriver.GetCurrentStateNormalizedTime());
            normalizedTime = lastResolvedNormalizedTime;
            return true;
        }

        if (!hasObservedCurrentAttackState)
        {
            return false;
        }

        if (player.AnimatorDriver != null && player.AnimatorDriver.IsInTransition())
        {
            normalizedTime = lastResolvedNormalizedTime;
            return true;
        }

        normalizedTime = 1f;
        return true;
    }

    private float ResolveAttackEndNormalized(CombatAttackDefinition attack)
    {
        float endNormalized = ResolveRecoveryEndNormalized(attack);
        float minimumEndNormalized = ResolveMinimumAttackEndNormalized(attack);
        return Mathf.Clamp01(Mathf.Max(endNormalized, minimumEndNormalized));
    }

    private void TryQueueNextComboAttack(float normalizedTime, PlayerInputSnapshot input)
    {
        if (currentAttack == null || !input.AttackShortReleased || !IsWithinComboWindow(currentAttack, normalizedTime))
        {
            return;
        }

        if (hasQueuedFollowUpAttack)
        {
            return;
        }

        string nextAttackId = ResolveRepeatedAttackId(input);
        if (string.IsNullOrEmpty(nextAttackId))
        {
            return;
        }

        if (statData != null && player.CurrentStamina < statData.GetAttackStaminaCost(nextAttackId))
        {
            return;
        }

        hasQueuedFollowUpAttack = true;
        queuedFollowUpAttackId = nextAttackId;
    }

    private static bool IsWithinComboWindow(CombatAttackDefinition attack, float normalizedTime)
    {
        float clampedTime = Mathf.Clamp01(normalizedTime);
        return clampedTime >= attack.comboWindowStartNormalized &&
               clampedTime <= attack.comboWindowEndNormalized;
    }

    private string ResolveRepeatedAttackId(PlayerInputSnapshot input)
    {
        return input.AttackLongPressTriggered ? PlayerAnimatorStates.Attack3 : string.Empty;
    }

    private void TransitionToRecoveryState()
    {
        if (hasQueuedFollowUpAttack)
        {
            attackEntryId = queuedFollowUpAttackId;
            queuedFollowUpAttackId = null;
            hasQueuedFollowUpAttack = false;
            StartAttack();
            return;
        }

        bool hasMoveInput = player.HasEffectiveLocomotionInput;

        if (hasMoveInput)
        {
            bool isRunning = player.ShouldUseRunningLocomotionFromIntent();
            player.CrossFadeToMoveLocomotion(0.08f, isRunning);
        }
        else
        {
            player.CrossFadeToLocomotion(0.08f);
        }

        stateMachine.ChangeState(hasMoveInput ? player.MoveState : player.IdleState);
    }

    private bool HasDirectAttackEntry => !string.IsNullOrWhiteSpace(attackEntryId);

    private bool ShouldUseAttackRootMotion()
    {
        return currentAttack != null &&
               (string.Equals(currentAttack.ResolvedAnimatorStateName, PlayerAnimatorStates.DashAttack, System.StringComparison.Ordinal) ||
                string.Equals(currentAttack.ResolvedAnimatorStateName, PlayerAnimatorStates.GroggyAttack, System.StringComparison.Ordinal));
    }
}
