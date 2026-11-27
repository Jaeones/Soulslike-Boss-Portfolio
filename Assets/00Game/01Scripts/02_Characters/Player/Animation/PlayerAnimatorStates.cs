using System.Collections;
using UnityEngine;

public static class PlayerAnimatorStates
{
    public const string Intro = "Intro";
    public const string IdleA = "Idle_A";
    public const string IdleB = "Idle_B";
    public const string IdleAToB = "Idle_A_to_B";
    public const string IdleBToA = "Idle_B_to_A";
    public const string WalkA = "Walk_A";
    public const string WalkB = "Walk_B";
    public const string IdleAToWalkA = "Idle_A_To_Walk_A";
    public const string IdleBToWalkB = "Idle_B_To_Walk_B";
    public const string WalkAToIdleA = "Walk_A_To_Idle_A";
    public const string WalkBToIdleB = "Walk_B_To_Idle_B";
    public const string RunA = "Run_A";
    public const string RunB = "Run_B";
    public const string WalkLockOn = "WalkLockOn";
    public const string Run = "Run";
    public const string RunLockOn = "RunLockOn";
    public const string Roll = "Roll";

    public const string GuardStart = "Guard_Start";
    public const string GuardLoop = "Guard_Loop";
    public const string GuardEnd = "Guard_End";
    public const string GuardAccept = "Guard_Accept";
    public const string GuardChipHit = GuardAccept;
    public const string ParryStart = "Parry_Start";
    public const string ParrySuccess = "Parry_Success";
    public const string Parry = ParryStart;

    public const string HitFront = "HitFront";
    public const string HitBack = "HitBack";
    public const string HitLeft = "HitLeft";
    public const string HitRight = "HitRight";
    public const string HitSideBack = HitBack;
    public const string HitSlam = "HitSlam";
    public const string HitDown = "HitDown";
    public const string HitDash = "HitDash";
    public const string HitSmash = "HitSmash";
    public const string HitSpear = "HitSpear";

    public const string Attack1 = "Attack1";
    public const string Attack3 = "Attack3";
    public const string DashAttack = "Skill_A";
    public const string GroggyAttack = "Skill_G";
    public const string BonfireIgnite = "Bonfire_Ignite";
    public const string BonfireStart = "Bonfire_Start";
    public const string BonfireIdle = "Bonfire_Idle";
    public const string BonfireEnd = "Bonfire_End";
    public const string Interaction = "Interaction";
    public const string DrinkAstus = "DrinkAstus";
    public const string DrinkUpper = "Drink_Upper";
    public const string EquipUpper = "Equip_Upper";
    public const string UnequipUpper = "Unequip_Upper";

    public static int ToShortHash(string stateName)
    {
        return Animator.StringToHash(stateName);
    }

    public static int ToFullPathHash(string stateName)
    {
        return Animator.StringToHash($"Base Layer.{stateName}");
    }

    public static int ToUpperBodyEquipFullPathHash(string stateName)
    {
        return Animator.StringToHash($"UpperBody_Equip.{stateName}");
    }
}

public sealed class PlayerAnimatorLocomotionResolver
{
    public string ResolveMoveState(bool isBattleStance, bool isLockedOn, bool isRunning)
    {
        if (isLockedOn)
        {
            return isRunning ? PlayerAnimatorStates.RunLockOn : PlayerAnimatorStates.WalkLockOn;
        }

        if (isRunning)
        {
            return isBattleStance ? PlayerAnimatorStates.RunB : PlayerAnimatorStates.RunA;
        }

        return isBattleStance ? PlayerAnimatorStates.WalkB : PlayerAnimatorStates.WalkA;
    }

    public string ResolvePreferredLocomotionState(bool isBattleStance, bool isLockedOn, Vector2 input, bool isRunning)
    {
        if (input.sqrMagnitude > 0.0001f)
        {
            return ResolveMoveState(isBattleStance, isLockedOn, isRunning);
        }

        return isBattleStance ? PlayerAnimatorStates.IdleB : PlayerAnimatorStates.IdleA;
    }
}

public static class PlayerInteractionAnimationSequence
{
    private const float DefaultEnterCrossFadeDuration = 0.05f;
    private const float DefaultExitCrossFadeDuration = 0.08f;
    private const float DefaultCompletionNormalizedTime = 0.98f;

    public static bool TryStart(PlayerController player)
    {
        if (player?.AnimatorDriver == null || !player.AnimatorDriver.IsReady)
        {
            return false;
        }

        player.SetAnimatorMoveRunning(false);

        if (player.AnimatorDriver.TryCrossFadeToState(PlayerAnimatorStates.Interaction, DefaultEnterCrossFadeDuration, 0, 0f))
        {
            return true;
        }

        if (!player.AnimatorDriver.HasTrigger(PlayerAnimatorParams.InteractionTriggerName))
        {
            return false;
        }

        player.AnimatorDriver.ResetTrigger(PlayerAnimatorParams.InteractionTrigger);
        return player.TrySetAnimatorTrigger(PlayerAnimatorParams.InteractionTrigger, PlayerAnimatorParams.InteractionTriggerName);
    }

    public static IEnumerator WaitForCompletion(
        PlayerController player,
        float stateEnterTimeout,
        float animationTimeout,
        float completionNormalizedTime = DefaultCompletionNormalizedTime)
    {
        if (player?.AnimatorDriver == null || !player.AnimatorDriver.IsReady)
        {
            yield break;
        }

        bool enteredState = player.AnimatorDriver.IsCurrentState(PlayerAnimatorStates.Interaction);
        float enterExpireAt = Time.time + Mathf.Max(0.05f, stateEnterTimeout);

        while (!enteredState && Time.time <= enterExpireAt)
        {
            if (player.AnimatorDriver.IsCurrentState(PlayerAnimatorStates.Interaction))
            {
                enteredState = true;
                break;
            }

            yield return null;
        }

        if (!enteredState)
        {
            yield break;
        }

        bool requestedExit = false;
        float animationExpireAt = Time.time + Mathf.Max(0.05f, animationTimeout);

        while (Time.time <= animationExpireAt)
        {
            bool isCurrentState = player.AnimatorDriver.IsCurrentState(PlayerAnimatorStates.Interaction);
            bool isInTransition = player.AnimatorDriver.IsInTransition();

            if (!requestedExit && isCurrentState &&
                player.AnimatorDriver.GetCurrentStateNormalizedTime() >= completionNormalizedTime)
            {
                requestedExit = true;
                player.CrossFadeToLocomotion(DefaultExitCrossFadeDuration);
            }

            if (requestedExit && !isCurrentState && !isInTransition)
            {
                yield break;
            }

            if (!requestedExit && !isCurrentState && !isInTransition)
            {
                yield break;
            }

            yield return null;
        }

        player.CrossFadeToLocomotion(DefaultExitCrossFadeDuration);
    }
}
