using System.Collections.Concurrent;
using Npgsql;

namespace PlaywrightDungeonCrawl.Dungeon;

/// <param name="Rating">1 to 5, or null if the player skipped it.</param>
/// <param name="Page">The site path the player came from, so feedback can be tied to a floor or room.</param>
/// <param name="Login">The GitHub login, when the player was signed in.</param>
public sealed record FeedbackEntry(int? Rating, string Message, string? Email, string Page, string? Login, DateTimeOffset At);

/// <summary>Where player feedback is kept.</summary>
public interface IFeedbackStore
{
    Task AddAsync(FeedbackEntry entry, CancellationToken ct = default);
}

/// <summary>Local play and tests: feedback lasts until the site restarts.</summary>
public sealed class InMemoryFeedbackStore : IFeedbackStore
{
    public ConcurrentQueue<FeedbackEntry> Entries { get; } = new();

    public Task AddAsync(FeedbackEntry entry, CancellationToken ct = default)
    {
        Entries.Enqueue(entry);
        return Task.CompletedTask;
    }
}

public sealed class PostgresFeedbackStore(NpgsqlDataSource db) : IFeedbackStore
{
    public async Task AddAsync(FeedbackEntry entry, CancellationToken ct = default)
    {
        await using var insert = db.CreateCommand(
            "insert into feedback (rating, message, email, page, login, created_at) values ($1, $2, $3, $4, $5, $6)");
        insert.Parameters.AddWithValue(entry.Rating is { } r ? r : DBNull.Value);
        insert.Parameters.AddWithValue(entry.Message);
        insert.Parameters.AddWithValue(entry.Email is { } e ? e : DBNull.Value);
        insert.Parameters.AddWithValue(entry.Page);
        insert.Parameters.AddWithValue(entry.Login is { } l ? l : DBNull.Value);
        insert.Parameters.AddWithValue(entry.At);
        await insert.ExecuteNonQueryAsync(ct);
    }
}

public static class FeedbackRules
{
    public const int MaxMessage = 2000;
    public const int MaxEmail = 200;

    /// <summary>The page feedback came from: a same-site path, or "/" when it isn't one.</summary>
    public static string FromPage(string? from)
    {
        var path = Accounts.LocalPath(from);
        return path.Length > 200 ? path[..200] : path;
    }

    /// <summary>A light check: something@something.something, nothing more.</summary>
    public static bool LooksLikeEmail(string email) =>
        email.Length <= MaxEmail && email.IndexOf('@') is > 0 and var at && email.LastIndexOf('.') > at + 1 &&
        !email.EndsWith('.') && !email.Any(char.IsWhiteSpace);
}
