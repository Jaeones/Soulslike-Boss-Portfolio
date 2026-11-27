using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerAnimationEvents : MonoBehaviour
{
    private PlayerController _playerController;

    private void Awake()
    {
        _playerController = GetComponentInParent<PlayerController>();
    }

    private void OnAnimatorMove()
    {
        if (_playerController == null) return;
        _playerController.ApplyAttackRootMotion();
        _playerController.ApplyRollRootMotion();
    }

    public void HitStart()
    {
        if (_playerController == null) return;
        _playerController.HitStart();
    }

    public void Hit()
    {
        if (_playerController == null) return;
        _playerController.Hit();
    }

    public void HitEnd()
    {
        if (_playerController == null) return;
        _playerController.HitEnd();
    }

    public void Run()
    {
        if (_playerController == null) return;
        _playerController.Run();
    }

    public void Roll()
    {
        if (_playerController == null) return;
        _playerController.Roll();
    }

    public void FootL()
    {
        if (_playerController == null) return;
        _playerController.OnFootstepAnimationEvent("L");
    }

    public void FootR()
    {
        if (_playerController == null) return;
        _playerController.OnFootstepAnimationEvent("R");
    }

    public void OnEstusHealApply()
    {
        if (_playerController == null) return;
        _playerController.OnEstusHealApplyAnimationEvent();
    }

    public void ShowEstusVisual()
    {
        if (_playerController == null) return;
        _playerController.ShowEstusVisualAnimationEvent();
    }

    public void HideEstusVisual()
    {
        if (_playerController == null) return;
        _playerController.HideEstusVisualAnimationEvent();
    }

    public void ShowHandWeapon()
    {
        if (_playerController == null) return;
        _playerController.ShowHandWeaponAnimationEvent();
    }

    public void ShowBackWeapon()
    {
        if (_playerController == null) return;
        _playerController.ShowBackWeaponAnimationEvent();
    }
}
