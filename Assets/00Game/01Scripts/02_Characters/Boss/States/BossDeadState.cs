using UnityEngine;
using System.Collections;

public class BossDeadState : BossState
{
    private const string DeathAnimationName = "Die";
    private const float DeathAnimationCrossFade = 0.2f;
    private const float DeathAnimationTimeout = 10.0f;

    public BossDeadState(BossController boss) : base(boss) { }

    public override IEnumerator Enter()
    {
        boss.StopAgent();
        boss.AE_DisableHitbox();
        boss.SetRootMotionMode(boss.ShouldUseReactionRootMotion(), false);

        var col = boss.GetComponent<Collider>();
        if (col != null)
        {
            col.enabled = false;
        }

        boss.PlayAnim(DeathAnimationName, DeathAnimationCrossFade);

        Time.timeScale = 0.3f;
        yield return WaitForDeathAnimationToFinish();
        Time.timeScale = 1.0f;

        BossDeathPresentation deathPresentation = boss.GetComponent<BossDeathPresentation>();
        if (deathPresentation == null)
        {
            deathPresentation = boss.gameObject.AddComponent<BossDeathPresentation>();
        }

        if (deathPresentation != null)
        {
            yield return deathPresentation.PlaySequence();
        }
        else
        {
            yield return FadeOutBoss(1.5f);
        }

        GameObject.Destroy(boss.gameObject);
    }

    private IEnumerator WaitForDeathAnimationToFinish()
    {
        Animator animator = boss.Anim;
        if (animator == null)
        {
            yield return new WaitForSecondsRealtime(2.0f);
            yield break;
        }

        int stateHash = Animator.StringToHash(DeathAnimationName);
        if (!animator.HasState(0, stateHash))
        {
            yield return new WaitForSecondsRealtime(2.0f);
            yield break;
        }

        float elapsedRealtime = 0f;
        bool hasObservedDeathState = false;

        while (elapsedRealtime < DeathAnimationTimeout)
        {
            AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
            if (stateInfo.IsName(DeathAnimationName))
            {
                hasObservedDeathState = true;
                if (!animator.IsInTransition(0) && stateInfo.normalizedTime >= 0.98f)
                {
                    yield break;
                }
            }
            else if (hasObservedDeathState && !animator.IsInTransition(0))
            {
                yield break;
            }

            elapsedRealtime += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    private IEnumerator FadeOutBoss(float duration)
    {
        Renderer[] renderers = boss.GetComponentsInChildren<Renderer>();
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(1f, 0f, elapsed / duration);
            foreach (Renderer renderer in renderers)
            {
                foreach (Material material in renderer.materials)
                {
                    if (material.HasProperty("_Color"))
                    {
                        Color color = material.color;
                        color.a = alpha;
                        material.color = color;
                    }
                }
            }

            yield return null;
        }
    }
}
