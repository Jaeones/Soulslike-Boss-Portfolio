using UnityEngine;
using UnityEngine.UI;

public class UIBossHealthBar : MonoBehaviour
{
    [Header("UI 참조")]
    [SerializeField] private GameObject root;
    [SerializeField] private Slider hpSlider;
    [SerializeField] private Text hpText;
    [SerializeField] private Text bossNameText;
    [SerializeField] private string defaultBossName = "Orlea, the Waning Grace";
    [SerializeField] private float hpDecreaseSpeed = 7200f;
    [SerializeField] private float hpIncreaseSpeed = 1800f;

    private EnemyBase _boundEnemyBoss;
    private float _targetHp;
    private float _displayedHp;
    private bool _hasDisplayedHp;

    private void Awake()
    {
        if (root == null)
        {
            root = gameObject;
        }

        if (hpSlider == null)
        {
            hpSlider = GetComponentInChildren<Slider>(true);
        }

        if (bossNameText == null)
        {
            bossNameText = FindNamedText(root != null ? root.transform : transform, "BossNameText");
        }
    }

    private void OnEnable()
    {
        GameLoopEvents.BossFightStarted += HandleBossFightStarted;
        GameLoopEvents.BossFightEnded += HandleBossFightEnded;
        GameLoopEvents.BossHPChanged += HandleBossHpChanged;
    }

    private void OnDisable()
    {
        GameLoopEvents.BossFightStarted -= HandleBossFightStarted;
        GameLoopEvents.BossFightEnded -= HandleBossFightEnded;
        GameLoopEvents.BossHPChanged -= HandleBossHpChanged;
        UnbindBoss();
    }

    private void Update()
    {
        if (hpSlider == null || !_hasDisplayedHp)
        {
            return;
        }

        float speed = _displayedHp < _targetHp ? hpIncreaseSpeed : hpDecreaseSpeed;
        _displayedHp = Mathf.MoveTowards(_displayedHp, _targetHp, Mathf.Max(1f, speed) * Time.unscaledDeltaTime);
        hpSlider.value = Mathf.Clamp(_displayedHp, hpSlider.minValue, hpSlider.maxValue);
    }

    public void BindBoss(EnemyBase enemyBoss, string bossDisplayName = null)
    {
        UnbindBoss();

        _boundEnemyBoss = enemyBoss;
        _hasDisplayedHp = false;
        ApplyBossName(bossDisplayName);

        if (_boundEnemyBoss != null)
        {
            _boundEnemyBoss.OnHpChanged += HandleBossHpChanged;
            _boundEnemyBoss.OnDied += HandleBossDied;
            HandleBossHpChanged(_boundEnemyBoss.CurrentHp, _boundEnemyBoss.MaxHp);
            Show();
            return;
        }

        Hide();
    }

    public void UnbindBoss()
    {
        if (_boundEnemyBoss != null)
        {
            _boundEnemyBoss.OnHpChanged -= HandleBossHpChanged;
            _boundEnemyBoss.OnDied -= HandleBossDied;
            _boundEnemyBoss = null;
        }

        _targetHp = 0f;
        _displayedHp = 0f;
        _hasDisplayedHp = false;
        ApplyBossName(null);
    }

    public void SetVisible(bool visible)
    {
        if (visible)
        {
            Show();
            return;
        }

        Hide();
    }

    private void HandleBossFightStarted(EnemyBase _)
    {
        Show();
    }

    private void HandleBossFightEnded(EnemyBase _, bool __)
    {
        Hide();
    }

    private void HandleBossDied()
    {
        if (hpSlider != null)
        {
            _targetHp = 0f;
            _displayedHp = 0f;
            _hasDisplayedHp = true;
            hpSlider.value = 0f;
        }
    }

    private void HandleBossHpChanged(float current, float max)
    {
        if (hpSlider != null)
        {
            hpSlider.minValue = 0f;
            hpSlider.maxValue = Mathf.Max(1f, max);

            float clampedHp = Mathf.Clamp(current, 0f, hpSlider.maxValue);
            _targetHp = clampedHp;
            if (!_hasDisplayedHp)
            {
                _displayedHp = clampedHp;
                _hasDisplayedHp = true;
            }
            else
            {
                _displayedHp = Mathf.Clamp(_displayedHp, hpSlider.minValue, hpSlider.maxValue);
            }

            if (!Application.isPlaying)
            {
                _displayedHp = clampedHp;
            }

            hpSlider.value = Mathf.Clamp(_displayedHp, hpSlider.minValue, hpSlider.maxValue);
        }

        if (hpText != null)
        {
            hpText.text = $"{Mathf.CeilToInt(current)} / {Mathf.CeilToInt(max)}";
        }
    }

    private void Show()
    {
        if (root != null && !root.activeSelf)
        {
            root.SetActive(true);
        }
    }

    private void Hide()
    {
        if (root != null && root.activeSelf)
        {
            root.SetActive(false);
        }
    }

    private void ApplyBossName(string bossDisplayName)
    {
        if (bossNameText == null)
        {
            return;
        }

        bossNameText.text = string.IsNullOrWhiteSpace(bossDisplayName) ? defaultBossName : bossDisplayName;
    }

    private static Text FindNamedText(Transform parent, string targetName)
    {
        if (parent == null)
        {
            return null;
        }

        if (parent.name == targetName)
        {
            return parent.GetComponent<Text>();
        }

        for (int i = 0; i < parent.childCount; i++)
        {
            Text nested = FindNamedText(parent.GetChild(i), targetName);
            if (nested != null)
            {
                return nested;
            }
        }

        return null;
    }
}
