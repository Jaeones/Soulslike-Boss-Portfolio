using System.Collections;
using UnityEngine;

public abstract class BossState
{
    protected BossController boss;
    public BossState(BossController boss) { this.boss = boss; }
    public abstract IEnumerator Enter();
}

public class BossHitReactState : BossState
{
    public BossHitReactState(BossController boss) : base(boss) { }

    public override IEnumerator Enter()
    {
        boss.BeginHitReactionRuntime();
        boss.StopAgent();
        boss.SetRootMotionMode(boss.ShouldUseReactionRootMotion(), false);
        boss.PlayAnim(boss.CurrentHitReactionAnimationName, 0.05f);

        float elapsed = 0f;
        float duration = Mathf.Max(0.05f, boss.CurrentHitReactionDuration);

        while (elapsed < duration)
        {
            if (boss.IsDead || boss.isPhaseTransitioning || boss.currentState != EnemyBase.State.HitReact)
            {
                yield break;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        boss.EndHitReactionRuntime();
        boss.SetRootMotionMode(false, false);

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
