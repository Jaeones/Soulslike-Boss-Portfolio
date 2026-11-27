using UnityEngine;

public class PlayerEstusFlask : MonoBehaviour
{
    [Header("Estus Settings")]
    [SerializeField] private EstusFlaskData flaskData;
    [SerializeField] private bool enableDebugLog = true;

    [Header("Held Visual")]
    [SerializeField] private GameObject heldVisualObject;
    [SerializeField] private string heldVisualObjectName = string.Empty;

    [Header("Weapon Visual")]
    [SerializeField] private GameObject weaponVisualObject;
    [SerializeField] private string weaponVisualObjectName = "PlayerWeapon";

    private int _currentUses;
    private bool _isUsing;
    private float _lastUseFinishedTime = -999f;

    public bool HasData => flaskData != null;
    public int CurrentUses => _currentUses;
    public int MaxUses => flaskData != null ? Mathf.Max(0, flaskData.maxUses) : 0;
    public float DrinkDuration => flaskData != null ? Mathf.Max(0.05f, flaskData.drinkDuration) : 0.8f;
    public float HealApplyTime => flaskData != null ? Mathf.Clamp(flaskData.healApplyTime, 0f, DrinkDuration) : 0.4f;
    public bool IsUsing => _isUsing;

    public void Initialize()
    {
        if (flaskData == null)
        {
            _currentUses = 0;
            HideHeldVisual();
            PublishEstusChanged();
            return;
        }

        if (_currentUses <= 0)
        {
            _currentUses = flaskData.maxUses;
        }
        else
        {
            _currentUses = Mathf.Clamp(_currentUses, 0, flaskData.maxUses);
        }

        ResolveHeldVisualObject();
        ResolveWeaponVisualObject();
        HideHeldVisual();
        PublishEstusChanged();
    }

    public bool CanUse(PlayerController player, IState currentState)
    {
        if (flaskData == null)
        {
            Log("Estus data is missing.");
            return false;
        }

        if (player == null || player.IsDead)
        {
            return false;
        }

        if (_isUsing)
        {
            return false;
        }

        if (_currentUses <= 0)
        {
            Log("No estus uses left.");
            return false;
        }

        if (Time.time - _lastUseFinishedTime < flaskData.cooldown)
        {
            return false;
        }

        if (currentState == player.RollState || currentState == player.AttackState)
        {
            Log("Cannot use estus in the current state.");
            return false;
        }

        if (Mathf.Approximately(player.CurrentHp, player.MaxHp))
        {
            Log("HP is already full.");
            return false;
        }

        return true;
    }

    public bool TryBeginUse(PlayerController player, IState currentState)
    {
        if (!CanUse(player, currentState))
        {
            return false;
        }

        _isUsing = true;
        return true;
    }

    public bool ApplyHeal(PlayerController player)
    {
        if (!_isUsing || player == null || flaskData == null)
        {
            return false;
        }

        if (_currentUses <= 0)
        {
            return false;
        }

        float healValue = flaskData.usePercentHeal
            ? player.MaxHp * Mathf.Clamp01(flaskData.healPercent)
            : flaskData.healAmount;

        float beforeHp = player.CurrentHp;
        player.Heal(healValue);
        _currentUses = Mathf.Max(0, _currentUses - 1);
        PublishEstusChanged();

        Log($"Estus heal applied: {beforeHp} -> {player.CurrentHp} (remaining: {_currentUses})");
        return true;
    }

    public void EndUse()
    {
        if (!_isUsing)
        {
            return;
        }

        _isUsing = false;
        _lastUseFinishedTime = Time.time;
    }

    public void RefillAll()
    {
        if (flaskData == null)
        {
            _currentUses = 0;
            HideHeldVisual();
            PublishEstusChanged();
            return;
        }

        _currentUses = flaskData.maxUses;
        _isUsing = false;
        HideHeldVisual();
        PublishEstusChanged();
    }

    public void ShowHeldVisual()
    {
        ResolveHeldVisualObject();
        ResolveWeaponVisualObject();
        SetHeldVisualActive(true);
        SetWeaponVisualActive(false);
    }

    public void HideHeldVisual()
    {
        SetHeldVisualActive(false);
        SetWeaponVisualActive(true);
    }

    private void PublishEstusChanged()
    {
        GameLoopEvents.RaiseEstusChanged(_currentUses, MaxUses);
    }

    // Drink 연출용 오브젝트는 Player 프리팹 자식으로 두고 참조만 연결한다.
    private void ResolveHeldVisualObject()
    {
        if (heldVisualObject != null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(heldVisualObjectName))
        {
            return;
        }

        Transform heldVisualTransform = FindChildRecursive(transform, heldVisualObjectName);
        if (heldVisualTransform == null)
        {
            Log($"Estus visual object not found: {heldVisualObjectName}");
            return;
        }

        heldVisualObject = heldVisualTransform.gameObject;
    }

    private void SetHeldVisualActive(bool active)
    {
        if (heldVisualObject == null)
        {
            ResolveHeldVisualObject();
        }

        if (heldVisualObject == null || heldVisualObject.activeSelf == active)
        {
            return;
        }

        heldVisualObject.SetActive(active);
    }

    private void ResolveWeaponVisualObject()
    {
        if (weaponVisualObject != null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(weaponVisualObjectName))
        {
            return;
        }

        Transform weaponVisualTransform = FindChildRecursive(transform, weaponVisualObjectName);
        if (weaponVisualTransform == null)
        {
            Log($"Weapon visual object not found: {weaponVisualObjectName}");
            return;
        }

        weaponVisualObject = weaponVisualTransform.gameObject;
    }

    private void SetWeaponVisualActive(bool active)
    {
        if (weaponVisualObject == null)
        {
            ResolveWeaponVisualObject();
        }

        if (weaponVisualObject == null || weaponVisualObject.activeSelf == active)
        {
            return;
        }

        weaponVisualObject.SetActive(active);
    }

    private Transform FindChildRecursive(Transform parent, string childName)
    {
        if (parent == null || string.IsNullOrWhiteSpace(childName))
        {
            return null;
        }

        if (parent.name == childName)
        {
            return parent;
        }

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform found = FindChildRecursive(parent.GetChild(i), childName);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private void Log(string message)
    {
        if (!enableDebugLog)
        {
            return;
        }

        Debug.Log(message);
    }
}
