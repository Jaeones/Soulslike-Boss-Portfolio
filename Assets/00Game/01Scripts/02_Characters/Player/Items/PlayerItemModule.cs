using System.Collections;
using UnityEngine;

public sealed class PlayerItemModule
{
    private readonly PlayerController _player;
    private readonly bool _useEstusItemModule;
    private readonly int _legacyMaxFlaskCount;
    private readonly float _legacyFlaskHealAmount;
    private readonly float _legacyFlaskUseDelay;
    private readonly float _legacyFlaskAfterDelay;

    private PlayerEstusFlask _estusFlask;
    private Coroutine _legacyFlaskCoroutine;
    private bool _isUsingLegacyFlask;
    private int _currentFlaskCount;
    private bool _isEstusUseCancelled;
    private float _appliedEstusHealThisUse;

    public PlayerItemModule(
        PlayerController player,
        bool useEstusItemModule,
        int legacyMaxFlaskCount,
        float legacyFlaskHealAmount,
        float legacyFlaskUseDelay,
        float legacyFlaskAfterDelay)
    {
        _player = player;
        _useEstusItemModule = useEstusItemModule;
        _legacyMaxFlaskCount = legacyMaxFlaskCount;
        _legacyFlaskHealAmount = legacyFlaskHealAmount;
        _legacyFlaskUseDelay = legacyFlaskUseDelay;
        _legacyFlaskAfterDelay = legacyFlaskAfterDelay;
    }

    public bool IsUsingLegacyFlask => _isUsingLegacyFlask;
    public bool IsEstusModuleReady => _useEstusItemModule && _estusFlask != null;
    public bool IsEstusUseCancelled => _isEstusUseCancelled;
    public bool IsEstusUseActive => _estusFlask != null && _estusFlask.IsUsing;
    public int CurrentFlaskCount => _currentFlaskCount;
    public int MaxFlaskCount => IsEstusModuleReady ? _estusFlask.MaxUses : _legacyMaxFlaskCount;
    public PlayerEstusFlask EstusFlask => _estusFlask;

    public void Initialize(PlayerEstusFlask estusFlask)
    {
        _estusFlask = estusFlask;
        _isUsingLegacyFlask = false;

        if (IsEstusModuleReady)
        {
            _estusFlask.Initialize();
            _estusFlask.HideHeldVisual();
            _currentFlaskCount = _estusFlask.CurrentUses;
        }
        else
        {
            _currentFlaskCount = _legacyMaxFlaskCount;
        }

        PublishFlaskChanged();
    }

    public bool TryUseEstusModuleInput(PlayerInputSnapshot inputSnapshot, IState currentState)
    {
        if (!IsEstusModuleReady || _estusFlask == null)
        {
            return false;
        }

        if (!inputSnapshot.UseFlaskPressed)
        {
            return false;
        }

        if (_player.StateMachine.CurrentState == _player.UseItemState)
        {
            return true;
        }

        if (!_estusFlask.CanUse(_player, currentState))
        {
            return false;
        }

        _player.UseItemState?.PrepareEnter(currentState);
        _player.StateMachine.ChangeState(_player.UseItemState);
        return true;
    }

    public void TryUseLegacyFlaskInput(PlayerInputSnapshot inputSnapshot, IState currentState)
    {
        if (IsEstusModuleReady || !inputSnapshot.UseFlaskPressed)
        {
            return;
        }

        if (_currentFlaskCount <= 0)
        {
            Debug.Log("No flask uses left.");
            return;
        }

        if (currentState == _player.RollState || currentState == _player.AttackState)
        {
            Debug.Log("Cannot use flask in the current state.");
            return;
        }

        if (Mathf.Approximately(_player.CurrentHp, _player.MaxHp))
        {
            Debug.Log("HP is already full.");
            return;
        }

        if (_legacyFlaskCoroutine != null)
        {
            _player.StopCoroutine(_legacyFlaskCoroutine);
        }

        _legacyFlaskCoroutine = _player.StartCoroutine(UseLegacyFlaskRoutine());
    }

    public bool TryBeginEstusUse(IState currentState)
    {
        if (_estusFlask == null || !_estusFlask.TryBeginUse(_player, currentState))
        {
            return false;
        }

        _isEstusUseCancelled = false;
        _appliedEstusHealThisUse = 0f;
        return true;
    }

    public float GetEstusDrinkDuration()
    {
        return _estusFlask != null ? _estusFlask.DrinkDuration : 0.8f;
    }

    public float GetEstusHealApplyTime()
    {
        return _estusFlask != null ? _estusFlask.HealApplyTime : 0.4f;
    }

    public bool ApplyEstusHeal()
    {
        if (_estusFlask == null || _isEstusUseCancelled)
        {
            return false;
        }

        float beforeHp = _player.CurrentHp;
        if (!_estusFlask.ApplyHeal(_player))
        {
            return false;
        }

        _appliedEstusHealThisUse += Mathf.Max(0f, _player.CurrentHp - beforeHp);
        SyncEstusCountFromModule();
        return true;
    }

    public void EndEstusUse()
    {
        if (_estusFlask == null)
        {
            return;
        }

        _estusFlask.EndUse();
        if (_isEstusUseCancelled)
        {
            return;
        }

        _appliedEstusHealThisUse = 0f;
    }

    public void SyncEstusCountFromModule()
    {
        if (!IsEstusModuleReady || _estusFlask == null)
        {
            return;
        }

        _currentFlaskCount = _estusFlask.CurrentUses;
        PublishFlaskChanged();
    }

    public void HandleDeath()
    {
        InterruptActiveUse();
    }

    public void HandleRespawnRefill()
    {
        InterruptActiveUse();

        if (IsEstusModuleReady && _estusFlask != null)
        {
            _estusFlask.RefillAll();
            _currentFlaskCount = _estusFlask.CurrentUses;
        }
        else
        {
            _currentFlaskCount = _legacyMaxFlaskCount;
        }

        PublishFlaskChanged();
    }

    public void InterruptActiveUse()
    {
        StopLegacyFlaskRoutine();
        _player.AnimatorDriver?.ResetTrigger(PlayerAnimatorParams.DrinkTrigger);
        _player.AnimatorDriver?.ResetTrigger(PlayerAnimatorParams.DrinkUpperTrigger);

        if (_estusFlask == null)
        {
            return;
        }

        _isEstusUseCancelled = true;
        if (_appliedEstusHealThisUse > 0f)
        {
            _player.RevertHeal(_appliedEstusHealThisUse);
            _appliedEstusHealThisUse = 0f;
        }

        _estusFlask.EndUse();
        _estusFlask.HideHeldVisual();
    }

    private IEnumerator UseLegacyFlaskRoutine()
    {
        _isUsingLegacyFlask = true;
        _player.StateMachine.ChangeState(_player.IdleState);
        _player.SetVelocity(Vector3.zero);
        _player.SetGuarding(false);

        _currentFlaskCount = Mathf.Max(0, _currentFlaskCount - 1);
        PublishFlaskChanged();

        if (!_player.TrySetAnimatorTrigger(PlayerAnimatorParams.DrinkTrigger, PlayerAnimatorParams.DrinkTriggerName))
        {
            Debug.LogWarning("Animator does not have tDrink. Applying heal without drink motion.");
        }

        Debug.Log("Started flask use.");
        yield return new WaitForSeconds(_legacyFlaskUseDelay);

        float prevHp = _player.CurrentHp;
        _player.Heal(_legacyFlaskHealAmount);
        _player.NotifyStaminaChanged();
        Debug.Log($"Flask heal applied: {prevHp} -> {_player.CurrentHp}");

        yield return new WaitForSeconds(_legacyFlaskAfterDelay);
        _isUsingLegacyFlask = false;
        _legacyFlaskCoroutine = null;
    }

    private void StopLegacyFlaskRoutine()
    {
        if (_legacyFlaskCoroutine != null)
        {
            _player.StopCoroutine(_legacyFlaskCoroutine);
            _legacyFlaskCoroutine = null;
        }

        _isUsingLegacyFlask = false;
    }

    private void PublishFlaskChanged()
    {
        _player.PublishFlaskChanged(_currentFlaskCount, MaxFlaskCount);
    }
}
