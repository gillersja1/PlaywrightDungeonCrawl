using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Npgsql;
using NpgsqlTypes;

namespace PlaywrightDungeonCrawl.Dungeon;

/// <summary>
/// A signed-in player's progress, in the same shape the browser keeps in
/// localStorage (see progress.js).
/// </summary>
public sealed partial record PlayerProgress(string[] Scrolls, string[] Riddles, string[] Treasures, Dictionary<string, int> Help)
{
    public static PlayerProgress Empty => new([], [], [], []);

    private const int MaxHelpEntries = 500;

    [GeneratedRegex(@"^(hint|nudge):[a-z0-9-]{1,40}:\d{1,2}$")]
    private static partial Regex HelpKey();

    /// <summary>Drops anything that isn't a real floor, room or piece of help.</summary>
    public PlayerProgress Sanitize(DungeonCatalog catalog)
    {
        var floors = catalog.Floors.Select(f => f.Slug).ToHashSet();
        var rooms = catalog.Floors.Select(f => f.Room.Slug).ToHashSet();
        return new(
            (Scrolls ?? []).Where(floors.Contains).Distinct().ToArray(),
            (Riddles ?? []).Where(floors.Contains).Distinct().ToArray(),
            (Treasures ?? []).Where(rooms.Contains).Distinct().ToArray(),
            (Help ?? []).Where(h => HelpKey().IsMatch(h.Key) && h.Value is >= 0 and <= 100)
                .Take(MaxHelpEntries)
                .ToDictionary());
    }

    /// <summary>
    /// Progress only grows, so two copies combine by union. Help keeps the
    /// cost it was first charged at.
    /// </summary>
    public PlayerProgress Merge(PlayerProgress other)
    {
        var help = new Dictionary<string, int>(Help);
        foreach (var (key, cost) in other.Help)
        {
            help.TryAdd(key, cost);
        }
        return new(
            Scrolls.Union(other.Scrolls).ToArray(),
            Riddles.Union(other.Riddles).ToArray(),
            Treasures.Union(other.Treasures).ToArray(),
            help);
    }
}

public interface IProgressStore
{
    /// <summary>Merges <paramref name="incoming"/> into the player's saved progress and returns the result.</summary>
    Task<PlayerProgress> MergeAsync(string playerId, string login, PlayerProgress incoming, CancellationToken ct = default);

    Task<PlayerProgress> GetAsync(string playerId, CancellationToken ct = default);

    Task ResetAsync(string playerId, CancellationToken ct = default);
}

/// <summary>For local play and tests: progress lasts until the site restarts.</summary>
public sealed class InMemoryProgressStore : IProgressStore
{
    private readonly ConcurrentDictionary<string, PlayerProgress> players = new();

    public Task<PlayerProgress> MergeAsync(string playerId, string login, PlayerProgress incoming, CancellationToken ct = default) =>
        Task.FromResult(players.AddOrUpdate(playerId, incoming, (_, saved) => saved.Merge(incoming)));

    public Task<PlayerProgress> GetAsync(string playerId, CancellationToken ct = default) =>
        Task.FromResult(players.TryGetValue(playerId, out var saved) ? saved : PlayerProgress.Empty);

    public Task ResetAsync(string playerId, CancellationToken ct = default)
    {
        players.TryRemove(playerId, out _);
        return Task.CompletedTask;
    }
}

public sealed class PostgresProgressStore(NpgsqlDataSource db) : IProgressStore
{
    public async Task<PlayerProgress> MergeAsync(string playerId, string login, PlayerProgress incoming, CancellationToken ct = default)
    {
        await using var connection = await db.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);

        // Lock the row, so two tabs saving at once can't lose each other's progress.
        await using var select = new NpgsqlCommand(
            "select progress from player_progress where github_id = $1 for update", connection, transaction);
        select.Parameters.Add(new() { Value = playerId });
        var saved = await select.ExecuteScalarAsync(ct) is string json
            ? JsonSerializer.Deserialize<PlayerProgress>(json, JsonSerializerOptions.Web) ?? PlayerProgress.Empty
            : PlayerProgress.Empty;
        var merged = saved.Merge(incoming);

        await using var upsert = new NpgsqlCommand(
            """
            insert into player_progress (github_id, login, progress, updated_at) values ($1, $2, $3, now())
            on conflict (github_id) do update set login = excluded.login, progress = excluded.progress, updated_at = now()
            """, connection, transaction);
        upsert.Parameters.Add(new() { Value = playerId });
        upsert.Parameters.Add(new() { Value = login });
        upsert.Parameters.Add(new() { Value = JsonSerializer.Serialize(merged, JsonSerializerOptions.Web), NpgsqlDbType = NpgsqlDbType.Jsonb });
        await upsert.ExecuteNonQueryAsync(ct);

        await transaction.CommitAsync(ct);
        return merged;
    }

    public async Task<PlayerProgress> GetAsync(string playerId, CancellationToken ct = default)
    {
        await using var select = db.CreateCommand("select progress from player_progress where github_id = $1");
        select.Parameters.Add(new() { Value = playerId });
        return await select.ExecuteScalarAsync(ct) is string json
            ? JsonSerializer.Deserialize<PlayerProgress>(json, JsonSerializerOptions.Web) ?? PlayerProgress.Empty
            : PlayerProgress.Empty;
    }

    public async Task ResetAsync(string playerId, CancellationToken ct = default)
    {
        await using var delete = db.CreateCommand("delete from player_progress where github_id = $1");
        delete.Parameters.Add(new() { Value = playerId });
        await delete.ExecuteNonQueryAsync(ct);
    }
}

/// <summary>
/// Keeps ASP.NET's data protection keys (which sign the sign-in cookie) in
/// Postgres. Without this, every restart of the container would sign everyone out.
/// </summary>
public sealed class PostgresXmlRepository(NpgsqlDataSource db) : IXmlRepository
{
    public IReadOnlyCollection<XElement> GetAllElements()
    {
        using var select = db.CreateCommand("select xml from data_protection_keys");
        using var reader = select.ExecuteReader();
        var keys = new List<XElement>();
        while (reader.Read())
        {
            keys.Add(XElement.Parse(reader.GetString(0)));
        }
        return keys;
    }

    public void StoreElement(XElement element, string friendlyName)
    {
        using var insert = db.CreateCommand(
            "insert into data_protection_keys (name, xml) values ($1, $2) on conflict (name) do update set xml = excluded.xml");
        insert.Parameters.Add(new() { Value = friendlyName });
        insert.Parameters.Add(new() { Value = element.ToString(SaveOptions.DisableFormatting) });
        insert.ExecuteNonQuery();
    }
}

public static class PostgresDatabase
{
    public static async Task EnsureCreatedAsync(NpgsqlDataSource db, CancellationToken ct = default)
    {
        await using var create = db.CreateCommand(
            """
            create table if not exists player_progress (
                github_id text primary key,
                login text not null,
                progress jsonb not null,
                updated_at timestamptz not null default now());
            create table if not exists data_protection_keys (
                name text primary key,
                xml text not null);
            create table if not exists player_profiles (
                github_id text primary key,
                login text not null,
                on_leaderboard boolean not null default false);
            create table if not exists player_achievements (
                github_id text not null,
                kind text not null,
                slug text not null,
                earned_at timestamptz not null default now(),
                primary key (github_id, kind, slug));
            """);
        await create.ExecuteNonQueryAsync(ct);
    }

    /// <summary>
    /// Accepts either an Npgsql connection string or the postgresql://user:pass@host/db
    /// URL that Neon (and most hosts) hand out.
    /// </summary>
    public static string NormalizeConnectionString(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme is not ("postgres" or "postgresql"))
        {
            return value;
        }
        var user = uri.UserInfo.Split(':', 2);
        var sslMode = uri.Query.TrimStart('?').Split('&')
            .Select(p => p.Split('=', 2))
            .FirstOrDefault(p => p[0] == "sslmode")?.ElementAtOrDefault(1);
        return new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.Port > 0 ? uri.Port : 5432,
            Database = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/')),
            Username = Uri.UnescapeDataString(user[0]),
            Password = user.Length > 1 ? Uri.UnescapeDataString(user[1]) : null,
            SslMode = sslMode == "disable" ? SslMode.Disable : SslMode.Require,
        }.ConnectionString;
    }
}
