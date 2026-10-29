namespace PlaywrightDungeonCrawl.Dungeon;

/// <summary>
/// The learner's test project lives in its own repository, so cloning it
/// gives them only what they need (and no spoilers).
/// </summary>
public static class StarterKit
{
    public const string DefaultRepoUrl = "https://github.com/gillersja1/PlaywrightDungeonCrawl-Starter";

    public static string RepoUrl(IConfiguration config) => config["Dungeon:StarterRepoUrl"] ?? DefaultRepoUrl;
}
