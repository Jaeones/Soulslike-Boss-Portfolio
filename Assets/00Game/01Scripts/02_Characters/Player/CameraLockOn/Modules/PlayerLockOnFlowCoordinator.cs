using Cinemachine;
using UnityEngine;

public sealed class PlayerLockOnFlowCoordinator
{
    private readonly PlayerController _player;
    private readonly LockOnSystem _lockOnSystem;
    private readonly CinemachineVirtualCamera _lockOnCamera;
    private readonly Transform _cameraFollowTarget;
    private readonly Transform _playerCameraTarget;
    private readonly PlayerCameraStateController _cameraStateController;
    private readonly FreeLookSyncModule _freeLookSyncModule;
    private readonly LockOnPresentationModule _lockOnPresentationModule;
    private readonly string _freeCamStateName;
    private readonly string _lockOnCamStateName;
    private readonly bool _syncFreeLookDuringLockOn;
    private readonly float _lookAtTargetWeight;
    private readonly float _lookAtHeightOffset;
    private readonly float _lockOnMinTargetHorizontalBias;
    private readonly float _lockOnMaxTargetHorizontalBias;
    private readonly float _lockOnDistanceForMaxBias;
    private readonly float _lockOnMaxTargetVerticalBias;
    private readonly float _lockOnLookPointSmoothTime;
    private readonly GameObject _lockOnIndicatorPrefab;
    private readonly Vector3 _lockOnIndicatorOffset;
    private readonly bool _indicatorFaceCamera;

    public PlayerLockOnFlowCoordinator(
        PlayerController player,
        LockOnSystem lockOnSystem,
        CinemachineVirtualCamera lockOnCamera,
        Transform cameraFollowTarget,
        Transform playerCameraTarget,
        PlayerCameraStateController cameraStateController,
        FreeLookSyncModule freeLookSyncModule,
        LockOnPresentationModule lockOnPresentationModule,
        string freeCamStateName,
        string lockOnCamStateName,
        bool syncFreeLookDuringLockOn,
        float lookAtTargetWeight,
        float lookAtHeightOffset,
        float lockOnMinTargetHorizontalBias,
        float lockOnMaxTargetHorizontalBias,
        float lockOnDistanceForMaxBias,
        float lockOnMaxTargetVerticalBias,
        float lockOnLookPointSmoothTime,
        GameObject lockOnIndicatorPrefab,
        Vector3 lockOnIndicatorOffset,
        bool indicatorFaceCamera)
    {
        _player = player;
        _lockOnSystem = lockOnSystem;
        _lockOnCamera = lockOnCamera;
        _cameraFollowTarget = cameraFollowTarget;
        _playerCameraTarget = playerCameraTarget;
        _cameraStateController = cameraStateController;
        _freeLookSyncModule = freeLookSyncModule;
        _lockOnPresentationModule = lockOnPresentationModule;
        _freeCamStateName = freeCamStateName;
        _lockOnCamStateName = lockOnCamStateName;
        _syncFreeLookDuringLockOn = syncFreeLookDuringLockOn;
        _lookAtTargetWeight = lookAtTargetWeight;
        _lookAtHeightOffset = lookAtHeightOffset;
        _lockOnMinTargetHorizontalBias = lockOnMinTargetHorizontalBias;
        _lockOnMaxTargetHorizontalBias = lockOnMaxTargetHorizontalBias;
        _lockOnDistanceForMaxBias = lockOnDistanceForMaxBias;
        _lockOnMaxTargetVerticalBias = lockOnMaxTargetVerticalBias;
        _lockOnLookPointSmoothTime = lockOnLookPointSmoothTime;
        _lockOnIndicatorPrefab = lockOnIndicatorPrefab;
        _lockOnIndicatorOffset = lockOnIndicatorOffset;
        _indicatorFaceCamera = indicatorFaceCamera;
    }

    public void UpdateLockOnFrame()
    {
        if (_player == null)
        {
            return;
        }

        if (_player.IsLockedOn)
        {
            if (_lockOnSystem == null || !_lockOnSystem.ValidateCurrentTarget())
            {
                ForceUnlock(false);
                return;
            }

            if (_lockOnCamera != null)
            {
                _lockOnPresentationModule?.ApplyLockOnCameraTargets(
                    _cameraFollowTarget != null ? _cameraFollowTarget : _player.transform,
                    _playerCameraTarget != null ? _playerCameraTarget : _player.transform,
                    _lockOnSystem.CurrentTarget,
                    _lockOnCamera,
                    _lookAtHeightOffset,
                    _lookAtTargetWeight,
                    _lockOnMinTargetHorizontalBias,
                    _lockOnMaxTargetHorizontalBias,
                    _lockOnDistanceForMaxBias,
                    _lockOnMaxTargetVerticalBias,
                    _lockOnLookPointSmoothTime);
            }
            else
            {
                Debug.LogWarning("락온 카메라 참조가 없어 타겟 추적이 적용되지 않습니다.");
            }

            _lockOnPresentationModule?.UpdateIndicator(
                _lockOnSystem.CurrentTarget,
                _lockOnIndicatorPrefab,
                _lockOnIndicatorOffset,
                _indicatorFaceCamera);
            return;
        }

        _lockOnPresentationModule?.SetIndicatorActive(false);
    }

    public void ToggleLockOn()
    {
        if (_player == null || _lockOnSystem == null)
        {
            Debug.LogWarning("락온 시스템 참조가 없습니다.");
            return;
        }

        if (_player.IsLockedOn)
        {
            ForceUnlock(false);
            return;
        }

        bool isLocked = _lockOnSystem.TryLockOn();
        if (!isLocked)
        {
            Debug.Log("전방에 락온 가능한 대상이 없습니다.");
            return;
        }

        if (_lockOnCamera != null)
        {
            PrepareLockOnCamera();
            _lockOnPresentationModule?.ApplyLockOnCameraTargets(
                _cameraFollowTarget != null ? _cameraFollowTarget : _player.transform,
                _playerCameraTarget != null ? _playerCameraTarget : _player.transform,
                _lockOnSystem.CurrentTarget,
                _lockOnCamera,
                _lookAtHeightOffset,
                _lookAtTargetWeight,
                _lockOnMinTargetHorizontalBias,
                _lockOnMaxTargetHorizontalBias,
                _lockOnDistanceForMaxBias,
                _lockOnMaxTargetVerticalBias,
                _lockOnLookPointSmoothTime);
        }

        _cameraStateController?.PlayState(_lockOnCamStateName);
        _lockOnPresentationModule?.UpdateIndicator(
            _lockOnSystem.CurrentTarget,
            _lockOnIndicatorPrefab,
            _lockOnIndicatorOffset,
            _indicatorFaceCamera);
        Debug.Log($"락온 대상: {_lockOnSystem.CurrentTarget.name}");
    }

    public void ForceUnlock(bool silentLog = true)
    {
        if (_lockOnSystem != null)
        {
            _lockOnSystem.ClearLockOn();
        }

        _lockOnPresentationModule?.SetIndicatorActive(false);
        _lockOnPresentationModule?.ClearCameraTargets(_lockOnCamera, _cameraFollowTarget != null ? _cameraFollowTarget : _player != null ? _player.transform : null);
        _freeLookSyncModule?.SyncFromMainCamera(_syncFreeLookDuringLockOn);
        _cameraStateController?.PlayState(_freeCamStateName);

        if (!silentLog)
        {
            Debug.Log("락온 해제");
        }
    }

    private void PrepareLockOnCamera()
    {
        if (_lockOnCamera == null)
        {
            return;
        }

        CinemachineOrbitalTransposer orbital = _lockOnCamera.GetCinemachineComponent<CinemachineOrbitalTransposer>();
        if (orbital != null)
        {
            orbital.m_XAxis.Value = 0f;
            orbital.m_XAxis.m_InputAxisValue = 0f;
        }

        _lockOnCamera.PreviousStateIsValid = false;
    }
}
