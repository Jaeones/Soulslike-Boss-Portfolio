using System.Collections;
using UnityEngine;

public class BossIdleState : BossState
{
    private float rotationThreshold = 5f;
    private float stopRotationThreshold = 2f;

    public BossIdleState(BossController boss) : base(boss) { }

    public override IEnumerator Enter()
    {
        boss.StopAgent();
        if (boss.Agent.isOnNavMesh)
        {
            boss.Agent.velocity = Vector3.zero;
        }

        boss.SetRootMotionMode(false, false);

        if (!boss.IsCurrentAnimState("Idle"))
        {
            boss.PlayAnim("Idle", 0.2f);
        }

        yield return new WaitForSeconds(0.1f);

        while (true)
        {
            if (boss.target == null || boss.IsDead)
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

            Vector3 dir = (boss.target.position - boss.transform.position).normalized;
            dir.y = 0f;
            Quaternion targetRot = Quaternion.LookRotation(dir);
            float angleDiff = Quaternion.Angle(boss.transform.rotation, targetRot);

            if (angleDiff > rotationThreshold)
            {
                boss.PlayAnim("Walk", 0.2f);

                while (angleDiff > stopRotationThreshold)
                {
                    if (boss.IsDead || boss.target == null)
                    {
                        yield break;
                    }

                    if (boss.IsCombatResumeDelayActive)
                    {
                        break;
                    }

                    dir = (boss.target.position - boss.transform.position).normalized;
                    dir.y = 0f;
                    targetRot = Quaternion.LookRotation(dir);

                    boss.transform.rotation = Quaternion.Slerp(
                        boss.transform.rotation,
                        targetRot,
                        Time.deltaTime * boss.rotationSpeed);

                    angleDiff = Quaternion.Angle(boss.transform.rotation, targetRot);

                    float d = Vector3.Distance(boss.transform.position, boss.target.position);
                    if (d > boss.attackStoppingDistance + 0.5f)
                    {
                        if (boss.TryChangeState(boss.moveState, EnemyBase.State.Move))
                        {
                            yield break;
                        }
                    }

                    yield return null;
                }

                if (!boss.IsCurrentAnimState("Idle"))
                {
                    boss.PlayAnim("Idle", 0.2f);
                }
            }

            float dist = Vector3.Distance(boss.transform.position, boss.target.position);
            if (boss.CanStartCombatAction(dist))
            {
                if (boss.TryChangeState(boss.actionState, EnemyBase.State.Action))
                {
                    yield break;
                }
            }

            if (dist > boss.attackStoppingDistance + 0.8f)
            {
                if (boss.TryChangeState(boss.moveState, EnemyBase.State.Move))
                {
                    yield break;
                }
            }

            yield return null;
        }
    }
}
