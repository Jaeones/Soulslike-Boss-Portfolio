using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

#if UNITY_EDITOR
using UnityEditor;
#endif

[System.Serializable]
public class BossAttackData
{
    [InspectorName("공격 ID")]
    public string attackId;
    [InspectorName("애니메이션 상태 이름")]
    public string animatorStateName;
    [InspectorName("플레이어 피격 타입")]
    public PlayerHitReactionType playerHitReactionType = PlayerHitReactionType.DefaultDirectional;

    [Header("Damage")]
    [InspectorName("체력 피해량")]
    [Min(1)] public int damage = 10;
    [InspectorName("공격 스태미나 소모")]
    [Min(0f)] public float staminaCost = 0f;
    [InspectorName("플레이어 스태미나 피해")]
    [Min(0f)] public float staminaDamage = 0f;
    [InspectorName("그로기 피해")]
    [Min(0f)] public float groggyDamage = 0f;
    [InspectorName("포이즈 피해")]
    [Min(0f)] public float poiseDamage = 0f;

    [Header("Timing")]
    [InspectorName("임팩트 시점")]
    [Range(0f, 1f)] public float impactNormalized = 0.5f;
    [InspectorName("공격 활성 시작")]
    [Range(0f, 1f)] public float activeStartNormalized = 0.35f;
    [InspectorName("공격 활성 종료")]
    [Range(0f, 1f)] public float activeEndNormalized = 0.5f;
    [InspectorName("리커버리 종료")]
    [Range(0f, 1f)] public float recoveryEndNormalized = 1f;

    [Header("Control")]
    [InspectorName("콤보 전환 시작")]
    [Range(0f, 1f)] public float comboWindowStartNormalized = 0.6f;
    [InspectorName("추적 허용 종료")]
    [Range(0f, 1f)] public float trackUntilNormalized = 0.15f;
    [InspectorName("방향 고정 종료")]
    [Range(0f, 1f)] public float facingLockNormalized = 0.24f;

    [Header("Armor")]
    [InspectorName("슈퍼아머 사용")]
    public bool hasSuperArmor;
    [InspectorName("슈퍼아머 시작")]
    [Range(0f, 1f)] public float superArmorStartNormalized = 0f;
    [InspectorName("슈퍼아머 종료")]
    [Range(0f, 1f)] public float superArmorEndNormalized = 0f;

    [Header("Motion")]
    [InspectorName("전진 기준값")]
    [Min(0f)] public float dashDistance = 0f;
    [InspectorName("준비 시작 속도")]
    [Min(0.01f)] public float prepareStartSpeed = 0.1f;
    [InspectorName("준비 종료 속도")]
    [Min(0.01f)] public float prepareEndSpeed = 0.8f;
    [InspectorName("임팩트 속도")]
    [Min(0.01f)] public float impactSpeed = 5f;
    [InspectorName("회복 속도")]
    [Min(0.01f)] public float recoverySpeed = 0.3f;
    [InspectorName("공격 후 대기 배수")]
    [Min(0f)] public float postAttackWaitMultiplier = 0.15f;

    [Header("Feedback")]
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
    public string ResolvedAnimatorStateName => string.IsNullOrWhiteSpace(animatorStateName) ? attackId : animatorStateName;

    public void Normalize(int attackIndex = -1)
    {
        if (string.IsNullOrWhiteSpace(attackId))
        {
            attackId = attackIndex >= 0 ? $"Attack_{attackIndex + 1}" : "Attack";
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
        trackUntilNormalized = Mathf.Clamp(trackUntilNormalized, 0f, recoveryEndNormalized);
        facingLockNormalized = Mathf.Clamp(
            Mathf.Max(trackUntilNormalized, facingLockNormalized),
            0f,
            recoveryEndNormalized);

        superArmorStartNormalized = Mathf.Clamp(superArmorStartNormalized, 0f, recoveryEndNormalized);
        superArmorEndNormalized = Mathf.Clamp(superArmorEndNormalized, superArmorStartNormalized, recoveryEndNormalized);
        hasSuperArmor = hasSuperArmor && superArmorEndNormalized > superArmorStartNormalized;

        dashDistance = Mathf.Max(0f, dashDistance);
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

    public CombatAttackDefinition ToRuntimeDefinition()
    {
        return new CombatAttackDefinition
        {
            attackId = ResolvedAttackId,
            attackName = ResolvedAttackId,
            animatorStateName = ResolvedAnimatorStateName,
            playerHitReactionType = playerHitReactionType,
            damage = damage,
            staminaCost = staminaCost,
            staminaDamage = staminaDamage,
            groggyDamage = groggyDamage,
            poiseDamage = poiseDamage,
            startupStartNormalized = 0f,
            impactNormalized = impactNormalized,
            activeStartNormalized = activeStartNormalized,
            activeEndNormalized = activeEndNormalized,
            recoveryEndNormalized = recoveryEndNormalized,
            comboWindowStartNormalized = comboWindowStartNormalized,
            comboWindowEndNormalized = comboWindowStartNormalized,
            rollCancelStartNormalized = comboWindowStartNormalized,
            trackUntilNormalized = trackUntilNormalized,
            facingLockNormalized = facingLockNormalized,
            hasSuperArmor = hasSuperArmor,
            superArmorStartNormalized = superArmorStartNormalized,
            superArmorEndNormalized = superArmorEndNormalized,
            dashDistance = dashDistance,
            dashDuration = 0.1f,
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

    public static BossAttackData FromLegacy(CombatAttackDefinition legacy)
    {
        if (legacy == null)
        {
            return null;
        }

        return new BossAttackData
        {
            attackId = legacy.ResolvedAttackId,
            animatorStateName = legacy.ResolvedAnimatorStateName,
            playerHitReactionType = legacy.playerHitReactionType,
            damage = legacy.damage,
            staminaCost = legacy.staminaCost,
            staminaDamage = legacy.staminaDamage,
            groggyDamage = legacy.groggyDamage,
            poiseDamage = legacy.poiseDamage,
            impactNormalized = legacy.impactNormalized,
            activeStartNormalized = legacy.activeStartNormalized,
            activeEndNormalized = legacy.activeEndNormalized,
            recoveryEndNormalized = legacy.recoveryEndNormalized,
            comboWindowStartNormalized = legacy.comboWindowStartNormalized,
            trackUntilNormalized = legacy.trackUntilNormalized,
            facingLockNormalized = legacy.facingLockNormalized,
            hasSuperArmor = legacy.hasSuperArmor,
            superArmorStartNormalized = legacy.superArmorStartNormalized,
            superArmorEndNormalized = legacy.superArmorEndNormalized,
            dashDistance = legacy.dashDistance,
            prepareStartSpeed = legacy.prepareStartSpeed,
            prepareEndSpeed = legacy.prepareEndSpeed,
            impactSpeed = legacy.impactSpeed,
            recoverySpeed = legacy.recoverySpeed,
            postAttackWaitMultiplier = legacy.postAttackWaitMultiplier,
            isHeavyAttack = legacy.isHeavyAttack,
            cameraShakeIntensity = legacy.cameraShakeIntensity,
            cameraShakeDuration = legacy.cameraShakeDuration,
            hitStopDuration = legacy.hitStopDuration,
            hitStopMultiplier = legacy.hitStopMultiplier
        };
    }
}

[CreateAssetMenu(fileName = "BossAttackLibrary", menuName = "Data/Combat/Boss Attack Library")]
public class BossAttackLibrary : ScriptableObject
{
    [Header("보스 공격 데이터")]
    [SerializeField, InspectorName("보스 공격 목록")] private List<BossAttackData> bossAttacks = new List<BossAttackData>();
    [SerializeField, HideInInspector, FormerlySerializedAs("attacks")] private List<CombatAttackDefinition> legacyAttacks = new List<CombatAttackDefinition>();

    private readonly Dictionary<string, CombatAttackDefinition> _attackMap = new Dictionary<string, CombatAttackDefinition>();

    public CombatAttackDefinition Resolve(string attackId)
    {
        RebuildCacheIfNeeded();

        if (!string.IsNullOrWhiteSpace(attackId) && _attackMap.TryGetValue(attackId, out CombatAttackDefinition definition))
        {
            return definition;
        }

        return CreateFallback(attackId);
    }

    public List<string> GetRegisteredAttackIds()
    {
        RebuildCacheIfNeeded();

        List<string> result = new List<string>();
        if (bossAttacks == null)
        {
            return result;
        }

        foreach (BossAttackData attack in bossAttacks)
        {
            if (attack == null)
            {
                continue;
            }

            string attackId = attack.ResolvedAttackId;
            if (string.IsNullOrWhiteSpace(attackId) || result.Contains(attackId))
            {
                continue;
            }

            result.Add(attackId);
        }

        return result;
    }

    private void OnValidate()
    {
        EnsureMigrated();
        NormalizeAll();
        _attackMap.Clear();
    }

    private void RebuildCacheIfNeeded()
    {
        if (_attackMap.Count > 0)
        {
            return;
        }

        EnsureMigrated();
        NormalizeAll();

        foreach (BossAttackData attack in bossAttacks)
        {
            if (attack == null)
            {
                continue;
            }

            CombatAttackDefinition runtimeDefinition = attack.ToRuntimeDefinition();
            runtimeDefinition.Normalize();

            string attackKey = runtimeDefinition.ResolvedAttackId;
            if (!string.IsNullOrWhiteSpace(attackKey))
            {
                _attackMap[attackKey] = runtimeDefinition;
            }

            string animatorKey = runtimeDefinition.ResolvedAnimatorStateName;
            if (!string.IsNullOrWhiteSpace(animatorKey))
            {
                _attackMap[animatorKey] = runtimeDefinition;
            }
        }
    }

    private void NormalizeAll()
    {
        if (bossAttacks == null)
        {
            return;
        }

        for (int i = 0; i < bossAttacks.Count; i++)
        {
            BossAttackData attack = bossAttacks[i];
            if (attack == null)
            {
                continue;
            }

            attack.Normalize(i);
        }
    }

    private void EnsureMigrated()
    {
        if ((bossAttacks == null || bossAttacks.Count == 0) &&
            legacyAttacks != null &&
            legacyAttacks.Count > 0)
        {
            if (bossAttacks == null)
            {
                bossAttacks = new List<BossAttackData>();
            }

            bossAttacks.Clear();
            foreach (CombatAttackDefinition legacyAttack in legacyAttacks)
            {
                BossAttackData migrated = BossAttackData.FromLegacy(legacyAttack);
                if (migrated != null)
                {
                    migrated.Normalize(bossAttacks.Count);
                    bossAttacks.Add(migrated);
                }
            }
        }

        if (bossAttacks != null &&
            bossAttacks.Count > 0 &&
            legacyAttacks != null &&
            legacyAttacks.Count > 0)
        {
            legacyAttacks.Clear();
#if UNITY_EDITOR
            EditorUtility.SetDirty(this);
#endif
        }
    }

    public static CombatAttackDefinition CreateFallback(string attackId)
    {
        string motionName = string.IsNullOrWhiteSpace(attackId) ? "BossAttack" : attackId;
        bool isSkill = motionName.Contains("Skill");
        bool isHeavy = motionName.Contains("Skill_J") || motionName.Contains("Skill_G");
        bool isComboLastHit = motionName.EndsWith("_3") || motionName.EndsWith("_4") || motionName.EndsWith("_7");
        bool isDash = motionName == "Dash" || motionName.Contains("Skill_H");

        CombatAttackDefinition attack = new CombatAttackDefinition
        {
            attackId = motionName,
            attackName = motionName,
            animatorStateName = motionName,
            playerHitReactionType = isHeavy ? PlayerHitReactionType.Smash : PlayerHitReactionType.DefaultDirectional,
            damage = isHeavy ? 36 : isSkill ? 28 : 20,
            staminaCost = isHeavy ? 28f : isSkill ? 22f : 14f,
            staminaDamage = isHeavy ? 40f : isSkill ? 28f : 18f,
            groggyDamage = isHeavy ? 34f : isSkill ? 22f : 14f,
            poiseDamage = isHeavy ? 32f : isSkill ? 20f : 12f,
            startupStartNormalized = 0f,
            impactNormalized = isHeavy ? 0.55f : 0.48f,
            activeStartNormalized = isHeavy ? 0.44f : 0.36f,
            activeEndNormalized = isHeavy ? 0.66f : 0.52f,
            recoveryEndNormalized = 1f,
            trackUntilNormalized = isHeavy ? 0.08f : isDash ? 0.22f : 0.16f,
            facingLockNormalized = isHeavy ? 0.16f : isDash ? 0.3f : 0.24f,
            hasSuperArmor = isHeavy,
            superArmorStartNormalized = isHeavy ? 0.16f : 0f,
            superArmorEndNormalized = isHeavy ? 0.72f : 0f,
            dashDistance = isDash ? 2.6f : 0f,
            dashDuration = 0.1f,
            prepareStartSpeed = isHeavy ? 0.05f : 0.1f,
            prepareEndSpeed = 0.8f,
            impactSpeed = isHeavy ? 3.5f : 5f,
            recoverySpeed = isHeavy ? 0.18f : 0.3f,
            postAttackWaitMultiplier = isHeavy || isComboLastHit ? 1.2f : 0.15f,
            isHeavyAttack = isHeavy,
            cameraShakeIntensity = isHeavy ? 0.15f : isComboLastHit ? 0.08f : isSkill ? 0.05f : 0f,
            cameraShakeDuration = 0.08f,
            hitStopDuration = isSkill ? 0.12f : 0.08f,
            hitStopMultiplier = isHeavy ? 1.8f : isComboLastHit ? 1.4f : isSkill ? 1.2f : 1f
        };

        if (motionName.Contains("Skill_J"))
        {
            attack.playerHitReactionType = PlayerHitReactionType.Down;
            attack.damage = 42;
            attack.impactNormalized = 0.42f;
            attack.activeStartNormalized = 0.36f;
            attack.activeEndNormalized = 0.6f;
            attack.dashDistance = 3.2f;
        }
        else if (motionName.Contains("Skill_G"))
        {
            attack.playerHitReactionType = PlayerHitReactionType.Smash;
            attack.damage = 48;
            attack.impactNormalized = 12f / 72f;
            attack.activeStartNormalized = 12f / 72f;
            attack.activeEndNormalized = 41f / 72f;
            attack.prepareStartSpeed = 0.2f;
            attack.prepareEndSpeed = 0.5f;
            attack.impactSpeed = 1f;
            attack.recoverySpeed = 1f;
            attack.postAttackWaitMultiplier = 1.5f;
            attack.hasSuperArmor = true;
            attack.superArmorStartNormalized = 0.18f;
            attack.superArmorEndNormalized = 0.78f;
        }
        else if (motionName == "Dash")
        {
            attack.playerHitReactionType = PlayerHitReactionType.Dash;
            attack.damage = 18;
            attack.impactNormalized = 0.4f;
            attack.activeStartNormalized = 0.32f;
            attack.activeEndNormalized = 0.46f;
            attack.dashDistance = 3.4f;
            attack.postAttackWaitMultiplier = 0.1f;
            attack.trackUntilNormalized = 0.24f;
            attack.facingLockNormalized = 0.34f;
        }

        if (motionName.Contains("Slam") || motionName.Contains("Skill_D"))
        {
            attack.playerHitReactionType = PlayerHitReactionType.Down;
        }
        else if (motionName.Contains("Skill_H"))
        {
            attack.playerHitReactionType = PlayerHitReactionType.Down;
        }
        else if (motionName.Contains("Skill_K"))
        {
            attack.playerHitReactionType = PlayerHitReactionType.Spear;
        }

        attack.Normalize();
        return attack;
    }
}
