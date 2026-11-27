using UnityEngine;

public sealed class PlayerParryState : PlayerState
{
    private const float CrossFadeDuration = 0.04f;
    private bool _hasConsumedStamina;

    public PlayerParryState(PlayerController player, StateMachine stateMachine, PlayerStatData statData)
        : base(player, stateMachine, statData)
    {
    }

    public override void Enter()
    {
        startTime = Time.time;
        _hasConsumedStamina = false;

        player.SetGuarding(false);
        player.ForceBattleStance();
        player.SetVelocity(Vector3.zero);

        if (statData == null || player.Stamina == null || !player.Stamina.TryUse(statData.parryStaminaCost))
        {
            TransitionToLocomotion();
            return;
        }

        _hasConsumedStamina = true;
        player.SetParryWindowActive(false);

        if (player.AnimatorDriver != null)
        {
            player.AnimatorDriver.TryCrossFadeToState(PlayerAnimatorStates.Parry, CrossFadeDuration, 0, 0f);
        }
    }

    public override void Update()
    {
        base.Update();

        if (!_hasConsumedStamina || statData == null)
        {
            TransitionToLocomotion();
            return;
        }

        float elapsed = Time.time - startTime;
        float startup = ResolveStartupDuration();
        float activeEnd = startup + ResolveActiveDuration();
        float recoveryEnd = activeEnd + ResolveRecoveryDuration();

        player.SetParryWindowActive(elapsed >= startup && elapsed <= activeEnd);

        if (elapsed < recoveryEnd)
        {
            return;
        }

        TransitionToLocomotion();
    }

    public override void PhysicsUpdate()
    {
        base.PhysicsUpdate();
        player.SetVelocity(Vector3.zero);
    }

    public override void Exit()
    {
        player.SetParryWindowActive(false);
        _hasConsumedStamina = false;
    }

    public void ResetRuntimeState()
    {
        player.SetParryWindowActive(false);
        _hasConsumedStamina = false;
        startTime = 0f;
    }

    private float ResolveStartupDuration()
    {
        return statData != null ? Mathf.Max(0.01f, statData.parryStartupDuration) : 0.06f;
    }

    private float ResolveActiveDuration()
    {
        return statData != null ? Mathf.Max(0.01f, statData.parryActiveDuration) : 0.08f;
    }

    private float ResolveRecoveryDuration()
    {
        return statData != null ? Mathf.Max(0.01f, statData.parryRecoveryDuration) : 0.24f;
    }

    private void TransitionToLocomotion()
    {
        bool hasMoveInput = player.HasEffectiveLocomotionInput;
        if (hasMoveInput)
        {
            bool isRunning = player.ShouldUseRunningLocomotionFromIntent();
            player.CrossFadeToMoveLocomotion(0.08f, isRunning);
            stateMachine.ChangeState(player.MoveState);
            return;
        }

        player.CrossFadeToLocomotion(0.08f);
        stateMachine.ChangeState(player.IdleState);
    }
}
