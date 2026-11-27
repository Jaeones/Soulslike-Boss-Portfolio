using Cinemachine;
using UnityEngine;

public sealed class LockOnPresentationModule
{
    private readonly Transform _lockOnLookPoint;
    private GameObject _lockOnIndicatorInstance;
    private Vector3 _lookPointVelocity;

    public LockOnPresentationModule()
    {
        GameObject lookPointObject = new GameObject("LockOnLookPoint");
        _lockOnLookPoint = lookPointObject.transform;
    }

    public void ApplyLockOnCameraTargets(
        Transform followTarget,
        Transform playerCameraTarget,
        Transform target,
        CinemachineVirtualCamera lockOnCamera,
        float lookAtHeightOffset,
        float lookAtTargetWeight,
        float minTargetHorizontalBias,
        float maxTargetHorizontalBias,
        float distanceForMaxBias,
        float maxTargetVerticalBias,
        float lookPointSmoothTime)
    {
        if (_lockOnLookPoint == null || followTarget == null || playerCameraTarget == null || target == null || lockOnCamera == null)
        {
            return;
        }

        Vector3 playerFocusPosition = playerCameraTarget.position + Vector3.up * lookAtHeightOffset;
        Vector3 targetFocusPosition = GetLockOnTargetFocusPosition(target, lookAtHeightOffset);
        Vector3 desiredLookPoint = CalculateDarkSoulsStyleLookPoint(
            playerFocusPosition,
            targetFocusPosition,
            lookAtTargetWeight,
            minTargetHorizontalBias,
            maxTargetHorizontalBias,
            distanceForMaxBias,
            maxTargetVerticalBias);

        bool shouldSnapLookPoint =
            lockOnCamera.LookAt != _lockOnLookPoint ||
            (_lockOnLookPoint.position - desiredLookPoint).sqrMagnitude >= 9f;

        if (lookPointSmoothTime <= 0f || shouldSnapLookPoint)
        {
            _lockOnLookPoint.position = desiredLookPoint;
            _lookPointVelocity = Vector3.zero;
        }
        else
        {
            _lockOnLookPoint.position = Vector3.SmoothDamp(
                _lockOnLookPoint.position,
                desiredLookPoint,
                ref _lookPointVelocity,
                lookPointSmoothTime,
                Mathf.Infinity,
                Time.unscaledDeltaTime > 0f ? Time.unscaledDeltaTime : Time.deltaTime);
        }

        lockOnCamera.LookAt = _lockOnLookPoint;
        lockOnCamera.Follow = followTarget;
    }

    public void UpdateIndicator(
        Transform target,
        GameObject indicatorPrefab,
        Vector3 indicatorOffset,
        bool indicatorFaceCamera)
    {
        if (target == null)
        {
            SetIndicatorActive(false);
            return;
        }

        if (!EnsureLockOnIndicatorInstance(indicatorPrefab, indicatorFaceCamera))
        {
            return;
        }

        if (!TryUpdateIndicatorPosition(target, indicatorOffset))
        {
            SetIndicatorActive(false);
            return;
        }

        SetIndicatorActive(true);
    }

    public void SetIndicatorActive(bool active)
    {
        if (_lockOnIndicatorInstance == null)
        {
            return;
        }

        if (_lockOnIndicatorInstance.activeSelf != active)
        {
            _lockOnIndicatorInstance.SetActive(active);
        }
    }

    public void ClearCameraTargets(CinemachineVirtualCamera lockOnCamera, Transform playerTransform)
    {
        if (lockOnCamera == null)
        {
            return;
        }

        _lookPointVelocity = Vector3.zero;
        lockOnCamera.LookAt = null;
        lockOnCamera.Follow = playerTransform;
    }

    public void Dispose()
    {
        if (_lockOnLookPoint != null)
        {
            Object.Destroy(_lockOnLookPoint.gameObject);
        }

        if (_lockOnIndicatorInstance != null)
        {
            Object.Destroy(_lockOnIndicatorInstance);
        }
    }

    private bool TryUpdateIndicatorPosition(Transform target, Vector3 indicatorOffset)
    {
        Canvas indicatorCanvas = _lockOnIndicatorInstance != null ? _lockOnIndicatorInstance.GetComponent<Canvas>() : null;
        if (indicatorCanvas == null || indicatorCanvas.renderMode == RenderMode.WorldSpace)
        {
            return TryUpdateWorldSpaceIndicator(target, indicatorOffset);
        }

        Vector3 worldPosition = GetTargetChestPosition(target) + indicatorOffset;
        Camera activeCamera = ResolveIndicatorCamera();
        if (activeCamera == null)
        {
            return false;
        }

        Vector3 screenPoint = activeCamera.WorldToScreenPoint(worldPosition);
        if (screenPoint.z <= 0f)
        {
            return false;
        }

        RectTransform canvasRect = indicatorCanvas.transform as RectTransform;
        if (canvasRect == null)
        {
            return false;
        }

        Camera eventCamera = indicatorCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : activeCamera;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect,
                screenPoint,
                eventCamera,
                out Vector2 localPoint))
        {
            return false;
        }

        for (int i = 0; i < canvasRect.childCount; i++)
        {
            if (canvasRect.GetChild(i) is RectTransform childRect)
            {
                childRect.anchoredPosition = localPoint;
            }
        }

        return true;
    }

    private bool TryUpdateWorldSpaceIndicator(Transform target, Vector3 indicatorOffset)
    {
        Transform indicatorTransform = _lockOnIndicatorInstance.transform;
        Transform lockOnAnchor = FindLockOnAnchor(target);
        if (lockOnAnchor != null)
        {
            if (indicatorTransform.parent != lockOnAnchor)
            {
                indicatorTransform.SetParent(lockOnAnchor, false);
            }

            indicatorTransform.localPosition = indicatorOffset;
            indicatorTransform.localRotation = Quaternion.identity;
            return true;
        }

        if (indicatorTransform.parent != null)
        {
            indicatorTransform.SetParent(null, true);
        }

        indicatorTransform.position = GetTargetChestPosition(target) + indicatorOffset;
        indicatorTransform.rotation = Quaternion.identity;
        return true;
    }

    private Vector3 GetLockOnTargetFocusPosition(Transform target, float lookAtHeightOffset)
    {
        if (target == null)
        {
            return Vector3.zero;
        }

        Collider targetCollider = target.GetComponentInChildren<Collider>();
        if (targetCollider == null)
        {
            return target.position + Vector3.up * lookAtHeightOffset;
        }

        Bounds bounds = targetCollider.bounds;
        float upperBodyOffset = Mathf.Min(bounds.extents.y * 0.35f, lookAtHeightOffset);
        return bounds.center + Vector3.up * upperBodyOffset;
    }

    private Vector3 GetTargetChestPosition(Transform target)
    {
        if (target == null)
        {
            return Vector3.zero;
        }

        Transform lockOnAnchor = FindLockOnAnchor(target);
        if (lockOnAnchor != null)
        {
            return lockOnAnchor.position;
        }

        Animator targetAnimator = target.GetComponentInChildren<Animator>();
        if (targetAnimator != null && targetAnimator.isHuman)
        {
            Transform chestBone = targetAnimator.GetBoneTransform(HumanBodyBones.UpperChest);
            if (chestBone == null)
            {
                chestBone = targetAnimator.GetBoneTransform(HumanBodyBones.Chest);
            }

            if (chestBone == null)
            {
                chestBone = targetAnimator.GetBoneTransform(HumanBodyBones.Spine);
            }

            if (chestBone != null)
            {
                return chestBone.position;
            }
        }

        Collider targetCollider = target.GetComponentInChildren<Collider>();
        if (targetCollider != null)
        {
            Bounds bounds = targetCollider.bounds;
            return bounds.center + Vector3.up * (bounds.extents.y * 0.15f);
        }

        return target.position;
    }

    private Transform FindLockOnAnchor(Transform target)
    {
        if (target == null)
        {
            return null;
        }

        Transform directMatch = FindLockOnAnchorInHierarchy(target);
        if (directMatch != null)
        {
            return directMatch;
        }

        Transform ancestor = target.parent;
        while (ancestor != null)
        {
            Transform ancestorMatch = FindLockOnAnchorInHierarchy(ancestor);
            if (ancestorMatch != null)
            {
                return ancestorMatch;
            }

            ancestor = ancestor.parent;
        }

        return null;
    }

    private Transform FindLockOnAnchorInHierarchy(Transform root)
    {
        if (root == null)
        {
            return null;
        }

        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            Transform child = children[i];
            if (child == null)
            {
                continue;
            }

            string childName = child.name;
            if (string.IsNullOrWhiteSpace(childName))
            {
                continue;
            }

            if (string.Equals(childName.Trim(), "LockOnAnchor", System.StringComparison.OrdinalIgnoreCase))
            {
                return child;
            }
        }

        return null;
    }

    private Vector3 CalculateDarkSoulsStyleLookPoint(
        Vector3 playerFocusPosition,
        Vector3 targetFocusPosition,
        float lookAtTargetWeight,
        float minTargetHorizontalBias,
        float maxTargetHorizontalBias,
        float distanceForMaxBias,
        float maxTargetVerticalBias)
    {
        float framingStrength = Mathf.Clamp01(lookAtTargetWeight);
        Vector3 toTarget = targetFocusPosition - playerFocusPosition;
        Vector3 horizontalToTarget = Vector3.ProjectOnPlane(toTarget, Vector3.up);
        float horizontalDistance = horizontalToTarget.magnitude;

        float safeDistanceForMaxBias = Mathf.Max(0.01f, distanceForMaxBias);
        float distanceT = Mathf.Clamp01(horizontalDistance / safeDistanceForMaxBias);
        float baseHorizontalBias = Mathf.Lerp(minTargetHorizontalBias, maxTargetHorizontalBias, distanceT);
        float horizontalBias = baseHorizontalBias * framingStrength;

        Vector3 horizontalOffset = horizontalDistance > 0.001f
            ? horizontalToTarget / horizontalDistance * horizontalBias
            : Vector3.zero;

        float verticalBias = Mathf.Clamp(toTarget.y * framingStrength, -maxTargetVerticalBias, maxTargetVerticalBias);
        return playerFocusPosition + horizontalOffset + Vector3.up * verticalBias;
    }

    private Camera ResolveIndicatorCamera()
    {
        if (Define.MainCam != null)
        {
            return Define.MainCam;
        }

        return Camera.main;
    }

    private bool EnsureLockOnIndicatorInstance(GameObject indicatorPrefab, bool indicatorFaceCamera)
    {
        if (_lockOnIndicatorInstance != null)
        {
            EnsureBillboardComponent(_lockOnIndicatorInstance, indicatorFaceCamera);
            return true;
        }

        if (indicatorPrefab == null)
        {
            Debug.LogWarning("락온 표시 프리팹이 연결되지 않아 대상 표시를 생략합니다.");
            return false;
        }

        _lockOnIndicatorInstance = Object.Instantiate(indicatorPrefab);
        _lockOnIndicatorInstance.name = "LockOnIndicator_Runtime";
        EnsureBillboardComponent(_lockOnIndicatorInstance, indicatorFaceCamera);
        _lockOnIndicatorInstance.SetActive(false);
        return true;
    }

    private void EnsureBillboardComponent(GameObject indicatorInstance, bool indicatorFaceCamera)
    {
        if (indicatorInstance == null)
        {
            return;
        }

        Billboard billboard = indicatorInstance.GetComponent<Billboard>();
        if (billboard == null)
        {
            billboard = indicatorInstance.AddComponent<Billboard>();
        }

        Canvas indicatorCanvas = indicatorInstance.GetComponent<Canvas>();
        bool useBillboard = indicatorFaceCamera && (indicatorCanvas == null || indicatorCanvas.renderMode == RenderMode.WorldSpace);
        billboard.enabled = useBillboard;
    }
}
