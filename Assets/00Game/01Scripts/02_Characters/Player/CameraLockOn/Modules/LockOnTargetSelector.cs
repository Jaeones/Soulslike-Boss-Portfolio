using UnityEngine;

public sealed class LockOnTargetSelector
{
    // 락온 후보를 모으고 점수를 계산해 가장 적합한 타겟을 반환한다.
    public Transform SelectBestTarget(
        PlayerController player,
        float scanRadius,
        float maxLockOnAngle,
        LayerMask targetLayers,
        string primaryTag,
        string secondaryTag)
    {
        if (player == null)
        {
            return null;
        }

        Collider[] colliders = Physics.OverlapSphere(player.transform.position, scanRadius, targetLayers);
        Transform bestTarget = null;
        float bestScore = float.MaxValue;

        Vector3 playerForward = ResolveReferenceForward(player);
        Vector3 playerPos = player.transform.position;

        foreach (Collider col in colliders)
        {
            if (!LockOnTargetUtility.TryResolveTargetRoot(col, player.transform, primaryTag, secondaryTag, out Transform targetRoot))
            {
                continue;
            }

            Vector3 targetFocusPosition = LockOnTargetUtility.GetTargetFocusPosition(targetRoot, col);
            Vector3 targetDistancePosition = LockOnTargetUtility.GetDistanceCheckPosition(targetRoot, playerPos, col);
            Vector3 toTarget = targetFocusPosition - playerPos;
            toTarget.y = 0f;

            Vector3 distanceVector = targetDistancePosition - playerPos;
            distanceVector.y = 0f;

            float sqrDistance = distanceVector.sqrMagnitude;
            if (sqrDistance < 0.0001f)
            {
                continue;
            }

            float angle = Vector3.Angle(playerForward, toTarget.normalized);
            if (angle > maxLockOnAngle)
            {
                continue;
            }

            float score = sqrDistance + angle * angle * 0.1f;
            if (score < bestScore)
            {
                bestScore = score;
                bestTarget = targetRoot;
            }
        }

        return bestTarget;
    }

    private static Vector3 ResolveReferenceForward(PlayerController player)
    {
        Vector3 playerForward = player.transform.forward;
        if (Define.MainCam == null)
        {
            return playerForward;
        }

        playerForward = Define.MainCam.transform.forward;
        playerForward.y = 0f;
        if (playerForward.sqrMagnitude > 0.0001f)
        {
            return playerForward.normalized;
        }

        return player.transform.forward;
    }
}
