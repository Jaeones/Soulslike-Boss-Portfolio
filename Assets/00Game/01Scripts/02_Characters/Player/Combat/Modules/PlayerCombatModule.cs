using UnityEngine;

public sealed class PlayerCombatModule
{
    private readonly PlayerController _player;
    private readonly WeaponHitBox _weaponHitBox;
    private float _activeStartNormalized;
    private float _activeEndNormalized;
    private float _currentAttackNormalizedTime;
    private bool _isHitWindowActive;
    private bool _hasPlayedAttackSwingSfx;

    public PlayerCombatModule(PlayerController player, WeaponHitBox weaponHitBox)
    {
        _player = player;
        _weaponHitBox = weaponHitBox;
    }

    public int CurrentAttackDamage { get; private set; }
    public string CurrentAttackName { get; private set; } = "Basic Attack";
    public CombatAttackDefinition CurrentAttackDefinition { get; private set; }
    public bool IsSuperArmorActive =>
        CurrentAttackDefinition != null &&
        CurrentAttackDefinition.IsInSuperArmorWindow(_currentAttackNormalizedTime);

    public void SetCurrentAttackDefinition(CombatAttackDefinition attack)
    {
        ForceHitEnd();
        CurrentAttackDefinition = attack;
        if (CurrentAttackDefinition == null)
        {
            CurrentAttackDamage = 0;
            CurrentAttackName = "Basic Attack";
            _activeStartNormalized = 0f;
            _activeEndNormalized = 0f;
            _currentAttackNormalizedTime = 0f;
            _hasPlayedAttackSwingSfx = false;
            return;
        }

        CurrentAttackDefinition.Normalize();
        CurrentAttackDamage = Mathf.Max(1, CurrentAttackDefinition.damage);
        CurrentAttackName = CurrentAttackDefinition.ResolvedAttackName;
        _activeStartNormalized = CurrentAttackDefinition.activeStartNormalized;
        _activeEndNormalized = CurrentAttackDefinition.activeEndNormalized;
        _currentAttackNormalizedTime = 0f;
        _hasPlayedAttackSwingSfx = false;
    }

    public void ClearCurrentAttackInfo()
    {
        CurrentAttackDefinition = null;
        _activeStartNormalized = 0f;
        _activeEndNormalized = 0f;
        _currentAttackNormalizedTime = 0f;
        CurrentAttackDamage = 0;
        CurrentAttackName = "Basic Attack";
        _hasPlayedAttackSwingSfx = false;
    }

    public void OnAnimationHitStart()
    {
        // Hit authority is driven by normalized-time attack windows.
        // Animation events are kept only for clip/tooling compatibility.
    }

    public void Hit()
    {
        // Hit authority is driven by normalized-time attack windows.
        // Animation events are kept only for clip/tooling compatibility.
    }

    public void OnAnimationHitEnd()
    {
        // Hit authority is driven by normalized-time attack windows.
        // Animation events are kept only for clip/tooling compatibility.
    }

    public void UpdateAttackWindow(float normalizedTime)
    {
        _currentAttackNormalizedTime = Mathf.Clamp01(normalizedTime);
        if (!UseDataDrivenHitWindow())
        {
            return;
        }

        float clampedTime = _currentAttackNormalizedTime;
        bool shouldBeActive = clampedTime >= _activeStartNormalized && clampedTime <= _activeEndNormalized;
        if (shouldBeActive != _isHitWindowActive)
        {
            SetHitWindowActive(shouldBeActive);
        }

    }

    public void ForceHitEnd()
    {
        _currentAttackNormalizedTime = 0f;
        _hasPlayedAttackSwingSfx = false;
        SetHitWindowActive(false);
    }

    public void TickActiveHitScan()
    {
        if (!UseDataDrivenHitWindow() || !_isHitWindowActive)
        {
            return;
        }

        TryExecuteHit(logMiss: false);
    }

    private bool UseDataDrivenHitWindow()
    {
        return _activeEndNormalized > _activeStartNormalized;
    }

    private void TryExecuteHit(bool logMiss)
    {
        if (_player == null || _player.StateMachine == null || _player.StateMachine.CurrentState != _player.AttackState)
        {
            return;
        }

        if (!_isHitWindowActive)
        {
            return;
        }

        if (_weaponHitBox == null)
        {
            Debug.LogWarning("Weapon hit box is not assigned.");
            return;
        }

        CombatAttackDefinition attack = CurrentAttackDefinition;
        if (attack == null)
        {
            attack = new CombatAttackDefinition
            {
                attackId = CurrentAttackName,
                attackName = CurrentAttackName,
                animatorStateName = CurrentAttackName,
                damage = CurrentAttackDamage,
                impactNormalized = _activeStartNormalized,
                activeStartNormalized = _activeStartNormalized,
                activeEndNormalized = _activeEndNormalized > _activeStartNormalized ? _activeEndNormalized : _activeStartNormalized,
                recoveryEndNormalized = 1f
            };
            attack.Normalize();
        }

        CombatHitResult hitResult = _weaponHitBox.PerformHit(attack, _player.transform.forward);
        if (hitResult.HasHit)
        {
            Debug.Log($"{CurrentAttackName} connected. Hits: {hitResult.HitCount}, Damage: {CurrentAttackDamage}");
            _player.PlayAttackHitFeedback(attack, hitResult);
        }
        else if (logMiss)
        {
            Debug.Log($"{CurrentAttackName} missed.");
        }
    }

    private void SetHitWindowActive(bool isActive)
    {
        _isHitWindowActive = isActive;
        if (isActive && !_hasPlayedAttackSwingSfx)
        {
            _hasPlayedAttackSwingSfx = true;
            _player?.PlayAttackSwingSfx();
        }

        if (_weaponHitBox == null)
        {
            return;
        }

        _weaponHitBox.SetHitBoxActive(isActive);
    }

    // 공격 정보와 히트박스를 함께 정리해 컨트롤러가 종료 처리만 조립하게 한다.
    public void ResetRuntimeState()
    {
        ForceHitEnd();
        ClearCurrentAttackInfo();
    }
}
