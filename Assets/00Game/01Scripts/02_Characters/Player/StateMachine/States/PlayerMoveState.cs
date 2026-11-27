using UnityEngine;

public class PlayerMoveState : PlayerState
{
    private bool _isRunning;

    public PlayerMoveState(PlayerController player, StateMachine stateMachine, PlayerStatData statData)
        : base(player, stateMachine, statData)
    {
    }

    public override void Enter()
    {
        base.Enter();
        _isRunning = ShouldUseRunningLocomotion();
        player.SetAnimatorMoveRunning(_isRunning);
        if (!player.SupportsAnimatorDrivenLocomotion())
        {
            player.CrossFadeToMoveEntryLocomotion(0.08f, _isRunning);
        }
    }

    public override void Update()
    {
        base.Update();
        PlayerInputSnapshot input = player.InputSnapshot;

        if (player.HasParryStartInput() && player.CanStartParry())
        {
            stateMachine.ChangeState(player.ParryState);
            return;
        }

        if (!player.HasEffectiveLocomotionInput)
        {
            player.SetAnimatorMoveRunning(false);
            _isRunning = false;
            stateMachine.ChangeState(player.IdleState);
            return;
        }

        bool wasRunning = _isRunning;
        _isRunning = ShouldUseRunningLocomotion();
        if (_isRunning)
        {
            if (player.Stamina == null || !player.Stamina.TryUsePerSecond(statData.runStaminaCostPerSecond, Time.deltaTime))
            {
                _isRunning = false;
            }
        }

        player.SetAnimatorMoveRunning(_isRunning);

        if (wasRunning != _isRunning)
        {
            if (!player.SupportsAnimatorDrivenLocomotion())
            {
                player.CrossFadeToMoveLocomotion(0.08f, _isRunning);
            }
        }

        if (input.RollPressed && player.CurrentStamina >= statData.rollCost)
        {
            stateMachine.ChangeState(player.RollState);
            Debug.Log("구르기 시도");
        }
        else if (player.HasAttackStartInput())
        {
            if (player.CanStartAttack())
            {
                stateMachine.ChangeState(player.AttackState);
                Debug.Log("공격 시도");
            }
            else
            {
                Debug.Log("스태미나 부족 공격 불가");
            }
        }
    }

    public override void PhysicsUpdate()
    {
        base.PhysicsUpdate();
        if (player.Locomotion == null)
        {
            return;
        }

        player.Locomotion.Move(
            player.EffectiveLocomotionInput,
            _isRunning,
            player.IsLockedOn,
            player.LockOnTarget,
            Define.MainCam != null ? Define.MainCam.transform : null,
            Time.deltaTime);
    }

    public override void Exit()
    {
        player.SetAnimatorMoveRunning(false);
        base.Exit();
    }

    private bool ShouldUseRunningLocomotion()
    {
        return player.ShouldUseRunningLocomotionFromIntent();
    }
}
