using UnityEngine;

internal static class LockOnTargetUtility
{
    public static bool TryResolveTargetRoot(
        Collider collider,
        Transform playerTransform,
        string primaryTag,
        string secondaryTag,
        out Transform targetRoot)
    {
        targetRoot = null;
        if (collider == null)
        {
            return false;
        }

        IDamageable damageable = collider.GetComponentInParent<IDamageable>();
        MonoBehaviour damageableBehaviour = damageable as MonoBehaviour;
        if (damageableBehaviour == null)
        {
            return false;
        }

        targetRoot = damageableBehaviour.transform;
        if (targetRoot == null || targetRoot == playerTransform)
        {
            targetRoot = null;
            return false;
        }

        if (!MatchesAllowedTag(targetRoot, primaryTag, secondaryTag))
        {
            targetRoot = null;
            return false;
        }

        return true;
    }

    private static bool MatchesAllowedTag(Transform targetRoot, string primaryTag, string secondaryTag)
    {
        if (targetRoot == null)
        {
            return false;
        }

        string targetTag = targetRoot.tag;
        return MatchesTag(targetTag, primaryTag) || MatchesTag(targetTag, secondaryTag);
    }

    private static bool MatchesTag(string actualTag, string expectedTag)
    {
        return !string.IsNullOrWhiteSpace(expectedTag) &&
               string.Equals(actualTag, expectedTag, System.StringComparison.Ordinal);
    }

    public static Vector3 GetTargetFocusPosition(Transform targetRoot, Collider preferredCollider = null, float fallbackHeightOffset = 1.2f)
    {
        if (targetRoot == null)
        {
            return Vector3.zero;
        }

        Transform lockOnAnchor = FindLockOnAnchor(targetRoot);
        if (lockOnAnchor != null)
        {
            return lockOnAnchor.position;
        }

        Collider referenceCollider = preferredCollider != null ? preferredCollider : targetRoot.GetComponentInChildren<Collider>();
        if (referenceCollider != null)
        {
            Bounds bounds = referenceCollider.bounds;
            float upperBodyOffset = Mathf.Min(bounds.extents.y * 0.35f, fallbackHeightOffset);
            return bounds.center + Vector3.up * upperBodyOffset;
        }

        return targetRoot.position + Vector3.up * fallbackHeightOffset;
    }

    public static Vector3 GetDistanceCheckPosition(Transform targetRoot, Vector3 origin, Collider preferredCollider = null)
    {
        if (targetRoot == null)
        {
            return origin;
        }

        Transform lockOnAnchor = FindLockOnAnchor(targetRoot);
        if (lockOnAnchor != null)
        {
            return lockOnAnchor.position;
        }

        Collider referenceCollider = preferredCollider != null ? preferredCollider : targetRoot.GetComponentInChildren<Collider>();
        if (referenceCollider != null)
        {
            Vector3 closestPoint = referenceCollider.ClosestPoint(origin);
            if ((closestPoint - origin).sqrMagnitude > 0.000001f)
            {
                return closestPoint;
            }

            return referenceCollider.bounds.center;
        }

        return targetRoot.position;
    }

    private static Transform FindLockOnAnchor(Transform root)
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
}
