using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public sealed class InjuredKnightQuestItemInteractable : MonoBehaviour, IPlayerInteractable
{
    [Header("Interaction")]
    [SerializeField] private KeyCode interactionKey = KeyCode.E;
    [SerializeField] private int interactionPriority = 20;
    [SerializeField] private string interactionLabel = "\uC90D\uAE30";

    [Header("Item Panel")]
    [SerializeField] private string itemDisplayName = "\uB0A1\uC740 \uC885\uCD94";
    [SerializeField] private string itemActionLabel = "\uD68D\uB4DD";
    [SerializeField] private Sprite itemIcon;

    [Header("Quest")]
    [SerializeField] private string pickupNotice = "\uC885\uC758 \uCD94\uB97C \uC190\uC5D0 \uB123\uC5C8\uB2E4.";
    [SerializeField] private bool markGoalKnownOnCollect = true;
    [SerializeField] private GameObject disableTargetOnCollect;

    [Header("Player Interaction Animation")]
    [SerializeField] [Min(0.05f)] private float pickupStateEnterTimeout = 0.2f;
    [SerializeField] [Min(0.05f)] private float pickupAnimationTimeout = 1.2f;

    [Header("Soul Visual")]
    [SerializeField] private bool enableSoulVisuals = true;
    [SerializeField] private Transform hiddenItemMeshRoot;
    [SerializeField] private Material soulBillboardMaterial;
    [SerializeField] private Vector3 visualAnchorLocalOffset = new Vector3(0f, 0.85f, 0f);
    [SerializeField] private float bobAmplitude = 0.12f;
    [SerializeField] private float bobFrequency = 1.6f;
    [SerializeField] private float pulseFrequency = 2.1f;
    [SerializeField] private float coreSize = 0.72f;
    [SerializeField] private float haloSize = 1.15f;
    [SerializeField] private bool useGroundAuraQuad = true;
    [SerializeField] private float groundAuraSize = 1.9f;
    [SerializeField] private Color soulCoreColor = new Color(0.96f, 0.97f, 1f, 0.88f);
    [SerializeField] private Color soulHaloColor = new Color(0.54f, 0.74f, 1f, 0.52f);
    [SerializeField] private Color soulGroundColor = new Color(0.44f, 0.62f, 1f, 0.34f);
    [SerializeField] private float pointLightIntensity = 1.6f;
    [SerializeField] private float pointLightRange = 4.8f;

    private PlayerController _interactingPlayer;
    private PlayerController _pickupSequencePlayer;
    private Transform _soulVisualRoot;
    private Transform _coreQuadTransform;
    private Transform _haloQuadTransform;
    private Transform _groundAuraTransform;
    private MeshRenderer _coreRenderer;
    private MeshRenderer _haloRenderer;
    private MeshRenderer _groundRenderer;
    private Light _soulLight;
    private MaterialPropertyBlock _corePropertyBlock;
    private MaterialPropertyBlock _haloPropertyBlock;
    private MaterialPropertyBlock _groundPropertyBlock;
    private bool _soulVisualCreated;
    private Coroutine _pickupSequence;

    public bool RequiresManualInteraction => true;
    public Object PromptOwner => this;
    public Transform InteractionTransform => transform;

    private void Awake()
    {
        ResolveVisualReferences();
    }

    private void OnEnable()
    {
        PlayerInteractableRegistry.Register(this);
    }

    private void Reset()
    {
        Collider triggerCollider = GetComponent<Collider>();
        if (triggerCollider != null)
        {
            triggerCollider.isTrigger = true;
        }

        if (disableTargetOnCollect == null)
        {
            disableTargetOnCollect = gameObject;
        }

        ResolveVisualReferences();
    }

    private void Start()
    {
        if (IsAlreadyCollected())
        {
            DisableCollectedObject();
            return;
        }

        EnsureSoulVisuals();
    }

    private void Update()
    {
        if (!_soulVisualCreated || _soulVisualRoot == null)
        {
            return;
        }

        float time = Time.time;
        float bobOffset = Mathf.Sin(time * bobFrequency) * bobAmplitude;
        float pulse = 0.5f + 0.5f * Mathf.Sin(time * pulseFrequency);

        _soulVisualRoot.localPosition = visualAnchorLocalOffset + Vector3.up * bobOffset;

        if (_coreQuadTransform != null)
        {
            float scale = coreSize * Mathf.Lerp(0.92f, 1.08f, pulse);
            _coreQuadTransform.localScale = new Vector3(scale, scale, 1f);
        }

        if (_haloQuadTransform != null)
        {
            float scale = haloSize * Mathf.Lerp(0.96f, 1.18f, pulse);
            _haloQuadTransform.localScale = new Vector3(scale, scale, 1f);
        }

        if (useGroundAuraQuad && _groundAuraTransform != null)
        {
            float scale = groundAuraSize * Mathf.Lerp(0.95f, 1.08f, pulse);
            _groundAuraTransform.localScale = new Vector3(scale, scale, 1f);
        }

        ApplySoulRendererColor(
            _coreRenderer,
            _corePropertyBlock,
            ScaleColor(soulCoreColor, Mathf.Lerp(1.05f, 1.28f, pulse), Mathf.Lerp(0.92f, 1f, pulse)));
        ApplySoulRendererColor(
            _haloRenderer,
            _haloPropertyBlock,
            ScaleColor(soulHaloColor, Mathf.Lerp(0.88f, 1.18f, pulse), Mathf.Lerp(0.85f, 1f, pulse)));
        if (useGroundAuraQuad)
        {
            ApplySoulRendererColor(
                _groundRenderer,
                _groundPropertyBlock,
                ScaleColor(soulGroundColor, Mathf.Lerp(0.84f, 1.04f, pulse), Mathf.Lerp(0.82f, 0.98f, pulse)));
        }

        if (_soulLight != null)
        {
            _soulLight.color = Color.Lerp(soulHaloColor, soulCoreColor, 0.62f);
            _soulLight.intensity = pointLightIntensity * Mathf.Lerp(0.88f, 1.2f, pulse);
            _soulLight.range = pointLightRange * Mathf.Lerp(0.96f, 1.08f, pulse);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (IsAlreadyCollected())
        {
            SyncCollectedState();
            return;
        }

        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player == null)
        {
            return;
        }

        _interactingPlayer = player;
        player.Interaction?.RegisterCandidate(this);
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
        CancelPickupSequence();

        if (_interactingPlayer != null)
        {
            _interactingPlayer.Interaction?.UnregisterCandidate(this);
            _interactingPlayer = null;
        }
    }

    public bool CanInteract(PlayerController player)
    {
        return isActiveAndEnabled &&
               player != null &&
               !player.IsDead &&
               !SimpleDialogueManager.IsOpen &&
               !UIItemPickupPanel.IsOpen &&
               !IsAlreadyCollected() &&
               (interactionKey == KeyCode.None || interactionKey == player.InteractionKey);
    }

    public int GetInteractionPriority(PlayerController player)
    {
        return interactionPriority;
    }

    public string GetInteractionLabel(PlayerController player)
    {
        return string.IsNullOrWhiteSpace(interactionLabel) ? "\uC90D\uAE30" : interactionLabel;
    }

    public void Interact(PlayerController player)
    {
        if (!CanInteract(player) || _pickupSequence != null)
        {
            return;
        }

        _interactingPlayer = player;
        UIInteractionPrompt.Hide(this);
        _pickupSequence = StartCoroutine(Co_PlayPickupAnimationThenShowPanel(player));
    }

    private IEnumerator Co_PlayPickupAnimationThenShowPanel(PlayerController player)
    {
        bool shouldShowPickupPanel = false;
        _pickupSequencePlayer = player;

        if (_pickupSequencePlayer != null)
        {
            _pickupSequencePlayer.BeginExternalInteractionLock();
        }

        try
        {
            if (player != null && PlayerInteractionAnimationSequence.TryStart(player))
            {
                yield return PlayerInteractionAnimationSequence.WaitForCompletion(
                    player,
                    pickupStateEnterTimeout,
                    pickupAnimationTimeout);
            }

            shouldShowPickupPanel = player != null && !IsAlreadyCollected();
        }
        finally
        {
            if (_pickupSequencePlayer != null)
            {
                _pickupSequencePlayer.EndExternalInteractionLock();
            }

            _pickupSequencePlayer = null;
            _pickupSequence = null;
        }

        if (shouldShowPickupPanel)
        {
            ShowPickupPanel(player);
        }
    }

    private void ShowPickupPanel(PlayerController player)
    {
        KeyCode confirmKey = interactionKey == KeyCode.None && player != null
            ? player.InteractionKey
            : interactionKey;

        bool panelShown = UIItemPickupPanel.TryShow(
            player,
            itemDisplayName,
            confirmKey,
            ConfirmCollectFromPanel,
            itemActionLabel,
            itemIcon);

        if (!panelShown)
        {
            ConfirmCollectFromPanel();
        }
    }

    private void ConfirmCollectFromPanel()
    {
        QuestProgressionManager progression = QuestProgressionManager.Instance;
        if (progression == null)
        {
            return;
        }

        bool collected = progression.MarkInjuredKnightQuestItemCollected(markGoalKnownOnCollect);
        if (!collected)
        {
            SyncCollectedState();
            return;
        }

        if (!string.IsNullOrWhiteSpace(pickupNotice))
        {
            UIInteractionPrompt.ShowNotice(pickupNotice);
        }

        DisableCollectedObject();
    }

    private void ResolveVisualReferences()
    {
        if (hiddenItemMeshRoot == null)
        {
            Transform meshRoot = transform.Find("Bell_Pepper");
            if (meshRoot != null)
            {
                hiddenItemMeshRoot = meshRoot;
            }
        }
    }

    private void EnsureSoulVisuals()
    {
        if (!enableSoulVisuals || _soulVisualCreated)
        {
            return;
        }

        ResolveVisualReferences();
        if (hiddenItemMeshRoot != null)
        {
            hiddenItemMeshRoot.gameObject.SetActive(false);
        }

        if (soulBillboardMaterial == null)
        {
            Debug.LogWarning("[InjuredKnightQuestItemInteractable] Soul billboard material is missing.", this);
            return;
        }

        GameObject rootObject = new GameObject("SoulVisualRoot");
        rootObject.layer = gameObject.layer;
        _soulVisualRoot = rootObject.transform;
        _soulVisualRoot.SetParent(transform, false);
        _soulVisualRoot.localPosition = visualAnchorLocalOffset;

        _coreRenderer = CreateSoulQuad(
            "SoulCore",
            new Vector3(0f, 0.02f, 0f),
            Quaternion.identity,
            coreSize,
            billboard: true,
            out _coreQuadTransform);

        _haloRenderer = CreateSoulQuad(
            "SoulHalo",
            new Vector3(0f, 0.09f, 0.02f),
            Quaternion.identity,
            haloSize,
            billboard: true,
            out _haloQuadTransform);

        if (useGroundAuraQuad)
        {
            _groundRenderer = CreateSoulQuad(
                "SoulGroundAura",
                new Vector3(0f, -0.78f, 0f),
                Quaternion.Euler(90f, 0f, 0f),
                groundAuraSize,
                billboard: false,
                out _groundAuraTransform);
        }

        GameObject lightObject = new GameObject("SoulLight");
        lightObject.layer = gameObject.layer;
        lightObject.transform.SetParent(_soulVisualRoot, false);
        lightObject.transform.localPosition = new Vector3(0f, 0.05f, 0f);
        _soulLight = lightObject.AddComponent<Light>();
        _soulLight.type = LightType.Point;
        _soulLight.color = Color.Lerp(soulHaloColor, soulCoreColor, 0.62f);
        _soulLight.intensity = pointLightIntensity;
        _soulLight.range = pointLightRange;
        _soulLight.shadows = LightShadows.None;

        _corePropertyBlock = new MaterialPropertyBlock();
        _haloPropertyBlock = new MaterialPropertyBlock();
        _groundPropertyBlock = new MaterialPropertyBlock();
        _soulVisualCreated = true;
    }

    private MeshRenderer CreateSoulQuad(
        string objectName,
        Vector3 localPosition,
        Quaternion localRotation,
        float size,
        bool billboard,
        out Transform quadTransform)
    {
        GameObject quadObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quadObject.name = objectName;
        quadObject.layer = gameObject.layer;
        quadTransform = quadObject.transform;
        quadTransform.SetParent(_soulVisualRoot, false);
        quadTransform.localPosition = localPosition;
        quadTransform.localRotation = localRotation;
        quadTransform.localScale = new Vector3(size, size, 1f);

        Collider quadCollider = quadObject.GetComponent<Collider>();
        if (quadCollider != null)
        {
            Destroy(quadCollider);
        }

        if (billboard)
        {
            Billboard billboardComponent = quadObject.AddComponent<Billboard>();
            if (billboardComponent != null)
            {
                quadObject.transform.localRotation = Quaternion.identity;
            }
        }

        MeshRenderer renderer = quadObject.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = soulBillboardMaterial;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        return renderer;
    }

    private void ApplySoulRendererColor(MeshRenderer renderer, MaterialPropertyBlock propertyBlock, Color color)
    {
        if (renderer == null || propertyBlock == null)
        {
            return;
        }

        propertyBlock.Clear();
        propertyBlock.SetColor("_BaseColor", color);
        propertyBlock.SetColor("_Color", color);
        propertyBlock.SetColor("_EmissionColor", ScaleColor(color, 1.35f, 1f));
        renderer.SetPropertyBlock(propertyBlock);
    }

    private static Color ScaleColor(Color source, float brightness, float alphaMultiplier)
    {
        return new Color(
            Mathf.Clamp01(source.r * brightness),
            Mathf.Clamp01(source.g * brightness),
            Mathf.Clamp01(source.b * brightness),
            Mathf.Clamp01(source.a * alphaMultiplier));
    }

    private bool IsAlreadyCollected()
    {
        QuestProgressionManager progression = QuestProgressionManager.Instance;
        return progression != null && progression.InjuredKnightQuestItemCollected;
    }

    private void SyncCollectedState()
    {
        if (IsAlreadyCollected())
        {
            DisableCollectedObject();
        }
    }

    private void DisableCollectedObject()
    {
        if (_interactingPlayer != null)
        {
            _interactingPlayer.Interaction?.UnregisterCandidate(this);
            _interactingPlayer = null;
        }

        Collider triggerCollider = GetComponent<Collider>();
        if (triggerCollider != null)
        {
            triggerCollider.enabled = false;
        }

        GameObject target = disableTargetOnCollect != null ? disableTargetOnCollect : gameObject;
        if (target != null && target.activeSelf)
        {
            target.SetActive(false);
        }
    }

    private void CancelPickupSequence()
    {
        if (_pickupSequence != null)
        {
            StopCoroutine(_pickupSequence);
            _pickupSequence = null;
        }

        if (_pickupSequencePlayer != null)
        {
            _pickupSequencePlayer.EndExternalInteractionLock();
            _pickupSequencePlayer = null;
        }
    }
}

[RequireComponent(typeof(Collider))]
public sealed class BossCorpseQuestItemInteractable : MonoBehaviour, IPlayerInteractable
{
    [Header("Interaction")]
    [SerializeField] private KeyCode interactionKey = KeyCode.E;
    [SerializeField] private int interactionPriority = 25;
    [SerializeField] private string interactionLabel = "줍기";

    [Header("Item Panel")]
    [SerializeField] private string itemDisplayName = "낡은 종추";
    [SerializeField] private string itemActionLabel = "획득";
    [SerializeField] private Sprite itemIcon;

    [Header("Quest")]
    [SerializeField] private string pickupNotice = "종의 추를 손에 넣었다.";
    [SerializeField] private bool markGoalKnownOnCollect = true;
    [SerializeField] private GameObject disableTargetOnCollect;

    [Header("Player Interaction Animation")]
    [SerializeField] [Min(0.05f)] private float pickupStateEnterTimeout = 0.2f;
    [SerializeField] [Min(0.05f)] private float pickupAnimationTimeout = 1.2f;

    [Header("Soul Visual")]
    [SerializeField] private bool enableSoulVisuals = true;
    [SerializeField] private Material soulBillboardMaterial;
    [SerializeField] private Vector3 visualAnchorLocalOffset = new Vector3(0f, 0.18f, 0f);
    [SerializeField] private float bobAmplitude = 0.04f;
    [SerializeField] private float bobFrequency = 1.35f;
    [SerializeField] private float pulseFrequency = 1.8f;
    [SerializeField] private float coreSize = 0.9f;
    [SerializeField] private float haloSize = 1.25f;
    [SerializeField] private bool useGroundAuraQuad = false;
    [SerializeField] private float groundAuraSize = 1.6f;
    [SerializeField] private Color soulCoreColor = new Color(1f, 1f, 1f, 0.94f);
    [SerializeField] private Color soulHaloColor = new Color(0.78f, 0.9f, 1f, 0.46f);
    [SerializeField] private Color soulGroundColor = new Color(0.44f, 0.62f, 1f, 0.34f);
    [SerializeField] private float pointLightIntensity = 1f;
    [SerializeField] private float pointLightRange = 4.4f;

    private Collider _triggerCollider;
    private PlayerController _interactingPlayer;
    private PlayerController _pickupSequencePlayer;
    private SoulPickupVisual _soulVisual;
    private Coroutine _pickupSequence;

    public bool RequiresManualInteraction => true;
    public Object PromptOwner => this;
    public Transform InteractionTransform => transform;
    public bool HasPendingLoot => !IsAlreadyCollected();

    private void Awake()
    {
        EnsureTriggerSetup();
    }

    private void Reset()
    {
        EnsureTriggerSetup();
        if (disableTargetOnCollect == null)
        {
            disableTargetOnCollect = gameObject;
        }
    }

    private void OnEnable()
    {
        PlayerInteractableRegistry.Register(this);
        EnsureTriggerSetup();
        if (IsAlreadyCollected())
        {
            DisableCollectedObject();
        }
    }

    private void OnDisable()
    {
        PlayerInteractableRegistry.Unregister(this);
        CancelPickupSequence();

        if (_interactingPlayer != null)
        {
            _interactingPlayer.Interaction?.UnregisterCandidate(this);
            _interactingPlayer = null;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (IsAlreadyCollected())
        {
            DisableCollectedObject();
            return;
        }

        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player == null)
        {
            return;
        }

        _interactingPlayer = player;
        player.Interaction?.RegisterCandidate(this);
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

    public void ActivateLoot()
    {
        if (IsAlreadyCollected())
        {
            DisableCollectedObject();
            return;
        }

        gameObject.SetActive(true);
        enabled = true;

        if (_triggerCollider != null)
        {
            _triggerCollider.enabled = true;
        }

        EnsureSoulVisuals();
    }

    public void ConfigureSoulVisuals(Material billboardMaterial)
    {
        soulBillboardMaterial = billboardMaterial;

        if (isActiveAndEnabled && gameObject.activeInHierarchy && !IsAlreadyCollected())
        {
            EnsureSoulVisuals();
        }
    }

    public bool CanInteract(PlayerController player)
    {
        return isActiveAndEnabled &&
               player != null &&
               !player.IsDead &&
               !SimpleDialogueManager.IsOpen &&
               !UIItemPickupPanel.IsOpen &&
               !IsAlreadyCollected() &&
               (interactionKey == KeyCode.None || interactionKey == player.InteractionKey);
    }

    public int GetInteractionPriority(PlayerController player)
    {
        return interactionPriority;
    }

    public string GetInteractionLabel(PlayerController player)
    {
        return string.IsNullOrWhiteSpace(interactionLabel) ? "줍기" : interactionLabel;
    }

    public void Interact(PlayerController player)
    {
        if (!CanInteract(player) || _pickupSequence != null)
        {
            return;
        }

        _interactingPlayer = player;
        UIInteractionPrompt.Hide(this);
        _pickupSequence = StartCoroutine(Co_PlayPickupAnimationThenShowPanel(player));
    }

    private IEnumerator Co_PlayPickupAnimationThenShowPanel(PlayerController player)
    {
        bool shouldShowPickupPanel = false;
        _pickupSequencePlayer = player;

        if (_pickupSequencePlayer != null)
        {
            _pickupSequencePlayer.BeginExternalInteractionLock();
        }

        try
        {
            if (player != null && PlayerInteractionAnimationSequence.TryStart(player))
            {
                yield return PlayerInteractionAnimationSequence.WaitForCompletion(
                    player,
                    pickupStateEnterTimeout,
                    pickupAnimationTimeout);
            }

            shouldShowPickupPanel = player != null && !IsAlreadyCollected();
        }
        finally
        {
            if (_pickupSequencePlayer != null)
            {
                _pickupSequencePlayer.EndExternalInteractionLock();
            }

            _pickupSequencePlayer = null;
            _pickupSequence = null;
        }

        if (shouldShowPickupPanel)
        {
            ShowPickupPanel(player);
        }
    }

    private void ShowPickupPanel(PlayerController player)
    {
        KeyCode confirmKey = interactionKey == KeyCode.None && player != null
            ? player.InteractionKey
            : interactionKey;

        bool panelShown = UIItemPickupPanel.TryShow(
            player,
            itemDisplayName,
            confirmKey,
            ConfirmCollectFromPanel,
            itemActionLabel,
            itemIcon);

        if (!panelShown)
        {
            ConfirmCollectFromPanel();
        }
    }

    private void ConfirmCollectFromPanel()
    {
        QuestProgressionManager progression = QuestProgressionManager.Instance;
        if (progression == null)
        {
            return;
        }

        bool collected = progression.MarkInjuredKnightQuestItemCollected(markGoalKnownOnCollect);
        if (!collected)
        {
            DisableCollectedObject();
            return;
        }

        if (!string.IsNullOrWhiteSpace(pickupNotice))
        {
            UIInteractionPrompt.ShowNotice(pickupNotice);
        }

        DisableCollectedObject();
    }

    private bool IsAlreadyCollected()
    {
        QuestProgressionManager progression = QuestProgressionManager.Instance;
        return progression != null && progression.InjuredKnightQuestItemCollected;
    }

    private void DisableCollectedObject()
    {
        if (_interactingPlayer != null)
        {
            _interactingPlayer.Interaction?.UnregisterCandidate(this);
            _interactingPlayer = null;
        }

        GameObject targetToDisable = disableTargetOnCollect != null ? disableTargetOnCollect : gameObject;
        if (targetToDisable != null && targetToDisable.activeSelf)
        {
            targetToDisable.SetActive(false);
        }
    }

    private void EnsureTriggerSetup()
    {
        _triggerCollider = GetComponent<Collider>();
        if (_triggerCollider != null)
        {
            _triggerCollider.isTrigger = true;
        }
    }

    private void EnsureSoulVisuals()
    {
        if (!enableSoulVisuals || soulBillboardMaterial == null)
        {
            return;
        }

        if (_soulVisual == null)
        {
            _soulVisual = GetComponent<SoulPickupVisual>();
            if (_soulVisual == null)
            {
                _soulVisual = gameObject.AddComponent<SoulPickupVisual>();
            }
        }

        _soulVisual.Configure(
            soulBillboardMaterial,
            visualAnchorLocalOffset,
            bobAmplitude,
            bobFrequency,
            pulseFrequency,
            coreSize,
            haloSize,
            useGroundAuraQuad,
            groundAuraSize,
            soulCoreColor,
            soulHaloColor,
            soulGroundColor,
            pointLightIntensity,
            pointLightRange);
    }

    private void CancelPickupSequence()
    {
        if (_pickupSequence != null)
        {
            StopCoroutine(_pickupSequence);
            _pickupSequence = null;
        }

        if (_pickupSequencePlayer != null)
        {
            _pickupSequencePlayer.EndExternalInteractionLock();
            _pickupSequencePlayer = null;
        }
    }
}
