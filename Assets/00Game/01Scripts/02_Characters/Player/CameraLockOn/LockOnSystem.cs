using UnityEngine;

public class LockOnSystem : MonoBehaviour
{
    [Header("참조")]
    [SerializeField] private PlayerController player;

    [Header("락온 설정")]
    [SerializeField] private float scanRadius = 12f;
    [SerializeField] private float maxLockOnAngle = 70f;
    [SerializeField] private LayerMask enemyLayer;
    [SerializeField] private LayerMask bossLayer;
    [SerializeField] private string enemyTag = "Enemy";
    [SerializeField] private string bossTag = "Boss";

    public Transform CurrentTarget { get; private set; }
    public bool HasTarget => CurrentTarget != null;

    private readonly LockOnTargetSelector _targetSelector = new LockOnTargetSelector();
    private readonly LockOnTargetValidator _targetValidator = new LockOnTargetValidator();

    private void Awake()
    {
        if (player == null)
        {
            player = GetComponent<PlayerController>();
        }
    }

    public bool TryLockOn()
    {
        Transform target = _targetSelector.SelectBestTarget(
            player,
            scanRadius,
            maxLockOnAngle,
            ResolveTargetLayers(),
            enemyTag,
            bossTag);
        if (target == null)
        {
            return false;
        }

        SetTarget(target);
        return true;
    }

    public void ClearLockOn()
    {
        CurrentTarget = null;
        if (player != null)
        {
            player.ClearLockOnTarget();
        }
    }

    public bool ValidateCurrentTarget()
    {
        if (!HasTarget || player == null)
        {
            return false;
        }

        if (!_targetValidator.IsTargetValid(player, CurrentTarget, scanRadius))
        {
            ClearLockOn();
            return false;
        }

        return true;
    }

    public float ScanRadius => scanRadius;

    private LayerMask ResolveTargetLayers()
    {
        int combinedMask = enemyLayer.value | bossLayer.value;

        int enemyLayerIndex = LayerMask.NameToLayer("Enemy");
        if (enemyLayerIndex >= 0)
        {
            combinedMask |= 1 << enemyLayerIndex;
        }

        int bossLayerIndex = LayerMask.NameToLayer("Boss");
        if (bossLayerIndex >= 0)
        {
            combinedMask |= 1 << bossLayerIndex;
        }

        return combinedMask;
    }

    private void SetTarget(Transform target)
    {
        CurrentTarget = target;
        if (player != null)
        {
            player.SetLockOnTarget(CurrentTarget);
        }
    }
}
