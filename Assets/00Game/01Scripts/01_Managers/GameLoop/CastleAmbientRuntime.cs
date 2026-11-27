using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class CastleAmbientRuntime : MonoBehaviour
{
    private const string RuntimeObjectName = "CastleAmbientRuntime";
    private const string FogWallObjectName = "FX_Environment_FogWall_Entrance_Loop";

    private const float WindVolume = 0.21f;
    private const float BonfireVolume = 0.65f;
    private const float FogWallVolume = 0.5f;
    private const float ResolveInterval = 1f;

    private static CastleAmbientRuntime _instance;
    private static bool _sceneHookRegistered;

    private AudioSource _windSource;
    private AudioSource _bonfireSource;
    private AudioSource _fogWallSource;

    private BonfireCheckpoint _bonfireCheckpoint;
    private Transform _fogWallTransform;
    private float _nextResolveTime;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        if (_sceneHookRegistered)
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            _sceneHookRegistered = false;
        }

        _instance = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (!_sceneHookRegistered)
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
            _sceneHookRegistered = true;
        }

        EnsureForScene(SceneManager.GetActiveScene());
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode _)
    {
        EnsureForScene(scene);
    }

    private static void EnsureForScene(Scene scene)
    {
        bool isCastleScene = string.Equals(
            TitleFlowConstants.NormalizeSceneName(scene.name),
            TitleFlowConstants.GameSceneName,
            StringComparison.Ordinal);

        if (!isCastleScene)
        {
            if (_instance != null)
            {
                Destroy(_instance.gameObject);
                _instance = null;
            }

            return;
        }

        if (_instance != null && _instance.gameObject.scene == scene)
        {
            return;
        }

        if (_instance != null)
        {
            Destroy(_instance.gameObject);
            _instance = null;
        }

        GameObject runtimeObject = new GameObject(RuntimeObjectName);
        SceneManager.MoveGameObjectToScene(runtimeObject, scene);
        _instance = runtimeObject.AddComponent<CastleAmbientRuntime>();
    }

    private void Awake()
    {
        _windSource = CreateSource("WindSource", spatialBlend: 0f, minDistance: 0f, maxDistance: 0f);
        _bonfireSource = CreateSource("BonfireSource", spatialBlend: 1f, minDistance: 3f, maxDistance: 16f);
        _fogWallSource = CreateSource("FogWallSource", spatialBlend: 1f, minDistance: 4f, maxDistance: 22f);

        _windSource.clip = GameAudioResources.LoadClip(GameAudioCatalog.Ambient.CastleWindLoop);
        _bonfireSource.clip = GameAudioResources.LoadClip(GameAudioCatalog.Ambient.BonfireLoop);
        _fogWallSource.clip = GameAudioResources.LoadClip(GameAudioCatalog.Ambient.FogWallLoop);

        _windSource.volume = WindVolume;
        _bonfireSource.volume = BonfireVolume;
        _fogWallSource.volume = FogWallVolume;

        if (_windSource.clip != null)
        {
            _windSource.Play();
        }

        ResolveTargets(force: true);
    }

    private void Update()
    {
        if (Time.unscaledTime >= _nextResolveTime)
        {
            ResolveTargets(force: false);
        }

        UpdateBonfireAmbient();
        UpdateFogWallAmbient();
    }

    private void OnDestroy()
    {
        if (_instance == this)
        {
            _instance = null;
        }
    }

    private void ResolveTargets(bool force)
    {
        if (force || _bonfireCheckpoint == null)
        {
            _bonfireCheckpoint = FindFirstObjectByType<BonfireCheckpoint>(FindObjectsInactive.Include);
        }

        if (force || _fogWallTransform == null)
        {
            _fogWallTransform = FindNamedTransform(FogWallObjectName);
        }

        _nextResolveTime = Time.unscaledTime + ResolveInterval;
    }

    private void UpdateBonfireAmbient()
    {
        if (_bonfireCheckpoint == null || _bonfireSource == null)
        {
            StopIfPlaying(_bonfireSource);
            return;
        }

        _bonfireSource.transform.position = _bonfireCheckpoint.transform.position;

        if (_bonfireCheckpoint.IsActivated)
        {
            PlayIfStopped(_bonfireSource);
        }
        else
        {
            StopIfPlaying(_bonfireSource);
        }
    }

    private void UpdateFogWallAmbient()
    {
        if (_fogWallTransform == null || _fogWallSource == null)
        {
            StopIfPlaying(_fogWallSource);
            return;
        }

        _fogWallSource.transform.position = _fogWallTransform.position;

        if (_fogWallTransform.gameObject.activeInHierarchy)
        {
            PlayIfStopped(_fogWallSource);
        }
        else
        {
            StopIfPlaying(_fogWallSource);
        }
    }

    private AudioSource CreateSource(string childName, float spatialBlend, float minDistance, float maxDistance)
    {
        GameObject child = new GameObject(childName);
        child.transform.SetParent(transform, false);

        AudioSource source = child.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = true;
        source.spatialBlend = spatialBlend;
        source.minDistance = minDistance;
        source.maxDistance = maxDistance;
        source.rolloffMode = spatialBlend > 0f ? AudioRolloffMode.Linear : AudioRolloffMode.Logarithmic;
        source.dopplerLevel = 0f;
        source.pitch = 1f;
        return source;
    }

    private static void PlayIfStopped(AudioSource source)
    {
        if (source != null && source.clip != null && !source.isPlaying)
        {
            source.Play();
        }
    }

    private static void StopIfPlaying(AudioSource source)
    {
        if (source != null && source.isPlaying)
        {
            source.Stop();
        }
    }

    private static Transform FindNamedTransform(string targetName)
    {
        if (string.IsNullOrWhiteSpace(targetName))
        {
            return null;
        }

        Transform[] transforms = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform candidate = transforms[i];
            if (candidate != null && string.Equals(candidate.name, targetName, StringComparison.Ordinal))
            {
                return candidate;
            }
        }

        return null;
    }
}
