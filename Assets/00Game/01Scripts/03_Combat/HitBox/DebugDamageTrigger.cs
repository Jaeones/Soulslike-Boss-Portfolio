using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class DebugDamageTrigger : MonoBehaviour
{
    [Header("데미지 설정")]
    [SerializeField] private int damageAmount = 9999;
    [SerializeField] private float hitInterval = 0.3f;
    [SerializeField] private bool oneShotPerPlayer = false;
    [SerializeField] private bool enableDebugLog = true;

    private readonly Dictionary<int, float> _lastHitTimeByPlayer = new Dictionary<int, float>();
    private readonly HashSet<int> _alreadyHitPlayers = new HashSet<int>();

    private void Reset()
    {
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            col.isTrigger = true;
        }
    }

    private void OnDisable()
    {
        _lastHitTimeByPlayer.Clear();
        _alreadyHitPlayers.Clear();
    }

    private void OnTriggerEnter(Collider other)
    {
        TryApplyDamage(other);
    }

    private void OnTriggerStay(Collider other)
    {
        TryApplyDamage(other);
    }

    private void TryApplyDamage(Collider other)
    {
        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player == null || player.IsDead)
        {
            return;
        }

        int playerId = player.GetInstanceID();
        if (oneShotPerPlayer && _alreadyHitPlayers.Contains(playerId))
        {
            return;
        }

        if (_lastHitTimeByPlayer.TryGetValue(playerId, out float lastHitTime))
        {
            if (Time.time < lastHitTime + Mathf.Max(0f, hitInterval))
            {
                return;
            }
        }

        Vector3 attackerPos = transform.position;
        Vector3 hitPoint = other.ClosestPoint(attackerPos);
        if ((hitPoint - attackerPos).sqrMagnitude < 0.0001f)
        {
            hitPoint = player.transform.position;
        }

        // PlayerController.IsFrontHit 로직과 맞추기 위해 "공격자 -> 피격자" 방향을 전달
        Vector3 hitNormal = player.transform.position - attackerPos;
        if (hitNormal.sqrMagnitude > 0.0001f)
        {
            hitNormal.Normalize();
        }
        else
        {
            hitNormal = Vector3.zero;
        }

        int finalDamage = Mathf.Max(0, damageAmount);
        player.TakeDamage(finalDamage, hitPoint, hitNormal);

        _lastHitTimeByPlayer[playerId] = Time.time;
        if (oneShotPerPlayer)
        {
            _alreadyHitPlayers.Add(playerId);
        }

        if (enableDebugLog)
        {
            Debug.Log($"[DebugDamageTrigger] 플레이어 데미지 적용: {finalDamage} (Object: {name})");
        }
    }
}
