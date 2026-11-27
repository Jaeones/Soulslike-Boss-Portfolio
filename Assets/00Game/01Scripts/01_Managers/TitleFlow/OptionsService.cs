using System.Collections.Generic;
using UnityEngine;

public sealed class OptionsService : Singleton<OptionsService>
{
    private const string MasterVolumeKey = "options.masterVolume";
    private const string QualityLevelKey = "options.qualityLevel";
    private const string ResolutionIndexKey = "options.resolutionIndex";
    private const string FullScreenKey = "options.fullScreen";

    private Resolution[] _resolutionOptions = { };

    public float MasterVolume { get; private set; } = 1f;
    public int QualityLevelIndex { get; private set; }
    public int ResolutionIndex { get; private set; }
    public bool IsFullScreen { get; private set; }

    public string CurrentQualityLabel
    {
        get
        {
            string[] qualityNames = QualitySettings.names;
            if (qualityNames == null || qualityNames.Length == 0)
            {
                return "Default";
            }

            int index = Mathf.Clamp(QualityLevelIndex, 0, qualityNames.Length - 1);
            return qualityNames[index];
        }
    }

    public string CurrentResolutionLabel
    {
        get
        {
            if (_resolutionOptions == null || _resolutionOptions.Length == 0)
            {
                Resolution current = Screen.currentResolution;
                return $"{current.width} x {current.height}";
            }

            Resolution resolution = _resolutionOptions[Mathf.Clamp(ResolutionIndex, 0, _resolutionOptions.Length - 1)];
            return $"{resolution.width} x {resolution.height}";
        }
    }

    public string CurrentVolumeLabel => $"{Mathf.RoundToInt(MasterVolume * 100f)}%";
    public string CurrentFullScreenLabel => IsFullScreen ? "On" : "Off";

    protected override void Awake()
    {
        base.Awake();

        if (Instance != this)
        {
            return;
        }

        CacheResolutions();
        Load();
        ApplyAll();
    }

    public void ChangeMasterVolume(float delta)
    {
        SetMasterVolume(MasterVolume + delta);
    }

    public void CycleQuality(int delta)
    {
        string[] qualityNames = QualitySettings.names;
        if (qualityNames == null || qualityNames.Length == 0)
        {
            return;
        }

        int count = qualityNames.Length;
        QualityLevelIndex = WrapIndex(QualityLevelIndex + delta, count);
        QualitySettings.SetQualityLevel(QualityLevelIndex, true);
        Persist();
    }

    public void CycleResolution(int delta)
    {
        if (_resolutionOptions == null || _resolutionOptions.Length == 0)
        {
            return;
        }

        ResolutionIndex = WrapIndex(ResolutionIndex + delta, _resolutionOptions.Length);
        ApplyResolution();
        Persist();
    }

    public void ToggleFullScreen()
    {
        IsFullScreen = !IsFullScreen;
        ApplyResolution();
        Persist();
    }

    private void SetMasterVolume(float value)
    {
        MasterVolume = Mathf.Clamp01(value);
        AudioListener.volume = MasterVolume;
        Persist();
    }

    private void CacheResolutions()
    {
        Resolution[] source = Screen.resolutions;
        if (source == null || source.Length == 0)
        {
            _resolutionOptions = new[] { Screen.currentResolution };
            return;
        }

        List<Resolution> deduped = new List<Resolution>();
        foreach (Resolution resolution in source)
        {
            bool alreadyAdded = false;
            for (int i = 0; i < deduped.Count; i++)
            {
                if (deduped[i].width == resolution.width && deduped[i].height == resolution.height)
                {
                    alreadyAdded = true;
                    break;
                }
            }

            if (!alreadyAdded)
            {
                deduped.Add(resolution);
            }
        }

        _resolutionOptions = deduped.ToArray();
    }

    private void Load()
    {
        MasterVolume = PlayerPrefs.GetFloat(MasterVolumeKey, 1f);
        QualityLevelIndex = Mathf.Clamp(
            PlayerPrefs.GetInt(QualityLevelKey, QualitySettings.GetQualityLevel()),
            0,
            Mathf.Max(0, QualitySettings.names.Length - 1));
        ResolutionIndex = Mathf.Clamp(
            PlayerPrefs.GetInt(ResolutionIndexKey, FindClosestResolutionIndex()),
            0,
            Mathf.Max(0, _resolutionOptions.Length - 1));
        IsFullScreen = PlayerPrefs.GetInt(FullScreenKey, Screen.fullScreen ? 1 : 0) == 1;
    }

    private void ApplyAll()
    {
        AudioListener.volume = MasterVolume;

        if (QualitySettings.names != null && QualitySettings.names.Length > 0)
        {
            QualitySettings.SetQualityLevel(QualityLevelIndex, true);
        }

        ApplyResolution();
    }

    private void ApplyResolution()
    {
        if (_resolutionOptions == null || _resolutionOptions.Length == 0)
        {
            return;
        }

        Resolution resolution = _resolutionOptions[Mathf.Clamp(ResolutionIndex, 0, _resolutionOptions.Length - 1)];
        Screen.SetResolution(resolution.width, resolution.height, IsFullScreen);
    }

    private void Persist()
    {
        PlayerPrefs.SetFloat(MasterVolumeKey, MasterVolume);
        PlayerPrefs.SetInt(QualityLevelKey, QualityLevelIndex);
        PlayerPrefs.SetInt(ResolutionIndexKey, ResolutionIndex);
        PlayerPrefs.SetInt(FullScreenKey, IsFullScreen ? 1 : 0);
        PlayerPrefs.Save();
    }

    private int FindClosestResolutionIndex()
    {
        if (_resolutionOptions == null || _resolutionOptions.Length == 0)
        {
            return 0;
        }

        Resolution current = Screen.currentResolution;
        for (int i = 0; i < _resolutionOptions.Length; i++)
        {
            if (_resolutionOptions[i].width == current.width && _resolutionOptions[i].height == current.height)
            {
                return i;
            }
        }

        return Mathf.Clamp(_resolutionOptions.Length - 1, 0, _resolutionOptions.Length - 1);
    }

    private static int WrapIndex(int value, int count)
    {
        if (count <= 0)
        {
            return 0;
        }

        int wrapped = value % count;
        return wrapped < 0 ? wrapped + count : wrapped;
    }
}
