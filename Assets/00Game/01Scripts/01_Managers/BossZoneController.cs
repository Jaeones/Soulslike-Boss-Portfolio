using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class BossZoneController : MonoBehaviour
{
    [Header("참조")]
    [SerializeField] private PlayerController player;
    [SerializeField] private EnemyBase boss;
    [SerializeField] private Transform checkpoint;
    [SerializeField] private GameObject fogWall;
    [SerializeField] private GameObject bossUiRoot;
    [SerializeField] private BossHpUI bossHpUI;
    [SerializeField] private GameObject youDiedUi;

    [Header("설정")]
    [SerializeField] private float respawnDelay = 2.5f;
    [SerializeField] private bool disableFogWallWhenBossDied = true;
    [SerializeField] private bool disableBossUiWhenBossDied = true;

    private bool _isBossFightStarted;
    private bool _isRespawning;
    private bool _isPlayerBound;
    private bool _isBossBound;

    private void Reset()
    {
        Collider triggerCol = GetComponent<Collider>();
        if (triggerCol != null)
        {
            triggerCol.isTrigger = true;
        }
    }

    private void Awake()
    {
        if (bossUiRoot != null)
        {
            bossUiRoot.SetActive(false);
        }

        if (youDiedUi != null)
        {
            youDiedUi.SetActive(false);
        }

        if (fogWall != null)
        {
            fogWall.SetActive(false);
        }
    }

    private void OnDisable()
    {
        UnbindEvents();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_isBossFightStarted)
        {
            return;
        }

        PlayerController enteredPlayer = other.GetComponentInParent<PlayerController>();
        if (enteredPlayer == null)
        {
            return;
        }

        if (player == null)
        {
            player = enteredPlayer;
        }

        StartBossFight();
    }

    private void StartBossFight()
    {
        if (player == null || boss == null)
        {
            Debug.LogWarning("보스존 시작 실패: Player 또는 Boss 참조가 없습니다.");
            return;
        }

        _isBossFightStarted = true;
        BindEvents();

        if (fogWall != null)
        {
            fogWall.SetActive(true);
        }

        if (bossUiRoot != null)
        {
            bossUiRoot.SetActive(true);
        }

        if (bossHpUI != null)
        {
            bossHpUI.BindBoss(boss);
        }

        Debug.Log("보스존 진입: 안개벽 생성 및 보스 UI 활성화");
    }

    private void BindEvents()
    {
        if (!_isPlayerBound && player != null)
        {
            player.OnPlayerDied += HandlePlayerDied;
            _isPlayerBound = true;
        }

        if (!_isBossBound && boss != null)
        {
            boss.OnDied += HandleBossDied;
            _isBossBound = true;
        }
    }

    private void UnbindEvents()
    {
        if (_isPlayerBound && player != null)
        {
            player.OnPlayerDied -= HandlePlayerDied;
        }

        if (_isBossBound && boss != null)
        {
            boss.OnDied -= HandleBossDied;
        }

        _isPlayerBound = false;
        _isBossBound = false;
    }

    private void HandlePlayerDied()
    {
        if (_isRespawning)
        {
            return;
        }

        StartCoroutine(RespawnRoutine());
    }

    private void HandleBossDied()
    {
        if (disableFogWallWhenBossDied && fogWall != null)
        {
            fogWall.SetActive(false);
        }

        if (disableBossUiWhenBossDied && bossUiRoot != null)
        {
            bossUiRoot.SetActive(false);
        }

        Debug.Log("보스 처치 완료: 보스존 UI 정리");
    }

    private IEnumerator RespawnRoutine()
    {
        _isRespawning = true;

        if (youDiedUi != null)
        {
            youDiedUi.SetActive(true);
        }

        Debug.Log("YOU DIED 연출 시작");
        yield return new WaitForSeconds(respawnDelay);

        if (player != null)
        {
            Vector3 spawnPos = checkpoint != null ? checkpoint.position : player.transform.position;
            Quaternion spawnRot = checkpoint != null ? checkpoint.rotation : player.transform.rotation;
            player.RespawnFromCheckpoint(spawnPos, spawnRot);
        }

        if (youDiedUi != null)
        {
            youDiedUi.SetActive(false);
        }

        Debug.Log("체크포인트 부활 완료");
        _isRespawning = false;
    }
}

