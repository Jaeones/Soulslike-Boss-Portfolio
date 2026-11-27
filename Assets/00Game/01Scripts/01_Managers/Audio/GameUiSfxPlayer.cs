using System.Collections.Generic;
using UnityEngine;

public enum UiSfxId
{
    Move,
    Confirm,
    Back,
    PauseOpen,
    PauseClose,
    PromptAppear,
    DialogueAdvance,
    ItemPickupOpen,
    AreaTitleReveal,
    YouDiedStinger,
    CheckpointRetry
}

public sealed class GameUiSfxPlayer : MonoBehaviour
{
    private const string RuntimeObjectName = "GameUiSfxPlayer";

    private static readonly Dictionary<UiSfxId, string> ResourcePaths = new Dictionary<UiSfxId, string>
    {
        { UiSfxId.Move, GameAudioCatalog.Ui.Move },
        { UiSfxId.Confirm, GameAudioCatalog.Ui.Confirm },
        { UiSfxId.Back, GameAudioCatalog.Ui.Back },
        { UiSfxId.PauseOpen, GameAudioCatalog.Ui.PauseOpen },
        { UiSfxId.PauseClose, GameAudioCatalog.Ui.PauseClose },
        { UiSfxId.PromptAppear, GameAudioCatalog.Ui.PromptAppear },
        { UiSfxId.DialogueAdvance, GameAudioCatalog.Ui.DialogueAdvance },
        { UiSfxId.ItemPickupOpen, GameAudioCatalog.Ui.ItemPickupOpen },
        { UiSfxId.AreaTitleReveal, GameAudioCatalog.Ui.AreaTitleReveal },
        { UiSfxId.YouDiedStinger, GameAudioCatalog.Ui.YouDiedStinger },
        { UiSfxId.CheckpointRetry, GameAudioCatalog.Ui.CheckpointRetry }
    };

    private static GameUiSfxPlayer _instance;

    private readonly Dictionary<UiSfxId, AudioClip> _clipCache = new Dictionary<UiSfxId, AudioClip>();
    private AudioSource _audioSource;

    public static bool Play(UiSfxId id, float volumeScale = 1f)
    {
        GameUiSfxPlayer instance = EnsureInstance();
        if (instance == null)
        {
            return false;
        }

        AudioClip clip = instance.GetClip(id);
        if (clip == null || instance._audioSource == null)
        {
            return false;
        }

        instance._audioSource.pitch = 1f;
        instance._audioSource.PlayOneShot(clip, Mathf.Clamp01(instance.GetDefaultVolume(id) * volumeScale));
        return true;
    }

    private static GameUiSfxPlayer EnsureInstance()
    {
        if (_instance != null)
        {
            return _instance;
        }

        _instance = FindFirstObjectByType<GameUiSfxPlayer>(FindObjectsInactive.Include);
        if (_instance != null)
        {
            _instance.EnsureAudioSource();
            return _instance;
        }

        GameObject runtimeObject = new GameObject(RuntimeObjectName);
        DontDestroyOnLoad(runtimeObject);
        _instance = runtimeObject.AddComponent<GameUiSfxPlayer>();
        _instance.EnsureAudioSource();
        return _instance;
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);
        EnsureAudioSource();
    }

    private AudioClip GetClip(UiSfxId id)
    {
        if (_clipCache.TryGetValue(id, out AudioClip clip))
        {
            return clip;
        }

        if (!ResourcePaths.TryGetValue(id, out string resourcePath))
        {
            return null;
        }

        clip = GameAudioResources.LoadClip(resourcePath);
        _clipCache[id] = clip;
        return clip;
    }

    private float GetDefaultVolume(UiSfxId id)
    {
        switch (id)
        {
            case UiSfxId.Move:
                return 0.72f;
            case UiSfxId.Confirm:
            case UiSfxId.Back:
                return 0.8f;
            case UiSfxId.PauseOpen:
            case UiSfxId.PauseClose:
            case UiSfxId.PromptAppear:
            case UiSfxId.DialogueAdvance:
                return 0.82f;
            case UiSfxId.ItemPickupOpen:
            case UiSfxId.AreaTitleReveal:
                return 0.88f;
            case UiSfxId.YouDiedStinger:
                return 1f;
            case UiSfxId.CheckpointRetry:
                return 0.92f;
            default:
                return 0.85f;
        }
    }

    private void EnsureAudioSource()
    {
        if (_audioSource == null)
        {
            _audioSource = GetComponent<AudioSource>();
        }

        if (_audioSource == null)
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
        }

        _audioSource.playOnAwake = false;
        _audioSource.loop = false;
        _audioSource.spatialBlend = 0f;
    }
}
