namespace PlaywrightDungeonCrawl.Dungeon;

public enum LootSlot { Title, Torch }

/// <param name="Color">For torches: the accent colour it gives the site; <paramref name="Dim"/> is its darker shade.</param>
public sealed record LootItem(string Id, LootSlot Slot, string Name, int Price, string Description, string? Color = null, string? Dim = null);

/// <summary>
/// The Quartermaster's loot shop. Items are cosmetic and paid from the wallet: score earned, less what
/// the shop has already had. Buying never lowers the score, the rank or a place on the leaderboard.
/// </summary>
public static class Shop
{
    public static readonly IReadOnlyList<LootItem> Items =
    [
        new("title-lockpicker", LootSlot.Title, "Lockpicker", 40, "For crawlers who open doors the polite way: with a script."),
        new("title-flag-hoarder", LootSlot.Title, "Flag Hoarder", 60, "Collects PDC{...} flags. Cannot explain why."),
        new("title-selector-whisperer", LootSlot.Title, "Selector Whisperer", 100, "getByRole first, ask questions later."),
        new("title-friend-of-the-warden", LootSlot.Title, "Friend of the Warden", 150, "He doesn't have many. Be nice."),
        new("title-flake-eater", LootSlot.Title, "Ate the Flaky Test", 250, "It passed on the rerun. You did not."),
        new("torch-ghostfire", LootSlot.Torch, "Ghostfire torch", 50, "A cold blue flame. Spooky, but good for reading.", "#66dcff", "#2f8fb0"),
        new("torch-verdigris", LootSlot.Torch, "Verdigris torch", 75, "Burns green, like old copper and older bugs.", "#6fdc8c", "#3b9a55"),
        new("torch-witchlight", LootSlot.Torch, "Witchlight torch", 75, "Violet, flickering, faintly judgemental.", "#c69cff", "#8a5ccc"),
        new("torch-bloodmoon", LootSlot.Torch, "Bloodmoon torch", 120, "Red as a failing test. Wear it with pride.", "#ff6b6b", "#b8433f"),
    ];

    public static LootItem? Find(string? id) => Items.FirstOrDefault(i => i.Id == id);

    /// <summary>XP spent in the shop, at the prices paid.</summary>
    public static int Spent(PlayerProgress progress) => progress.Loot.Values.Sum();

    /// <summary>
    /// The title a player shows on the leaderboard: the one they equipped, provided they own it and their
    /// verified score covers everything they've bought, so it can't be had by editing what the browser sends.
    /// </summary>
    public static string? VerifiedTitle(PlayerProgress progress, int verifiedScore) =>
        progress.Equipped.TryGetValue(nameof(LootSlot.Title).ToLowerInvariant(), out var id)
        && progress.Loot.ContainsKey(id) && Find(id) is { Slot: LootSlot.Title } item && Spent(progress) <= verifiedScore
            ? item.Name
            : null;
}
