using UnityEngine;

public sealed class LockOnTargetValidator
{
    // 현재 타겟이 락온 유지 조건을 만족하는지만 판단한다.
    public bool IsTargetValid(PlayerController player, Transform currentTarget, float scanRadius)
    {
        if (player == null || currentTarget == null)
        {
            return false;
        }

        if (!currentTarget.gameObject.activeInHierarchy)
        {
            return false;
        }

        Vector3 playerPosition = player.transform.position;
        Vector3 distanceCheckPosition = LockOnTargetUtility.GetDistanceCheckPosition(currentTarget, playerPosition);
        Vector3 toTarget = distanceCheckPosition - playerPosition;
        return toTarget.sqrMagnitude <= scanRadius * scanRadius;
    }
}
