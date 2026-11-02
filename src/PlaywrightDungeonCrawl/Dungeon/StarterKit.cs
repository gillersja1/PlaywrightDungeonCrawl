namespace PlaywrightDungeonCrawl.Dungeon;

/// <summary>
/// The learner's test project lives in its own repository, so cloning it
/// gives them only what they need (and no spoilers). There's one per language.
/// </summary>
public static class StarterKit
{
    public const string DefaultRepoUrl = "https://github.com/gillersja1/PlaywrightDungeonCrawl-Starter";
    public const string DefaultTypeScriptRepoUrl = "https://github.com/gillersja1/PlaywrightDungeonCrawl-Starter-TS";

    public static string RepoUrl(IConfiguration config) => config["Dungeon:StarterRepoUrl"] ?? DefaultRepoUrl;

    public static string RepoUrl(IConfiguration config, CodeLanguage language) => language == CodeLanguage.TypeScript
        ? config["Dungeon:StarterRepoUrlTs"] ?? DefaultTypeScriptRepoUrl
        : RepoUrl(config);
}
