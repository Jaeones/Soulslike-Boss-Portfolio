using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EffectManager : Singleton<EffectManager>
{
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    // 히트 스톱 (타격시 순간 정지)
    public void DoHitStop(float duration, float timeScale = 0.0f)
    {
        StopCoroutine("HitStopCoroutine");
        StartCoroutine(HitStopCoroutine(duration, timeScale));
    }

    private IEnumerator HitStopCoroutine(float duration, float timeScale)
    {
        Time.timeScale = timeScale;
        yield return new WaitForSecondsRealtime(duration);
        Time.timeScale = 1.0f;
    }

    // 카메라 흔들림 (타격 진동)
    public void DoCameraShake(float duration, float magnitude)
    {
        StopCoroutine("CameraShakeCoroutine");
        StartCoroutine(CameraShakeCoroutine(duration, magnitude));
    }

    private IEnumerator CameraShakeCoroutine(float duration, float magnitude)
    {
        Vector3 originalPos = Define.MainCam.transform.localPosition;
        float elapsed = 0.0f;

        while (elapsed < duration)
        {
            float x = Random.Range(-1f, 1f) * magnitude;
            float y = Random.Range(-1f, 1f) * magnitude;
            Define.MainCam.transform.localPosition = originalPos + new Vector3(x, y, 0);

            elapsed += Time.deltaTime;
            yield return null;
        }
        Define.MainCam.transform.localPosition = originalPos;
    }

    // 피격 플래시 (하얗게 번쩍임)
    public void DoFlashEffect(Renderer targetRenderer, Color flashColor, float duration)
    {
        StartCoroutine(FlashRoutine(targetRenderer, flashColor, duration));
    }

    private IEnumerator FlashRoutine(Renderer targetRenderer, Color flashColor, float duration)
    {
        if (targetRenderer == null)
        {
            yield break;
        }

        Material mat = targetRenderer.material;
        if (mat == null)
        {
            yield break;
        }

        int colorPropertyId = ResolveFlashColorProperty(mat);
        if (colorPropertyId < 0)
        {
            yield break;
        }

        Color originalColor = mat.GetColor(colorPropertyId);
        mat.SetColor(colorPropertyId, flashColor);

        yield return new WaitForSeconds(duration);

        if (mat != null && mat.HasProperty(colorPropertyId))
        {
            mat.SetColor(colorPropertyId, originalColor);
        }
    }

    private static int ResolveFlashColorProperty(Material material)
    {
        if (material == null)
        {
            return -1;
        }

        if (material.HasProperty(EmissionColorId))
        {
            return EmissionColorId;
        }

        if (material.HasProperty(BaseColorId))
        {
            return BaseColorId;
        }

        if (material.HasProperty(ColorId))
        {
            return ColorId;
        }

        return -1;
    }
}