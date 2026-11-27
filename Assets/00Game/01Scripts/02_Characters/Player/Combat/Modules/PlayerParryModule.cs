using UnityEngine;

public readonly struct PlayerParryHitResult
{
    public PlayerParryHitResult(bool parryConsumedDamage, bool parrySucceeded)
    {
        ParryConsumedDamage = parryConsumedDamage;
        ParrySucceeded = parrySucceeded;
    }

    public bool ParryConsumedDamage { get; }
    public bool ParrySucceeded { get; }
}

public sealed class PlayerParryModule
{
    private readonly PlayerController _player;

    public PlayerParryModule(PlayerController player)
    {
        _player = player;
    }

    public PlayerParryHitResult HandleIncomingDamage(int amount, Vector3 hitPoint, Vector3 hitNormal)
    {
        if (_player == null || !_player.IsParryWindowActive)
        {
            return new PlayerParryHitResult(false, false);
        }

        CombatAttackDefinition incomingAttack = CombatDamageContext.CurrentAttack;
        if (incomingAttack == null || incomingAttack.ignoreParry)
        {
            _player.LogParryMessage("[Parry] Current attack is not parryable.");
            return new PlayerParryHitResult(false, false);
        }

        if (!IsFrontHit(hitPoint, hitNormal))
        {
            _player.LogParryMessage("[Parry] Hit came from outside the parry angle.");
            return new PlayerParryHitResult(false, false);
        }

        _player.NotifySuccessfulParry(incomingAttack, CombatDamageContext.CurrentAttacker, hitPoint, hitNormal);
        _player.LogParryMessage($"[Parry] Success against {incomingAttack.ResolvedAttackName}.");
        return new PlayerParryHitResult(true, true);
    }

    private bool IsFrontHit(Vector3 hitPoint, Vector3 hitNormal)
    {
        Vector3 forward = _player.transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f)
        {
            return false;
        }

        Vector3 toAttacker = hitNormal.sqrMagnitude > 0.0001f
            ? -hitNormal
            : hitPoint - _player.transform.position;
        toAttacker.y = 0f;
        if (toAttacker.sqrMagnitude < 0.0001f)
        {
            return false;
        }

        float dot = Vector3.Dot(forward.normalized, toAttacker.normalized);
        float threshold = Mathf.Cos(_player.statData.parryAngle * 0.5f * Mathf.Deg2Rad);
        return dot >= threshold;
    }
}
