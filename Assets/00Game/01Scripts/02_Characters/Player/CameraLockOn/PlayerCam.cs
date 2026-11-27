using Cinemachine;
using UnityEngine;

/// <summary>
/// 플레이어 카메라 제어 스크립트.
/// - 자유 시점(FreeCam) / 락온 시점(LockOnCam) 전환
/// - 락온 조립 흐름 연결
/// - 플레이어 사망/리스폰 시 강제 언락 처리
/// </summary>
public class PlayerCam : MonoBehaviour
{
    [Header("References")]
    // 플레이어 본체 컨트롤러(FSM 상태/사망/락온 플래그 보유)
    public PlayerController player;
    // 락온 대상 탐색/검증을 담당하는 시스템
    public LockOnSystem lockOnSystem;
    // 락온 전용 가상 카메라
    public CinemachineVirtualCamera lockOnCamera;
    // 자유 시점 카메라(FreeLook 또는 VirtualCameraBase 계열)
    public CinemachineVirtualCameraBase freeLookCamera;

    [Header("Camera Targets")]
    // 자유시점/락온 카메라가 공통으로 따라갈 기준점
    [SerializeField] private Transform cameraTargetRoot;
    [SerializeField] private float cameraFollowSmoothTime = 0.18f;
    [SerializeField] private float cameraFollowSnapDistance = 2.5f;

    [Header("State-Driven Camera")]
    // 상태 기반으로 하위 카메라를 전환하는 시네머신 카메라
    [SerializeField] private CinemachineStateDrivenCamera stateDrivenCamera;
    // State-Driven Camera가 참조하는 Animator
    [SerializeField] private Animator cameraAnimator;
    // 자유 시점 상태 이름(Animator State 이름과 완전히 일치해야 함)
    [SerializeField] private string freeCamStateName = "FreeCam";
    // 락온 시점 상태 이름(Animator State 이름과 완전히 일치해야 함)
    [SerializeField] private string lockOnCamStateName = "LockOnCam";
    // Animator 레이어 인덱스(기본 Base Layer = 0)
    [SerializeField] private int animatorLayerIndex = 0;
    // 상태 전환 블렌드 시간(짧을수록 즉시, 길수록 부드럽게)
    [SerializeField] private float stateBlendDuration = 0.2f;

    [Header("Input")]
    // 키보드 락온 토글 키
    public KeyCode lockOnKey = KeyCode.Tab;
    // 마우스 버튼 인덱스(기본값: 휠 클릭 = 2)
    public int lockOnMouseButton = 2;

    [Header("LockOn Framing")]
    // LookAt 포인트 보간 가중치
    // 다크소울식 락온 구도에서 타겟 편향 강도를 조절한다.
    [Range(0f, 1f)] public float lookAtTargetWeight = 0.6f;
    // 플레이어/타겟의 시선 기준 높이(발밑이 아니라 상체 근처를 보게 보정)
    public float lookAtHeightOffset = 1.2f;
    [SerializeField] private float lockOnMinTargetHorizontalBias = 0.35f;
    [SerializeField] private float lockOnMaxTargetHorizontalBias = 1.2f;
    [SerializeField] private float lockOnDistanceForMaxBias = 7f;
    [SerializeField] private float lockOnMaxTargetVerticalBias = 0.4f;
    [SerializeField] private float lockOnLookPointSmoothTime = 0.08f;

    [Header("LockOn Indicator")]
    // 락온 표식 프리팹(월드 스페이스 UI 또는 마크 오브젝트)
    public GameObject lockOnIndicatorPrefab;
    // 타겟 기준 인디케이터 추가 오프셋
    public Vector3 lockOnIndicatorOffset = new Vector3(0f, 0.3f, 0f);
    // true면 Billboard 컴포넌트를 활성화해 카메라를 항상 바라보게 처리
    public bool indicatorFaceCamera = true;

    // 락온 해제 직전 1회 동기화로 자유시점 복귀 시점을 보정하기 위한 옵션
    [Header("FreeLook Sync")]
    // 현재 구조에서는 ForceUnlock() 시점 1회 동기화 여부로 사용한다.
    [SerializeField] private bool syncFreeLookDuringLockOn = true;
    // FreeLook 카메라 y축(0~1)로 변환할 때 사용할 피치 각도 범위
    [SerializeField] private float freeLookPitchMin = -55f;
    [SerializeField] private float freeLookPitchMax = 75f;

    private PlayerCameraStateController _cameraStateController;
    private FreeLookSyncModule _freeLookSyncModule;
    private LockOnPresentationModule _lockOnPresentationModule;
    private PlayerLockOnFlowCoordinator _lockOnFlowCoordinator;
    private PlayerCameraFollowAnchor _cameraFollowAnchor;
    private PlayerCameraFollowAnchor _lockOnCameraFollowAnchor;
    private readonly PlayerCameraReferenceResolver _cameraReferenceResolver = new PlayerCameraReferenceResolver();

    public CinemachineStateDrivenCamera StateDrivenCamera
    {
        get => stateDrivenCamera;
        set => stateDrivenCamera = value;
    }

    public Animator CameraAnimator
    {
        get => cameraAnimator;
        set => cameraAnimator = value;
    }

    public Transform CameraTargetRoot
    {
        get => cameraTargetRoot;
        set => cameraTargetRoot = value;
    }

    private void Awake()
    {
        // 필수 참조가 비어 있으면 같은 오브젝트에서 자동 탐색
        if (player == null)
        {
            player = GetComponent<PlayerController>();
        }

        if (lockOnSystem == null)
        {
            lockOnSystem = GetComponent<LockOnSystem>();
        }

        // State-Driven Camera를 하위에서 자동 탐색(비활성 포함)
        if (stateDrivenCamera == null)
        {
            stateDrivenCamera = GetComponentInChildren<CinemachineStateDrivenCamera>(true);
        }

        // Animator가 직접 연결되지 않았다면 State-Driven Camera에서 가져옴
        if (cameraAnimator == null && stateDrivenCamera != null)
        {
            cameraAnimator = stateDrivenCamera.GetComponent<Animator>();
        }

        _cameraReferenceResolver.Resolve(this);
        _cameraReferenceResolver.ReportUnresolvedReferences(this);

        if (cameraFollowSmoothTime <= 0f)
        {
            cameraFollowSmoothTime = 0.18f;
        }

        if (cameraFollowSnapDistance <= 0f)
        {
            cameraFollowSnapDistance = 2.5f;
        }

        if (lockOnMinTargetHorizontalBias < 0f)
        {
            lockOnMinTargetHorizontalBias = 0f;
        }

        if (lockOnMaxTargetHorizontalBias < lockOnMinTargetHorizontalBias)
        {
            lockOnMaxTargetHorizontalBias = lockOnMinTargetHorizontalBias;
        }

        if (lockOnDistanceForMaxBias <= 0f)
        {
            lockOnDistanceForMaxBias = 7f;
        }

        if (lockOnMaxTargetVerticalBias < 0f)
        {
            lockOnMaxTargetVerticalBias = 0f;
        }

        if (lockOnLookPointSmoothTime < 0f)
        {
            lockOnLookPointSmoothTime = 0f;
        }

        Transform cameraTargetSource = cameraTargetRoot != null ? cameraTargetRoot : transform;
        _cameraFollowAnchor = new PlayerCameraFollowAnchor(cameraTargetSource);
        _lockOnCameraFollowAnchor = new PlayerCameraFollowAnchor(
            cameraTargetSource,
            player != null ? player.transform : cameraTargetSource,
            PlayerCameraFollowAnchor.RotationMode.CopyYaw);
        _cameraFollowAnchor.ApplyToFreeLookCamera(freeLookCamera);

        _cameraStateController = new PlayerCameraStateController(cameraAnimator, animatorLayerIndex, stateBlendDuration);
        _freeLookSyncModule = new FreeLookSyncModule(freeLookCamera, freeLookPitchMin, freeLookPitchMax);
        _lockOnPresentationModule = new LockOnPresentationModule();
        _lockOnFlowCoordinator = new PlayerLockOnFlowCoordinator(
            player,
            lockOnSystem,
            lockOnCamera,
            _lockOnCameraFollowAnchor != null ? _lockOnCameraFollowAnchor.AnchorTransform : cameraTargetSource,
            cameraTargetSource,
            _cameraStateController,
            _freeLookSyncModule,
            _lockOnPresentationModule,
            freeCamStateName,
            lockOnCamStateName,
            syncFreeLookDuringLockOn,
            lookAtTargetWeight,
            lookAtHeightOffset,
            lockOnMinTargetHorizontalBias,
            lockOnMaxTargetHorizontalBias,
            lockOnDistanceForMaxBias,
            lockOnMaxTargetVerticalBias,
            lockOnLookPointSmoothTime,
            lockOnIndicatorPrefab,
            lockOnIndicatorOffset,
            indicatorFaceCamera);
    }

    private void Start()
    {
        // 시작 시 무조건 언락 상태로 정리(씬 재시작/플레이 모드 재진입 시 상태 꼬임 방지)
        ForceUnlock(true);
    }

    private void OnEnable()
    {
        // 사망/리스폰 이벤트를 구독해서 락온 잔존 상태를 방지
        GameLoopEvents.PlayerDied += HandlePlayerDied;
        GameLoopEvents.PlayerRespawned += HandlePlayerRespawned;
    }

    private void OnDisable()
    {
        // 메모리 누수/중복 호출 방지를 위해 반드시 구독 해제
        GameLoopEvents.PlayerDied -= HandlePlayerDied;
        GameLoopEvents.PlayerRespawned -= HandlePlayerRespawned;
    }

    private void Update()
    {
        if (player == null)
        {
            return;
        }

        // 플레이어 사망 상태에서는 카메라와 락온을 기본값으로 복구하고 즉시 종료
        if (player.IsDead)
        {
            ForceUnlock(true);
            return;
        }

        // 락온 토글 입력 처리
        if (Input.GetKeyDown(lockOnKey) || Input.GetMouseButtonDown(lockOnMouseButton))
        {
            ToggleLockOn();
        }

        _lockOnFlowCoordinator?.UpdateLockOnFrame();
    }

    private void ToggleLockOn()
    {
        if (player != null && !player.IsLockedOn)
        {
            _lockOnCameraFollowAnchor?.SyncImmediate();
        }

        _lockOnFlowCoordinator?.ToggleLockOn();
    }

    /// <summary>
    /// 락온 상태를 강제로 초기화.
    /// 사망/리스폰/타겟 소실 등 어떤 상황에서도 안전하게 공통 처리하기 위한 함수.
    /// </summary>
    public void ForceUnlock(bool silentLog = true)
    {
        _cameraFollowAnchor?.SyncImmediate();
        _lockOnCameraFollowAnchor?.SyncImmediate();
        _lockOnFlowCoordinator?.ForceUnlock(silentLog);
    }

    public void SyncCameraTargetsImmediate()
    {
        _cameraFollowAnchor?.SyncImmediate();
        _lockOnCameraFollowAnchor?.SyncImmediate();

        if (freeLookCamera != null)
        {
            freeLookCamera.PreviousStateIsValid = false;
        }

        if (lockOnCamera != null)
        {
            lockOnCamera.PreviousStateIsValid = false;
        }
    }

    // 플레이어 사망 시 카메라/락온 상태 초기화
    private void HandlePlayerDied(PlayerController deadPlayer)
    {
        if (player != null && deadPlayer == player)
        {
            ForceUnlock(true);
        }
    }

    // 플레이어 리스폰 시에도 락온 잔존 상태를 방지하기 위해 초기화
    private void HandlePlayerRespawned(PlayerController respawnedPlayer, Transform _)
    {
        if (player != null && respawnedPlayer == player)
        {
            _cameraFollowAnchor?.SyncImmediate();
            _lockOnCameraFollowAnchor?.SyncImmediate();
            ForceUnlock(true);
        }
    }

    private void OnDestroy()
    {
        _cameraFollowAnchor?.Dispose();
        _lockOnCameraFollowAnchor?.Dispose();
        _lockOnPresentationModule?.Dispose();
    }

    private void LateUpdate()
    {
        _cameraFollowAnchor?.Sync(Time.deltaTime, cameraFollowSmoothTime, cameraFollowSnapDistance);
        _lockOnCameraFollowAnchor?.Sync(Time.deltaTime, cameraFollowSmoothTime, cameraFollowSnapDistance);
    }

    private void OnDrawGizmosSelected()
    {
        // 씬 뷰에서 락온 탐색 반경 시각화
        if (lockOnSystem == null || player == null)
        {
            return;
        }

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(player.transform.position, lockOnSystem.ScanRadius);
    }
}
