using System.Collections.Generic;
using UnityEngine;

public sealed class PlayerAnimatorDriver
{
    private readonly Animator _animator;
    private readonly Dictionary<string, bool> _boolParamCache = new Dictionary<string, bool>();
    private readonly Dictionary<string, bool> _triggerParamCache = new Dictionary<string, bool>();

    public PlayerAnimatorDriver(Animator animator)
    {
        _animator = animator;
    }

    public bool IsReady => _animator != null;

    public bool HasBool(string name)
    {
        return HasParameter(name, AnimatorControllerParameterType.Bool, _boolParamCache);
    }

    public bool HasTrigger(string name)
    {
        return HasParameter(name, AnimatorControllerParameterType.Trigger, _triggerParamCache);
    }

    public void SetBool(int hash, bool value)
    {
        if (_animator == null)
        {
            return;
        }

        _animator.SetBool(hash, value);
    }

    public bool TrySetBool(int hash, string name, bool value)
    {
        if (!HasBool(name))
        {
            return false;
        }

        _animator.SetBool(hash, value);
        return true;
    }

    public void SetBoolIfExists(int hash, string name, bool value)
    {
        if (!HasBool(name))
        {
            return;
        }

        _animator.SetBool(hash, value);
    }

    public void SetFloat(int hash, float value)
    {
        if (_animator == null)
        {
            return;
        }

        _animator.SetFloat(hash, value);
    }

    public void SetFloat(int hash, float value, float dampTime, float deltaTime)
    {
        if (_animator == null)
        {
            return;
        }

        _animator.SetFloat(hash, value, dampTime, deltaTime);
    }

    public void SetLocomotionInput(Vector2 input, float dampTime, float deltaTime)
    {
        SetFloat(PlayerAnimatorParams.InputX, input.x, dampTime, deltaTime);
        SetFloat(PlayerAnimatorParams.InputY, input.y, dampTime, deltaTime);
    }

    public void SetInteger(int hash, int value)
    {
        if (_animator == null)
        {
            return;
        }

        _animator.SetInteger(hash, value);
    }

    public void ResetTrigger(int hash)
    {
        if (_animator == null)
        {
            return;
        }

        _animator.ResetTrigger(hash);
    }

    public bool TrySetTrigger(int hash, string name)
    {
        if (!HasTrigger(name))
        {
            return false;
        }

        _animator.SetTrigger(hash);
        return true;
    }

    public bool TrySetBattle(bool isBattle)
    {
        return TrySetBool(PlayerAnimatorParams.IsBattle, PlayerAnimatorParams.IsBattleName, isBattle);
    }

    public bool TrySetMove(bool isMove)
    {
        return TrySetBool(PlayerAnimatorParams.IsMove, PlayerAnimatorParams.IsMoveName, isMove);
    }

    public bool TrySetGuard(bool isGuard)
    {
        return TrySetBool(PlayerAnimatorParams.IsGuard, PlayerAnimatorParams.IsGuardName, isGuard);
    }

    public bool TrySetLockOn(bool isLockOn)
    {
        return TrySetBool(PlayerAnimatorParams.IsLockOn, PlayerAnimatorParams.IsLockOnName, isLockOn);
    }

    public bool HasState(string stateName, int layer = 0)
    {
        if (_animator == null || string.IsNullOrWhiteSpace(stateName))
        {
            return false;
        }

        int shortHash = PlayerAnimatorStates.ToShortHash(stateName);
        int fullHash = PlayerAnimatorStates.ToFullPathHash(stateName);
        if (_animator.HasState(layer, shortHash) || _animator.HasState(layer, fullHash))
        {
            return true;
        }

        if (layer == 1)
        {
            int upperBodyEquipFullHash = PlayerAnimatorStates.ToUpperBodyEquipFullPathHash(stateName);
            return _animator.HasState(layer, upperBodyEquipFullHash);
        }

        return false;
    }

    public bool TryCrossFadeToState(string stateName, float duration, int layer = 0, float normalizedTime = 0f)
    {
        if (!HasState(stateName, layer))
        {
            return false;
        }

        _animator.CrossFadeInFixedTime(stateName, Mathf.Max(0f, duration), layer, normalizedTime);
        return true;
    }

    public bool IsCurrentState(string stateName, int layer = 0)
    {
        if (_animator == null || string.IsNullOrWhiteSpace(stateName))
        {
            return false;
        }

        AnimatorStateInfo stateInfo = _animator.GetCurrentAnimatorStateInfo(layer);
        int shortHash = PlayerAnimatorStates.ToShortHash(stateName);
        int fullHash = PlayerAnimatorStates.ToFullPathHash(stateName);
        if (stateInfo.shortNameHash == shortHash || stateInfo.fullPathHash == fullHash)
        {
            return true;
        }

        if (layer == 1)
        {
            int upperBodyEquipFullHash = PlayerAnimatorStates.ToUpperBodyEquipFullPathHash(stateName);
            return stateInfo.fullPathHash == upperBodyEquipFullHash;
        }

        return false;
    }

    public bool IsInTransition(int layer = 0)
    {
        if (_animator == null)
        {
            return false;
        }

        return _animator.IsInTransition(layer);
    }

    public float GetCurrentStateNormalizedTime(int layer = 0)
    {
        if (_animator == null)
        {
            return 0f;
        }

        return _animator.GetCurrentAnimatorStateInfo(layer).normalizedTime;
    }

    private bool HasParameter(string name, AnimatorControllerParameterType type, Dictionary<string, bool> cache)
    {
        if (_animator == null || string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        if (cache.TryGetValue(name, out bool hasParameter))
        {
            return hasParameter;
        }

        foreach (AnimatorControllerParameter parameter in _animator.parameters)
        {
            if (parameter.type == type && parameter.name == name)
            {
                cache[name] = true;
                return true;
            }
        }

        cache[name] = false;
        return false;
    }
}
