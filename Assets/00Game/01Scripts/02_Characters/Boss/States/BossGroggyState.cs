using System.Collections;
using UnityEngine;

public class BossGroggyState : BossState
{
    public BossGroggyState(BossController boss) : base(boss) { }

    public override IEnumerator Enter()
    {
        boss.BeginGroggyRuntime();
        boss.StopAgent();
        bool useReactionRootMotion = boss.ShouldUseReactionRootMotion();
        boss.SetRootMotionMode(useReactionRootMotion, useReactionRootMotion);
        boss.PlayAnim(boss.GroggyAnimationStateName, 0.1f);

        float elapsed = 0f;
        float duration = Mathf.Max(0.1f, boss.groggyDuration);

        while ((!boss.HasConsumedCriticalStrikeThisGroggy && elapsed < duration) || boss.IsCriticalStrikeExecuting)
        {
            if (boss.IsDead)
            {
                yield break;
            }

            if (!boss.IsCriticalStrikeExecuting)
            {
                elapsed += Time.deltaTime;
            }

            yield return null;
        }

        if (!boss.IsDead)
        {
            boss.EndGroggyRuntime();
            boss.SetRootMotionMode(false, false);

            if (boss.TryConsumePendingPhase2Transition())
            {
                yield break;
            }

            Debug.Log("<color=green>[System] Groggy finished -> returning to combat.</color>");

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
