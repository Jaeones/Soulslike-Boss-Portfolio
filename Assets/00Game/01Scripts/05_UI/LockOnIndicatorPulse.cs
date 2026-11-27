using UnityEngine;
using UnityEngine.UI;

public sealed class LockOnIndicatorPulse : MonoBehaviour
{
    [SerializeField] private RectTransform baseRing;
    [SerializeField] private Image baseImage;
    [SerializeField] private RectTransform pulseRing;
    [SerializeField] private Image pulseImage;
    [SerializeField] private float pulseDuration = 1.05f;
    [SerializeField] private float baseScaleMin = 0.98f;
    [SerializeField] private float baseScaleMax = 1.02f;
    [SerializeField] private float baseAlphaMin = 0.78f;
    [SerializeField] private float baseAlphaMax = 0.94f;
    [SerializeField] private float pulseScaleMin = 0.96f;
    [SerializeField] private float pulseScaleMax = 1.14f;
    [SerializeField] private float pulseAlphaMin = 0.08f;
    [SerializeField] private float pulseAlphaMax = 0.34f;
    [SerializeField] private float popScale = 1.14f;
    [SerializeField] private float popDuration = 0.16f;

    private float _time;

    private void OnEnable()
    {
        _time = 0f;
        ApplyVisuals(0f);
    }

    private void Update()
    {
        _time += Time.unscaledDeltaTime;
        ApplyVisuals(_time);
    }

    private void ApplyVisuals(float elapsed)
    {
        float cycle = Mathf.Max(0.01f, pulseDuration);
        float wave = 0.5f + 0.5f * Mathf.Sin(elapsed * Mathf.PI * 2f / cycle);
        float pulseWave = 0.5f + 0.5f * Mathf.Sin(elapsed * Mathf.PI * 2f / cycle - Mathf.PI * 0.35f);

        float popBlend = 0f;
        if (popDuration > 0f && elapsed < popDuration)
        {
            float popT = Mathf.Clamp01(elapsed / popDuration);
            popBlend = 1f - Mathf.SmoothStep(0f, 1f, popT);
        }

        if (baseRing != null)
        {
            float baseScale = Mathf.Lerp(baseScaleMin, baseScaleMax, wave) + (popScale - 1f) * popBlend;
            baseRing.localScale = Vector3.one * baseScale;
        }

        if (pulseRing != null)
        {
            float pulseScale = Mathf.Lerp(pulseScaleMin, pulseScaleMax, pulseWave) + (popScale - 1f) * popBlend;
            pulseRing.localScale = Vector3.one * pulseScale;
        }

        SetAlpha(baseImage, Mathf.Lerp(baseAlphaMin, baseAlphaMax, wave));
        SetAlpha(pulseImage, Mathf.Lerp(pulseAlphaMin, pulseAlphaMax, pulseWave));
    }

    private static void SetAlpha(Graphic graphic, float alpha)
    {
        if (graphic == null)
        {
            return;
        }

        Color color = graphic.color;
        color.a = alpha;
        graphic.color = color;
    }
}
