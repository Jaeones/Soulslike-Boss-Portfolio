using UnityEngine;

public static class PlayerAnimatorParams
{
    public const string IsIdleName = "isIdle";
    public const string IsMoveName = "isMove";
    public const string IsRunName = "isRun";
    public const string IsLockOnName = "isLockOn";
    public const string IsAttackName = "isAttack";
    public const string IsGuardName = "isGuard";
    public const string IsBattleName = "isBattle";
    public const string InputXName = "InputX";
    public const string InputYName = "InputY";
    public const string RollTriggerName = "tRoll";
    public const string HitFrontTriggerName = "tHitFront";
    public const string HitSideBackTriggerName = "tHitSideBack";
    public const string GuardChipHitTriggerName = "tGuardChipHit";
    public const string AttackTriggerName = "tAttack";
    public const string DrinkTriggerName = "tDrink";
    public const string BonfireIgniteTriggerName = "tBonfireIgnite";
    public const string BonfireStartTriggerName = "tBonfireStart";
    public const string BonfireEndTriggerName = "tBonfireEnd";
    public const string InteractionTriggerName = "tInteraction";
    public const string DrinkUpperTriggerName = "tDrinkUpper";
    public const string DeadTriggerName = "tDead";
    public const string EquipTriggerName = "tEquip";
    public const string UnequipTriggerName = "tUnequip";

    public static readonly int IsIdle = Animator.StringToHash(IsIdleName);
    public static readonly int IsMove = Animator.StringToHash(IsMoveName);
    public static readonly int IsRun = Animator.StringToHash(IsRunName);
    public static readonly int IsLockOn = Animator.StringToHash(IsLockOnName);
    public static readonly int IsAttack = Animator.StringToHash(IsAttackName);
    public static readonly int IsGuard = Animator.StringToHash(IsGuardName);
    public static readonly int IsBattle = Animator.StringToHash(IsBattleName);
    public static readonly int InputX = Animator.StringToHash(InputXName);
    public static readonly int InputY = Animator.StringToHash(InputYName);
    public static readonly int RollTrigger = Animator.StringToHash(RollTriggerName);
    public static readonly int HitFrontTrigger = Animator.StringToHash(HitFrontTriggerName);
    public static readonly int HitSideBackTrigger = Animator.StringToHash(HitSideBackTriggerName);
    public static readonly int GuardChipHitTrigger = Animator.StringToHash(GuardChipHitTriggerName);
    public static readonly int AttackTrigger = Animator.StringToHash(AttackTriggerName);
    public static readonly int DrinkTrigger = Animator.StringToHash(DrinkTriggerName);
    public static readonly int BonfireIgniteTrigger = Animator.StringToHash(BonfireIgniteTriggerName);
    public static readonly int BonfireStartTrigger = Animator.StringToHash(BonfireStartTriggerName);
    public static readonly int BonfireEndTrigger = Animator.StringToHash(BonfireEndTriggerName);
    public static readonly int InteractionTrigger = Animator.StringToHash(InteractionTriggerName);
    public static readonly int DrinkUpperTrigger = Animator.StringToHash(DrinkUpperTriggerName);
    public static readonly int DeadTrigger = Animator.StringToHash(DeadTriggerName);
    public static readonly int EquipTrigger = Animator.StringToHash(EquipTriggerName);
    public static readonly int UnequipTrigger = Animator.StringToHash(UnequipTriggerName);
}
