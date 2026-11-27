using UnityEngine;
using UnityEngine.UI;

public class BossHpUI : MonoBehaviour
{
    [Header("UI 참조")]
    [SerializeField] private Slider hpSlider;

    [Header("옵션")]
    [SerializeField] private bool hideWhenUnbound = true;

    private EnemyBase _boundBoss;

    private void Awake()
    {
        if (hpSlider == null)
        {
            hpSlider = GetComponentInChildren<Slider>();
        }
    }

    private void OnDisable()
    {
        UnbindBoss();
    }

    public void BindBoss(EnemyBase boss)
    {
        UnbindBoss();

        _boundBoss = boss;
        if (_boundBoss == null)
        {
            if (hideWhenUnbound)
            {
                gameObject.SetActive(false);
            }
            return;
        }

        _boundBoss.OnHpChanged += HandleBossHpChanged;
        _boundBoss.OnDied += HandleBossDied;

        if (hideWhenUnbound && !gameObject.activeSelf)
        {
            gameObject.SetActive(true);
        }

        HandleBossHpChanged(_boundBoss.CurrentHp, _boundBoss.MaxHp);
        Debug.Log("보스 HP UI 바인딩 완료");
    }

    public void UnbindBoss()
    {
        if (_boundBoss != null)
        {
            _boundBoss.OnHpChanged -= HandleBossHpChanged;
            _boundBoss.OnDied -= HandleBossDied;
            _boundBoss = null;
        }

        if (hideWhenUnbound)
        {
            gameObject.SetActive(false);
        }
    }

    private void HandleBossHpChanged(float currentHp, float maxHp)
    {
        if (hpSlider == null)
        {
            return;
        }

        hpSlider.minValue = 0f;
        hpSlider.maxValue = Mathf.Max(1f, maxHp);
        hpSlider.value = Mathf.Clamp(currentHp, 0f, hpSlider.maxValue);
    }

    private void HandleBossDied()
    {
        if (hpSlider != null)
        {
            hpSlider.value = 0f;
        }
    }
}

