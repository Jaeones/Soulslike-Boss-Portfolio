using UnityEngine;

public class PlayerRollState : PlayerState
{
    private const float RollForceStartNormalized = 0.06f;
    private Vector3 rollDirection;

    public PlayerRollState(PlayerController player, StateMachine stateMachine, PlayerStatData statData)
        : base(player, stateMachine, statData)
    {
    }

    public override void Enter()
    {
        player.SetGuarding(false);
        player.ForceBattleStance();
        startTime = Time.time;
        player.TrySetAnimatorTrigger(PlayerAnimatorParams.RollTrigger, PlayerAnimatorParams.RollTriggerName);

        if (player.Stamina == null || !player.Stamina.TryUse(statData.rollCost))
        {
            Debug.Log("?ㅽ깭誘몃굹 遺議? 援щⅤ湲?遺덇?");
            stateMachine.ChangeState(player.IdleState);
            return;
        }

        if (player.IsLockedOn && player.LockOnTarget != null)
        {
            Vector3 toTarget = player.LockOnTarget.position - player.transform.position;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude < 0.0001f)
            {
                toTarget = player.transform.forward;
            }

            Vector3 lockForward = toTarget.normalized;
            Vector3 lockRight = Vector3.Cross(Vector3.up, lockForward).normalized;

            if (player.InputVector != Vector2.zero)
            {
                rollDirection = (lockForward * player.InputVector.y + lockRight * player.InputVector.x).normalized;
            }
            else
            {
                rollDirection = -lockForward;
            }

            player.transform.forward = lockForward;
        }
        else
        {
            rollDirection = player.transform.forward;
        }

        player.ClearHorizontalVelocity();
        player.SetRollRootMotionEnabled(false);
        player.IsInvincible = true;
        Debug.Log("援щⅤ湲?(臾댁쟻)");
    }

    public override void Update()
    {
        base.Update();

        float elapsedTime = Time.time - startTime;

        if (!player.IsUsingRollRootMotion &&
            player.IsLockedOn && player.LockOnTarget != null)
        {
            Vector3 toTarget = player.LockOnTarget.position - player.transform.position;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude > 0.0001f)
            {
                Quaternion targetRot = Quaternion.LookRotation(toTarget.normalized);
                player.transform.rotation = Quaternion.Slerp(player.transform.rotation, targetRot, Time.deltaTime * 18f);
            }
        }

        if (elapsedTime > statData.invincibleTime && player.IsInvincible)
        {
            player.IsInvincible = false;
            Debug.Log("臾댁쟻 醫낅즺");
        }

        if (elapsedTime > statData.rollDuration)
        {
            stateMachine.ChangeState(player.IdleState);
        }
    }

    public override void PhysicsUpdate()
    {
        base.PhysicsUpdate();

        if (rollDirection.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        if (ResolveRollNormalizedTime() < RollForceStartNormalized)
        {
            player.SetVelocity(Vector3.zero);
            return;
        }

        player.MoveWithSpeed(rollDirection.normalized, statData.rollForce, false);
    }

    public override void Exit()
    {
        base.Exit();
        player.SetRollRootMotionEnabled(false);
        player.SetVelocity(Vector3.zero);
        player.IsInvincible = false;
    }

    private float ResolveRollNormalizedTime()
    {
        if (statData == null || statData.rollDuration <= 0.0001f)
        {
            return 1f;
        }

        return Mathf.Clamp01((Time.time - startTime) / statData.rollDuration);
    }
}
