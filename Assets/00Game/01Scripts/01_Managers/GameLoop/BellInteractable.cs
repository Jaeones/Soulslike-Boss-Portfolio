using System.Collections;
using Cinemachine;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public sealed class BellInteractable : MonoBehaviour, IPlayerInteractable
{
    private const string PreferredFogWallObjectName = "FX_Environment_FogWall_Entrance_Loop";
    private const string LockDoorObjectName = "LockDoor";
    private const string DefaultDoorObjectName = "Door";
    private const string BellWaveAnchorObjectName = "BellWaveAnchor";

    [Header("Interaction")]
    [SerializeField] private KeyCode interactionKey = KeyCode.E;
    [SerializeField] private int interactionPriority = 15;
    [SerializeField] private string installPepperLabel = "종에 추를 단다";
    [SerializeField] private string ringBellLabel = "종을 울린다";

    [Header("Bell State")]
    [SerializeField] private GameObject bellPepperObject;
    [SerializeField] private bool markGoalKnownOnInstall = true;

    [Header("Player Interaction Animation")]
    [SerializeField] [Min(0.05f)] private float installStateEnterTimeout = 0.2f;
    [SerializeField] [Min(0.05f)] private float installAnimationTimeout = 1.2f;

    [Header("Quest Gate")]
    [SerializeField] private GameObject lockDoorObject;

    [Header("Audio")]
    [SerializeField] private AudioSource bellAudioSource;
    [SerializeField] private AudioClip bellRingClip;
    [SerializeField] [Range(0f, 1f)] private float bellRingVolume = 1f;
    [SerializeField] private Animator bellAnimator;
    [SerializeField] private string bellRingStateName = "BellRing";
    [SerializeField] [Min(0f)] private float bellRingFallbackDuration = 1.25f;

    [Header("Bell Wave VFX")]
    [SerializeField] private GameObject bellWavePrimaryPrefab;
    [SerializeField] private GameObject bellWaveSecondaryPrefab;
    [SerializeField] private Transform bellWaveAnchor;
    [SerializeField] [Min(0f)] private float bellWaveInitialDelay = 0.5f;
    [SerializeField] [Range(1, 3)] private int bellWaveRepeatCount = 3;
    [SerializeField] [Min(0.05f)] private float bellWaveRepeatInterval = 0.22f;

    [Header("Door")]
    [SerializeField] private Animator doorAnimator;
    [SerializeField] private AudioSource doorAudioSource;
    [SerializeField] private AudioClip doorOpenClip;
    [SerializeField] [Range(0f, 1f)] private float doorOpenVolume = 1f;
    [SerializeField] private string doorObjectName = DefaultDoorObjectName;
    [SerializeField] private string doorOpenStateName = "DoorOpen";
    [SerializeField] [Min(0f)] private float doorOpenDelay = 2f;

    [Header("Cinematic")]
    [SerializeField] private bool playBellDoorCinematic = true;
    [SerializeField] private Transform bellShotAnchor;
    [SerializeField] private Transform bellShotLookAt;
    [SerializeField] private Transform doorShotAnchor;
    [SerializeField] private Transform doorShotLookAt;
    [SerializeField] [Min(0f)] private float cinematicBlendTime = 0.35f;
    [SerializeField] [Min(0f)] private float bellShotDuration = 0.75f;
    [SerializeField] [Min(0f)] private float doorShotLeadTime = 0.2f;
    [SerializeField] [Min(0f)] private float doorShotHoldDuration = 1.1f;
    [SerializeField] [Range(20f, 80f)] private float cinematicFieldOfView = 42f;
    [SerializeField] [Min(0f)] private float bellShotDistance = 1.9f;
    [SerializeField] [Min(0f)] private float bellShotHeight = 1.2f;
    [SerializeField] private float bellShotSideBias = 0.45f;
    [SerializeField] [Min(0f)] private float doorShotDistance = 4.2f;
    [SerializeField] [Min(0f)] private float doorShotHeight = 1.8f;
    [SerializeField] private float doorShotSideBias = 0.3f;
    [SerializeField] private bool lockPlayerDuringSequence = true;

    [Header("Feedback")]
    [SerializeField] private string installNotice = "종에 추를 걸었다.";
    [SerializeField] private string ringNotice = "무거운 종소리가 성문 앞에 울려 퍼진다.";

    private PlayerController _interactingPlayer;
    private PlayerController _installSequencePlayer;
    private QuestProgressionManager _subscribedProgression;
    private Coroutine _installSequence;
    private Coroutine _bellSequence;
    private Coroutine _bellWaveRoutine;
    private CinemachineVirtualCamera _sequenceCamera;
    private CinemachineBrain _sequenceBrain;
    private CinemachineBlendDefinition _cachedBlend;
    private bool _hasCachedBlend;
    private bool _suppressDoorVisualStateSync;

    public bool RequiresManualInteraction => true;
    public Object PromptOwner => this;
    public Transform InteractionTransform => transform;

    private void Reset()
    {
        Collider triggerCollider = GetComponent<Collider>();
        if (triggerCollider != null)
        {
            triggerCollider.isTrigger = true;
        }

        if (bellPepperObject == null)
        {
            Transform bellPepperTransform = transform.Find("Bell_Pepper");
            if (bellPepperTransform != null)
            {
                bellPepperObject = bellPepperTransform.gameObject;
            }
        }

        if (bellAudioSource == null)
        {
            bellAudioSource = GetComponent<AudioSource>();
        }

        if (bellWaveAnchor == null)
        {
            bellWaveAnchor = FindNamedTransform(BellWaveAnchorObjectName, transform);
        }
    }

    private void OnEnable()
    {
        PlayerInteractableRegistry.Register(this);
        SubscribeProgressionState();

        ResolveBellAudioSource();
        ResolveBellAnimator();
        ResolveBellWaveReferences();
        ResolveDoorAnimator();
        ResolveDoorAudioSource();
    }

    private void Start()
    {
        ApplyBellState(QuestProgressionManager.Instance);
    }

    private void OnTriggerEnter(Collider other)
    {
        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player == null)
        {
            return;
        }

        _interactingPlayer = player;
        TryRegisterCurrentPlayer();
    }

    private void OnTriggerExit(Collider other)
    {
        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player == null || player != _interactingPlayer)
        {
            return;
        }

        player.Interaction?.UnregisterCandidate(this);
        _interactingPlayer = null;
    }

    private void OnDisable()
    {
        PlayerInteractableRegistry.Unregister(this);

        if (_interactingPlayer != null)
        {
            _interactingPlayer.Interaction?.UnregisterCandidate(this);
            _interactingPlayer = null;
        }

        if (_bellSequence != null)
        {
            StopCoroutine(_bellSequence);
            _bellSequence = null;
        }

        CancelInstallSequence();

        if (_bellWaveRoutine != null)
        {
            StopCoroutine(_bellWaveRoutine);
            _bellWaveRoutine = null;
        }

        DeactivateSequenceCamera();

        UnsubscribeProgressionState();
    }

    private void OnDestroy()
    {
        UnsubscribeProgressionState();
    }

    public bool CanInteract(PlayerController player)
    {
        if (!isActiveAndEnabled || player == null || player.IsDead || SimpleDialogueManager.IsOpen)
        {
            return false;
        }

        if (interactionKey != KeyCode.None && interactionKey != player.InteractionKey)
        {
            return false;
        }

        QuestProgressionManager progression = QuestProgressionManager.Instance;
        if (progression == null || progression.SealAccepted || progression.BossCleared)
        {
            return false;
        }

        return progression.InjuredKnightQuestItemCollected;
    }

    public int GetInteractionPriority(PlayerController player)
    {
        return interactionPriority;
    }

    public string GetInteractionLabel(PlayerController player)
    {
        QuestProgressionManager progression = QuestProgressionManager.Instance;
        bool isInstalled = progression != null && progression.BellPepperInstalled;
        string label = isInstalled ? ringBellLabel : installPepperLabel;
        return string.IsNullOrWhiteSpace(label) ? "상호작용" : label;
    }

    public void Interact(PlayerController player)
    {
        if (!CanInteract(player) || _installSequence != null)
        {
            return;
        }

        _interactingPlayer = player;

        QuestProgressionManager progression = QuestProgressionManager.Instance;
        if (progression == null)
        {
            return;
        }

        if (!progression.BellPepperInstalled)
        {
            _installSequence = StartCoroutine(Co_InstallBellPepper(player));
            return;
        }

        _suppressDoorVisualStateSync = true;
        if (!progression.AcceptSeal())
        {
            _suppressDoorVisualStateSync = false;
            return;
        }

        if (_bellSequence != null)
        {
            StopCoroutine(_bellSequence);
        }

        _bellSequence = StartCoroutine(Co_PlayBellAndOpenDoor());

        if (!string.IsNullOrWhiteSpace(ringNotice))
        {
            UIInteractionPrompt.ShowNotice(ringNotice);
        }
    }

    public bool DebugTriggerBellSequence(PlayerController player, bool prepareQuestState = true)
    {
        _interactingPlayer = player;

        if (prepareQuestState)
        {
            QuestProgressionManager progression = QuestProgressionManager.Instance;
            if (progression == null)
            {
                return false;
            }

            if (!progression.InjuredKnightQuestItemCollected)
            {
                progression.MarkInjuredKnightQuestItemCollected(markGoalKnown: true);
            }

            if (!progression.BellPepperInstalled)
            {
                progression.InstallBellPepper(markGoalKnownOnInstall);
            }

            if (!progression.SealAccepted && !progression.BossCleared)
            {
                _suppressDoorVisualStateSync = true;
                progression.AcceptSeal();
            }
        }

        ApplyBellState(QuestProgressionManager.Instance);

        if (_bellSequence != null)
        {
            StopCoroutine(_bellSequence);
        }

        _bellSequence = StartCoroutine(Co_PlayBellAndOpenDoor());

        if (!string.IsNullOrWhiteSpace(ringNotice))
        {
            UIInteractionPrompt.ShowNotice(ringNotice);
        }

        return true;
    }

    private IEnumerator Co_PlayBellAndOpenDoor()
    {
        PlayerController sequencePlayer = _interactingPlayer;
        if (lockPlayerDuringSequence && sequencePlayer != null)
        {
            sequencePlayer.BeginExternalInteractionLock();
        }

        UIInteractionPrompt.Hide(this);

        try
        {
            PlayBellRingSfx();
            PlayBellRingAnimation();
            StartBellWaveSequence();

            float bellDuration = GetCurrentAnimationDuration(bellAnimator, bellRingFallbackDuration);
            float waitDuration = Mathf.Max(doorOpenDelay, bellDuration);
            float bellFocusDuration = Mathf.Clamp(bellShotDuration, 0f, waitDuration);
            float remainingBeforeDoor = Mathf.Max(0f, waitDuration - bellFocusDuration);

            if (playBellDoorCinematic)
            {
                ActivateBellShot(sequencePlayer);
            }

            if (bellFocusDuration > 0f)
            {
                yield return new WaitForSeconds(bellFocusDuration);
            }

            if (playBellDoorCinematic && remainingBeforeDoor > 0f)
            {
                float delayBeforeDoorShot = Mathf.Max(0f, remainingBeforeDoor - doorShotLeadTime);
                if (delayBeforeDoorShot > 0f)
                {
                    yield return new WaitForSeconds(delayBeforeDoorShot);
                }

                ActivateDoorShot(sequencePlayer);

                float actualLead = Mathf.Min(doorShotLeadTime, remainingBeforeDoor);
                if (actualLead > 0f)
                {
                    yield return new WaitForSeconds(actualLead);
                }
            }
            else if (remainingBeforeDoor > 0f)
            {
                yield return new WaitForSeconds(remainingBeforeDoor);
            }

            PlayDoorOpenAnimation();

            if (playBellDoorCinematic && doorShotHoldDuration > 0f)
            {
                yield return new WaitForSeconds(doorShotHoldDuration);
            }
        }
        finally
        {
            _suppressDoorVisualStateSync = false;
            ApplyDoorVisualState(QuestProgressionManager.Instance);

            if (lockPlayerDuringSequence && sequencePlayer != null)
            {
                sequencePlayer.EndExternalInteractionLock();
            }

            DeactivateSequenceCamera();
            TryRegisterCurrentPlayer();
            _bellSequence = null;
        }
    }

    public void PlayBellRingSfx()
    {
        if (bellRingClip == null)
        {
            return;
        }

        ResolveBellAudioSource();
        if (bellAudioSource == null)
        {
            return;
        }

        bellAudioSource.pitch = 1f;
        bellAudioSource.PlayOneShot(bellRingClip, bellRingVolume);
    }

    public void PlayBellRingAnimation()
    {
        ResolveBellAnimator();
        if (bellAnimator == null || string.IsNullOrWhiteSpace(bellRingStateName))
        {
            return;
        }

        int stateHash = Animator.StringToHash(bellRingStateName);
        int fullPathHash = Animator.StringToHash($"Base Layer.{bellRingStateName}");

        if (bellAnimator.HasState(0, fullPathHash))
        {
            bellAnimator.Play(fullPathHash, 0, 0f);
            bellAnimator.Update(0f);
            return;
        }

        if (bellAnimator.HasState(0, stateHash))
        {
            bellAnimator.Play(stateHash, 0, 0f);
            bellAnimator.Update(0f);
            return;
        }

        bellAnimator.Rebind();
        bellAnimator.Update(0f);
    }

    public void PlayDoorOpenAnimation()
    {
        ResolveDoorAnimator();
        if (doorAnimator == null || string.IsNullOrWhiteSpace(doorOpenStateName))
        {
            return;
        }

        int stateHash = Animator.StringToHash(doorOpenStateName);
        int fullPathHash = Animator.StringToHash($"Base Layer.{doorOpenStateName}");

        if (doorAnimator.HasState(0, fullPathHash))
        {
            doorAnimator.Play(fullPathHash, 0, 0f);
            doorAnimator.Update(0f);
            PlayDoorOpenSfx();
            return;
        }

        if (doorAnimator.HasState(0, stateHash))
        {
            doorAnimator.Play(stateHash, 0, 0f);
            doorAnimator.Update(0f);
            PlayDoorOpenSfx();
        }
    }

    private void ApplyDoorVisualState(QuestProgressionManager progression)
    {
        bool shouldShowOpenedDoor = progression != null && (progression.SealAccepted || progression.BossCleared);
        ApplyDoorVisualState(shouldShowOpenedDoor);
    }

    private void ApplyDoorVisualState(bool opened)
    {
        ResolveDoorAnimator();
        if (doorAnimator == null)
        {
            return;
        }

        if (!opened)
        {
            doorAnimator.Rebind();
            doorAnimator.Update(0f);
            return;
        }

        if (string.IsNullOrWhiteSpace(doorOpenStateName))
        {
            return;
        }

        int stateHash = Animator.StringToHash(doorOpenStateName);
        int fullPathHash = Animator.StringToHash($"Base Layer.{doorOpenStateName}");

        if (doorAnimator.HasState(0, fullPathHash))
        {
            doorAnimator.Play(fullPathHash, 0, 1f);
            doorAnimator.Update(0f);
            return;
        }

        if (doorAnimator.HasState(0, stateHash))
        {
            doorAnimator.Play(stateHash, 0, 1f);
            doorAnimator.Update(0f);
        }
    }

    private IEnumerator Co_InstallBellPepper(PlayerController player)
    {
        if (player == null)
        {
            _installSequence = null;
            yield break;
        }

        _installSequencePlayer = player;
        _installSequencePlayer.BeginExternalInteractionLock();
        UIInteractionPrompt.Hide(this);

        try
        {
            if (PlayerInteractionAnimationSequence.TryStart(player))
            {
                yield return PlayerInteractionAnimationSequence.WaitForCompletion(
                    player,
                    installStateEnterTimeout,
                    installAnimationTimeout);
            }

            QuestProgressionManager progression = QuestProgressionManager.Instance;
            if (progression == null || !progression.InstallBellPepper(markGoalKnownOnInstall))
            {
                yield break;
            }

            if (!string.IsNullOrWhiteSpace(installNotice))
            {
                UIInteractionPrompt.ShowNotice(installNotice);
            }
        }
        finally
        {
            if (_installSequencePlayer != null)
            {
                _installSequencePlayer.EndExternalInteractionLock();
            }

            _installSequencePlayer = null;
            _installSequence = null;
            TryRegisterCurrentPlayer();
        }
    }

    private void HandleProgressionStateChanged()
    {
        if (this == null)
        {
            return;
        }

        QuestProgressionManager progression = QuestProgressionManager.Instance;
        ApplyBellState(progression);
        TryRegisterCurrentPlayer();
    }

    private void ApplyBellState(QuestProgressionManager progression)
    {
        if (bellPepperObject == null)
        {
            Transform bellPepperTransform = transform.Find("Bell_Pepper");
            if (bellPepperTransform != null)
            {
                bellPepperObject = bellPepperTransform.gameObject;
            }
        }

        if (bellPepperObject != null)
        {
            bool shouldShowPepper = progression != null && (progression.BellPepperInstalled || progression.SealAccepted);
            if (bellPepperObject.activeSelf != shouldShowPepper)
            {
                bellPepperObject.SetActive(shouldShowPepper);
            }
        }

        ApplyLockDoorState(progression);
        if (!_suppressDoorVisualStateSync)
        {
            ApplyDoorVisualState(progression);
        }
    }

    private void SubscribeProgressionState()
    {
        QuestProgressionManager progression = QuestProgressionManager.Instance;
        if (progression == null)
        {
            return;
        }

        if (_subscribedProgression == progression)
        {
            ApplyBellState(progression);
            return;
        }

        UnsubscribeProgressionState();
        _subscribedProgression = progression;
        _subscribedProgression.StateChanged += HandleProgressionStateChanged;
        ApplyBellState(_subscribedProgression);
    }

    private void UnsubscribeProgressionState()
    {
        if (_subscribedProgression == null)
        {
            return;
        }

        _subscribedProgression.StateChanged -= HandleProgressionStateChanged;
        _subscribedProgression = null;
    }

    private void TryRegisterCurrentPlayer()
    {
        if (_interactingPlayer == null)
        {
            return;
        }

        if (CanInteract(_interactingPlayer))
        {
            _interactingPlayer.Interaction?.RegisterCandidate(this);
            return;
        }

        _interactingPlayer.Interaction?.UnregisterCandidate(this);
    }

    private void CancelInstallSequence()
    {
        if (_installSequence != null)
        {
            StopCoroutine(_installSequence);
            _installSequence = null;
        }

        if (_installSequencePlayer != null)
        {
            _installSequencePlayer.EndExternalInteractionLock();
            _installSequencePlayer = null;
        }
    }

    private void ResolveBellAudioSource()
    {
        if (bellAudioSource == null)
        {
            bellAudioSource = GetComponent<AudioSource>();
        }

        if (bellAudioSource == null)
        {
            bellAudioSource = gameObject.AddComponent<AudioSource>();
        }

        bellAudioSource.playOnAwake = false;
        bellAudioSource.loop = false;
        bellAudioSource.spatialBlend = 1f;
        bellAudioSource.minDistance = 4f;
        bellAudioSource.maxDistance = 28f;
        bellAudioSource.rolloffMode = AudioRolloffMode.Linear;

        if (bellRingClip == null)
        {
            bellRingClip = GameAudioResources.LoadClip(GameAudioCatalog.World.ChurchBell);
        }
    }

    private void ResolveBellAnimator()
    {
        if (bellAnimator == null)
        {
            bellAnimator = GetComponentInChildren<Animator>(true);
        }
    }

    private void ResolveBellWaveReferences()
    {
        if (bellWaveAnchor == null)
        {
            bellWaveAnchor = FindNamedTransform(BellWaveAnchorObjectName, transform);
        }
    }

    private void ResolveDoorAnimator()
    {
        if (doorAnimator != null)
        {
            return;
        }

        Transform doorTransform = FindClosestNamedTransform(
            string.IsNullOrWhiteSpace(doorObjectName) ? DefaultDoorObjectName : doorObjectName);

        if (doorTransform == null)
        {
            return;
        }

        doorAnimator = doorTransform.GetComponent<Animator>();
        if (doorAnimator == null)
        {
            doorAnimator = doorTransform.GetComponentInChildren<Animator>(true);
        }
    }

    private void ResolveDoorAudioSource()
    {
        if (doorAudioSource != null)
        {
            return;
        }

        ResolveDoorAnimator();
        Transform audioRoot = doorAnimator != null ? doorAnimator.transform : null;
        if (audioRoot == null)
        {
            return;
        }

        doorAudioSource = audioRoot.GetComponent<AudioSource>();
        if (doorAudioSource == null)
        {
            doorAudioSource = audioRoot.gameObject.AddComponent<AudioSource>();
        }

        doorAudioSource.playOnAwake = false;
        doorAudioSource.loop = false;
        doorAudioSource.spatialBlend = 1f;
        doorAudioSource.minDistance = 5f;
        doorAudioSource.maxDistance = 32f;
        doorAudioSource.rolloffMode = AudioRolloffMode.Linear;
    }

    private void PlayDoorOpenSfx()
    {
        if (doorOpenClip == null)
        {
            return;
        }

        ResolveDoorAudioSource();
        if (doorAudioSource == null)
        {
            return;
        }

        doorAudioSource.pitch = 1f;
        doorAudioSource.PlayOneShot(doorOpenClip, doorOpenVolume);
    }

    private void SpawnBellWaveFx()
    {
        ResolveBellWaveReferences();

        Transform spawnAnchor = bellWaveAnchor != null
            ? bellWaveAnchor
            : (bellAnimator != null ? bellAnimator.transform : transform);

        if (spawnAnchor == null)
        {
            return;
        }

        SpawnBellWaveInstance(bellWavePrimaryPrefab, spawnAnchor);
        SpawnBellWaveInstance(bellWaveSecondaryPrefab, spawnAnchor);
    }

    private void StartBellWaveSequence()
    {
        if (_bellWaveRoutine != null)
        {
            StopCoroutine(_bellWaveRoutine);
        }

        _bellWaveRoutine = StartCoroutine(Co_PlayBellWaveSequence());
    }

    private IEnumerator Co_PlayBellWaveSequence()
    {
        if (bellWaveInitialDelay > 0f)
        {
            yield return new WaitForSeconds(bellWaveInitialDelay);
        }

        int repeatCount = Mathf.Clamp(bellWaveRepeatCount, 1, 3);
        float repeatInterval = Mathf.Max(0.05f, bellWaveRepeatInterval);

        for (int i = 0; i < repeatCount; i++)
        {
            SpawnBellWaveFx();

            if (i < repeatCount - 1)
            {
                yield return new WaitForSeconds(repeatInterval);
            }
        }

        _bellWaveRoutine = null;
    }

    private void SpawnBellWaveInstance(GameObject prefab, Transform spawnAnchor)
    {
        if (prefab == null || spawnAnchor == null)
        {
            return;
        }

        Instantiate(prefab, spawnAnchor.position, spawnAnchor.rotation);
    }

    private void ActivateBellShot(PlayerController sequencePlayer)
    {
        Transform bellTransform = bellAnimator != null ? bellAnimator.transform : transform;
        Vector3 focusPosition = bellShotLookAt != null
            ? bellShotLookAt.position
            : bellTransform.position + Vector3.up * bellShotHeight;
        Vector3 cameraPosition = bellShotAnchor != null
            ? bellShotAnchor.position
            : CalculateShotPosition(
                focusPosition,
                sequencePlayer != null ? sequencePlayer.transform.position : transform.position,
                bellTransform.forward,
                bellShotDistance,
                bellShotHeight,
                bellShotSideBias);

        ActivateSequenceCamera(cameraPosition, focusPosition);
    }

    private void ActivateDoorShot(PlayerController sequencePlayer)
    {
        ResolveDoorAnimator();
        Transform doorTransform = doorAnimator != null ? doorAnimator.transform : null;
        if (doorTransform == null)
        {
            return;
        }

        Vector3 focusPosition = doorShotLookAt != null
            ? doorShotLookAt.position
            : GetDoorFocusPosition(doorTransform);
        Vector3 cameraPosition = doorShotAnchor != null
            ? doorShotAnchor.position
            : CalculateShotPosition(
                focusPosition,
                sequencePlayer != null ? sequencePlayer.transform.position : transform.position,
                doorTransform.forward,
                doorShotDistance,
                doorShotHeight,
                doorShotSideBias);

        ActivateSequenceCamera(cameraPosition, focusPosition);
    }

    private Vector3 CalculateShotPosition(
        Vector3 focusPosition,
        Vector3 referencePosition,
        Vector3 fallbackForward,
        float distance,
        float height,
        float sideBias)
    {
        Vector3 referenceDirection = Vector3.ProjectOnPlane(referencePosition - focusPosition, Vector3.up);
        if (referenceDirection.sqrMagnitude < 0.001f)
        {
            Vector3 fallbackDirection = Vector3.ProjectOnPlane(-fallbackForward, Vector3.up);
            referenceDirection = fallbackDirection.sqrMagnitude > 0.001f ? fallbackDirection : Vector3.back;
        }

        referenceDirection.Normalize();
        Vector3 sideDirection = Vector3.Cross(Vector3.up, referenceDirection);
        Vector3 shotDirection = (referenceDirection + sideDirection * sideBias).normalized;
        return focusPosition + shotDirection * distance + Vector3.up * height;
    }

    private Vector3 GetDoorFocusPosition(Transform doorTransform)
    {
        Renderer doorRenderer = doorTransform.GetComponentInChildren<Renderer>();
        if (doorRenderer != null)
        {
            Bounds bounds = doorRenderer.bounds;
            return bounds.center + Vector3.up * Mathf.Min(bounds.extents.y * 0.2f, 1f);
        }

        return doorTransform.position + Vector3.up * 1.2f;
    }

    private void ActivateSequenceCamera(Vector3 cameraPosition, Vector3 lookAtPosition)
    {
        if (!EnsureSequenceCamera())
        {
            return;
        }

        _sequenceCamera.transform.SetPositionAndRotation(
            cameraPosition,
            Quaternion.LookRotation((lookAtPosition - cameraPosition).normalized, Vector3.up));
        _sequenceCamera.Priority = 1000;
        _sequenceCamera.PreviousStateIsValid = false;
    }

    private void DeactivateSequenceCamera()
    {
        if (_sequenceCamera != null)
        {
            _sequenceCamera.Priority = -10;
        }

        if (_sequenceBrain != null && _hasCachedBlend)
        {
            _sequenceBrain.m_DefaultBlend = _cachedBlend;
        }

        _hasCachedBlend = false;
    }

    private bool EnsureSequenceCamera()
    {
        if (_sequenceCamera == null)
        {
            GameObject cameraObject = new GameObject("BellSequenceCamera");
            _sequenceCamera = cameraObject.AddComponent<CinemachineVirtualCamera>();
            _sequenceCamera.Priority = -10;
            _sequenceCamera.m_Lens.FieldOfView = cinematicFieldOfView;
        }

        if (_sequenceBrain == null)
        {
            CinemachineBrain[] brains = Object.FindObjectsByType<CinemachineBrain>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (brains.Length > 0)
            {
                _sequenceBrain = brains[0];
            }
        }

        if (_sequenceBrain == null)
        {
            return false;
        }

        if (!_hasCachedBlend)
        {
            _cachedBlend = _sequenceBrain.m_DefaultBlend;
            _hasCachedBlend = true;
        }

        _sequenceBrain.m_DefaultBlend = new CinemachineBlendDefinition(
            CinemachineBlendDefinition.Style.EaseInOut,
            cinematicBlendTime);

        _sequenceCamera.m_Lens.FieldOfView = cinematicFieldOfView;
        return true;
    }

    private void ApplyLockDoorState(QuestProgressionManager progression)
    {
        ResolveLockDoorObject();
        if (lockDoorObject == null)
        {
            return;
        }

        bool shouldBeActive = progression == null || (!progression.SealAccepted && !progression.BossCleared);
        if (lockDoorObject.activeSelf != shouldBeActive)
        {
            lockDoorObject.SetActive(shouldBeActive);
        }
    }

    private void ResolveLockDoorObject()
    {
        if (lockDoorObject != null)
        {
            return;
        }

        Transform preferredFogWall = FindNamedTransform(PreferredFogWallObjectName);
        if (preferredFogWall == null)
        {
            return;
        }

        Transform lockDoorTransform = preferredFogWall.Find(LockDoorObjectName);
        if (lockDoorTransform == null)
        {
            lockDoorTransform = FindNamedTransform(LockDoorObjectName, preferredFogWall);
        }

        if (lockDoorTransform != null)
        {
            lockDoorObject = lockDoorTransform.gameObject;
        }
    }

    private static Transform FindNamedTransform(string objectName, Transform root = null)
    {
        Transform[] transforms = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform candidate = transforms[i];
            if (candidate == null || candidate.name != objectName)
            {
                continue;
            }

            if (root == null || candidate.IsChildOf(root))
            {
                return candidate;
            }
        }

        return null;
    }

    private Transform FindClosestNamedTransform(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
        {
            return null;
        }

        Transform[] transforms = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        Transform closest = null;
        float closestSqrDistance = float.PositiveInfinity;
        Vector3 origin = transform.position;

        for (int i = 0; i < transforms.Length; i++)
        {
            Transform candidate = transforms[i];
            if (candidate == null || candidate.name != objectName)
            {
                continue;
            }

            float sqrDistance = (candidate.position - origin).sqrMagnitude;
            if (sqrDistance < closestSqrDistance)
            {
                closest = candidate;
                closestSqrDistance = sqrDistance;
            }
        }

        return closest;
    }

    private static float GetCurrentAnimationDuration(Animator animator, float fallbackDuration)
    {
        if (animator == null)
        {
            return fallbackDuration;
        }

        AnimatorClipInfo[] clipInfos = animator.GetCurrentAnimatorClipInfo(0);
        if (clipInfos != null)
        {
            for (int i = 0; i < clipInfos.Length; i++)
            {
                AnimationClip clip = clipInfos[i].clip;
                if (clip != null && clip.length > 0f)
                {
                    return clip.length;
                }
            }
        }

        return fallbackDuration;
    }

    private void OnDrawGizmosSelected()
    {
        if (!playBellDoorCinematic)
        {
            return;
        }

        Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.9f);
        Vector3 bellFocusPosition = bellShotLookAt != null
            ? bellShotLookAt.position
            : transform.position + Vector3.up * bellShotHeight;
        Gizmos.DrawSphere(bellFocusPosition, 0.08f);
        if (bellShotAnchor != null)
        {
            Gizmos.DrawLine(bellShotAnchor.position, bellFocusPosition);
            Gizmos.DrawWireSphere(bellShotAnchor.position, 0.1f);
        }

        ResolveDoorAnimator();
        if (doorAnimator != null || doorShotLookAt != null)
        {
            Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.9f);
            Vector3 doorFocusPosition = doorShotLookAt != null
                ? doorShotLookAt.position
                : GetDoorFocusPosition(doorAnimator.transform);
            Gizmos.DrawSphere(doorFocusPosition, 0.08f);
            if (doorShotAnchor != null)
            {
                Gizmos.DrawLine(doorShotAnchor.position, doorFocusPosition);
                Gizmos.DrawWireSphere(doorShotAnchor.position, 0.1f);
            }
        }
    }
}
