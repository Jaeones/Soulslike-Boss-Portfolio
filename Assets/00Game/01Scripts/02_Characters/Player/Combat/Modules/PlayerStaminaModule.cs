using System;
using UnityEngine;

public sealed class PlayerStaminaModule
{
    private readonly PlayerStatData _statData;
    private readonly Action<float, float> _onStaminaChanged;

    public PlayerStaminaModule(PlayerStatData statData, Action<float, float> onStaminaChanged)
    {
        _statData = statData;
        _onStaminaChanged = onStaminaChanged;
    }

    public float Current { get; private set; }
    public float Max => _statData != null ? _statData.maxStamina : 0f;
    public bool IsEmpty => Current <= 0f;

    // 스태미나를 최대치로 초기화하고 UI/이벤트를 동기화한다.
    public void Initialize()
    {
        Current = Max;
        PublishChanged();
    }

    // 리스폰이나 디버그 보정처럼 값을 직접 맞춰야 할 때 사용한다.
    public void ForceSet(float value)
    {
        Current = Mathf.Clamp(value, 0f, Max);
        PublishChanged();
    }

    public void Refill()
    {
        ForceSet(Max);
    }

    public bool CanUse(float amount)
    {
        if (amount <= 0f)
        {
            return true;
        }

        return Current >= amount;
    }

    public bool TryUse(float amount)
    {
        if (!CanUse(amount))
        {
            return false;
        }

        if (amount <= 0f)
        {
            return true;
        }

        Current = Mathf.Clamp(Current - amount, 0f, Max);
        PublishChanged();
        return true;
    }

    // 초당 소모 규칙을 공통화해서 이동/가드 등 지속 소모에 재사용한다.
    public bool TryUsePerSecond(float amountPerSecond, float deltaTime)
    {
        return TryUse(amountPerSecond * deltaTime);
    }

    // 가드 여부에 따른 회복 배율까지 모듈 안에서 처리한다.
    public void Recover(float deltaTime, bool isGuarding)
    {
        if (_statData == null || Current >= Max)
        {
            return;
        }

        float recoverMultiplier = isGuarding ? _statData.guardStaminaRecoveryMultiplier : 1f;
        float recoverAmount = _statData.staminaRecovery * recoverMultiplier * deltaTime;
        float nextValue = Mathf.Clamp(Current + recoverAmount, 0f, Max);
        if (Mathf.Approximately(nextValue, Current))
        {
            return;
        }

        Current = nextValue;
        PublishChanged();
    }

    private void PublishChanged()
    {
        _onStaminaChanged?.Invoke(Current, Max);
    }
}
