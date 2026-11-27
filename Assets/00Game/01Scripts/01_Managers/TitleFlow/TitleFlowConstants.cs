public static class TitleFlowConstants
{
    public const string TitleSceneName = "Title";
    public const string LobbySceneName = "Lobby";
    public const string GameSceneName = "Castle";
    public const string BossSceneName = "Boss";

    public static string NormalizeSceneName(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            return string.Empty;
        }

        string normalized = sceneName.Trim().Replace('\\', '/');
        int slashIndex = normalized.LastIndexOf('/');
        if (slashIndex >= 0 && slashIndex < normalized.Length - 1)
        {
            normalized = normalized[(slashIndex + 1)..];
        }

        if (normalized.EndsWith(".unity", System.StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[..^6];
        }

        return normalized.ToLowerInvariant() switch
        {
            "developscenes" => BossSceneName,
            "medievalcastle" => GameSceneName,
            "boss" => BossSceneName,
            "castle" => GameSceneName,
            "title" => TitleSceneName,
            "lobby" => LobbySceneName,
            _ => normalized
        };
    }
}
