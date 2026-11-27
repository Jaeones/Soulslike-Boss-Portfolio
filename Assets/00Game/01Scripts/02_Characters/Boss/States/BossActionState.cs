using System.Collections;
using UnityEngine;

public class BossActionState : BossState
{
    private const float DefaultDashDuration = 0.1f;
    private const float MinDistance = 1.35f;
    private const float SkillGAllLastFrame = 72f;
    private const float SkillGSlowEndNormalized = 10f / SkillGAllLastFrame;
    private const float SkillGMidEndNormalized = 20f / SkillGAllLastFrame;
    private Vector3 _attackDesiredForward = Vector3.forward;
    private Vector3 _attackLockedForward = Vector3.forward;
    private bool _isAttackFacingActive;

    public BossActionState(BossController boss) : base(boss) { }

    public override IEnumerator Enter()
    {
        if (boss.IsExternalEntranceControlActive ||
            boss.isPhaseTransitioning ||
            boss.currentState == EnemyBase.State.Groggy ||
            boss.currentState == EnemyBase.State.HitReact ||
            boss.target == null)
        {
            yield break;
        }

        CombatAttackDefinition lastAttack = null;
        string previousMotion = null;
        boss.SetRootMotionMode(boss.ShouldUseActionRootMotion(), false);

        while (boss.GetAttackQueueCount() > 0)
        {
            if (boss.IsExternalEntranceControlActive ||
                boss.IsDead ||
                boss.isPhaseTransitioning ||
                boss.currentState == EnemyBase.State.Groggy ||
                boss.currentState == EnemyBase.State.HitReact ||
                boss.target == null)
            {
                break;
            }

            float currentDist = Vector3.Distance(boss.transform.position, boss.target.position);
            if (currentDist > boss.ResolveCombatActionAbortDistance())
            {
                break;
            }

            string rawName = boss.GetNextAttackInQueue();
            if (string.IsNullOrEmpty(rawName))
            {
                break;
            }

            string currentMotion = rawName.Replace("_Delay", "");
            CombatAttackDefinition attack = boss.ResolveAttackDefinition(currentMotion);
            if (attack == null)
            {
                continue;
            }

            string nextQueuedMotion = boss.PeekNextAttackInQueue();
            if (!string.IsNullOrWhiteSpace(nextQueuedMotion))
            {
                nextQueuedMotion = nextQueuedMotion.Replace("_Delay", "");
            }

            bool shouldImmediateChainCombo = IsImmediateComboFollowup(currentMotion, nextQueuedMotion);
            bool isComboFollowupFromPrevious = IsImmediateComboFollowup(previousMotion, currentMotion);

            if (!isComboFollowupFromPrevious &&
                !boss.TryConsumeStamina(boss.ResolveAttackStaminaCost(attack)))
            {
                break;
            }

            boss.RecordExecutedPattern(currentMotion);
            lastAttack = attack;
            previousMotion = currentMotion;

            boss.StopAgent();
            boss.RotateToTargetWithOffset();
            boss.BeginAttackExecution(attack);
            BeginAttackFacing(attack);
            boss.PlayAnim(attack.ResolvedAnimatorStateName, shouldImmediateChainCombo ? 0.02f : 0.05f);

            yield return null;

            float elapsed = 0f;
            bool hasImpactProcessed = false;
            bool hasSpecialPauseProcessed = false;

            while (elapsed < 5.0f)
            {
                if (boss.IsDead ||
                    boss.isPhaseTransitioning ||
                    boss.currentState == EnemyBase.State.Groggy ||
                    boss.currentState == EnemyBase.State.HitReact)
                {
                    break;
                }

                elapsed += Time.deltaTime;

                AnimatorStateInfo stateInfo = boss.Anim.GetCurrentAnimatorStateInfo(0);
                if (stateInfo.IsName(attack.ResolvedAnimatorStateName))
                {
                    float normalizedTime = Mathf.Clamp01(stateInfo.normalizedTime);

                    if (!hasSpecialPauseProcessed &&
                        string.Equals(attack.ResolvedAttackId, "Skill_H", System.StringComparison.Ordinal) &&
                        normalizedTime >= (15f / 47f))
                    {
                        boss.SetAnimSpeed(0f);
                        yield return new WaitForSeconds(0.3f);
                        elapsed += 0.3f;
                        hasSpecialPauseProcessed = true;
                        continue;
                    }

                    UpdateAttackFacing(attack, normalizedTime);
                    boss.UpdateAttackExecution(normalizedTime);
                    boss.TickAttackExecution();

                    if (!hasImpactProcessed && normalizedTime >= attack.impactNormalized)
                    {
                        float dashDistance = CalculateDashDistance(attack);
                        if (dashDistance > 0f && ShouldApplyScriptedDash())
                        {
                            float dashDuration = attack.dashDuration > 0f ? attack.dashDuration : DefaultDashDuration;
                            boss.StartCoroutine(Co_FastDash(dashDuration, dashDistance));
                        }

                        hasImpactProcessed = true;
                    }

                    ApplyAnimationSpeed(attack, normalizedTime);

                    if (shouldImmediateChainCombo &&
                        normalizedTime >= ResolveComboChainExitNormalized(attack))
                    {
                        break;
                    }

                    if (!shouldImmediateChainCombo &&
                        boss.GetAttackQueueCount() > 0 &&
                        normalizedTime > Mathf.Max(attack.activeEndNormalized, 0.75f))
                    {
                        break;
                    }

                    if (normalizedTime >= Mathf.Max(0.98f, attack.recoveryEndNormalized))
                    {
                        break;
                    }
                }
                else if (elapsed > 0.25f && !boss.Anim.IsInTransition(0))
                {
                    break;
                }

                yield return null;
            }

            boss.EndAttackExecution();
            EndAttackFacing();
        }

        boss.EndAttackExecution();
        EndAttackFacing();
        boss.SetRootMotionMode(false, false);
        boss.SetAnimSpeed(1.0f);

        if (!boss.IsDead)
        {
            if (lastAttack != null)
            {
                boss.StartCoroutine(boss.Co_WaitAfterAttack(lastAttack.postAttackWaitMultiplier));
            }
            else
            {
                while (!boss.IsDead && !boss.isPhaseTransitioning)
                {
                    if (boss.TryChangeState(boss.idleState, EnemyBase.State.Idle))
                    {
                        yield break;
                    }

                    yield return null;
                }
            }
        }
    }

    private void ApplyAnimationSpeed(CombatAttackDefinition attack, float normalizedTime)
    {
        string attackId = attack != null ? attack.ResolvedAttackId : string.Empty;
        if (string.Equals(attackId, "Skill_G", System.StringComparison.Ordinal))
        {
            if (normalizedTime < SkillGSlowEndNormalized)
            {
                boss.SetAnimSpeed(0.2f * boss.baseAttackSpeed);
                return;
            }

            if (normalizedTime < SkillGMidEndNormalized)
            {
                boss.SetAnimSpeed(0.5f * boss.baseAttackSpeed);
                return;
            }

            boss.SetAnimSpeed(1f * boss.baseAttackSpeed);
            return;
        }

        if (string.Equals(attackId, "Attack_3Combo_1", System.StringComparison.Ordinal))
        {
            const float attack3Combo1SlowEndNormalized = 3f / 42f;

            if (normalizedTime < attack3Combo1SlowEndNormalized)
            {
                boss.SetAnimSpeed(0.3f * boss.baseAttackSpeed);
                return;
            }

            boss.SetAnimSpeed(1f * boss.baseAttackSpeed);
            return;
        }

        if (string.Equals(attackId, "Attack_3Combo_2", System.StringComparison.Ordinal))
        {
            const float attack3Combo2SlowEndNormalized = 4f / 30f;

            if (normalizedTime < attack3Combo2SlowEndNormalized)
            {
                boss.SetAnimSpeed(0.3f * boss.baseAttackSpeed);
                return;
            }

            boss.SetAnimSpeed(1f * boss.baseAttackSpeed);
            return;
        }

        if (string.Equals(attackId, "Attack_3Combo_3", System.StringComparison.Ordinal))
        {
            const float attack3Combo3SlowEndNormalized = 6f / 40f;

            if (normalizedTime < attack3Combo3SlowEndNormalized)
            {
                boss.SetAnimSpeed(0.3f * boss.baseAttackSpeed);
                return;
            }

            boss.SetAnimSpeed(1f * boss.baseAttackSpeed);
            return;
        }

        if (string.Equals(attackId, "Attack_4Combo_1", System.StringComparison.Ordinal))
        {
            const float attack4Combo1SlowEndNormalized = 7f / 37f;

            if (normalizedTime < attack4Combo1SlowEndNormalized)
            {
                boss.SetAnimSpeed(0.3f * boss.baseAttackSpeed);
                return;
            }

            boss.SetAnimSpeed(1f * boss.baseAttackSpeed);
            return;
        }

        if (string.Equals(attackId, "Attack_4Combo_2", System.StringComparison.Ordinal))
        {
            const float attack4Combo2SlowEndNormalized = 3f / 32f;

            if (normalizedTime < attack4Combo2SlowEndNormalized)
            {
                boss.SetAnimSpeed(0.3f * boss.baseAttackSpeed);
                return;
            }

            boss.SetAnimSpeed(1f * boss.baseAttackSpeed);
            return;
        }

        if (string.Equals(attackId, "Attack_4Combo_4", System.StringComparison.Ordinal))
        {
            const float attack4Combo4SlowEndNormalized = 6f / 40f;

            if (normalizedTime < attack4Combo4SlowEndNormalized)
            {
                boss.SetAnimSpeed(0.2f * boss.baseAttackSpeed);
                return;
            }

            boss.SetAnimSpeed(1f * boss.baseAttackSpeed);
            return;
        }

        if (string.Equals(attackId, "Skill_C", System.StringComparison.Ordinal))
        {
            const float skillCSlowEndNormalized = 8f / 70f;
            const float skillCSlowerEndNormalized = 11f / 70f;

            if (normalizedTime < skillCSlowEndNormalized)
            {
                boss.SetAnimSpeed(0.5f * boss.baseAttackSpeed);
                return;
            }

            if (normalizedTime < skillCSlowerEndNormalized)
            {
                boss.SetAnimSpeed(0.3f * boss.baseAttackSpeed);
                return;
            }

            boss.SetAnimSpeed(1f * boss.baseAttackSpeed);
            return;
        }

        if (string.Equals(attackId, "Skill_D", System.StringComparison.Ordinal))
        {
            const float skillDFirstSlowEndNormalized = 6f / 42f;
            const float skillDSecondSlowEndNormalized = 9f / 42f;

            if (normalizedTime < skillDFirstSlowEndNormalized)
            {
                boss.SetAnimSpeed(0.3f * boss.baseAttackSpeed);
                return;
            }

            if (normalizedTime < skillDSecondSlowEndNormalized)
            {
                boss.SetAnimSpeed(0.5f * boss.baseAttackSpeed);
                return;
            }

            boss.SetAnimSpeed(1f * boss.baseAttackSpeed);
            return;
        }

        if (string.Equals(attackId, "Skill_H", System.StringComparison.Ordinal))
        {
            const float skillHSlowEndNormalized = 15f / 47f;

            if (normalizedTime < skillHSlowEndNormalized)
            {
                boss.SetAnimSpeed(0.5f * boss.baseAttackSpeed);
                return;
            }

            boss.SetAnimSpeed(1f * boss.baseAttackSpeed);
            return;
        }

        if (string.Equals(attackId, "Skill_I", System.StringComparison.Ordinal))
        {
            const float skillISlowEndNormalized = 17f / 46f;

            if (normalizedTime < skillISlowEndNormalized)
            {
                boss.SetAnimSpeed(0.3f * boss.baseAttackSpeed);
                return;
            }

            boss.SetAnimSpeed(1f * boss.baseAttackSpeed);
            return;
        }

        if (string.Equals(attackId, "Skill_J", System.StringComparison.Ordinal))
        {
            const float skillJSlowEndNormalized = 6f / 65f;

            if (normalizedTime < skillJSlowEndNormalized)
            {
                boss.SetAnimSpeed(0.2f * boss.baseAttackSpeed);
                return;
            }

            boss.SetAnimSpeed(1f * boss.baseAttackSpeed);
            return;
        }

        if (string.Equals(attackId, "Skill_K", System.StringComparison.Ordinal))
        {
            const float skillKSlowEndNormalized = 7f / 60f;

            if (normalizedTime < skillKSlowEndNormalized)
            {
                boss.SetAnimSpeed(0.3f * boss.baseAttackSpeed);
                return;
            }

            boss.SetAnimSpeed(1f * boss.baseAttackSpeed);
            return;
        }

        float prepareEnd = Mathf.Max(0.01f, attack.impactNormalized);
        float impactEnd = Mathf.Max(attack.activeEndNormalized, attack.impactNormalized);

        if (normalizedTime < prepareEnd)
        {
            float t = normalizedTime / prepareEnd;
            float currentSpeed = Mathf.Lerp(attack.prepareStartSpeed, attack.prepareEndSpeed, t);
            boss.SetAnimSpeed(currentSpeed * boss.baseAttackSpeed);
            return;
        }

        if (normalizedTime < impactEnd)
        {
            boss.SetAnimSpeed(attack.impactSpeed * boss.baseAttackSpeed);
            return;
        }

        boss.SetAnimSpeed(attack.recoverySpeed * boss.baseAttackSpeed);
    }

    private void BeginAttackFacing(CombatAttackDefinition attack)
    {
        _isAttackFacingActive = attack != null;
        if (!_isAttackFacingActive)
        {
            return;
        }

        _attackDesiredForward = ResolveFacingDirection();
        _attackLockedForward = _attackDesiredForward;
        ApplyImmediateFacing(_attackDesiredForward);
    }

    private void UpdateAttackFacing(CombatAttackDefinition attack, float normalizedTime)
    {
        if (!_isAttackFacingActive)
        {
            return;
        }

        float clampedTime = Mathf.Clamp01(normalizedTime);
        if (attack.IsInTrackingWindow(clampedTime))
        {
            Vector3 desiredForward = ResolveFacingDirection();
            if (desiredForward.sqrMagnitude > 0.0001f)
            {
                _attackDesiredForward = desiredForward;
            }
        }

        if (attack.IsInFacingLockWindow(clampedTime))
        {
            RotateTowardsFacing(_attackDesiredForward, boss.rotationSpeed * Time.deltaTime);
            _attackLockedForward = boss.transform.forward;
            return;
        }

        ApplyImmediateFacing(_attackLockedForward);
    }

    private void EndAttackFacing()
    {
        _isAttackFacingActive = false;
        _attackDesiredForward = boss.transform.forward;
        _attackLockedForward = boss.transform.forward;
    }

    private Vector3 ResolveFacingDirection()
    {
        if (boss.target != null)
        {
            Vector3 toTarget = boss.target.position - boss.transform.position;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude > 0.0001f)
            {
                return toTarget.normalized;
            }
        }

        Vector3 fallback = boss.transform.forward;
        fallback.y = 0f;
        if (fallback.sqrMagnitude > 0.0001f)
        {
            return fallback.normalized;
        }

        return Vector3.forward;
    }

    private void RotateTowardsFacing(Vector3 desiredForward, float turnLerp)
    {
        desiredForward.y = 0f;
        if (desiredForward.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(desiredForward.normalized, Vector3.up);
        boss.transform.rotation = Quaternion.Slerp(
            boss.transform.rotation,
            targetRotation,
            Mathf.Clamp01(turnLerp));
    }

    private void ApplyImmediateFacing(Vector3 desiredForward)
    {
        desiredForward.y = 0f;
        if (desiredForward.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        boss.transform.rotation = Quaternion.LookRotation(desiredForward.normalized, Vector3.up);
    }

    private IEnumerator Co_FastDash(float duration, float distance)
    {
        float time = 0f;
        Vector3 startPos = boss.transform.position;
        Vector3 targetPos = startPos + (boss.transform.forward * distance);

        while (time < duration)
        {
            time += Time.deltaTime;
            float t = Mathf.Sin((time / duration) * Mathf.PI * 0.5f);
            boss.transform.position = Vector3.Lerp(startPos, targetPos, t);
            yield return null;
        }
    }

    private bool ShouldApplyScriptedDash()
    {
        return !boss.ShouldUseActionRootMotion();
    }

    private float CalculateDashDistance(CombatAttackDefinition attack)
    {
        if (boss.target == null)
        {
            return 0f;
        }

        float dist = Vector3.Distance(boss.transform.position, boss.target.position);
        float maxAdvance = Mathf.Max(0f, dist - MinDistance);
        return Mathf.Min(attack.dashDistance, maxAdvance);
    }

    private static bool IsImmediateComboFollowup(string currentMotion, string nextMotion)
    {
        return IsSequentialComboPair(currentMotion, nextMotion, "Attack_3Combo_", 3) ||
               IsSequentialComboPair(currentMotion, nextMotion, "Attack_4Combo_", 4);
    }

    private static bool IsSequentialComboPair(string currentMotion, string nextMotion, string prefix, int maxStep)
    {
        if (string.IsNullOrWhiteSpace(currentMotion) || string.IsNullOrWhiteSpace(nextMotion))
        {
            return false;
        }

        if (!currentMotion.StartsWith(prefix, System.StringComparison.Ordinal) ||
            !nextMotion.StartsWith(prefix, System.StringComparison.Ordinal))
        {
            return false;
        }

        if (!int.TryParse(currentMotion.Substring(prefix.Length), out int currentStep) ||
            !int.TryParse(nextMotion.Substring(prefix.Length), out int nextStep))
        {
            return false;
        }

        if (currentStep < 1 || currentStep > maxStep || nextStep < 1 || nextStep > maxStep)
        {
            return false;
        }

        return nextStep == currentStep + 1;
    }

    private static float ResolveComboChainExitNormalized(CombatAttackDefinition attack)
    {
        if (attack == null)
        {
            return 0.6f;
        }

        return Mathf.Clamp01(Mathf.Max(
            attack.activeEndNormalized,
            attack.comboWindowStartNormalized));
    }
}
