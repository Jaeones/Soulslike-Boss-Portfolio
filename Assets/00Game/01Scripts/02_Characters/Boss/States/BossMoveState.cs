using System.Collections;
using UnityEngine;

public class BossMoveState : BossState
{
    public BossMoveState(BossController boss) : base(boss) { }

    public override IEnumerator Enter()
    {
        boss.ResumeAgent();
        boss.Agent.speed = boss.moveSpeed;
        boss.SetRootMotionMode(boss.ShouldUseMoveRootMotion(), false);
        boss.Agent.stoppingDistance = boss.attackStoppingDistance;
        boss.PlayAnim("Walk", 0.1f);

        while (true)
        {
            if (boss.target == null)
            {
                yield break;
            }

            if (boss.IsExternalEntranceControlActive)
            {
                boss.StopAgent();
                yield return null;
                continue;
            }

            if (boss.IsCombatResumeDelayActive)
            {
                boss.StopAgent();
                if (boss.Agent.isOnNavMesh)
                {
                    boss.Agent.velocity = Vector3.zero;
                }

                if (!boss.IsCurrentAnimState("Idle"))
                {
                    boss.PlayAnim("Idle", 0.1f);
                }

                yield return null;
                continue;
            }

            boss.Agent.SetDestination(boss.target.position);
            Vector3 steeringDirection = boss.Agent.desiredVelocity.sqrMagnitude > 0.0001f
                ? boss.Agent.desiredVelocity
                : boss.target.position - boss.transform.position;
            boss.RotateTowardsDirection(steeringDirection, boss.RootMotionSteeringTurnMultiplier);

            float dist = Vector3.Distance(boss.transform.position, boss.target.position);

            if (boss.CanStartCombatAction(dist))
            {
                boss.StopAgent();

                if (boss.Agent.isOnNavMesh)
                {
                    boss.Agent.velocity = Vector3.zero;
                }

                if (boss.TryChangeState(boss.actionState, EnemyBase.State.Action))
                {
                    yield break;
                }
            }

            if (dist <= boss.attackStoppingDistance + 0.2f)
            {
                boss.StopAgent();

                if (boss.Agent.isOnNavMesh)
                {
                    boss.Agent.velocity = Vector3.zero;
                }

                if (boss.TryChangeState(boss.idleState, EnemyBase.State.Idle))
                {
                    yield break;
                }
            }

            yield return null;
        }
    }
}
