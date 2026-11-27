using UnityEngine;

public class PlayerUseItemState : PlayerState
{
    private bool _isHealApplied;
    private float _stateDuration;
    private float _healApplyTime;
    private bool _requestedUpperBodyDrinkOverlay;
    private bool _usesUpperBodyDrinkOverlay;
    private bool _isRunning;

    public PlayerUseItemState(PlayerController player, StateMachine stateMachine, PlayerStatData statData)
        : base(player, stateMachine, statData)
    {
    }

    public bool UsesUpperBodyDrinkOverlay => _usesUpperBodyDrinkOverlay;

    public override void Enter()
    {
        if (player.Item == null || player.GetEstusFlask() == null)
        {
            ClearRuntimeFlags();
            stateMachine.ChangeState(player.IdleState);
            return;
        }

        if (!player.Item.TryBeginEstusUse(stateMachine.CurrentState))
        {
            ClearRuntimeFlags();
            stateMachine.ChangeState(player.IdleState);
            return;
        }

        startTime = Time.time;
        _isHealApplied = false;
        _stateDuration = player.Item.GetEstusDrinkDuration();
        _healApplyTime = player.Item.GetEstusHealApplyTime();
        _usesUpperBodyDrinkOverlay = _requestedUpperBodyDrinkOverlay && player.SupportsUpperBodyDrinkOverlay();
        _requestedUpperBodyDrinkOverlay = false;
        _isRunning = false;

        player.SetGuarding(false);
        player.ResetCombatRuntimeState();
        player.SetAnimatorMoveRunning(false);

        if (!player.TriggerEstusDrinkAnimation(_usesUpperBodyDrinkOverlay))
        {
            if (_usesUpperBodyDrinkOverlay && player.TriggerEstusDrinkAnimation(false))
            {
                _usesUpperBodyDrinkOverlay = false;
            }
            else
            {
                Debug.LogWarning("Animator does not have a usable estus drink trigger.");
            }
        }

        if (!_usesUpperBodyDrinkOverlay)
        {
            player.SetVelocity(Vector3.zero);
        }
    }

    public override void Update()
    {
        float elapsed = Time.time - startTime;

        UpdateMovingDrinkLocomotion();

        if (!_isHealApplied && elapsed >= _healApplyTime)
        {
            ApplyHealOnce();
        }

        if (elapsed >= _stateDuration)
        {
            player.Item.EndEstusUse();
            if (player.InputVector != Vector2.zero)
            {
                stateMachine.ChangeState(player.MoveState);
            }
            else
            {
                stateMachine.ChangeState(player.IdleState);
            }
        }
    }

    public override void PhysicsUpdate()
    {
        if (!_usesUpperBodyDrinkOverlay)
        {
            player.SetVelocity(Vector3.zero);
            return;
        }

        if (player.Locomotion == null)
        {
            return;
        }

        if (player.InputVector.sqrMagnitude <= 0.0001f)
        {
            player.SetVelocity(Vector3.zero);
            return;
        }

        player.Locomotion.Move(
            player.InputVector,
            _isRunning,
            player.IsLockedOn,
            player.LockOnTarget,
            Define.MainCam != null ? Define.MainCam.transform : null,
            Time.deltaTime);
    }

    public override void Exit()
    {
        player.SetAnimatorMoveRunning(false);

        if (player.Item != null && !player.Item.IsEstusUseCancelled)
        {
            player.Item.EndEstusUse();
        }

        ClearRuntimeFlags();
    }

    public void PrepareEnter(IState previousState)
    {
        _requestedUpperBodyDrinkOverlay = previousState == player.MoveState;
    }

    public void OnEstusHealApplyAnimationEvent()
    {
        ApplyHealOnce();
    }

    private void ApplyHealOnce()
    {
        if (_isHealApplied || player.Item == null)
        {
            return;
        }

        _isHealApplied = player.Item.ApplyEstusHeal();
    }

    private void UpdateMovingDrinkLocomotion()
    {
        if (!_usesUpperBodyDrinkOverlay)
        {
            _isRunning = false;
            player.SetAnimatorMoveRunning(false);
            return;
        }

        _isRunning = player.InputSnapshot.SprintHeld && player.CurrentStamina > 0f;
        if (_isRunning && (player.Stamina == null || !player.Stamina.TryUsePerSecond(statData.runStaminaCostPerSecond, Time.deltaTime)))
        {
            _isRunning = false;
        }

        if (player.InputVector.sqrMagnitude <= 0.0001f)
        {
            _isRunning = false;
        }

        player.SetAnimatorMoveRunning(_isRunning);
    }

    private void ClearRuntimeFlags()
    {
        _requestedUpperBodyDrinkOverlay = false;
        _usesUpperBodyDrinkOverlay = false;
        _isRunning = false;
    }
}
