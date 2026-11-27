using System.Collections.Generic;
using UnityEngine;

public class WeaponHitBox : MonoBehaviour
{
    private const float SweepSampleSpacing = 0.12f;
    private const int MaxSweepSamples = 6;

    [Header("히트박스 설정")]
    [SerializeField] private Collider hitCollider;
    [SerializeField] private LayerMask targetLayers;

    private readonly HashSet<IDamageable> _alreadyHitTargets = new HashSet<IDamageable>();
    private readonly HashSet<Collider> _overlapCandidates = new HashSet<Collider>();
    private Transform _ownerRoot;
    private bool _isActive;
    private bool _hasPreviousSample;
    private Vector3 _previousCenter;
    private Vector3 _previousExtents;
    private Quaternion _previousRotation;

    private void Awake()
    {
        if (hitCollider == null)
        {
            hitCollider = GetComponent<Collider>();
        }

        if (hitCollider != null)
        {
            hitCollider.isTrigger = true;
            hitCollider.enabled = false;
        }

        PlayerController owner = GetComponentInParent<PlayerController>();
        _ownerRoot = owner != null ? owner.transform : transform.root;
    }

    public void SetHitBoxActive(bool active)
    {
        _isActive = active;

        if (active)
        {
            _alreadyHitTargets.Clear();
            _overlapCandidates.Clear();
            _hasPreviousSample = false;
        }
        else
        {
            _overlapCandidates.Clear();
            _hasPreviousSample = false;
        }

        if (hitCollider != null)
        {
            hitCollider.enabled = active;
        }
    }

    public CombatHitResult PerformHit(CombatAttackDefinition attack, Vector3 hitNormal)
    {
        if (!_isActive || hitCollider == null || attack == null)
        {
            return default;
        }

        Bounds bounds = hitCollider.bounds;
        Vector3 currentCenter = bounds.center;
        Vector3 currentExtents = bounds.extents;
        Quaternion currentRotation = hitCollider.transform.rotation;
        CollectSweepCandidates(currentCenter, currentExtents, currentRotation);

        int hitCount = 0;
        Vector3 firstHitPoint = Vector3.zero;
        Vector3 firstHitNormal = Vector3.zero;
        bool hasFirstHit = false;

        foreach (Collider col in _overlapCandidates)
        {
            IDamageable damageable = ResolveDamageable(col);
            if (damageable == null)
            {
                continue;
            }

            MonoBehaviour targetMb = damageable as MonoBehaviour;
            if (targetMb == null)
            {
                continue;
            }

            Transform targetRoot = targetMb.transform.root;
            if (_ownerRoot != null && targetRoot == _ownerRoot)
            {
                continue;
            }

            if (_alreadyHitTargets.Contains(damageable))
            {
                continue;
            }

            _alreadyHitTargets.Add(damageable);
            Vector3 hitPoint = col.ClosestPoint(bounds.center);
            if (!hasFirstHit)
            {
                firstHitPoint = hitPoint;
                firstHitNormal = hitNormal.sqrMagnitude > 0.0001f ? hitNormal.normalized : Vector3.zero;
                hasFirstHit = true;
            }

            using (CombatDamageContext.Push(attack, _ownerRoot))
            {
                damageable.TakeDamage(attack.damage, hitPoint, hitNormal);
            }
            hitCount++;
        }

        _overlapCandidates.Clear();
        _previousCenter = currentCenter;
        _previousExtents = currentExtents;
        _previousRotation = currentRotation;
        _hasPreviousSample = true;

        return new CombatHitResult(hitCount, firstHitPoint, firstHitNormal);
    }

    private void CollectSweepCandidates(Vector3 currentCenter, Vector3 currentExtents, Quaternion currentRotation)
    {
        _overlapCandidates.Clear();

        if (!_hasPreviousSample)
        {
            CollectOverlaps(currentCenter, currentExtents, currentRotation);
            return;
        }

        float distance = Vector3.Distance(_previousCenter, currentCenter);
        float maxExtent = Mathf.Max(
            Mathf.Max(_previousExtents.x, _previousExtents.y),
            Mathf.Max(currentExtents.x, currentExtents.y));
        int sampleCount = Mathf.Clamp(
            Mathf.CeilToInt(distance / Mathf.Max(0.01f, maxExtent + SweepSampleSpacing)),
            1,
            MaxSweepSamples);

        for (int i = 0; i <= sampleCount; i++)
        {
            float t = sampleCount > 0 ? i / (float)sampleCount : 1f;
            Vector3 sampleCenter = Vector3.Lerp(_previousCenter, currentCenter, t);
            Vector3 sampleExtents = Vector3.Lerp(_previousExtents, currentExtents, t);
            Quaternion sampleRotation = Quaternion.Slerp(_previousRotation, currentRotation, t);
            CollectOverlaps(sampleCenter, sampleExtents, sampleRotation);
        }
    }

    private void CollectOverlaps(Vector3 center, Vector3 extents, Quaternion rotation)
    {
        Collider[] overlaps = Physics.OverlapBox(
            center,
            extents,
            rotation,
            ResolveTargetMask(),
            QueryTriggerInteraction.Collide
        );

        foreach (Collider overlap in overlaps)
        {
            if (overlap != null)
            {
                _overlapCandidates.Add(overlap);
            }
        }
    }

    private LayerMask ResolveTargetMask()
    {
        return targetLayers.value != 0 ? targetLayers : Physics.AllLayers;
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

    private void OnDrawGizmosSelected()
    {
        if (hitCollider == null)
        {
            return;
        }

        Gizmos.color = _isActive ? Color.red : Color.gray;
        Bounds bounds = hitCollider.bounds;
        Gizmos.matrix = Matrix4x4.identity;
        Gizmos.DrawWireCube(bounds.center, bounds.size);
    }
}
