using UnityEngine;

public sealed class PlayerLocomotionMotor
{
    private const float FreeMoveLargeTurnAngle = 135f;
    private const float FreeMoveMinTurnSpeedMultiplier = 0.45f;
    private const float FreeMoveMinMoveBlend = 0.35f;
    private const float FreeMoveMinSpeedMultiplier = 0.65f;
    private const float FreeMoveLateralTurnSpeedMultiplier = 0.55f;
    private const float FreeMoveLateralMoveBlendMultiplier = 0.45f;
    private const float FreeMoveTravelDirectionSpeedMultiplier = 0.8f;
    private const float FreeMoveRunTurnSpeedMultiplier = 0.82f;
    private const float FreeMoveRunMoveBlendMultiplier = 0.8f;
    private const float FreeMoveRunTravelDirectionSpeedMultiplier = 0.72f;
    private const float FreeMoveRunSpeedMultiplier = 0.9f;
    private const float GroundProbeDistance = 0.15f;
    private const float GroundSnapVelocity = -2f;
    private const float GroundSnapMaxRiseVelocity = 0.5f;
    private const float GroundSnapMaxSlopeAngle = 60f;

    private readonly PlayerStatData _statData;
    private readonly Rigidbody _rigidbody;
    private readonly Transform _ownerTransform;
    private readonly CapsuleCollider _capsuleCollider;
    private Vector3 _freeMoveTravelDirection;
    private bool _hasFreeMoveTravelDirection;

    public PlayerLocomotionMotor(PlayerStatData statData, Rigidbody rigidbody, Transform ownerTransform, CapsuleCollider capsuleCollider)
    {
        _statData = statData;
        _rigidbody = rigidbody;
        _ownerTransform = ownerTransform;
        _capsuleCollider = capsuleCollider;
    }

    // 자유 이동과 락온 이동의 실제 속도/회전 계산을 한곳에서 처리한다.
    public void Move(Vector2 input, bool isRunning, bool isLockedOn, Transform lockOnTarget, Transform cameraTransform, float deltaTime)
    {
        if (_statData == null || _rigidbody == null || _ownerTransform == null)
        {
            return;
        }

        float moveSpeed = _statData.moveSpeed;
        if (isRunning)
        {
            moveSpeed *= _statData.runSpeedMultiplier;
        }

        if (isLockedOn && lockOnTarget != null)
        {
            ResetFreeMoveSmoothing();
            MoveWithLockOn(input, lockOnTarget, moveSpeed, deltaTime);
            return;
        }

        MoveFree(input, cameraTransform, moveSpeed, isRunning, deltaTime);
    }

    // 기존 컨트롤러 래퍼 호환을 위해 속도 적용 함수는 별도로 유지한다.
    public void ApplyVelocity(Vector3 direction, float speed, bool rotateToMoveDirection, float deltaTime)
    {
        Vector3 currentVelocity = _rigidbody.velocity;
        Vector3 targetVelocity = direction * speed;
        targetVelocity.y = ResolveVerticalVelocity(currentVelocity.y);
        _rigidbody.velocity = targetVelocity;

        if (!rotateToMoveDirection || direction.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        RotateTowards(direction, deltaTime);
    }

    // 이동 중에는 현재 Y 속도를 그대로 보존하지 않고, 지면 근처에서는 약한 음수 속도로 붙여 둔다.
    private float ResolveVerticalVelocity(float currentVerticalVelocity)
    {
        if (_rigidbody == null)
        {
            return currentVerticalVelocity;
        }

        if (currentVerticalVelocity > GroundSnapMaxRiseVelocity)
        {
            return currentVerticalVelocity;
        }

        if (!TryGetGroundHit(out RaycastHit hit))
        {
            return currentVerticalVelocity;
        }

        if (Vector3.Angle(hit.normal, Vector3.up) > GroundSnapMaxSlopeAngle)
        {
            return currentVerticalVelocity;
        }

        return GroundSnapVelocity;
    }

    private bool TryGetGroundHit(out RaycastHit hit)
    {
        hit = default;
        if (_rigidbody == null)
        {
            return false;
        }

        if (_rigidbody.SweepTest(Vector3.down, out hit, GroundProbeDistance, QueryTriggerInteraction.Ignore))
        {
            return true;
        }

        if (_capsuleCollider == null)
        {
            return false;
        }

        Vector3 origin = _capsuleCollider.bounds.center + Vector3.up * 0.05f;
        float radius = Mathf.Max(0.05f, Mathf.Min(_capsuleCollider.bounds.extents.x, _capsuleCollider.bounds.extents.z) * 0.9f);
        float distance = _capsuleCollider.bounds.extents.y + GroundProbeDistance;

        return Physics.SphereCast(origin, radius, Vector3.down, out hit, distance, ~0, QueryTriggerInteraction.Ignore);
    }

    private void MoveWithLockOn(Vector2 input, Transform lockOnTarget, float moveSpeed, float deltaTime)
    {
        Vector3 toTarget = lockOnTarget.position - _ownerTransform.position;
        toTarget.y = 0f;
        if (toTarget.sqrMagnitude <= 0.0001f)
        {
            toTarget = _ownerTransform.forward;
            toTarget.y = 0f;
        }

        Vector3 lookDirection = toTarget.normalized;
        RotateTowards(lookDirection, deltaTime);

        Vector3 lockRight = Vector3.Cross(Vector3.up, lookDirection).normalized;
        Vector3 moveDirection = lookDirection * input.y + lockRight * input.x;
        if (moveDirection.sqrMagnitude > 0.0001f)
        {
            moveDirection.Normalize();
        }

        ApplyVelocity(moveDirection, moveSpeed, false, deltaTime);
    }

    private void MoveFree(Vector2 input, Transform cameraTransform, float moveSpeed, bool isRunning, float deltaTime)
    {
        if (cameraTransform == null)
        {
            ResetFreeMoveSmoothing();
            ApplyVelocity(Vector3.zero, moveSpeed, false, deltaTime);
            return;
        }

        Vector3 cameraForward = cameraTransform.forward;
        Vector3 cameraRight = cameraTransform.right;
        cameraForward.y = 0f;
        cameraRight.y = 0f;

        Vector3 desiredDirection = cameraForward.normalized * input.y + cameraRight.normalized * input.x;
        if (desiredDirection.sqrMagnitude <= 0.0001f)
        {
            ResetFreeMoveSmoothing();
            ApplyVelocity(Vector3.zero, moveSpeed, false, deltaTime);
            return;
        }

        desiredDirection.Normalize();
        float lateralBias = Mathf.Abs(input.x) / Mathf.Max(0.0001f, Mathf.Abs(input.x) + Mathf.Abs(input.y));

        Vector3 currentForward = _ownerTransform.forward;
        currentForward.y = 0f;
        if (currentForward.sqrMagnitude <= 0.0001f)
        {
            currentForward = desiredDirection;
        }
        else
        {
            currentForward.Normalize();
        }

        float turnAngle = Vector3.Angle(currentForward, desiredDirection);
        float turnFactor = Mathf.InverseLerp(FreeMoveLargeTurnAngle, 0f, turnAngle);
        float turnSpeedMultiplier = Mathf.Lerp(FreeMoveMinTurnSpeedMultiplier, 1f, turnFactor);
        turnSpeedMultiplier *= Mathf.Lerp(FreeMoveLateralTurnSpeedMultiplier, 1f, 1f - lateralBias);
        if (isRunning)
        {
            turnSpeedMultiplier *= FreeMoveRunTurnSpeedMultiplier;
        }
        RotateTowards(desiredDirection, deltaTime, turnSpeedMultiplier);

        Vector3 facingForward = _ownerTransform.forward;
        facingForward.y = 0f;
        if (facingForward.sqrMagnitude <= 0.0001f)
        {
            facingForward = desiredDirection;
        }
        else
        {
            facingForward.Normalize();
        }

        Vector3 currentTravelDirection = _hasFreeMoveTravelDirection ? _freeMoveTravelDirection : facingForward;
        if (currentTravelDirection.sqrMagnitude <= 0.0001f)
        {
            currentTravelDirection = desiredDirection;
        }
        else
        {
            currentTravelDirection.Normalize();
        }

        float travelTurnSpeedMultiplier = Mathf.Lerp(FreeMoveLateralTurnSpeedMultiplier, 1f, 1f - lateralBias);
        if (isRunning)
        {
            travelTurnSpeedMultiplier *= FreeMoveRunTravelDirectionSpeedMultiplier;
        }
        _freeMoveTravelDirection = RotateDirectionTowards(
            currentTravelDirection,
            desiredDirection,
            deltaTime,
            FreeMoveTravelDirectionSpeedMultiplier * travelTurnSpeedMultiplier);
        _hasFreeMoveTravelDirection = _freeMoveTravelDirection.sqrMagnitude > 0.0001f;

        float moveBlend = Mathf.Lerp(
            FreeMoveMinMoveBlend * Mathf.Lerp(FreeMoveLateralMoveBlendMultiplier, 1f, 1f - lateralBias),
            1f,
            turnFactor);
        if (isRunning)
        {
            moveBlend *= FreeMoveRunMoveBlendMultiplier;
        }

        Vector3 blendedDirection = Vector3.Slerp(
            facingForward,
            _freeMoveTravelDirection,
            moveBlend);
        if (blendedDirection.sqrMagnitude > 0.0001f)
        {
            blendedDirection.Normalize();
        }

        float adjustedMoveSpeed = moveSpeed * Mathf.Lerp(FreeMoveMinSpeedMultiplier, 1f, turnFactor);
        if (isRunning)
        {
            adjustedMoveSpeed *= FreeMoveRunSpeedMultiplier;
        }
        ApplyVelocity(blendedDirection, adjustedMoveSpeed, false, deltaTime);
    }

    // 회전 속도는 PlayerStatData의 rotationSpeed를 사용해 일관되게 맞춘다.
    private void RotateTowards(Vector3 direction, float deltaTime, float speedMultiplier = 1f)
    {
        if (direction.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        Quaternion currentRotation = _ownerTransform.rotation;
        Quaternion targetRotation = Quaternion.LookRotation(direction.normalized);
        float maxDegreesDelta = _statData.rotationSpeed * Mathf.Max(0f, speedMultiplier) * deltaTime;
        _ownerTransform.rotation = Quaternion.RotateTowards(currentRotation, targetRotation, maxDegreesDelta);
    }

    private Vector3 RotateDirectionTowards(Vector3 currentDirection, Vector3 targetDirection, float deltaTime, float speedMultiplier)
    {
        if (targetDirection.sqrMagnitude <= 0.0001f)
        {
            return Vector3.zero;
        }

        if (currentDirection.sqrMagnitude <= 0.0001f)
        {
            return targetDirection.normalized;
        }

        float maxRadiansDelta = _statData.rotationSpeed * Mathf.Max(0f, speedMultiplier) * Mathf.Deg2Rad * deltaTime;
        return Vector3.RotateTowards(
            currentDirection.normalized,
            targetDirection.normalized,
            maxRadiansDelta,
            0f).normalized;
    }

    private void ResetFreeMoveSmoothing()
    {
        _freeMoveTravelDirection = Vector3.zero;
        _hasFreeMoveTravelDirection = false;
    }
}
