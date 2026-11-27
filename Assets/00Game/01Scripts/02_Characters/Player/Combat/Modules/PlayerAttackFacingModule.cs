using UnityEngine;

public sealed class PlayerAttackFacingModule
{
    private readonly PlayerController _player;
    private readonly float _turnSpeed;

    private bool _isActive;
    private float _trackUntilNormalized;
    private float _facingLockNormalized;
    private Vector3 _desiredForward = Vector3.forward;
    private Vector3 _lockedForward = Vector3.forward;

    public PlayerAttackFacingModule(PlayerController player, float turnSpeed)
    {
        _player = player;
        _turnSpeed = turnSpeed;
    }

    public void Begin(float trackUntilNormalized, float facingLockNormalized)
    {
        if (_player == null)
        {
            return;
        }

        _isActive = true;
        _trackUntilNormalized = Mathf.Clamp01(trackUntilNormalized);
        _facingLockNormalized = Mathf.Clamp01(Mathf.Max(_trackUntilNormalized, facingLockNormalized));
        _desiredForward = ResolveAttackFacingDirection();
        _lockedForward = _desiredForward;
        ApplyImmediateFacing(_desiredForward);
    }

    public void Update(float normalizedTime)
    {
        if (_player == null || !_isActive)
        {
            return;
        }

        float clampedTime = Mathf.Clamp01(normalizedTime);
        if (clampedTime <= _trackUntilNormalized)
        {
            Vector3 desiredForward = ResolveAttackFacingDirection();
            if (desiredForward.sqrMagnitude > 0.0001f)
            {
                _desiredForward = desiredForward;
            }
        }

        if (clampedTime <= _facingLockNormalized)
        {
            RotateTowardsFacing(_desiredForward, _turnSpeed * Time.deltaTime);
            _lockedForward = _player.transform.forward;
            return;
        }

        ApplyImmediateFacing(_lockedForward);
    }

    public void End()
    {
        if (_player == null)
        {
            return;
        }

        _isActive = false;
        _trackUntilNormalized = 0f;
        _facingLockNormalized = 0f;
        _desiredForward = _player.transform.forward;
        _lockedForward = _player.transform.forward;
    }

    private Vector3 ResolveAttackFacingDirection()
    {
        if (_player.IsLockedOn && _player.LockOnTarget != null)
        {
            Vector3 toTarget = _player.LockOnTarget.position - _player.transform.position;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude > 0.0001f)
            {
                return toTarget.normalized;
            }
        }

        if (_player.InputVector.sqrMagnitude > 0.0001f && Define.MainCam != null)
        {
            Vector3 cameraForward = Define.MainCam.transform.forward;
            Vector3 cameraRight = Define.MainCam.transform.right;
            cameraForward.y = 0f;
            cameraRight.y = 0f;

            Vector3 desiredForward = cameraForward.normalized * _player.InputVector.y +
                                     cameraRight.normalized * _player.InputVector.x;
            desiredForward.y = 0f;
            if (desiredForward.sqrMagnitude > 0.0001f)
            {
                return desiredForward.normalized;
            }
        }

        Vector3 fallbackForward = _player.transform.forward;
        fallbackForward.y = 0f;
        if (fallbackForward.sqrMagnitude > 0.0001f)
        {
            return fallbackForward.normalized;
        }

        return Vector3.forward;
    }

    private void RotateTowardsFacing(Vector3 desiredForward, float maxDegreesDelta)
    {
        desiredForward.y = 0f;
        if (desiredForward.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(desiredForward.normalized, Vector3.up);
        _player.transform.rotation = Quaternion.RotateTowards(
            _player.transform.rotation,
            targetRotation,
            Mathf.Max(0f, maxDegreesDelta));
    }

    private void ApplyImmediateFacing(Vector3 desiredForward)
    {
        desiredForward.y = 0f;
        if (desiredForward.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        _player.transform.rotation = Quaternion.LookRotation(desiredForward.normalized, Vector3.up);
    }
}
