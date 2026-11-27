using System.Collections;
using TMPro;
using UnityEngine;

[RequireComponent(typeof(BossZoneTrigger))]
public sealed class BossEntranceDirector : MonoBehaviour
{
    private const int SceneEntryRetryFrames = 90;
    private const string CutSceneCanvasName = "CutSceneCanvas";
    private const string CutSceneTextName = "CutSceneText";

    [Header("참조")]
    [SerializeField] private BossZoneTrigger zoneTrigger;
    [SerializeField] private BossController boss;
    [SerializeField] private Transform playerArrivalPoint;
    [SerializeField] private Transform playerLookTarget;
    [SerializeField] private Canvas cutSceneCanvas;
    [SerializeField] private TMP_Text cutSceneText;
    [SerializeField] private AudioSource cutSceneSpeechAudioSource;
    [SerializeField] private bool autoCreateCutSceneSpeechAudioSource = true;
    [SerializeField] private bool playDialogueSpeech;

    [Header("정책")]
    [SerializeField] private bool triggerOnSceneEntry = true;
    [SerializeField] private bool skipIfIntroAlreadySeen = true;
    [SerializeField] private bool forceSceneEntryIntro;
#if UNITY_EDITOR
    [SerializeField] private bool forceIntroReplayInEditor = true;
#endif

    [Header("타이밍")]
    [SerializeField] private float preIntroDelay = 0.15f;
    [SerializeField] private float playerApproachDuration = 0.8f;
    [SerializeField] private float cameraBlendLeadTime = 0.3f;
    [SerializeField] private float bossAnimationLeadDelay = 0.15f;
    [SerializeField] private float bossNameRevealDelay = 0.45f;
    [SerializeField] private float postRevealHoldDuration = 1.3f;

    [Header("보스 연출")]
    [SerializeField] private string bossIntroAnimation = "Exhausted";
    [SerializeField] private float bossIntroCrossFade = 0.2f;
    [SerializeField] private float bossIntroAnimationCompletionNormalized = 0.98f;
    [SerializeField] private float bossIntroAnimationCompletionTimeout = 8f;

    [Header("Talk Cutscene")]
    [SerializeField] private bool useDialogueCutsceneSequence = true;
    [SerializeField] private string preIntroIdleAnimation = "PreintroIdle";
    [SerializeField] private string dialogueStandUpAnimation = "StandUp";
    [SerializeField] private string dialogueBackstabAnimation = "Backstab_Stabbed";
    [SerializeField] private string dialogueExhaustedAnimation = "Exhausted";
    [SerializeField] private int standUpTriggerElementIndex = 2;
    [SerializeField] private float standUpLeadBeforeElementEnd = 2f;
    [SerializeField] private float defaultDialogueElementDuration = 4f;
    [SerializeField] private float[] dialogueElementDurations = { 4f, 4f, 4f, 4f };
    [SerializeField] private int[] dialogueTimingGroupStartElementIndices = { 0, 2, 4 };
    [SerializeField] [Range(0.1f, 1f)] private float dialogueTypewriterRevealRatio = 0.85f;
    [SerializeField] private float dialogueTypewriterMinimumRevealDuration = 0.2f;
    [SerializeField] private float dialogueTypewriterMinimumHoldDuration = 0.08f;
    [SerializeField] private float dialogueTypewriterSpeedMultiplier = 1f;
    [SerializeField] private AudioClip[] dialogueSpeechClips;
    [SerializeField] private int[] dialogueSpeechStartElementIndices = { 0, 2, 4 };
    [SerializeField] private float finalDialoguePenultimateWeight = 3f;
    [SerializeField] private float finalDialogueLastWeight = 7f;

    private Coroutine _entranceRoutine;
    private Coroutine _sceneEntryRoutine;
    private Coroutine _dialogueBossSequenceRoutine;
    private bool _sceneEntryAttempted;
    private bool _forceCurrentEncounterIntro;
    private string[] _pendingDialogueCutsceneLines;

    private void Reset()
    {
        zoneTrigger = GetComponent<BossZoneTrigger>();
    }

    private void Awake()
    {
        if (zoneTrigger == null)
        {
            zoneTrigger = GetComponent<BossZoneTrigger>();
        }

        if (boss == null && zoneTrigger != null)
        {
            boss = zoneTrigger.BossProxy;
        }

        ResolveCutSceneDialogueReferences();
        ResolveCutSceneSpeechAudioSource();
        HideCutSceneDialogue();
    }

    private void Start()
    {
        if (triggerOnSceneEntry)
        {
            QueueSceneEntryAttempt();
        }
    }

    private void OnEnable()
    {
        GameLoopEvents.PlayerRespawned += HandlePlayerRespawned;
    }

    private void OnDisable()
    {
        GameLoopEvents.PlayerRespawned -= HandlePlayerRespawned;

        if (_sceneEntryRoutine != null)
        {
            StopCoroutine(_sceneEntryRoutine);
            _sceneEntryRoutine = null;
        }

        if (_entranceRoutine == null)
        {
            StopCutSceneSpeech();
            HideCutSceneDialogue();
            return;
        }

        StopCoroutine(_entranceRoutine);
        _entranceRoutine = null;
        if (_dialogueBossSequenceRoutine != null)
        {
            StopCoroutine(_dialogueBossSequenceRoutine);
            _dialogueBossSequenceRoutine = null;
        }

        StopCutSceneSpeech();
        CleanupInterruptedSequence();
    }

    public bool UseDialogueCutsceneSequence => useDialogueCutsceneSequence;

    public bool TryStartDialogueCutsceneEncounter(PlayerController enteredPlayer, string[] dialogueLines)
    {
        if (!useDialogueCutsceneSequence)
        {
            return TryStartScriptedEncounter(enteredPlayer, forceIntro: true);
        }

        _pendingDialogueCutsceneLines = dialogueLines != null && dialogueLines.Length > 0
            ? (string[])dialogueLines.Clone()
            : null;

        bool started = TryStartEncounter(enteredPlayer, forceIntro: true);
        if (!started)
        {
            _pendingDialogueCutsceneLines = null;
        }

        return started;
    }

    public bool TryHandleTriggerEnter(PlayerController enteredPlayer)
    {
        return TryStartEncounter(enteredPlayer, forceIntro: false);
    }

    public bool TryStartScriptedEncounter(PlayerController enteredPlayer, bool forceIntro = true)
    {
        if (zoneTrigger == null)
        {
            zoneTrigger = GetComponent<BossZoneTrigger>();
        }

        zoneTrigger?.AuthorizeManualEncounterStart();
        return TryStartEncounter(enteredPlayer, forceIntro);
    }

    private void QueueSceneEntryAttempt()
    {
        if (_sceneEntryAttempted || _sceneEntryRoutine != null || !isActiveAndEnabled)
        {
            return;
        }

        _sceneEntryRoutine = StartCoroutine(Co_AttemptSceneEntryStart());
    }

    private IEnumerator Co_AttemptSceneEntryStart()
    {
        for (int frame = 0; frame < SceneEntryRetryFrames; frame++)
        {
            if (!IsSceneEntryStartupReady())
            {
                yield return null;
                continue;
            }

            if (!ShouldAutoTriggerOnSceneEntry())
            {
                _sceneEntryAttempted = true;
                _sceneEntryRoutine = null;
                yield break;
            }

            PlayerController player = zoneTrigger != null ? zoneTrigger.Player : null;
            if (player == null)
            {
                player = FindFirstObjectByType<PlayerController>(FindObjectsInactive.Exclude);
            }

            if (player != null)
            {
                TryConsumeSceneEntry(player);
                _sceneEntryRoutine = null;
                yield break;
            }

            yield return null;
        }

        _sceneEntryRoutine = null;
    }

    private void HandlePlayerRespawned(PlayerController respawnedPlayer, Transform _)
    {
        if (!triggerOnSceneEntry || _sceneEntryAttempted || respawnedPlayer == null)
        {
            return;
        }

        TryConsumeSceneEntry(respawnedPlayer);
    }

    private IEnumerator Co_PlayEntrance(PlayerController enteredPlayer)
    {
        PlayerController player = enteredPlayer != null ? enteredPlayer : zoneTrigger.Player;

        if (player == null)
        {
            zoneTrigger.CancelEntranceSequence();
            _forceCurrentEncounterIntro = false;
            _entranceRoutine = null;
            yield break;
        }

        boss.BeginExternalEntranceControl();
        player.BeginExternalInteractionLock();
        player.ForceUnlockCameraLockOn(true);
        player.SyncCameraTargetsImmediate();

        if (preIntroDelay > 0f)
        {
            yield return new WaitForSeconds(preIntroDelay);
        }

        yield return MovePlayerToArrivalPoint(player);
        SnapPlayerFacing(player);

        if (zoneTrigger.FogWall != null)
        {
            zoneTrigger.FogWall.EnableWall();
        }

        boss.ActivateEntranceCamera();

        if (cameraBlendLeadTime > 0f)
        {
            yield return new WaitForSeconds(cameraBlendLeadTime);
        }

        boss.ShowEntranceBackWeapon(true);

        if (bossAnimationLeadDelay > 0f)
        {
            yield return new WaitForSeconds(bossAnimationLeadDelay);
        }

        if (!string.IsNullOrWhiteSpace(bossIntroAnimation))
        {
            zoneTrigger.BeginBossFightPresentation(player);
            if (bossIntroAnimation == "Exhausted")
            {
                boss.SetRootMotionMode(boss.ShouldUseIntroRootMotion(), false);
                boss.SetAnimSpeed(boss.IntroExhaustedAnimSpeed);
            }
            boss.PlayAnim(bossIntroAnimation, bossIntroCrossFade);
        }
        else
        {
            zoneTrigger.BeginBossFightPresentation(player);
        }

        if (bossNameRevealDelay > 0f)
        {
            yield return new WaitForSeconds(bossNameRevealDelay);
        }

        bool startedFight = zoneTrigger.FinalizeBossFightStart(player);
        boss.ShowBossNameUI(true);

        if (!string.IsNullOrWhiteSpace(bossIntroAnimation))
        {
            yield return WaitForBossAnimationCompletion(
                bossIntroAnimation,
                Mathf.Max(0.5f, bossIntroAnimationCompletionTimeout),
                Mathf.Clamp01(bossIntroAnimationCompletionNormalized));
        }

        if (postRevealHoldDuration > 0f)
        {
            yield return new WaitForSeconds(postRevealHoldDuration);
        }

        boss.DeactivateEntranceCamera();
        boss.SetRootMotionMode(false, false);
        boss.CompleteExternalEntranceControl();
        boss.SetAnimSpeed(1f);
        if (startedFight)
        {
            QuestProgressionManager.Instance?.MarkBossIntroSeen();
        }

        player.EndExternalInteractionLock();
        _forceCurrentEncounterIntro = false;
        _entranceRoutine = null;
    }

    private IEnumerator Co_PlayDialogueCutsceneEntrance(PlayerController enteredPlayer, string[] dialogueLines)
    {
        PlayerController player = enteredPlayer != null ? enteredPlayer : zoneTrigger.Player;

        if (player == null)
        {
            zoneTrigger.CancelEntranceSequence();
            _pendingDialogueCutsceneLines = null;
            _forceCurrentEncounterIntro = false;
            _entranceRoutine = null;
            yield break;
        }

        ResolveCutSceneDialogueReferences();
        HideCutSceneDialogue();

        boss.BeginExternalEntranceControl();
        player.BeginExternalInteractionLock();
        player.ForceUnlockCameraLockOn(true);
        player.SyncCameraTargetsImmediate();

        if (preIntroDelay > 0f)
        {
            yield return new WaitForSeconds(preIntroDelay);
        }

        yield return MovePlayerToArrivalPoint(player);
        SnapPlayerFacing(player);

        if (zoneTrigger.FogWall != null)
        {
            zoneTrigger.FogWall.EnableWall();
        }

        boss.ActivateEntranceCamera();

        if (cameraBlendLeadTime > 0f)
        {
            yield return new WaitForSeconds(cameraBlendLeadTime);
        }

        boss.ShowEntranceBackWeapon(true);
        zoneTrigger.BeginBossFightPresentationWithoutBgm(player);

        if (bossAnimationLeadDelay > 0f)
        {
            yield return new WaitForSeconds(bossAnimationLeadDelay);
        }

        PlayDialogueBossAnimation(preIntroIdleAnimation, 1f, useRotationRootMotion: false);
        yield return PlayDialogueElements(dialogueLines);
        zoneTrigger.PlayPresentationBossBgm();
        if (_dialogueBossSequenceRoutine != null)
        {
            StopCoroutine(_dialogueBossSequenceRoutine);
            _dialogueBossSequenceRoutine = null;
        }

        bool startedFight = zoneTrigger.FinalizeBossFightStart(player);
        boss.ShowBossNameUI(true);

        HideCutSceneDialogue();
        boss.DeactivateEntranceCamera();
        boss.SetRootMotionMode(false, false);
        boss.CompleteExternalEntranceControl();
        boss.SetAnimSpeed(1f);

        if (startedFight)
        {
            QuestProgressionManager.Instance?.MarkBossIntroSeen();
        }

        player.EndExternalInteractionLock();
        _pendingDialogueCutsceneLines = null;
        _forceCurrentEncounterIntro = false;
        _entranceRoutine = null;
    }

    private IEnumerator PlayDialogueElements(string[] dialogueLines)
    {
        if (dialogueLines == null || dialogueLines.Length == 0)
        {
            yield break;
        }

        StopCutSceneSpeech();

        for (int i = 0; i < dialogueLines.Length; i++)
        {
            string line = dialogueLines[i] ?? string.Empty;
            TryPlayDialogueSpeechForElement(i);

            if (i <= 1)
            {
                PlayDialogueBossAnimation(preIntroIdleAnimation, 1f, useRotationRootMotion: false);
            }

            if (i == standUpTriggerElementIndex && _dialogueBossSequenceRoutine == null)
            {
                _dialogueBossSequenceRoutine = StartCoroutine(
                    Co_PlayDelayedDialogueBossSequence(
                        ResolveDialogueBossSequenceTriggerDelay(i, dialogueLines.Length)));
            }

            float elementDuration = ResolveDialogueElementDuration(i, dialogueLines.Length);
            if (elementDuration > 0f)
            {
                yield return PlayTypedDialogueElement(line, elementDuration);
            }
        }

        StopCutSceneSpeech();
        HideCutSceneDialogue();
    }

    private IEnumerator Co_PlayDelayedDialogueBossSequence(float delay)
    {
        if (delay > 0f)
        {
            yield return new WaitForSeconds(delay);
        }

        yield return Co_PlayDialogueBossSequence();
    }

    private IEnumerator Co_PlayDialogueBossSequence()
    {
        PlayDialogueBossAnimation(dialogueStandUpAnimation, 1f, useRotationRootMotion: true);
        yield return WaitForBossAnimationCompletion(
            dialogueStandUpAnimation,
            Mathf.Max(0.5f, bossIntroAnimationCompletionTimeout),
            Mathf.Clamp01(bossIntroAnimationCompletionNormalized));

        if (!string.IsNullOrWhiteSpace(dialogueBackstabAnimation))
        {
            yield return WaitForBossAnimationCompletion(
                dialogueBackstabAnimation,
                Mathf.Max(0.5f, bossIntroAnimationCompletionTimeout),
                Mathf.Clamp01(bossIntroAnimationCompletionNormalized));
        }

        if (!string.IsNullOrWhiteSpace(dialogueExhaustedAnimation))
        {
            boss.SetRootMotionMode(boss.ShouldUseIntroRootMotion(), false);
            boss.SetAnimSpeed(boss.IntroExhaustedAnimSpeed);
            boss.PlayAnim(dialogueExhaustedAnimation, bossIntroCrossFade);
            yield return WaitForBossAnimationCompletion(
                dialogueExhaustedAnimation,
                Mathf.Max(0.5f, bossIntroAnimationCompletionTimeout),
                Mathf.Clamp01(bossIntroAnimationCompletionNormalized));
        }

        _dialogueBossSequenceRoutine = null;
    }

    private IEnumerator MovePlayerToArrivalPoint(PlayerController player)
    {
        if (player == null)
        {
            yield break;
        }

        Transform arrivalPoint = ResolvePlayerArrivalPoint();
        if (arrivalPoint == null || playerApproachDuration <= 0.01f)
        {
            SnapPlayerFacing(player);
            yield break;
        }

        Vector3 startPosition = player.transform.position;
        Quaternion startRotation = player.transform.rotation;
        Quaternion targetRotation = ResolvePlayerFacing(player);
        float elapsed = 0f;

        while (elapsed < playerApproachDuration)
        {
            float t = elapsed / playerApproachDuration;
            float easedT = 1f - Mathf.Pow(1f - t, 3f);
            Vector3 nextPosition = Vector3.Lerp(startPosition, arrivalPoint.position, easedT);
            Quaternion nextRotation = Quaternion.Slerp(startRotation, targetRotation, easedT);
            player.transform.SetPositionAndRotation(nextPosition, nextRotation);
            player.SetVelocity(Vector3.zero);
            elapsed += Time.deltaTime;
            yield return null;
        }

        player.transform.SetPositionAndRotation(arrivalPoint.position, targetRotation);
        player.SetVelocity(Vector3.zero);
    }

    private void SnapPlayerFacing(PlayerController player)
    {
        if (player == null)
        {
            return;
        }

        Vector3 position = player.transform.position;
        Transform arrivalPoint = ResolvePlayerArrivalPoint();
        if (arrivalPoint != null)
        {
            position = arrivalPoint.position;
        }

        player.transform.SetPositionAndRotation(position, ResolvePlayerFacing(player));
        player.SetVelocity(Vector3.zero);
    }

    private Quaternion ResolvePlayerFacing(PlayerController player)
    {
        Transform lookTarget = ResolvePlayerLookTarget();
        if (player == null || lookTarget == null)
        {
            return player != null ? player.transform.rotation : Quaternion.identity;
        }

        Vector3 forward = lookTarget.position - player.transform.position;
        forward.y = 0f;
        if (forward.sqrMagnitude <= 0.0001f)
        {
            return player.transform.rotation;
        }

        return Quaternion.LookRotation(forward.normalized, Vector3.up);
    }

    private bool ShouldSkipIntro()
    {
        if (_forceCurrentEncounterIntro)
        {
            return false;
        }

#if UNITY_EDITOR
        if (forceIntroReplayInEditor)
        {
            return false;
        }
#endif

        if (!skipIfIntroAlreadySeen)
        {
            return false;
        }

        QuestProgressionManager progression = QuestProgressionManager.Instance;
        return progression != null && progression.BossIntroSeen;
    }

    private bool ShouldAutoTriggerOnSceneEntry()
    {
        if (forceSceneEntryIntro)
        {
            return true;
        }

        QuestProgressionManager progression = QuestProgressionManager.Instance;
        if (progression == null)
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(progression.CurrentCheckpointId))
        {
            return false;
        }

        return !skipIfIntroAlreadySeen || !progression.BossIntroSeen;
    }

    private static bool IsSceneEntryStartupReady()
    {
        GameLoopManager gameLoopManager = GameLoopManager.Instance;
        return gameLoopManager == null || gameLoopManager.HasResolvedStartupCheckpointSpawn;
    }

    private Transform ResolvePlayerArrivalPoint()
    {
        if (playerArrivalPoint != null)
        {
            return playerArrivalPoint;
        }

        return boss != null ? boss.introTriggerPoint : null;
    }

    private Transform ResolvePlayerLookTarget()
    {
        if (playerLookTarget != null)
        {
            return playerLookTarget;
        }

        if (boss != null)
        {
            return boss.transform;
        }

        return ResolvePlayerArrivalPoint();
    }

    private void CleanupInterruptedSequence()
    {
        zoneTrigger?.CancelEntranceSequence();
        zoneTrigger?.CancelBossFightPresentation();

        if (boss != null)
        {
            boss.SetAnimSpeed(1f);
            boss.DeactivateEntranceCamera();
            boss.AbortExternalEntranceControl();
        }

        PlayerController player = zoneTrigger != null ? zoneTrigger.Player : null;
        player?.EndExternalInteractionLock();
        StopCutSceneSpeech();
        HideCutSceneDialogue();
        _pendingDialogueCutsceneLines = null;
        _dialogueBossSequenceRoutine = null;
        _forceCurrentEncounterIntro = false;
    }

    private void TryConsumeSceneEntry(PlayerController player)
    {
        if (_sceneEntryAttempted || player == null || zoneTrigger == null)
        {
            return;
        }

        _sceneEntryAttempted = true;
        _forceCurrentEncounterIntro = forceSceneEntryIntro;
        try
        {
            zoneTrigger.TryHandleSceneEntry(player);
        }
        finally
        {
            _forceCurrentEncounterIntro = false;
        }
    }

    private bool TryStartEncounter(PlayerController enteredPlayer, bool forceIntro)
    {
        if (_entranceRoutine != null)
        {
            return true;
        }

        if (!ResolveReferences())
        {
            return false;
        }

        if (!zoneTrigger.CanStartEncounter(enteredPlayer))
        {
            return false;
        }

        bool previousForceState = _forceCurrentEncounterIntro;
        bool shouldRestoreForceState = true;
        _forceCurrentEncounterIntro = forceIntro;

        try
        {
            if (ShouldSkipIntro())
            {
                boss.CompleteExternalEntranceControl();
                return zoneTrigger.StartBossFight(enteredPlayer);
            }

            if (!zoneTrigger.TryBeginEntranceSequence(enteredPlayer))
            {
                return true;
            }

            _entranceRoutine = StartCoroutine(
                _pendingDialogueCutsceneLines != null && _pendingDialogueCutsceneLines.Length > 0
                    ? Co_PlayDialogueCutsceneEntrance(enteredPlayer, _pendingDialogueCutsceneLines)
                    : Co_PlayEntrance(enteredPlayer));
            shouldRestoreForceState = false;
            return true;
        }
        finally
        {
            if (shouldRestoreForceState)
            {
                _forceCurrentEncounterIntro = previousForceState;
            }
        }
    }

    private bool ResolveReferences()
    {
        if (zoneTrigger == null)
        {
            zoneTrigger = GetComponent<BossZoneTrigger>();
        }

        if (zoneTrigger == null)
        {
            return false;
        }

        if (boss == null)
        {
            boss = zoneTrigger.BossProxy;
        }

        return boss != null;
    }

    private void ResolveCutSceneDialogueReferences()
    {
        if (cutSceneCanvas == null)
        {
            Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < canvases.Length; i++)
            {
                Canvas candidate = canvases[i];
                if (candidate != null && string.Equals(candidate.name, CutSceneCanvasName, System.StringComparison.Ordinal))
                {
                    cutSceneCanvas = candidate;
                    break;
                }
            }
        }

        if (cutSceneText == null)
        {
            if (cutSceneCanvas != null)
            {
                TMP_Text[] texts = cutSceneCanvas.GetComponentsInChildren<TMP_Text>(true);
                for (int i = 0; i < texts.Length; i++)
                {
                    TMP_Text candidate = texts[i];
                    if (candidate != null && string.Equals(candidate.name, CutSceneTextName, System.StringComparison.Ordinal))
                    {
                        cutSceneText = candidate;
                        break;
                    }
                }
            }

            if (cutSceneText == null)
            {
                TMP_Text[] texts = FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                for (int i = 0; i < texts.Length; i++)
                {
                    TMP_Text candidate = texts[i];
                    if (candidate != null && string.Equals(candidate.name, CutSceneTextName, System.StringComparison.Ordinal))
                    {
                        cutSceneText = candidate;
                        break;
                    }
                }
            }
        }
    }

    private void ResolveCutSceneSpeechAudioSource()
    {
        if (cutSceneSpeechAudioSource == null)
        {
            cutSceneSpeechAudioSource = GetComponent<AudioSource>();
        }

        if (cutSceneSpeechAudioSource == null && autoCreateCutSceneSpeechAudioSource)
        {
            cutSceneSpeechAudioSource = gameObject.AddComponent<AudioSource>();
        }

        if (cutSceneSpeechAudioSource == null)
        {
            return;
        }

        cutSceneSpeechAudioSource.playOnAwake = false;
        cutSceneSpeechAudioSource.loop = false;
        cutSceneSpeechAudioSource.spatialBlend = 0f;
        cutSceneSpeechAudioSource.dopplerLevel = 0f;
    }

    private void ShowCutSceneDialogue(string line)
    {
        ResolveCutSceneDialogueReferences();

        if (cutSceneCanvas != null && !cutSceneCanvas.gameObject.activeSelf)
        {
            cutSceneCanvas.gameObject.SetActive(true);
        }

        if (cutSceneText != null)
        {
            cutSceneText.text = line ?? string.Empty;
            cutSceneText.maxVisibleCharacters = int.MaxValue;
        }
    }

    private void HideCutSceneDialogue()
    {
        ResolveCutSceneDialogueReferences();

        if (cutSceneText != null)
        {
            cutSceneText.text = string.Empty;
            cutSceneText.maxVisibleCharacters = int.MaxValue;
        }

        if (cutSceneCanvas != null && cutSceneCanvas.gameObject.activeSelf)
        {
            cutSceneCanvas.gameObject.SetActive(false);
        }
    }

    private void StopCutSceneSpeech()
    {
        ResolveCutSceneSpeechAudioSource();
        if (cutSceneSpeechAudioSource != null && cutSceneSpeechAudioSource.isPlaying)
        {
            cutSceneSpeechAudioSource.Stop();
        }
    }

    private void TryPlayDialogueSpeechForElement(int elementIndex)
    {
        if (!playDialogueSpeech)
        {
            return;
        }

        int cueIndex = ResolveDialogueSpeechCueIndexForElement(elementIndex);
        if (cueIndex < 0)
        {
            return;
        }

        int cueStartIndex = ResolveDialogueSpeechCueStartIndex(cueIndex);
        if (cueStartIndex != elementIndex)
        {
            return;
        }

        if (dialogueSpeechClips == null || cueIndex >= dialogueSpeechClips.Length)
        {
            return;
        }

        AudioClip clip = dialogueSpeechClips[cueIndex];
        if (clip == null)
        {
            return;
        }

        ResolveCutSceneSpeechAudioSource();
        if (cutSceneSpeechAudioSource == null)
        {
            return;
        }

        cutSceneSpeechAudioSource.Stop();
        cutSceneSpeechAudioSource.clip = clip;
        cutSceneSpeechAudioSource.time = 0f;
        cutSceneSpeechAudioSource.Play();
    }

    private float ResolveDialogueElementDuration(int elementIndex, int totalElementCount)
    {
        if (dialogueElementDurations != null &&
            elementIndex >= 0 &&
            elementIndex < dialogueElementDurations.Length &&
            dialogueElementDurations[elementIndex] > 0f)
        {
            return dialogueElementDurations[elementIndex];
        }

        return Mathf.Max(0.1f, defaultDialogueElementDuration);
    }

    private IEnumerator PlayTypedDialogueElement(string line, float totalDuration)
    {
        ShowCutSceneDialogue(line);

        if (cutSceneText == null)
        {
            yield return new WaitForSeconds(Mathf.Max(0.05f, totalDuration));
            yield break;
        }

        cutSceneText.maxVisibleCharacters = 0;
        cutSceneText.ForceMeshUpdate();

        int characterCount = cutSceneText.textInfo.characterCount;
        if (characterCount <= 0)
        {
            yield return new WaitForSeconds(Mathf.Max(0.05f, totalDuration));
            yield break;
        }

        float clampedDuration = Mathf.Max(0.05f, totalDuration);
        float baseRevealDuration = clampedDuration * dialogueTypewriterRevealRatio;
        float revealDuration = Mathf.Clamp(
            baseRevealDuration / Mathf.Max(0.01f, dialogueTypewriterSpeedMultiplier),
            dialogueTypewriterMinimumRevealDuration,
            Mathf.Max(0.01f, clampedDuration - dialogueTypewriterMinimumHoldDuration));

        float holdDuration = Mathf.Max(0f, clampedDuration - revealDuration);
        float charactersPerSecond = characterCount / Mathf.Max(0.0001f, revealDuration);
        float revealedCharacters = 0f;

        while (revealedCharacters < characterCount)
        {
            revealedCharacters = Mathf.Min(characterCount, revealedCharacters + charactersPerSecond * Time.deltaTime);
            ApplyTypewriterVisualState(characterCount, revealedCharacters);
            yield return null;
        }

        ApplyTypewriterVisualState(characterCount, characterCount);

        if (holdDuration > 0f)
        {
            yield return new WaitForSeconds(holdDuration);
        }
    }

    private void ApplyTypewriterVisualState(int totalCharacterCount, float revealedCharacters)
    {
        if (cutSceneText == null)
        {
            return;
        }

        int fullyVisibleCount = Mathf.Clamp(Mathf.FloorToInt(revealedCharacters), 0, totalCharacterCount);
        float fractionalReveal = Mathf.Clamp01(revealedCharacters - fullyVisibleCount);
        int currentCharacterIndex = fullyVisibleCount < totalCharacterCount ? fullyVisibleCount : -1;

        cutSceneText.maxVisibleCharacters = currentCharacterIndex >= 0 ? fullyVisibleCount + 1 : totalCharacterCount;

        TMP_TextInfo textInfo = cutSceneText.textInfo;
        for (int i = 0; i < textInfo.characterCount; i++)
        {
            TMP_CharacterInfo characterInfo = textInfo.characterInfo[i];
            if (!characterInfo.isVisible)
            {
                continue;
            }

            byte alpha = i < fullyVisibleCount
                ? byte.MaxValue
                : i == currentCharacterIndex
                    ? (byte)Mathf.Clamp(Mathf.RoundToInt(Mathf.SmoothStep(0f, 255f, fractionalReveal)), 0, 255)
                    : (byte)0;

            int materialIndex = characterInfo.materialReferenceIndex;
            int vertexIndex = characterInfo.vertexIndex;
            Color32[] vertexColors = textInfo.meshInfo[materialIndex].colors32;
            if (vertexColors == null || vertexColors.Length <= vertexIndex + 3)
            {
                continue;
            }

            vertexColors[vertexIndex + 0].a = alpha;
            vertexColors[vertexIndex + 1].a = alpha;
            vertexColors[vertexIndex + 2].a = alpha;
            vertexColors[vertexIndex + 3].a = alpha;
        }

        cutSceneText.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);
    }

    private float ResolveDialogueBossSequenceTriggerDelay(int elementIndex, int totalElementCount)
    {
        float remainingDuration = ResolveDialogueRemainingDurationFromElement(elementIndex, totalElementCount);
        return Mathf.Max(0f, remainingDuration - Mathf.Max(0f, standUpLeadBeforeElementEnd));
    }

    private float ResolveDialogueRemainingDurationFromElement(int elementIndex, int totalElementCount)
    {
        int endIndex = ResolveDialogueTimingGroupEndIndex(elementIndex, totalElementCount);
        if (endIndex < elementIndex)
        {
            return ResolveDialogueElementDuration(elementIndex, totalElementCount);
        }

        float remainingDuration = 0f;
        for (int i = elementIndex; i <= endIndex; i++)
        {
            remainingDuration += ResolveDialogueElementDuration(i, totalElementCount);
        }

        return Mathf.Max(0.1f, remainingDuration);
    }

    private int ResolveDialogueTimingGroupEndIndex(int elementIndex, int totalElementCount)
    {
        if (totalElementCount <= 0)
        {
            return elementIndex;
        }

        int startIndex = elementIndex;
        int endExclusive = totalElementCount;

        if (dialogueTimingGroupStartElementIndices != null &&
            dialogueTimingGroupStartElementIndices.Length > 0)
        {
            for (int i = 0; i < dialogueTimingGroupStartElementIndices.Length; i++)
            {
                int candidateStart = Mathf.Max(0, dialogueTimingGroupStartElementIndices[i]);
                if (candidateStart > elementIndex)
                {
                    endExclusive = Mathf.Min(endExclusive, candidateStart);
                    break;
                }

                startIndex = candidateStart;
            }
        }

        return Mathf.Clamp(endExclusive - 1, startIndex, totalElementCount - 1);
    }

    private float ResolveDialogueSpeechElementDuration(int elementIndex, int totalElementCount)
    {
        int cueIndex = ResolveDialogueSpeechCueIndexForElement(elementIndex);
        if (cueIndex < 0 || dialogueSpeechClips == null || cueIndex >= dialogueSpeechClips.Length)
        {
            return 0f;
        }

        AudioClip clip = dialogueSpeechClips[cueIndex];
        if (clip == null)
        {
            return 0f;
        }

        int cueStartIndex = ResolveDialogueSpeechCueStartIndex(cueIndex);
        int cueEndIndex = ResolveDialogueSpeechCueEndIndex(cueIndex, totalElementCount);
        float totalWeight = 0f;
        float currentWeight = 0f;

        for (int i = cueStartIndex; i <= cueEndIndex; i++)
        {
            float weight = ResolveDialogueSpeechElementWeight(cueIndex, i, cueStartIndex, cueEndIndex);
            totalWeight += weight;

            if (i == elementIndex)
            {
                currentWeight = weight;
            }
        }

        if (totalWeight <= 0f || currentWeight <= 0f)
        {
            int coveredElementCount = Mathf.Max(1, cueEndIndex - cueStartIndex + 1);
            return Mathf.Max(0.1f, clip.length / coveredElementCount);
        }

        return Mathf.Max(0.1f, clip.length * (currentWeight / totalWeight));
    }

    private float ResolveDialogueSpeechElementWeight(int cueIndex, int elementIndex, int cueStartIndex, int cueEndIndex)
    {
        int coveredElementCount = cueEndIndex - cueStartIndex + 1;
        if (coveredElementCount == 2 &&
            dialogueSpeechClips != null &&
            cueIndex == dialogueSpeechClips.Length - 1)
        {
            if (elementIndex == cueStartIndex)
            {
                return Mathf.Max(0.01f, finalDialoguePenultimateWeight);
            }

            if (elementIndex == cueEndIndex)
            {
                return Mathf.Max(0.01f, finalDialogueLastWeight);
            }
        }

        return 1f;
    }

    private int ResolveDialogueSpeechCueIndexForElement(int elementIndex)
    {
        if (dialogueSpeechClips == null || dialogueSpeechClips.Length == 0 || elementIndex < 0)
        {
            return -1;
        }

        int resolvedCueIndex = -1;
        for (int i = 0; i < dialogueSpeechClips.Length; i++)
        {
            int cueStartIndex = ResolveDialogueSpeechCueStartIndex(i);
            if (cueStartIndex > elementIndex)
            {
                break;
            }

            resolvedCueIndex = i;
        }

        return resolvedCueIndex;
    }

    private int ResolveDialogueSpeechCueStartIndex(int cueIndex)
    {
        if (dialogueSpeechStartElementIndices != null &&
            cueIndex >= 0 &&
            cueIndex < dialogueSpeechStartElementIndices.Length)
        {
            return Mathf.Max(0, dialogueSpeechStartElementIndices[cueIndex]);
        }

        return Mathf.Max(0, cueIndex);
    }

    private int ResolveDialogueSpeechCueEndIndex(int cueIndex, int totalElementCount)
    {
        if (totalElementCount <= 0)
        {
            return 0;
        }

        int cueStartIndex = ResolveDialogueSpeechCueStartIndex(cueIndex);
        int endExclusive = totalElementCount;

        for (int i = cueIndex + 1; i < dialogueSpeechClips.Length; i++)
        {
            int nextCueStartIndex = ResolveDialogueSpeechCueStartIndex(i);
            if (nextCueStartIndex <= cueStartIndex)
            {
                continue;
            }

            endExclusive = Mathf.Min(totalElementCount, nextCueStartIndex);
            break;
        }

        return Mathf.Clamp(endExclusive - 1, cueStartIndex, totalElementCount - 1);
    }

    private void PlayDialogueBossAnimation(string stateName, float animationSpeed, bool useRotationRootMotion)
    {
        if (boss == null || string.IsNullOrWhiteSpace(stateName))
        {
            return;
        }

        bool useIntroRootMotion = boss.ShouldUseIntroRootMotion();
        boss.SetRootMotionMode(useIntroRootMotion, useIntroRootMotion && useRotationRootMotion);
        boss.SetAnimSpeed(Mathf.Max(0.01f, animationSpeed));
        boss.PlayAnim(stateName, bossIntroCrossFade);
    }

    private IEnumerator WaitForBossAnimationCompletion(string stateName, float timeout, float completionNormalized)
    {
        if (boss == null || boss.Anim == null || string.IsNullOrWhiteSpace(stateName))
        {
            yield break;
        }

        Animator animator = boss.Anim;
        int stateHash = Animator.StringToHash(stateName);
        if (!animator.HasState(0, stateHash))
        {
            yield break;
        }

        float elapsed = 0f;
        bool hasObservedTargetState = false;

        while (elapsed < timeout)
        {
            AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
            if (stateInfo.IsName(stateName))
            {
                hasObservedTargetState = true;
                if (!animator.IsInTransition(0) && stateInfo.normalizedTime >= completionNormalized)
                {
                    yield break;
                }
            }
            else if (hasObservedTargetState && !animator.IsInTransition(0))
            {
                yield break;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }
    }
}
