using UnityEngine;

public sealed class PlayerCameraStateController
{
    private readonly Animator _cameraAnimator;
    private readonly int _animatorLayerIndex;
    private readonly float _stateBlendDuration;

    public PlayerCameraStateController(Animator cameraAnimator, int animatorLayerIndex, float stateBlendDuration)
    {
        _cameraAnimator = cameraAnimator;
        _animatorLayerIndex = animatorLayerIndex;
        _stateBlendDuration = stateBlendDuration;
    }

    // 카메라 상태 전환과 방어 로직을 모아 PlayerCam은 상위 흐름만 조립하게 한다.
    public void PlayState(string targetStateName)
    {
        if (_cameraAnimator == null)
        {
            Debug.LogWarning("카메라 Animator가 연결되지 않아 State-Driven 전환을 실행할 수 없습니다.");
            return;
        }

        if (string.IsNullOrWhiteSpace(targetStateName))
        {
            Debug.LogWarning("카메라 상태 이름이 비어 있어 전환을 생략합니다.");
            return;
        }

        _cameraAnimator.CrossFade(targetStateName, Mathf.Max(0f, _stateBlendDuration), _animatorLayerIndex);
    }
}
