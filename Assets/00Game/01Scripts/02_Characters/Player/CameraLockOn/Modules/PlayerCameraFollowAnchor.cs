using Cinemachine;
using UnityEngine;

public sealed class PlayerCameraFollowAnchor
{
    public enum RotationMode
    {
        Identity,
        CopyYaw
    }

    private readonly Transform _positionSourceTransform;
    private readonly Transform _rotationSourceTransform;
    private readonly Transform _anchorTransform;
    private readonly RotationMode _rotationMode;
    private Vector3 _followVelocity;

    public PlayerCameraFollowAnchor(
        Transform positionSourceTransform,
        Transform rotationSourceTransform = null,
        RotationMode rotationMode = RotationMode.Identity)
    {
        _positionSourceTransform = positionSourceTransform;
        _rotationSourceTransform = rotationSourceTransform;
        _rotationMode = rotationMode;

        GameObject anchorObject = new GameObject("CameraFollowAnchor_Runtime");
        _anchorTransform = anchorObject.transform;
        SyncImmediate();
    }

    public Transform AnchorTransform => _anchorTransform;

    public void SyncImmediate()
    {
        if (_positionSourceTransform == null || _anchorTransform == null)
        {
            return;
        }

        _anchorTransform.position = _positionSourceTransform.position;
        _anchorTransform.rotation = ResolveRotation();
        _followVelocity = Vector3.zero;
    }

    public void Sync(float deltaTime, float smoothTime, float snapDistance)
    {
        if (_positionSourceTransform == null || _anchorTransform == null)
        {
            return;
        }

        Vector3 targetPosition = _positionSourceTransform.position;
        _anchorTransform.rotation = ResolveRotation();

        if (smoothTime <= 0f || deltaTime <= 0f)
        {
            _anchorTransform.position = targetPosition;
            _followVelocity = Vector3.zero;
            return;
        }

        if ((_anchorTransform.position - targetPosition).sqrMagnitude >= snapDistance * snapDistance)
        {
            _anchorTransform.position = targetPosition;
            _followVelocity = Vector3.zero;
            return;
        }

        _anchorTransform.position = Vector3.SmoothDamp(
            _anchorTransform.position,
            targetPosition,
            ref _followVelocity,
            smoothTime,
            Mathf.Infinity,
            deltaTime);
    }

    public void ApplyToFreeLookCamera(CinemachineVirtualCameraBase freeLookCamera)
    {
        if (_anchorTransform == null || freeLookCamera == null)
        {
            return;
        }

        if (freeLookCamera is CinemachineFreeLook freeLook)
        {
            freeLook.Follow = _anchorTransform;
            freeLook.LookAt = _anchorTransform;
            return;
        }

        if (freeLookCamera is CinemachineVirtualCamera virtualCamera)
        {
            virtualCamera.Follow = _anchorTransform;
            virtualCamera.LookAt = _anchorTransform;
        }
    }

    public void Dispose()
    {
        if (_anchorTransform != null)
        {
            Object.Destroy(_anchorTransform.gameObject);
        }
    }

    private Quaternion ResolveRotation()
    {
        if (_rotationMode != RotationMode.CopyYaw)
        {
            return Quaternion.identity;
        }

        Transform rotationSource = _rotationSourceTransform != null ? _rotationSourceTransform : _positionSourceTransform;
        if (rotationSource == null)
        {
            return Quaternion.identity;
        }

        Vector3 euler = rotationSource.eulerAngles;
        return Quaternion.Euler(0f, euler.y, 0f);
    }
}
