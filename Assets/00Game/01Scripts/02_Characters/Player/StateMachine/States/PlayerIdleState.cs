using UnityEngine;

public class PlayerIdleState : PlayerState
{
    public PlayerIdleState(PlayerController player, StateMachine stateMachine, PlayerStatData statData)
        : base(player, stateMachine, statData)
    {
        animBoolName = PlayerAnimatorParams.IsIdleName;
        animBoolHash = PlayerAnimatorParams.IsIdle;
    }

    public override void Enter()
    {
        base.Enter();
        player.SetAnimatorMoveRunning(false);
        player.SetVelocity(Vector3.zero);
        if (!player.SupportsAnimatorDrivenLocomotion())
        {
            player.CrossFadeToLocomotion(0.08f);
        }
    }

    public override void Update()
    {
        base.Update();
        // 입력 해석은 PlayerInputReader에서 끝내고 상태는 스냅샷만 사용한다.
        PlayerInputSnapshot input = player.InputSnapshot;

        if (player.HasParryStartInput() && player.CanStartParry())
        {
            stateMachine.ChangeState(player.ParryState);
            return;
        }

        if (player.HasEffectiveLocomotionInput)
        {
            stateMachine.ChangeState(player.MoveState);
            return;
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
                Debug.Log("스태미나 부족: 공격 불가");
            }
        }
    }
}
