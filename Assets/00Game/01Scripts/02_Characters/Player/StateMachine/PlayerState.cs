using UnityEngine;

public class PlayerState : IState
{
    protected PlayerController player;
    protected StateMachine stateMachine;
    protected PlayerStatData statData;

    protected float startTime;
    protected string animBoolName;
    protected int animBoolHash;

    public PlayerState(PlayerController player, StateMachine stateMachine, PlayerStatData statData)
    {
        this.player = player;
        this.stateMachine = stateMachine;
        this.statData = statData;
    }

    public virtual void Enter()
    {
        if (!string.IsNullOrEmpty(animBoolName))
        {
            player.TrySetAnimatorBool(animBoolHash, animBoolName, true);
        }

        startTime = Time.time;
        Debug.Log($"State Enter: {animBoolName}");
    }

    public virtual void Exit()
    {
        if (!string.IsNullOrEmpty(animBoolName))
        {
            player.TrySetAnimatorBool(animBoolHash, animBoolName, false);
        }
    }

    public virtual void HandleInput() { }
    public virtual void Update() { }
    public virtual void PhysicsUpdate() { }
}

public class PlayerHitState : PlayerState
{
    private const float RollBufferDuration = 0.22f;
    private const float HitStateEntryGrace = 0.1f;
    private const float HitSmashSlowPlaybackSpeed = 0.5f;
    private const float HitReactionDefaultPlaybackSpeed = 1f;
    private const float HitSmashSlowUntilNormalized = 8f / 59f;

    private bool _hasObservedHitReaction;
    private float _bufferedRollExpireAt;

    public PlayerHitState(PlayerController player, StateMachine stateMachine, PlayerStatData statData)
        : base(player, stateMachine, statData)
    {
    }

    public override void Enter()
    {
        base.Enter();
        _hasObservedHitReaction = false;
        _bufferedRollExpireAt = 0f;
        RestoreHitReactionPlaybackSpeed();

        player.SetGuarding(false);
        player.SetAnimatorMoveRunning(false);
        player.SetRollRootMotionEnabled(false);
        player.SetAttackRootMotionEnabled(false);
        player.IsInvincible = false;
        player.ClearHorizontalVelocity();
    }

    public override void Update()
    {
        base.Update();
        UpdateHitReactionPlaybackSpeed();

        if (player.InputSnapshot.RollPressed && player.CanBufferRollDuringCurrentHitReaction())
        {
            // 피격 리커버리 직후 한 번만 롤을 이어받도록 짧게 버퍼링한다.
            _bufferedRollExpireAt = Time.unscaledTime + RollBufferDuration;
        }

        bool isHitReactionActive = player.IsHitReactionAnimatorStateActive();
        if (isHitReactionActive)
        {
            _hasObservedHitReaction = true;

            if (!IsHitRecoveryReached())
            {
                return;
            }
        }
        else
        {
            if (player.AnimatorDriver != null && player.AnimatorDriver.IsInTransition())
            {
                return;
            }

            if (!_hasObservedHitReaction && Time.time - startTime < HitStateEntryGrace)
            {
                return;
            }
        }

        if (ShouldConsumeBufferedRoll())
        {
            player.ClearHitMoveRecoveryIntent();
            stateMachine.ChangeState(player.RollState);
            return;
        }

        player.RestoreLocomotionAfterHit();
    }

    public override void Exit()
    {
        _bufferedRollExpireAt = 0f;
        RestoreHitReactionPlaybackSpeed();
        player.ClearCurrentHitReactionProfile();
        base.Exit();
    }

    private bool ShouldConsumeBufferedRoll()
    {
        if (Time.unscaledTime > _bufferedRollExpireAt)
        {
            return false;
        }

        if (statData == null)
        {
            return false;
        }

        return player.CurrentStamina >= statData.rollCost;
    }

    private bool IsHitRecoveryReached()
    {
        if (player.AnimatorDriver == null || player.AnimatorDriver.IsInTransition())
        {
            return false;
        }

        return player.AnimatorDriver.GetCurrentStateNormalizedTime() >= player.GetCurrentHitRecoveryNormalized();
    }

    private void UpdateHitReactionPlaybackSpeed()
    {
        if (player.Anim == null || player.AnimatorDriver == null)
        {
            return;
        }

        if (!player.AnimatorDriver.IsCurrentState(PlayerAnimatorStates.HitSmash) || player.AnimatorDriver.IsInTransition())
        {
            RestoreHitReactionPlaybackSpeed();
            return;
        }

        float normalizedTime = Mathf.Clamp01(player.AnimatorDriver.GetCurrentStateNormalizedTime());
        player.Anim.speed = normalizedTime <= HitSmashSlowUntilNormalized
            ? HitSmashSlowPlaybackSpeed
            : HitReactionDefaultPlaybackSpeed;
    }

    private void RestoreHitReactionPlaybackSpeed()
    {
        if (player.Anim == null)
        {
            return;
        }

        player.Anim.speed = HitReactionDefaultPlaybackSpeed;
    }
}
