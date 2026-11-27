using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "PlayerStat", menuName = "Data/PlayerStat")]
public class PlayerStatData : ScriptableObject
{
    [System.Serializable]
    public struct AttackInfo
    {
        public string attackName;
        public string animatorStateName;
        public float damage;
        public float staminaCost;
        public float preDelay;
        public float postDelay;

        [Range(0f, 1f)] public float inputWindowStartNormalized;
        [Range(0f, 1f)] public float inputWindowEndNormalized;
        [Range(0f, 1f)] public float startupStartNormalized;
        [Range(0f, 1f)] public float activeStartNormalized;
        [Range(0f, 1f)] public float activeEndNormalized;
        [Range(0f, 1f)] public float recoveryEndNormalized;
        [Range(0f, 1f)] public float comboWindowStartNormalized;
        [Range(0f, 1f)] public float comboWindowEndNormalized;
        [Range(0f, 1f)] public float rollCancelStartNormalized;

        [Range(0f, 1f)] public float trackUntilNormalized;
        [Range(0f, 1f)] public float facingLockNormalized;

        public bool hasSuperArmor;
        [Range(0f, 1f)] public float superArmorStartNormalized;
        [Range(0f, 1f)] public float superArmorEndNormalized;

        public CombatAttackDefinition ToCombatAttackDefinition()
        {
            return CombatAttackDefinition.FromPlayerAttackInfo(this);
        }
    }

    [Header("Base Status (기본 능력치)")]
    [Tooltip("최대 체력")]
    public float maxHP = 1000;
    [Tooltip("최대 스태미너")]
    public float maxStamina = 100f;
    [Tooltip("초당 스태미나 회복량")]
    public float staminaRecovery = 15f;
    [Tooltip("가드 중 스태미나 회복 배율 (예: 0.4면 40% 속도로 회복)")]
    public float guardStaminaRecoveryMultiplier = 0.4f;

    [Header("Movement Status (이동 설정)")]
    [Tooltip("걷는 속도")]
    public float moveSpeed = 3f;
    [Tooltip("달리기 속도 배율")]
    public float runSpeedMultiplier = 1.5f;
    [Tooltip("달리기 초당 스태미나 소모량")]
    public float runStaminaCostPerSecond = 12f;
    [Tooltip("회전 속도")]
    public float rotationSpeed = 720f;

    [Header("구르기 설정")]
    [Tooltip("구르기에 적용되는 힘")]
    public float rollForce = 15f;
    [Tooltip("회피에 사용하는 스태미너 양")]
    public float rollCost = 20f;
    [Tooltip("구르기 애니메이션 시간")]
    public float rollDuration = 0.6f;
    [Tooltip("무적 시간")]
    public float invincibleTime = 0.4f;

    [Header("가드 설정")]
    [Tooltip("가드 가능 각도 (전방 기준 총 각도)")]
    [Range(30f, 180f)] public float guardBlockAngle = 120f;
    [Tooltip("들어온 데미지 1당 소모되는 스태미나 배율")]
    public float guardStaminaCostPerDamage = 1.2f;
    [Tooltip("가드 성공 시 남는 칩 데미지 비율 (0이면 완전 방어)")]
    [Range(0f, 1f)] public float guardChipDamageRatio = 0.05f;

    [Header("Parry Settings")]
    [Tooltip("패링이 적용되는 전방 각도")]
    [Range(30f, 180f)] public float parryAngle = 100f;
    [Tooltip("패링에 사용하는 스태미나")]
    [Min(0f)] public float parryStaminaCost = 12f;
    [Tooltip("패링 시작 준비 시간")]
    [Min(0.01f)] public float parryStartupDuration = 0.06f;
    [Tooltip("패링 활성 시간")]
    [Min(0.01f)] public float parryActiveDuration = 0.08f;
    [Tooltip("패링 후 경직 시간")]
    [Min(0.01f)] public float parryRecoveryDuration = 0.24f;

    [Header("Combat (전투 데이터)")]
    [Tooltip("공격 콤보 리스트")]
    [Header("Combat Authoring")]
    public PlayerAttackLibrary attackLibrary;
    [Tooltip("Legacy combo data. Used only when attackLibrary is not assigned.")]
    public List<AttackInfo> attackCombo;

    public bool HasAttackDefinitions => GetAttackCount() > 0;
    private void OnValidate()
    {
        if (attackCombo == null)
        {
            return;
        }

        for (int i = 0; i < attackCombo.Count; i++)
        {
            AttackInfo info = attackCombo[i];
            NormalizeAttackInfo(ref info, i);
            attackCombo[i] = info;
        }
    }

    private static void NormalizeAttackInfo(ref AttackInfo info, int attackIndex)
    {
        if (string.IsNullOrWhiteSpace(info.attackName))
        {
            info.attackName = $"Attack{attackIndex + 1}";
        }

        if (string.IsNullOrWhiteSpace(info.animatorStateName))
        {
            info.animatorStateName = $"Attack{attackIndex + 1}";
        }

        info.preDelay = Mathf.Max(0.01f, info.preDelay);
        info.postDelay = Mathf.Max(0.01f, info.postDelay);
        info.staminaCost = Mathf.Max(0f, info.staminaCost);
        info.damage = Mathf.Max(1f, info.damage);

        info.inputWindowStartNormalized = Mathf.Clamp01(info.inputWindowStartNormalized);
        info.inputWindowEndNormalized = Mathf.Clamp01(info.inputWindowEndNormalized);
        if (info.inputWindowEndNormalized < info.inputWindowStartNormalized)
        {
            info.inputWindowEndNormalized = info.inputWindowStartNormalized;
        }

        info.startupStartNormalized = Mathf.Clamp01(info.startupStartNormalized);
        info.activeStartNormalized = Mathf.Clamp01(info.activeStartNormalized);
        info.activeEndNormalized = Mathf.Clamp01(info.activeEndNormalized);
        if (info.activeEndNormalized < info.activeStartNormalized)
        {
            info.activeEndNormalized = info.activeStartNormalized;
        }

        info.recoveryEndNormalized = info.recoveryEndNormalized > 0f
            ? Mathf.Clamp01(info.recoveryEndNormalized)
            : 1f;
        if (info.recoveryEndNormalized < info.activeEndNormalized)
        {
            info.recoveryEndNormalized = info.activeEndNormalized;
        }

        info.comboWindowStartNormalized = info.comboWindowStartNormalized > 0f
            ? Mathf.Clamp01(info.comboWindowStartNormalized)
            : info.inputWindowStartNormalized;
        info.comboWindowEndNormalized = info.comboWindowEndNormalized > 0f
            ? Mathf.Clamp01(info.comboWindowEndNormalized)
            : info.inputWindowEndNormalized;
        if (info.comboWindowEndNormalized < info.comboWindowStartNormalized)
        {
            info.comboWindowEndNormalized = info.comboWindowStartNormalized;
        }

        info.rollCancelStartNormalized = info.rollCancelStartNormalized > 0f
            ? Mathf.Clamp01(info.rollCancelStartNormalized)
            : info.comboWindowStartNormalized;
        if (info.rollCancelStartNormalized > info.recoveryEndNormalized)
        {
            info.rollCancelStartNormalized = info.recoveryEndNormalized;
        }

        info.trackUntilNormalized = Mathf.Clamp(info.trackUntilNormalized, 0f, info.recoveryEndNormalized);
        info.facingLockNormalized = Mathf.Clamp(Mathf.Max(info.trackUntilNormalized, info.facingLockNormalized), 0f, info.recoveryEndNormalized);

        info.superArmorStartNormalized = Mathf.Clamp(info.superArmorStartNormalized, 0f, info.recoveryEndNormalized);
        info.superArmorEndNormalized = Mathf.Clamp(info.superArmorEndNormalized, info.superArmorStartNormalized, info.recoveryEndNormalized);
        info.hasSuperArmor = info.hasSuperArmor && info.superArmorEndNormalized > info.superArmorStartNormalized;
    }

    public int GetAttackCount()
    {
        if (attackLibrary != null && attackLibrary.Count > 0)
        {
            return attackLibrary.Count;
        }

        return attackCombo != null ? attackCombo.Count : 0;
    }

    public CombatAttackDefinition CreateAttackDefinition(int attackIndex)
    {
        if (attackLibrary != null && attackLibrary.Count > 0)
        {
            return attackLibrary.CreateAttackDefinition(attackIndex);
        }

        if (attackCombo == null || attackIndex < 0 || attackIndex >= attackCombo.Count)
        {
            return null;
        }

        return attackCombo[attackIndex].ToCombatAttackDefinition();
    }

    public CombatAttackDefinition CreateAttackDefinition(string attackKey)
    {
        if (string.IsNullOrWhiteSpace(attackKey))
        {
            return null;
        }

        if (attackLibrary != null && attackLibrary.Count > 0)
        {
            CombatAttackDefinition runtimeAttack = attackLibrary.CreateAttackDefinition(attackKey);
            if (runtimeAttack != null)
            {
                return runtimeAttack;
            }
        }

        if (attackCombo != null)
        {
            for (int i = 0; i < attackCombo.Count; i++)
            {
                AttackInfo info = attackCombo[i];
                if (!MatchesAttackKey(info.attackName, info.animatorStateName, attackKey))
                {
                    continue;
                }

                CombatAttackDefinition runtimeAttack = info.ToCombatAttackDefinition();
                runtimeAttack.Normalize(i);
                return runtimeAttack;
            }
        }

        return CreateFallbackSpecialAttackDefinition(attackKey);
    }

    public string GetAttackAnimatorStateName(int attackIndex)
    {
        if (attackLibrary != null && attackLibrary.TryGetAttackAsset(attackIndex, out CombatAttackDefinition attack))
        {
            return attack.ResolvedAnimatorStateName;
        }

        if (attackCombo != null && attackIndex >= 0 && attackIndex < attackCombo.Count)
        {
            string stateName = attackCombo[attackIndex].animatorStateName;
            if (!string.IsNullOrEmpty(stateName))
            {
                return stateName;
            }
        }

        return $"Attack{attackIndex + 1}";
    }

    public string GetAttackAnimatorStateName(string attackKey)
    {
        if (string.IsNullOrWhiteSpace(attackKey))
        {
            return string.Empty;
        }

        if (attackLibrary != null && attackLibrary.TryGetAttackAsset(attackKey, out CombatAttackDefinition attack))
        {
            return attack.ResolvedAnimatorStateName;
        }

        if (attackCombo != null)
        {
            for (int i = 0; i < attackCombo.Count; i++)
            {
                AttackInfo info = attackCombo[i];
                if (!MatchesAttackKey(info.attackName, info.animatorStateName, attackKey))
                {
                    continue;
                }

                return string.IsNullOrWhiteSpace(info.animatorStateName)
                    ? info.attackName
                    : info.animatorStateName;
            }
        }

        CombatAttackDefinition fallback = CreateFallbackSpecialAttackDefinition(attackKey);
        return fallback != null ? fallback.ResolvedAnimatorStateName : attackKey;
    }

    public float GetAttackStaminaCost(int attackIndex)
    {
        if (attackLibrary != null && attackLibrary.TryGetAttackAsset(attackIndex, out CombatAttackDefinition attack))
        {
            return Mathf.Max(0f, attack.staminaCost);
        }

        if (attackCombo != null && attackIndex >= 0 && attackIndex < attackCombo.Count)
        {
            return Mathf.Max(0f, attackCombo[attackIndex].staminaCost);
        }

        return 0f;
    }

    public float GetAttackStaminaCost(string attackKey)
    {
        if (string.IsNullOrWhiteSpace(attackKey))
        {
            return 0f;
        }

        if (attackLibrary != null && attackLibrary.TryGetAttackAsset(attackKey, out CombatAttackDefinition attack))
        {
            return Mathf.Max(0f, attack.staminaCost);
        }

        if (attackCombo != null)
        {
            for (int i = 0; i < attackCombo.Count; i++)
            {
                AttackInfo info = attackCombo[i];
                if (MatchesAttackKey(info.attackName, info.animatorStateName, attackKey))
                {
                    return Mathf.Max(0f, info.staminaCost);
                }
            }
        }

        CombatAttackDefinition fallback = CreateFallbackSpecialAttackDefinition(attackKey);
        if (fallback != null)
        {
            return Mathf.Max(0f, fallback.staminaCost);
        }

        return 0f;
    }

    private static bool MatchesAttackKey(string attackName, string animatorStateName, string attackKey)
    {
        return string.Equals(attackName, attackKey, System.StringComparison.OrdinalIgnoreCase) ||
               string.Equals(animatorStateName, attackKey, System.StringComparison.OrdinalIgnoreCase);
    }

    private CombatAttackDefinition CreateFallbackSpecialAttackDefinition(string attackKey)
    {
        if (string.Equals(attackKey, PlayerAnimatorStates.DashAttack, System.StringComparison.OrdinalIgnoreCase))
        {
            CombatAttackDefinition dashAttack = new CombatAttackDefinition
            {
                attackId = PlayerAnimatorStates.DashAttack,
                attackName = "DashAttack",
                animatorStateName = PlayerAnimatorStates.DashAttack,
                damage = 210,
                staminaCost = 16f,
                staminaDamage = 22f,
                groggyDamage = 16f,
                poiseDamage = 18f,
                startupStartNormalized = 0f,
                impactNormalized = 0.28f,
                activeStartNormalized = 0.22f,
                activeEndNormalized = 0.38f,
                recoveryEndNormalized = 0.84f,
                comboWindowStartNormalized = 0.62f,
                comboWindowEndNormalized = 0.78f,
                rollCancelStartNormalized = 0.72f,
                trackUntilNormalized = 0.2f,
                facingLockNormalized = 0.32f,
                dashDistance = 1.8f,
                dashDuration = 0.26f,
                prepareStartSpeed = 1.2f,
                prepareEndSpeed = 1.35f,
                impactSpeed = 1.4f,
                recoverySpeed = 1f,
                isHeavyAttack = false,
                hitStopDuration = 0.07f,
                hitStopMultiplier = 1.1f
            };
            dashAttack.Normalize();
            return dashAttack;
        }

        if (string.Equals(attackKey, PlayerAnimatorStates.GroggyAttack, System.StringComparison.OrdinalIgnoreCase))
        {
            CombatAttackDefinition groggyAttack = new CombatAttackDefinition
            {
                attackId = PlayerAnimatorStates.GroggyAttack,
                attackName = "GroggyAttack",
                animatorStateName = PlayerAnimatorStates.GroggyAttack,
                damage = 450,
                staminaCost = 0f,
                staminaDamage = 60f,
                groggyDamage = 120f,
                poiseDamage = 120f,
                startupStartNormalized = 0f,
                impactNormalized = 0.5f,
                activeStartNormalized = 0.42f,
                activeEndNormalized = 0.58f,
                recoveryEndNormalized = 1f,
                comboWindowStartNormalized = 1f,
                comboWindowEndNormalized = 1f,
                rollCancelStartNormalized = 1f,
                trackUntilNormalized = 0.1f,
                facingLockNormalized = 0.18f,
                hasSuperArmor = true,
                superArmorStartNormalized = 0.18f,
                superArmorEndNormalized = 0.6f,
                isHeavyAttack = true,
                cameraShakeIntensity = 0.2f,
                hitStopDuration = 0.12f,
                hitStopMultiplier = 1.4f
            };
            groggyAttack.Normalize();
            return groggyAttack;
        }

        return null;
    }
}
