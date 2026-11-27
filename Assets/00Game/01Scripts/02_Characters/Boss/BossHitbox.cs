using System;
using System.Collections.Generic;
using UnityEngine;

public class BossHitbox : MonoBehaviour
{
    private enum HitQueryType
    {
        WeaponBox,
        ForwardBodyBox,
        DashBodyBox,
        SlamSphere,
        StompSphere,
        WhirlwindSphere
    }

    private static readonly Collider[] EmptyOverlaps = Array.Empty<Collider>();

    [Header("Settings")]
    public EnemyBase owner;
    [SerializeField] private Collider hitCollider;
    [SerializeField] private LayerMask targetLayer;

    [Header("Named Anchors")]
    [SerializeField] private string slamPointName = "Slam_Point";
    [SerializeField] private string stompPointName = "Stomp_Point";

    [Header("Dash Query")]
    [SerializeField] private Vector3 dashBodyHalfExtents = new Vector3(0.85f, 1.0f, 1.45f);
    [SerializeField] private float dashBodyForwardOffset = 1.15f;

    [Header("Forward Query")]
    [SerializeField] private Vector3 forwardBodyHalfExtents = new Vector3(1.1f, 1.0f, 1.3f);
    [SerializeField] private float forwardBodyForwardOffset = 1.0f;

    [Header("Ground Query")]
    [SerializeField] private float slamRadius = 1.6f;
    [SerializeField] private float stompRadius = 2.1f;
    [SerializeField] private float whirlwindRadius = 1.85f;
    [SerializeField] private float groundQueryVerticalOffset = 0.12f;

    private readonly Dictionary<IDamageable, int> _targetHitCounts = new Dictionary<IDamageable, int>();
    private readonly Dictionary<IDamageable, float> _targetNextHitNormalizedTimes = new Dictionary<IDamageable, float>();
    private CombatAttackDefinition _currentAttack;
    private Transform _ownerRoot;
    private Transform _slamPoint;
    private Transform _stompPoint;
    private Transform _weaponTrailRoot;
    private MeshFilter[] _weaponTrailMeshFilters = Array.Empty<MeshFilter>();
    private bool _isActive;
    private float _currentNormalizedTime;
    private const float SkillCFirstHitNormalized = 13f / 70f;
    private const float SkillCSecondHitNormalized = 26f / 70f;
    private const float SkillCHitWindowNormalized = 0.04f;
    private const float SkillKFirstHitNormalized = 16f / 60f;
    private const float SkillKSecondHitNormalized = 31f / 60f;
    private const float SkillKHitWindowNormalized = 0.04f;
    private const float SkillGAllLastFrame = 72f;
    private const float SkillGFirstHitNormalized = 12f / SkillGAllLastFrame;
    private const float SkillGSecondHitNormalized = 38f / SkillGAllLastFrame;
    private const float SkillGHitWindowNormalized = 3f / SkillGAllLastFrame;

    private void Awake()
    {
        ResolveDependencies();
    }

    private void OnDisable()
    {
        ForceHitEnd(clearAttack: false);
    }

    public void Initialize(EnemyBase attackOwner)
    {
        owner = attackOwner;
        ResolveDependencies();
    }

    public void ConfigureQueryShapes(
        Vector3 forwardHalfExtentsOverride,
        float forwardForwardOffsetOverride,
        Vector3 dashHalfExtentsOverride,
        float dashForwardOffsetOverride,
        float slamRadiusOverride,
        float stompRadiusOverride,
        float whirlwindRadiusOverride,
        float groundQueryVerticalOffsetOverride)
    {
        forwardBodyHalfExtents = new Vector3(
            Mathf.Max(0.05f, forwardHalfExtentsOverride.x),
            Mathf.Max(0.05f, forwardHalfExtentsOverride.y),
            Mathf.Max(0.05f, forwardHalfExtentsOverride.z));
        forwardBodyForwardOffset = Mathf.Max(0f, forwardForwardOffsetOverride);

        dashBodyHalfExtents = new Vector3(
            Mathf.Max(0.05f, dashHalfExtentsOverride.x),
            Mathf.Max(0.05f, dashHalfExtentsOverride.y),
            Mathf.Max(0.05f, dashHalfExtentsOverride.z));
        dashBodyForwardOffset = Mathf.Max(0f, dashForwardOffsetOverride);

        slamRadius = Mathf.Max(0.05f, slamRadiusOverride);
        stompRadius = Mathf.Max(0.05f, stompRadiusOverride);
        whirlwindRadius = Mathf.Max(0.05f, whirlwindRadiusOverride);
        groundQueryVerticalOffset = Mathf.Max(0f, groundQueryVerticalOffsetOverride);
    }

    public void SetHitCollider(Collider collider)
    {
        hitCollider = collider;
    }

    public void BeginAttack(EnemyBase attackOwner, CombatAttackDefinition attack)
    {
        owner = attackOwner;
        _currentAttack = attack;
        ResolveDependencies();
        ForceHitEnd(clearAttack: false);
        ClearHitTracking();
    }

    public void UpdateAttackWindow(float normalizedTime)
    {
        if (_currentAttack == null)
        {
            return;
        }

        float clampedTime = Mathf.Clamp01(normalizedTime);
        _currentNormalizedTime = clampedTime;
        bool shouldBeActive = clampedTime >= _currentAttack.activeStartNormalized &&
                              clampedTime <= _currentAttack.activeEndNormalized;
        if (shouldBeActive != _isActive)
        {
            SetHitBoxActive(shouldBeActive);
        }
    }

    public CombatHitResult PerformHit()
    {
        if (!_isActive || _currentAttack == null)
        {
            return default;
        }

        Collider[] overlaps = QueryOverlaps(out Vector3 queryOrigin);
        if (overlaps == null || overlaps.Length == 0)
        {
            return default;
        }

        int hitCount = 0;
        Vector3 firstHitPoint = Vector3.zero;
        Vector3 firstHitNormal = Vector3.zero;
        bool hasFirstHit = false;

        foreach (Collider col in overlaps)
        {
            IDamageable target = ResolveDamageable(col);
            if (target == null)
            {
                continue;
            }

            MonoBehaviour targetMb = target as MonoBehaviour;
            if (targetMb == null)
            {
                continue;
            }

            Transform targetRoot = targetMb.transform.root;
            if (_ownerRoot != null && targetRoot == _ownerRoot)
            {
                continue;
            }

            if (!CanHitTarget(target))
            {
                continue;
            }

            _targetHitCounts.TryGetValue(target, out int currentHitCount);
            int hitPhaseIndex = ResolveScheduledHitPhaseIndex(currentHitCount);
            if (hitPhaseIndex < 0)
            {
                continue;
            }

            CombatAttackDefinition resolvedAttack = ResolveAttackForHit(hitPhaseIndex);
            RegisterHitTarget(target, currentHitCount, hitPhaseIndex);
            Vector3 hitPoint = col.ClosestPoint(queryOrigin);
            Vector3 hitNormal = ResolveHitNormal(hitPoint);
            if (!hasFirstHit)
            {
                firstHitPoint = hitPoint;
                firstHitNormal = hitNormal;
                hasFirstHit = true;
            }

            using (CombatDamageContext.Push(resolvedAttack, owner != null ? owner.transform : _ownerRoot))
            {
                target.TakeDamage(resolvedAttack.damage, hitPoint, hitNormal);
            }
            hitCount++;
        }

        return new CombatHitResult(hitCount, firstHitPoint, firstHitNormal);
    }

    public void EndAttack()
    {
        ForceHitEnd(clearAttack: true);
    }

    public bool TryGetWeaponTrailSample(out Vector3 basePoint, out Vector3 tipPoint)
    {
        basePoint = transform.position;
        tipPoint = transform.position;

        if (hitCollider == null)
        {
            return false;
        }

        if (TryGetWeaponTrailSampleFromMeshes(out basePoint, out tipPoint))
        {
            return true;
        }

        if (hitCollider is BoxCollider boxCollider)
        {
            Transform colliderTransform = boxCollider.transform;
            Vector3 center = colliderTransform.TransformPoint(boxCollider.center);
            float halfLength = Mathf.Abs(boxCollider.size.z * colliderTransform.lossyScale.z) * 0.5f;
            Vector3 forward = colliderTransform.forward.sqrMagnitude > 0.0001f
                ? colliderTransform.forward.normalized
                : transform.forward;

            basePoint = center - forward * halfLength;
            tipPoint = center + forward * halfLength;
            return true;
        }

        Bounds bounds = hitCollider.bounds;
        Vector3 fallbackForward = hitCollider.transform.forward.sqrMagnitude > 0.0001f
            ? hitCollider.transform.forward.normalized
            : transform.forward;
        float projectedHalfLength =
            Mathf.Abs(fallbackForward.x) * bounds.extents.x +
            Mathf.Abs(fallbackForward.y) * bounds.extents.y +
            Mathf.Abs(fallbackForward.z) * bounds.extents.z;

        basePoint = bounds.center - fallbackForward * projectedHalfLength;
        tipPoint = bounds.center + fallbackForward * projectedHalfLength;
        return true;
    }

    public void SetHitBoxActive(bool active)
    {
        _isActive = active;
        if (active)
        {
            ClearHitTracking();
        }

        if (hitCollider != null)
        {
            hitCollider.enabled = active;
        }
    }

    private void ResolveDependencies()
    {
        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
        }

        if (hitCollider == null)
        {
            hitCollider = GetComponent<Collider>();
        }

        if (hitCollider != null)
        {
            hitCollider.isTrigger = true;
            hitCollider.enabled = false;
        }

        if (owner == null)
        {
            owner = GetComponentInParent<EnemyBase>();
        }

        _ownerRoot = owner != null ? owner.transform : transform.root;
        _slamPoint = ResolveNamedPoint(slamPointName);
        _stompPoint = ResolveNamedPoint(stompPointName);
        ResolveWeaponTrailGeometry();
        EnsureTargetLayer();
    }

    private void ResolveWeaponTrailGeometry()
    {
        _weaponTrailRoot = hitCollider != null
            ? (hitCollider.transform.parent != null ? hitCollider.transform.parent : hitCollider.transform)
            : (transform.parent != null ? transform.parent : transform);

        _weaponTrailMeshFilters = _weaponTrailRoot != null
            ? _weaponTrailRoot.GetComponentsInChildren<MeshFilter>(includeInactive: true)
            : Array.Empty<MeshFilter>();
    }

    private bool TryGetWeaponTrailSampleFromMeshes(out Vector3 basePoint, out Vector3 tipPoint)
    {
        basePoint = transform.position;
        tipPoint = transform.position;

        if (_weaponTrailMeshFilters == null || _weaponTrailMeshFilters.Length == 0)
        {
            return false;
        }

        Vector3 axisOrigin;
        Vector3 axisDirection;
        ResolveWeaponTrailAxis(out axisOrigin, out axisDirection);
        if (axisDirection.sqrMagnitude <= 0.0001f)
        {
            return false;
        }

        bool hasProjection = false;
        float minProjection = 0f;
        float maxProjection = 0f;

        foreach (MeshFilter meshFilter in _weaponTrailMeshFilters)
        {
            if (meshFilter == null || meshFilter.sharedMesh == null)
            {
                continue;
            }

            if (meshFilter.transform == hitCollider.transform)
            {
                continue;
            }

            Bounds localBounds = meshFilter.sharedMesh.bounds;
            Vector3 min = localBounds.min;
            Vector3 max = localBounds.max;

            for (int x = 0; x <= 1; x++)
            {
                for (int y = 0; y <= 1; y++)
                {
                    for (int z = 0; z <= 1; z++)
                    {
                        Vector3 localCorner = new Vector3(
                            x == 0 ? min.x : max.x,
                            y == 0 ? min.y : max.y,
                            z == 0 ? min.z : max.z);
                        Vector3 worldCorner = meshFilter.transform.TransformPoint(localCorner);
                        float projection = Vector3.Dot(worldCorner - axisOrigin, axisDirection);

                        if (!hasProjection)
                        {
                            minProjection = projection;
                            maxProjection = projection;
                            hasProjection = true;
                            continue;
                        }

                        minProjection = Mathf.Min(minProjection, projection);
                        maxProjection = Mathf.Max(maxProjection, projection);
                    }
                }
            }
        }

        if (!hasProjection || maxProjection - minProjection <= 0.01f)
        {
            return false;
        }

        basePoint = axisOrigin + axisDirection * minProjection;
        tipPoint = axisOrigin + axisDirection * maxProjection;
        return true;
    }

    private void ResolveWeaponTrailAxis(out Vector3 axisOrigin, out Vector3 axisDirection)
    {
        axisOrigin = transform.position;
        axisDirection = transform.forward;

        if (hitCollider is BoxCollider boxCollider)
        {
            Transform colliderTransform = boxCollider.transform;
            axisOrigin = colliderTransform.TransformPoint(boxCollider.center);
            axisDirection = colliderTransform.forward.sqrMagnitude > 0.0001f
                ? colliderTransform.forward.normalized
                : transform.forward;
            return;
        }

        if (hitCollider != null)
        {
            axisOrigin = hitCollider.bounds.center;
            axisDirection = hitCollider.transform.forward.sqrMagnitude > 0.0001f
                ? hitCollider.transform.forward.normalized
                : transform.forward;
            return;
        }

        if (_weaponTrailRoot != null)
        {
            axisOrigin = _weaponTrailRoot.position;
            axisDirection = _weaponTrailRoot.forward.sqrMagnitude > 0.0001f
                ? _weaponTrailRoot.forward.normalized
                : transform.forward;
        }
    }

    private void EnsureTargetLayer()
    {
        if (targetLayer.value != 0)
        {
            return;
        }

        Transform target = owner != null ? owner.target : null;
        if (target != null)
        {
            targetLayer = 1 << target.gameObject.layer;
        }
    }

    private LayerMask ResolveTargetMask()
    {
        return targetLayer.value != 0 ? targetLayer : Physics.AllLayers;
    }

    private static IDamageable ResolveDamageable(Component targetComponent)
    {
        if (targetComponent == null)
        {
            return null;
        }

        MonoBehaviour[] behaviours = targetComponent.GetComponentsInParent<MonoBehaviour>(true);
        foreach (MonoBehaviour behaviour in behaviours)
        {
            if (behaviour is IDamageable damageable)
            {
                return damageable;
            }
        }

        return null;
    }

    private Vector3 ResolveHitNormal(Vector3 hitPoint)
    {
        Vector3 origin = owner != null ? owner.transform.position : transform.position;
        Vector3 hitNormal = hitPoint - origin;
        hitNormal.y = 0f;

        if (hitNormal.sqrMagnitude <= 0.0001f)
        {
            hitNormal = owner != null ? owner.transform.forward : transform.forward;
        }

        return hitNormal.normalized;
    }

    private Collider[] QueryOverlaps(out Vector3 queryOrigin)
    {
        HitQueryType queryType = ResolveHitQueryType();
        LayerMask mask = ResolveTargetMask();

        switch (queryType)
        {
            case HitQueryType.ForwardBodyBox:
            {
                queryOrigin = ResolveForwardQueryCenter();
                return Physics.OverlapBox(
                    queryOrigin,
                    forwardBodyHalfExtents,
                    ResolveOwnerRotation(),
                    mask,
                    QueryTriggerInteraction.Collide);
            }

            case HitQueryType.DashBodyBox:
            {
                queryOrigin = ResolveDashQueryCenter();
                return Physics.OverlapBox(
                    queryOrigin,
                    dashBodyHalfExtents,
                    ResolveOwnerRotation(),
                    mask,
                    QueryTriggerInteraction.Collide);
            }

            case HitQueryType.SlamSphere:
            {
                queryOrigin = ResolveGroundQueryCenter(_slamPoint, slamRadius);
                return Physics.OverlapSphere(
                    queryOrigin,
                    slamRadius,
                    mask,
                    QueryTriggerInteraction.Collide);
            }

            case HitQueryType.StompSphere:
            {
                queryOrigin = ResolveGroundQueryCenter(_stompPoint, stompRadius);
                return Physics.OverlapSphere(
                    queryOrigin,
                    stompRadius,
                    mask,
                    QueryTriggerInteraction.Collide);
            }

            case HitQueryType.WhirlwindSphere:
            {
                queryOrigin = ResolveWhirlwindQueryCenter();
                return Physics.OverlapSphere(
                    queryOrigin,
                    whirlwindRadius,
                    mask,
                    QueryTriggerInteraction.Collide);
            }

            default:
            {
                if (hitCollider == null)
                {
                    queryOrigin = ResolveFallbackCenter();
                    return EmptyOverlaps;
                }

                Bounds bounds = hitCollider.bounds;
                queryOrigin = bounds.center;
                return Physics.OverlapBox(
                    bounds.center,
                    bounds.extents,
                    hitCollider.transform.rotation,
                    mask,
                    QueryTriggerInteraction.Collide);
            }
        }
    }

    private HitQueryType ResolveHitQueryType()
    {
        string attackId = _currentAttack != null ? _currentAttack.ResolvedAttackId : string.Empty;

        if (string.IsNullOrWhiteSpace(attackId))
        {
            return HitQueryType.WeaponBox;
        }

        if (string.Equals(attackId, "Skill_H", StringComparison.Ordinal) ||
            string.Equals(attackId, "Dash", StringComparison.Ordinal))
        {
            return HitQueryType.DashBodyBox;
        }

        if (string.Equals(attackId, "Skill_E", StringComparison.Ordinal))
        {
            return HitQueryType.WhirlwindSphere;
        }

        if (string.Equals(attackId, "Skill_D", StringComparison.Ordinal))
        {
            return HitQueryType.SlamSphere;
        }

        if (string.Equals(attackId, "Skill_J", StringComparison.Ordinal))
        {
            return HitQueryType.StompSphere;
        }

        if (string.Equals(attackId, "JumpAttack", StringComparison.Ordinal))
        {
            return HitQueryType.StompSphere;
        }

        if (string.Equals(attackId, "Skill_B", StringComparison.Ordinal) ||
            string.Equals(attackId, "Skill_C", StringComparison.Ordinal) ||
            string.Equals(attackId, "Swipe", StringComparison.Ordinal) ||
            attackId.StartsWith("Swipe_", StringComparison.Ordinal) ||
            attackId.StartsWith("Attack_3Combo_", StringComparison.Ordinal) ||
            string.Equals(attackId, "Attack_4Combo_2", StringComparison.Ordinal) ||
            string.Equals(attackId, "Attack_4Combo_3", StringComparison.Ordinal) ||
            string.Equals(attackId, "Attack_4Combo_4", StringComparison.Ordinal))
        {
            return HitQueryType.ForwardBodyBox;
        }

        return HitQueryType.WeaponBox;
    }

    private Quaternion ResolveOwnerRotation()
    {
        return owner != null ? owner.transform.rotation : transform.rotation;
    }

    private Vector3 ResolveDashQueryCenter()
    {
        Transform root = owner != null ? owner.transform : transform;
        Vector3 forward = root.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude <= 0.0001f)
        {
            forward = transform.forward;
        }

        forward.Normalize();

        float bonusForward = _currentAttack != null ? Mathf.Max(0f, _currentAttack.dashDistance * 0.15f) : 0f;
        return root.position +
               Vector3.up * dashBodyHalfExtents.y +
               forward * (dashBodyForwardOffset + bonusForward);
    }

    private Vector3 ResolveForwardQueryCenter()
    {
        Transform root = owner != null ? owner.transform : transform;
        Vector3 forward = root.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude <= 0.0001f)
        {
            forward = transform.forward;
        }

        forward.Normalize();

        float bonusForward = _currentAttack != null ? Mathf.Max(0f, _currentAttack.dashDistance * 0.2f) : 0f;
        return root.position +
               Vector3.up * forwardBodyHalfExtents.y +
               forward * (forwardBodyForwardOffset + bonusForward);
    }

    private Vector3 ResolveGroundQueryCenter(Transform anchor, float radius)
    {
        if (anchor != null)
        {
            return anchor.position + Vector3.up * groundQueryVerticalOffset;
        }

        Transform root = owner != null ? owner.transform : transform;
        return root.position + root.forward * (radius * 0.35f) + Vector3.up * groundQueryVerticalOffset;
    }

    private Vector3 ResolveWhirlwindQueryCenter()
    {
        Transform root = owner != null ? owner.transform : transform;
        return root.position + Vector3.up;
    }

    private Vector3 ResolveFallbackCenter()
    {
        return owner != null ? owner.transform.position + Vector3.up : transform.position;
    }

    private bool CanHitTarget(IDamageable target)
    {
        if (target == null)
        {
            return false;
        }

        int maxHits = ResolveMaxHitsPerTarget();
        if (!_targetHitCounts.TryGetValue(target, out int currentHitCount))
        {
            currentHitCount = 0;
        }
        else
        {
            if (currentHitCount >= maxHits)
            {
                return false;
            }

            if (_targetNextHitNormalizedTimes.TryGetValue(target, out float nextAllowedNormalizedTime) &&
                _currentNormalizedTime < nextAllowedNormalizedTime)
            {
                return false;
            }
        }

        if (ResolveScheduledHitPhaseIndex(currentHitCount) < 0)
        {
            return false;
        }

        return true;
    }

    private void RegisterHitTarget(IDamageable target, int currentHitCount, int hitPhaseIndex)
    {
        if (target == null)
        {
            return;
        }

        _targetHitCounts[target] = Mathf.Max(currentHitCount + 1, hitPhaseIndex + 1);
        _targetNextHitNormalizedTimes[target] = _currentNormalizedTime + ResolveMultiHitIntervalNormalized();
    }

    private CombatAttackDefinition ResolveAttackForHit(int hitPhaseIndex)
    {
        if (_currentAttack == null)
        {
            return null;
        }

        string attackId = _currentAttack.ResolvedAttackId;
        if (!string.Equals(attackId, "Skill_G", StringComparison.Ordinal))
        {
            return _currentAttack;
        }

        CombatAttackDefinition resolvedAttack = _currentAttack.CreateRuntimeCopy();
        resolvedAttack.playerHitReactionType = hitPhaseIndex <= 0
            ? PlayerHitReactionType.Smash
            : PlayerHitReactionType.Down;
        resolvedAttack.Normalize();
        return resolvedAttack;
    }

    private int ResolveScheduledHitPhaseIndex(int currentHitCount)
    {
        string attackId = _currentAttack != null ? _currentAttack.ResolvedAttackId : string.Empty;

        if (string.Equals(attackId, "Skill_C", StringComparison.Ordinal))
        {
            return ResolveDualWindowHitPhaseIndex(
                currentHitCount,
                SkillCFirstHitNormalized,
                SkillCSecondHitNormalized,
                SkillCHitWindowNormalized);
        }

        if (string.Equals(attackId, "Skill_K", StringComparison.Ordinal))
        {
            return ResolveDualWindowHitPhaseIndex(
                currentHitCount,
                SkillKFirstHitNormalized,
                SkillKSecondHitNormalized,
                SkillKHitWindowNormalized);
        }

        if (string.Equals(attackId, "Skill_G", StringComparison.Ordinal))
        {
            return ResolveDualWindowHitPhaseIndex(
                currentHitCount,
                SkillGFirstHitNormalized,
                SkillGSecondHitNormalized,
                SkillGHitWindowNormalized);
        }

        if (!IsWithinScheduledHitWindow(target: null, currentHitCount))
        {
            return -1;
        }

        return currentHitCount;
    }

    private int ResolveDualWindowHitPhaseIndex(
        int currentHitCount,
        float firstHitNormalized,
        float secondHitNormalized,
        float hitWindowNormalized)
    {
        bool withinFirstWindow = _currentNormalizedTime >= firstHitNormalized &&
                                 _currentNormalizedTime <= firstHitNormalized + hitWindowNormalized;
        bool withinSecondWindow = _currentNormalizedTime >= secondHitNormalized &&
                                  _currentNormalizedTime <= secondHitNormalized + hitWindowNormalized;

        if (currentHitCount <= 0)
        {
            if (withinFirstWindow)
            {
                return 0;
            }

            if (withinSecondWindow)
            {
                return 1;
            }

            return -1;
        }

        if (currentHitCount == 1 && withinSecondWindow)
        {
            return 1;
        }

        return -1;
    }

    private int ResolveMaxHitsPerTarget()
    {
        string attackId = _currentAttack != null ? _currentAttack.ResolvedAttackId : string.Empty;
        if (string.Equals(attackId, "Skill_E", StringComparison.Ordinal))
        {
            return 4;
        }

        if (string.Equals(attackId, "Skill_C", StringComparison.Ordinal))
        {
            return 2;
        }

        if (string.Equals(attackId, "Skill_K", StringComparison.Ordinal))
        {
            return 2;
        }

        if (string.Equals(attackId, "Skill_G", StringComparison.Ordinal))
        {
            return 2;
        }

        return 1;
    }

    private float ResolveMultiHitIntervalNormalized()
    {
        string attackId = _currentAttack != null ? _currentAttack.ResolvedAttackId : string.Empty;
        if (string.Equals(attackId, "Skill_E", StringComparison.Ordinal))
        {
            return 0.1f;
        }

        if (string.Equals(attackId, "Skill_C", StringComparison.Ordinal))
        {
            return SkillCSecondHitNormalized - SkillCFirstHitNormalized;
        }

        if (string.Equals(attackId, "Skill_K", StringComparison.Ordinal))
        {
            return SkillKSecondHitNormalized - SkillKFirstHitNormalized;
        }

        if (string.Equals(attackId, "Skill_G", StringComparison.Ordinal))
        {
            return SkillGSecondHitNormalized - SkillGFirstHitNormalized;
        }

        return 1f;
    }

    private bool IsWithinScheduledHitWindow(IDamageable target, int currentHitCount)
    {
        string attackId = _currentAttack != null ? _currentAttack.ResolvedAttackId : string.Empty;
        if (string.Equals(attackId, "Skill_C", StringComparison.Ordinal))
        {
            float targetNormalizedTime = currentHitCount <= 0 ? SkillCFirstHitNormalized : SkillCSecondHitNormalized;
            return _currentNormalizedTime >= targetNormalizedTime &&
                   _currentNormalizedTime <= targetNormalizedTime + SkillCHitWindowNormalized;
        }

        if (string.Equals(attackId, "Skill_K", StringComparison.Ordinal))
        {
            float targetNormalizedTime = currentHitCount <= 0 ? SkillKFirstHitNormalized : SkillKSecondHitNormalized;
            return _currentNormalizedTime >= targetNormalizedTime &&
                   _currentNormalizedTime <= targetNormalizedTime + SkillKHitWindowNormalized;
        }

        if (string.Equals(attackId, "Skill_G", StringComparison.Ordinal))
        {
            float targetNormalizedTime = currentHitCount <= 0 ? SkillGFirstHitNormalized : SkillGSecondHitNormalized;
            return _currentNormalizedTime >= targetNormalizedTime &&
                   _currentNormalizedTime <= targetNormalizedTime + SkillGHitWindowNormalized;
        }

        if (!string.Equals(attackId, "Skill_C", StringComparison.Ordinal))
        {
            return true;
        }

        return true;
    }

    private void ClearHitTracking()
    {
        _targetHitCounts.Clear();
        _targetNextHitNormalizedTimes.Clear();
    }

    private Transform ResolveNamedPoint(string pointName)
    {
        if (string.IsNullOrWhiteSpace(pointName))
        {
            return null;
        }

        Transform searchRoot = owner != null ? owner.transform : transform.root;
        return FindChildRecursive(searchRoot, pointName);
    }

    private static Transform FindChildRecursive(Transform root, string name)
    {
        if (root == null)
        {
            return null;
        }

        if (string.Equals(root.name, name, StringComparison.Ordinal))
        {
            return root;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            Transform found = FindChildRecursive(child, name);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private void ForceHitEnd(bool clearAttack)
    {
        SetHitBoxActive(false);
        ClearHitTracking();
        if (clearAttack)
        {
            _currentAttack = null;
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = _isActive ? Color.red : Color.gray;

        HitQueryType queryType = ResolveHitQueryType();
        if (queryType == HitQueryType.ForwardBodyBox)
        {
            Gizmos.matrix = Matrix4x4.TRS(ResolveForwardQueryCenter(), ResolveOwnerRotation(), Vector3.one);
            Gizmos.DrawWireCube(Vector3.zero, forwardBodyHalfExtents * 2f);
            return;
        }

        if (queryType == HitQueryType.DashBodyBox)
        {
            Gizmos.matrix = Matrix4x4.TRS(ResolveDashQueryCenter(), ResolveOwnerRotation(), Vector3.one);
            Gizmos.DrawWireCube(Vector3.zero, dashBodyHalfExtents * 2f);
            return;
        }

        if (queryType == HitQueryType.SlamSphere)
        {
            Gizmos.matrix = Matrix4x4.identity;
            Gizmos.DrawWireSphere(ResolveGroundQueryCenter(_slamPoint, slamRadius), slamRadius);
            return;
        }

        if (queryType == HitQueryType.StompSphere)
        {
            Gizmos.matrix = Matrix4x4.identity;
            Gizmos.DrawWireSphere(ResolveGroundQueryCenter(_stompPoint, stompRadius), stompRadius);
            return;
        }

        if (queryType == HitQueryType.WhirlwindSphere)
        {
            Gizmos.matrix = Matrix4x4.identity;
            Gizmos.DrawWireSphere(ResolveWhirlwindQueryCenter(), whirlwindRadius);
            return;
        }

        if (hitCollider == null)
        {
            return;
        }

        Bounds bounds = hitCollider.bounds;
        Gizmos.matrix = Matrix4x4.identity;
        Gizmos.DrawWireCube(bounds.center, bounds.size);
    }
}
