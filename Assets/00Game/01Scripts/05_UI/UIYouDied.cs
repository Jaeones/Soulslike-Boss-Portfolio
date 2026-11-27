using System.Collections;
using UnityEngine;

public class UIYouDied : MonoBehaviour
{
    [Header("연출 설정")]
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private float fadeInDuration = 0.2f;
    [SerializeField] private float fadeOutDuration = 0.2f;

    private void Awake()
    {
        EnsureCanvasGroup();
        ValidateSceneReferences(true);
    }

    [ContextMenu("Validate Scene References")]
    private void ValidateSceneReferencesContext()
    {
        ValidateSceneReferences(false);
    }

    public void ShowImmediate()
    {
        EnsureCanvasGroup();
        gameObject.SetActive(true);
        canvasGroup.alpha = 1f;
    }

    public void HideImmediate()
    {
        EnsureCanvasGroup();
        PrepareHiddenState();
    }

    public IEnumerator PlayShow()
    {
        EnsureCanvasGroup();
        gameObject.SetActive(true);
        GameUiSfxPlayer.Play(UiSfxId.YouDiedStinger);
        yield return Fade(0f, 1f, fadeInDuration);
    }

    public IEnumerator PlayHide()
    {
        EnsureCanvasGroup();
        yield return Fade(canvasGroup.alpha, 0f, fadeOutDuration);
        gameObject.SetActive(false);
    }

    private IEnumerator Fade(float from, float to, float duration)
    {
        EnsureCanvasGroup();

        if (duration <= 0f)
        {
            canvasGroup.alpha = to;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            canvasGroup.alpha = Mathf.Lerp(from, to, t);
            yield return null;
        }

        canvasGroup.alpha = to;
    }

    private void PrepareHiddenState()
    {
        canvasGroup.alpha = 0f;
        gameObject.SetActive(false);
    }

    private void EnsureCanvasGroup()
    {
        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }

        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
    }

    private void ValidateSceneReferences(bool logOnlyOnce)
    {
        SceneReferenceValidator.Entry[] entries =
        {
            SceneReferenceValidator.Recommended(nameof(canvasGroup), canvasGroup, "미연결이어도 자동 보정되지만 명시 연결 권장")
        };

        if (logOnlyOnce)
        {
            SceneReferenceValidator.ValidateOnce(this, nameof(UIYouDied), entries);
            return;
        }

        SceneReferenceValidator.Validate(this, nameof(UIYouDied), entries);
    }
}
