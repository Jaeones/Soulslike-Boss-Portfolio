using System;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public readonly struct CombatHitResult
{
    public CombatHitResult(int hitCount, Vector3 firstHitPoint, Vector3 firstHitNormal)
    {
        HitCount = hitCount;
        FirstHitPoint = firstHitPoint;
        FirstHitNormal = firstHitNormal;
    }

    public int HitCount { get; }
    public Vector3 FirstHitPoint { get; }
    public Vector3 FirstHitNormal { get; }
    public bool HasHit => HitCount > 0;
}

[System.Serializable]
public enum PlayerHitReactionType
{
    [InspectorName("일반 방향 피격")]
    DefaultDirectional = 0,
    [InspectorName("돌진 피격")]
    Dash = 1,
    [InspectorName("내려찍기 피격")]
    Slam = 2,
    [InspectorName("다운 피격")]
    Down = 3,
    [InspectorName("강타 피격")]
    Smash = 4,
    [InspectorName("찌르기 피격")]
    Spear = 5
}

[System.Serializable]
public class CombatAttackDefinition
{
    [InspectorName("공격 ID")]
    public string attackId;
    [InspectorName("공격 이름")]
    public string attackName;
    [InspectorName("애니메이터 상태 이름")]
    public string animatorStateName;
    [InspectorName("플레이어 피격 타입")]
    public PlayerHitReactionType playerHitReactionType = PlayerHitReactionType.DefaultDirectional;

    [Header("기본 피해")]
    [InspectorName("체력 피해량")]
    [Min(1)] public int damage = 10;
    [InspectorName("공격 스태미나 소모")]
    [Min(0f)] public float staminaCost = 0f;
    [InspectorName("플레이어 스태미나 피해")]
    [Min(0f)] public float staminaDamage = 0f;
    [InspectorName("그로기 피해")]
    [Min(0f)] public float groggyDamage = 0f;
    [InspectorName("강인도 피해")]
    [Min(0f)] public float poiseDamage = 0f;

    [Header("타이밍")]
    [InspectorName("시작 준비 구간 시작")]
    [Range(0f, 1f)] public float startupStartNormalized = 0f;
    [InspectorName("타격 판정 시점")]
    [Range(0f, 1f)] public float impactNormalized = 0.5f;
    [InspectorName("공격 활성 시작")]
    [Range(0f, 1f)] public float activeStartNormalized = 0.35f;
    [InspectorName("공격 활성 종료")]
    [Range(0f, 1f)] public float activeEndNormalized = 0.5f;
    [InspectorName("리커버리 종료")]
    [Range(0f, 1f)] public float recoveryEndNormalized = 1f;

    [Header("제어")]
    [InspectorName("콤보 입력 시작")]
    [Range(0f, 1f)] public float comboWindowStartNormalized = 0.6f;
    [InspectorName("콤보 입력 종료")]
    [Range(0f, 1f)] public float comboWindowEndNormalized = 0.8f;
    [InspectorName("회피 캔슬 시작")]
    [Range(0f, 1f)] public float rollCancelStartNormalized = 0.6f;
    [InspectorName("추적 유지 종료")]
    [Range(0f, 1f)] public float trackUntilNormalized = 0.15f;
    [InspectorName("방향 고정 종료")]
    [Range(0f, 1f)] public float facingLockNormalized = 0.24f;

    [Header("슈퍼아머")]
    [InspectorName("슈퍼아머 사용")]
    public bool hasSuperArmor;
    [InspectorName("슈퍼아머 시작")]
    [Range(0f, 1f)] public float superArmorStartNormalized = 0f;
    [InspectorName("슈퍼아머 종료")]
    [Range(0f, 1f)] public float superArmorEndNormalized = 0f;
    public bool ignoreParry;

    [Header("이동")]
    [InspectorName("전진 거리")]
    [Min(0f)] public float dashDistance = 0f;
    [InspectorName("전진 시간")]
    [Min(0.01f)] public float dashDuration = 0.1f;
    [InspectorName("준비 시작 속도")]
    [Min(0.01f)] public float prepareStartSpeed = 0.1f;
    [InspectorName("준비 종료 속도")]
    [Min(0.01f)] public float prepareEndSpeed = 0.8f;
    [InspectorName("타격 속도")]
    [Min(0.01f)] public float impactSpeed = 5f;
    [InspectorName("리커버리 속도")]
    [Min(0.01f)] public float recoverySpeed = 0.3f;
    [InspectorName("후딜 대기 배수")]
    [Min(0f)] public float postAttackWaitMultiplier = 0.15f;

    [Header("피드백")]
    [InspectorName("강공 여부")]
    public bool isHeavyAttack;
    [InspectorName("카메라 흔들림 강도")]
    [Min(0f)] public float cameraShakeIntensity = 0f;
    [InspectorName("카메라 흔들림 시간")]
    [Min(0f)] public float cameraShakeDuration = 0.08f;
    [InspectorName("히트스톱 시간")]
    [Min(0f)] public float hitStopDuration = 0.12f;
    [InspectorName("히트스톱 배수")]
    [Min(0.01f)] public float hitStopMultiplier = 1f;

    public string ResolvedAttackId => string.IsNullOrWhiteSpace(attackId) ? ResolvedAnimatorStateName : attackId;
    public string ResolvedAttackName => string.IsNullOrWhiteSpace(attackName) ? ResolvedAttackId : attackName;
    public string ResolvedAnimatorStateName => string.IsNullOrWhiteSpace(animatorStateName) ? attackId : animatorStateName;

    public CombatAttackDefinition CreateRuntimeCopy()
    {
        return new CombatAttackDefinition
        {
            attackId = attackId,
            attackName = attackName,
            animatorStateName = animatorStateName,
            playerHitReactionType = playerHitReactionType,
            damage = damage,
            staminaCost = staminaCost,
            staminaDamage = staminaDamage,
            groggyDamage = groggyDamage,
            poiseDamage = poiseDamage,
            startupStartNormalized = startupStartNormalized,
            impactNormalized = impactNormalized,
            activeStartNormalized = activeStartNormalized,
            activeEndNormalized = activeEndNormalized,
            recoveryEndNormalized = recoveryEndNormalized,
            comboWindowStartNormalized = comboWindowStartNormalized,
            comboWindowEndNormalized = comboWindowEndNormalized,
            rollCancelStartNormalized = rollCancelStartNormalized,
            trackUntilNormalized = trackUntilNormalized,
            facingLockNormalized = facingLockNormalized,
            hasSuperArmor = hasSuperArmor,
            superArmorStartNormalized = superArmorStartNormalized,
            superArmorEndNormalized = superArmorEndNormalized,
            ignoreParry = ignoreParry,
            dashDistance = dashDistance,
            dashDuration = dashDuration,
            prepareStartSpeed = prepareStartSpeed,
            prepareEndSpeed = prepareEndSpeed,
            impactSpeed = impactSpeed,
            recoverySpeed = recoverySpeed,
            postAttackWaitMultiplier = postAttackWaitMultiplier,
            isHeavyAttack = isHeavyAttack,
            cameraShakeIntensity = cameraShakeIntensity,
            cameraShakeDuration = cameraShakeDuration,
            hitStopDuration = hitStopDuration,
            hitStopMultiplier = hitStopMultiplier
        };
    }

    public bool IsInActiveWindow(float normalizedTime)
    {
        float clampedTime = Mathf.Clamp01(normalizedTime);
        return clampedTime >= activeStartNormalized && clampedTime <= activeEndNormalized;
    }

    public bool IsInTrackingWindow(float normalizedTime)
    {
        return Mathf.Clamp01(normalizedTime) <= trackUntilNormalized;
    }

    public bool IsInFacingLockWindow(float normalizedTime)
    {
        return Mathf.Clamp01(normalizedTime) <= facingLockNormalized;
    }

    public bool IsInSuperArmorWindow(float normalizedTime)
    {
        if (!hasSuperArmor)
        {
            return false;
        }

        float clampedTime = Mathf.Clamp01(normalizedTime);
        return clampedTime >= superArmorStartNormalized && clampedTime <= superArmorEndNormalized;
    }

    public void Normalize(int attackIndex = -1)
    {
        if (string.IsNullOrWhiteSpace(attackId))
        {
            attackId = attackIndex >= 0 ? $"Attack_{attackIndex + 1}" : "Attack";
        }

        if (string.IsNullOrWhiteSpace(attackName))
        {
            attackName = attackId;
        }

        if (string.IsNullOrWhiteSpace(animatorStateName))
        {
            animatorStateName = attackId;
        }

        damage = Mathf.Max(1, damage);
        staminaCost = Mathf.Max(0f, staminaCost);
        staminaDamage = Mathf.Max(0f, staminaDamage);
        groggyDamage = Mathf.Max(0f, groggyDamage);
        poiseDamage = Mathf.Max(0f, poiseDamage);

        startupStartNormalized = Mathf.Clamp01(startupStartNormalized);
        impactNormalized = Mathf.Clamp01(impactNormalized);
        activeStartNormalized = Mathf.Clamp01(activeStartNormalized);
        activeEndNormalized = Mathf.Clamp01(activeEndNormalized);
        if (activeEndNormalized < activeStartNormalized)
        {
            activeEndNormalized = activeStartNormalized;
        }

        recoveryEndNormalized = recoveryEndNormalized > 0f
            ? Mathf.Clamp01(recoveryEndNormalized)
            : 1f;
        if (recoveryEndNormalized < activeEndNormalized)
        {
            recoveryEndNormalized = activeEndNormalized;
        }

        comboWindowStartNormalized = Mathf.Clamp(comboWindowStartNormalized, 0f, recoveryEndNormalized);
        comboWindowEndNormalized = Mathf.Clamp(comboWindowEndNormalized, comboWindowStartNormalized, recoveryEndNormalized);
        rollCancelStartNormalized = Mathf.Clamp(rollCancelStartNormalized, 0f, recoveryEndNormalized);
        trackUntilNormalized = Mathf.Clamp(trackUntilNormalized, 0f, recoveryEndNormalized);
        facingLockNormalized = Mathf.Clamp(Mathf.Max(trackUntilNormalized, facingLockNormalized), 0f, recoveryEndNormalized);

        superArmorStartNormalized = Mathf.Clamp(superArmorStartNormalized, 0f, recoveryEndNormalized);
        superArmorEndNormalized = Mathf.Clamp(superArmorEndNormalized, superArmorStartNormalized, recoveryEndNormalized);
        hasSuperArmor = hasSuperArmor && superArmorEndNormalized > superArmorStartNormalized;

        dashDistance = Mathf.Max(0f, dashDistance);
        dashDuration = Mathf.Max(0.01f, dashDuration);
        prepareStartSpeed = Mathf.Max(0.01f, prepareStartSpeed);
        prepareEndSpeed = Mathf.Max(0.01f, prepareEndSpeed);
        impactSpeed = Mathf.Max(0.01f, impactSpeed);
        recoverySpeed = Mathf.Max(0.01f, recoverySpeed);
        postAttackWaitMultiplier = Mathf.Max(0f, postAttackWaitMultiplier);

        cameraShakeIntensity = Mathf.Max(0f, cameraShakeIntensity);
        cameraShakeDuration = Mathf.Max(0f, cameraShakeDuration);
        hitStopDuration = Mathf.Max(0f, hitStopDuration);
        hitStopMultiplier = Mathf.Max(0.01f, hitStopMultiplier);
    }

    public static CombatAttackDefinition FromPlayerAttackInfo(PlayerStatData.AttackInfo info)
    {
        int roundedDamage = Mathf.Max(1, Mathf.RoundToInt(info.damage));
        bool isHeavy = info.damage >= 220f;

        CombatAttackDefinition definition = new CombatAttackDefinition
        {
            attackId = info.attackName,
            attackName = info.attackName,
            animatorStateName = info.animatorStateName,
            playerHitReactionType = isHeavy ? PlayerHitReactionType.Smash : PlayerHitReactionType.DefaultDirectional,
            damage = roundedDamage,
            staminaCost = Mathf.Max(0f, info.staminaCost),
            staminaDamage = Mathf.Max(8f, roundedDamage * 0.24f),
            groggyDamage = Mathf.Max(6f, roundedDamage * (isHeavy ? 0.16f : 0.1f)),
            poiseDamage = Mathf.Max(isHeavy ? 22f : 10f, roundedDamage * (isHeavy ? 0.14f : 0.07f)),
            startupStartNormalized = info.startupStartNormalized,
            impactNormalized = info.activeStartNormalized,
            activeStartNormalized = info.activeStartNormalized,
            activeEndNormalized = info.activeEndNormalized,
            recoveryEndNormalized = info.recoveryEndNormalized,
            comboWindowStartNormalized = info.comboWindowStartNormalized,
            comboWindowEndNormalized = info.comboWindowEndNormalized,
            rollCancelStartNormalized = info.rollCancelStartNormalized,
            trackUntilNormalized = info.trackUntilNormalized,
            facingLockNormalized = info.facingLockNormalized,
            hasSuperArmor = info.hasSuperArmor,
            superArmorStartNormalized = info.superArmorStartNormalized,
            superArmorEndNormalized = info.superArmorEndNormalized,
            dashDistance = 0f,
            dashDuration = 0.1f,
            prepareStartSpeed = 1f,
            prepareEndSpeed = 1f,
            impactSpeed = 1f,
            recoverySpeed = 1f,
            postAttackWaitMultiplier = 0f,
            cameraShakeIntensity = 0f,
            cameraShakeDuration = 0f,
            hitStopDuration = 0.06f,
            hitStopMultiplier = 1f,
            isHeavyAttack = isHeavy
        };

        definition.Normalize();
        return definition;
    }
}

public static class CombatDamageContext
{
    private readonly struct DamageFrame
    {
        public DamageFrame(CombatAttackDefinition attack, Transform attacker)
        {
            Attack = attack;
            Attacker = attacker;
        }

        public CombatAttackDefinition Attack { get; }
        public Transform Attacker { get; }
    }

    private sealed class Scope : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            if (_frames.Count > 0)
            {
                _frames.Pop();
            }
        }
    }

    private static readonly Stack<DamageFrame> _frames = new Stack<DamageFrame>();

    public static CombatAttackDefinition CurrentAttack => _frames.Count > 0 ? _frames.Peek().Attack : null;
    public static Transform CurrentAttacker => _frames.Count > 0 ? _frames.Peek().Attacker : null;

    public static IDisposable Push(CombatAttackDefinition attack, Transform attacker)
    {
        _frames.Push(new DamageFrame(attack, attacker));
        return new Scope();
    }
}

[CreateAssetMenu(fileName = "PlayerAttackLibrary", menuName = "Data/Combat/Player Attack Library")]
public class PlayerAttackLibrary : ScriptableObject
{
    [SerializeField] private List<CombatAttackDefinition> comboAttacks = new List<CombatAttackDefinition>();

    public int Count => comboAttacks != null ? comboAttacks.Count : 0;

    public bool TryGetAttackAsset(int attackIndex, out CombatAttackDefinition attack)
    {
        attack = null;
        if (comboAttacks == null || attackIndex < 0 || attackIndex >= comboAttacks.Count)
        {
            return false;
        }

        attack = comboAttacks[attackIndex];
        return attack != null;
    }

    public CombatAttackDefinition CreateAttackDefinition(int attackIndex)
    {
        if (!TryGetAttackAsset(attackIndex, out CombatAttackDefinition attack))
        {
            return null;
        }

        CombatAttackDefinition runtimeCopy = attack.CreateRuntimeCopy();
        runtimeCopy.Normalize(attackIndex);
        return runtimeCopy;
    }

    public bool TryGetAttackAsset(string attackKey, out CombatAttackDefinition attack)
    {
        attack = null;
        if (comboAttacks == null || string.IsNullOrWhiteSpace(attackKey))
        {
            return false;
        }

        for (int i = 0; i < comboAttacks.Count; i++)
        {
            CombatAttackDefinition candidate = comboAttacks[i];
            if (!MatchesAttackKey(candidate, attackKey))
            {
                continue;
            }

            attack = candidate;
            return true;
        }

        return false;
    }

    public CombatAttackDefinition CreateAttackDefinition(string attackKey)
    {
        if (!TryGetAttackAsset(attackKey, out CombatAttackDefinition attack))
        {
            return null;
        }

        CombatAttackDefinition runtimeCopy = attack.CreateRuntimeCopy();
        runtimeCopy.Normalize();
        return runtimeCopy;
    }

    [ContextMenu("Normalize Attacks")]
    public void NormalizeAll()
    {
        if (comboAttacks == null)
        {
            return;
        }

        for (int i = 0; i < comboAttacks.Count; i++)
        {
            CombatAttackDefinition attack = comboAttacks[i];
            if (attack == null)
            {
                continue;
            }

            attack.Normalize(i);
        }
    }

    private void OnValidate()
    {
        NormalizeAll();
    }

    private static bool MatchesAttackKey(CombatAttackDefinition attack, string attackKey)
    {
        if (attack == null || string.IsNullOrWhiteSpace(attackKey))
        {
            return false;
        }

        return string.Equals(attack.attackId, attackKey, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(attack.attackName, attackKey, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(attack.animatorStateName, attackKey, StringComparison.OrdinalIgnoreCase);
    }
}
