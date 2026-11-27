using System.Collections.Generic;
using UnityEngine;

public static class GameAudioResources
{
    private static readonly Dictionary<string, AudioClip> ClipCache = new Dictionary<string, AudioClip>();

    public static AudioClip LoadClip(string resourcePath)
    {
        if (string.IsNullOrWhiteSpace(resourcePath))
        {
            return null;
        }

        if (ClipCache.TryGetValue(resourcePath, out AudioClip cachedClip))
        {
            return cachedClip;
        }

        AudioClip clip = Resources.Load<AudioClip>(resourcePath);
        ClipCache[resourcePath] = clip;
        return clip;
    }

    public static AudioClip[] LoadClips(params string[] resourcePaths)
    {
        if (resourcePaths == null || resourcePaths.Length == 0)
        {
            return System.Array.Empty<AudioClip>();
        }

        List<AudioClip> loadedClips = new List<AudioClip>(resourcePaths.Length);
        for (int i = 0; i < resourcePaths.Length; i++)
        {
            AudioClip clip = LoadClip(resourcePaths[i]);
            if (clip != null)
            {
                loadedClips.Add(clip);
            }
        }

        return loadedClips.ToArray();
    }
}
