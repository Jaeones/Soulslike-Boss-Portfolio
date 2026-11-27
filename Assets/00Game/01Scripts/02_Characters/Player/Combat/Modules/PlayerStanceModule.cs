using System;

public sealed class PlayerStanceModule
{
    private readonly Action<bool> _onBattleStanceChanged;

    public PlayerStanceModule(Action<bool> onBattleStanceChanged)
    {
        _onBattleStanceChanged = onBattleStanceChanged;
    }

    public bool IsBattleStance { get; private set; }

    public void Initialize(bool startInBattleStance)
    {
        IsBattleStance = startInBattleStance;
        _onBattleStanceChanged?.Invoke(IsBattleStance);
    }

    public void SetBattleStance(bool isBattleStance)
    {
        if (IsBattleStance == isBattleStance)
        {
            return;
        }

        IsBattleStance = isBattleStance;
        _onBattleStanceChanged?.Invoke(IsBattleStance);
    }

    // 락온/공격/가드처럼 전투 흐름에서 자세를 강제로 발도 상태로 맞춘다.
    public void ForceBattleStance()
    {
        SetBattleStance(true);
    }

    public bool CanToggleManually(bool isNeutralState, bool isLockedOn, bool isInCombatFlow)
    {
        if (!isNeutralState)
        {
            return false;
        }

        if (isLockedOn)
        {
            return false;
        }

        if (isInCombatFlow)
        {
            return false;
        }

        return true;
    }

    public bool TryToggleManually(bool isNeutralState, bool isLockedOn, bool isInCombatFlow)
    {
        if (!CanToggleManually(isNeutralState, isLockedOn, isInCombatFlow))
        {
            return false;
        }

        SetBattleStance(!IsBattleStance);
        return true;
    }
}
