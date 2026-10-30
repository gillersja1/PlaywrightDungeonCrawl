using System.Collections.Concurrent;
using System.Security.Claims;
using Npgsql;

namespace PlaywrightDungeonCrawl.Dungeon;

/// <summary>Something the server saw a signed-in player earn, rather than something their browser reported.</summary>
public sealed record Achievement(string Kind, string Slug, DateTimeOffset EarnedAt)
{
    /// <summary>A valid treasure flag handed in, or a trap room cleared while signed in.</summary>
    public const string Treasure = "treasure";

    /// <summary>A floor's riddle answered in full while signed in.</summary>
    public const string Riddle = "riddle";
}

public sealed record PlayerRecord(string PlayerId, string Login, bool OnLeaderboard, IReadOnlyList<Achievement> Achievements)
{
    public static PlayerRecord Empty(string playerId, string login) => new(playerId, login, false, []);
}

/// <summary>
/// Verified achievements and leaderboard choices. Progress from the browser
/// (<see cref="IProgressStore"/>) can say anything; only what's recorded here
/// counts towards the leaderboard and certificates.
/// </summary>
public interface IPlayerRecords
{
    /// <summary>Records an achievement once; later calls keep the first time it was earned.</summary>
    Task RecordAsync(string playerId, string login, string kind, string slug, CancellationToken ct = default);

    Task<PlayerRecord?> GetAsync(string playerId, CancellationToken ct = default);

    Task SetOnLeaderboardAsync(string playerId, string login, bool show, CancellationToken ct = default);

    /// <summary>Everyone who chose to appear on the leaderboard.</summary>
    Task<IReadOnlyList<PlayerRecord>> ListedAsync(CancellationToken ct = default);

    /// <summary>Clears achievements (a progress reset); the leaderboard choice and any certificate stay.</summary>
    Task ResetAsync(string playerId, CancellationToken ct = default);

    Task<Certificate?> CertificateAsync(string id, CancellationToken ct = default);

    Task<Certificate?> CertificateForAsync(string playerId, CancellationToken ct = default);

    /// <summary>Issues the player's certificate, or reprints their existing one with a new name (same id and date).</summary>
    Task<Certificate> IssueCertificateAsync(Certificate certificate, CancellationToken ct = default);
}

public sealed record Standing(string Login, int FloorsCleared, int Score, DateTimeOffset? ReachedAt);

public static class Leaderboard
{
    /// <summary>
    /// A floor is cleared once its treasure and riddle are both verified. Score
    /// counts verified treasures and riddles, plus scrolls read, minus help
    /// taken, so it can't be raised by editing what the browser sends.
    /// </summary>
    public static Standing Score(PlayerRecord record, PlayerProgress progress, DungeonCatalog catalog)
    {
        bool Has(string kind, string slug) => record.Achievements.Any(a => a.Kind == kind && a.Slug == slug);

        var cleared = catalog.Floors.Where(f => Has(Achievement.Riddle, f.Slug) && Has(Achievement.Treasure, f.Room.Slug)).ToList();
        var riddles = catalog.Floors.Count(f => Has(Achievement.Riddle, f.Slug));
        var treasures = catalog.Floors.Count(f => Has(Achievement.Treasure, f.Room.Slug));
        var scrolls = progress.Scrolls.Count(s => catalog.Find(s) is not null);
        var help = progress.Help.Values.Sum();

        var score = Math.Max(0, scrolls * Rewards.Scroll + riddles * Rewards.Riddle + treasures * Rewards.Treasure - help);

        // When the last floor that counts was completed: earlier ranks higher on a tie.
        DateTimeOffset? reached = cleared.Count == 0
            ? null
            : cleared.Max(f => record.Achievements
                .Where(a => (a.Kind == Achievement.Riddle && a.Slug == f.Slug) || (a.Kind == Achievement.Treasure && a.Slug == f.Room.Slug))
                .Max(a => a.EarnedAt));

        return new(record.Login, cleared.Count, score, reached);
    }

    /// <summary>Floors cleared, then score, then whoever got there first. Equal standings share a rank.</summary>
    public static IReadOnlyList<(int Rank, Standing Standing)> Rank(IEnumerable<Standing> standings)
    {
        var ordered = standings
            .OrderByDescending(s => s.FloorsCleared)
            .ThenByDescending(s => s.Score)
            .ThenBy(s => s.ReachedAt ?? DateTimeOffset.MaxValue)
            .ThenBy(s => s.Login, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var ranked = new List<(int, Standing)>();
        for (var i = 0; i < ordered.Count; i++)
        {
            var same = i > 0 && ordered[i].FloorsCleared == ordered[i - 1].FloorsCleared
                && ordered[i].Score == ordered[i - 1].Score && ordered[i].ReachedAt == ordered[i - 1].ReachedAt;
            ranked.Add((same ? ranked[i - 1].Item1 : i + 1, ordered[i]));
        }
        return ranked;
    }

    public static async Task<IReadOnlyList<(int Rank, Standing Standing)>> BuildAsync(
        IPlayerRecords records, IProgressStore progress, DungeonCatalog catalog, CancellationToken ct = default)
    {
        var standings = new List<Standing>();
        foreach (var record in await records.ListedAsync(ct))
        {
            standings.Add(Score(record, await progress.GetAsync(record.PlayerId, ct), catalog));
        }
        return Rank(standings);
    }

    /// <summary>Records an achievement for the signed-in player, if there is one.</summary>
    public static async Task RecordIfSignedInAsync(HttpContext context, string kind, string slug)
    {
        var user = context.User;
        if (user.Identity?.IsAuthenticated != true || user.FindFirstValue(ClaimTypes.NameIdentifier) is not { } playerId)
        {
            return;
        }
        var records = context.RequestServices.GetRequiredService<IPlayerRecords>();
        await records.RecordAsync(playerId, user.Identity.Name ?? "", kind, slug, context.RequestAborted);
    }
}

/// <summary>For local play and tests: records last until the site restarts.</summary>
public sealed class InMemoryPlayerRecords(TimeProvider clock) : IPlayerRecords
{
    private readonly ConcurrentDictionary<string, PlayerRecord> players = new();
    private readonly ConcurrentDictionary<string, Certificate> certificates = new();

    public Task RecordAsync(string playerId, string login, string kind, string slug, CancellationToken ct = default)
    {
        players.AddOrUpdate(playerId,
            _ => new(playerId, login, false, [new(kind, slug, clock.GetUtcNow())]),
            (_, saved) => saved.Achievements.Any(a => a.Kind == kind && a.Slug == slug)
                ? saved with { Login = login }
                : saved with { Login = login, Achievements = [.. saved.Achievements, new(kind, slug, clock.GetUtcNow())] });
        return Task.CompletedTask;
    }

    public Task<PlayerRecord?> GetAsync(string playerId, CancellationToken ct = default) =>
        Task.FromResult(players.TryGetValue(playerId, out var record) ? record : null);

    public Task SetOnLeaderboardAsync(string playerId, string login, bool show, CancellationToken ct = default)
    {
        players.AddOrUpdate(playerId,
            _ => PlayerRecord.Empty(playerId, login) with { OnLeaderboard = show },
            (_, saved) => saved with { Login = login, OnLeaderboard = show });
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<PlayerRecord>> ListedAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<PlayerRecord>>(players.Values.Where(p => p.OnLeaderboard).ToList());

    public Task ResetAsync(string playerId, CancellationToken ct = default)
    {
        players.AddOrUpdate(playerId, _ => PlayerRecord.Empty(playerId, ""), (_, saved) => saved with { Achievements = [] });
        return Task.CompletedTask;
    }

    public Task<Certificate?> CertificateAsync(string id, CancellationToken ct = default) =>
        Task.FromResult(certificates.Values.FirstOrDefault(c => c.Id == id));

    public Task<Certificate?> CertificateForAsync(string playerId, CancellationToken ct = default) =>
        Task.FromResult(certificates.TryGetValue(playerId, out var certificate) ? certificate : null);

    public Task<Certificate> IssueCertificateAsync(Certificate certificate, CancellationToken ct = default) =>
        Task.FromResult(certificates.AddOrUpdate(certificate.PlayerId, certificate,
            (_, saved) => saved with { Name = certificate.Name, Login = certificate.Login }));
}

public sealed class PostgresPlayerRecords(NpgsqlDataSource db) : IPlayerRecords
{
    public async Task RecordAsync(string playerId, string login, string kind, string slug, CancellationToken ct = default)
    {
        await using var connection = await db.OpenConnectionAsync(ct);
        await using var batch = new NpgsqlBatch(connection)
        {
            BatchCommands =
            {
                Command("""
                    insert into player_profiles (github_id, login) values ($1, $2)
                    on conflict (github_id) do update set login = excluded.login
                    """, playerId, login),
                Command("""
                    insert into player_achievements (github_id, kind, slug) values ($1, $2, $3)
                    on conflict do nothing
                    """, playerId, kind, slug),
            },
        };
        await batch.ExecuteNonQueryAsync(ct);
    }

    public async Task<PlayerRecord?> GetAsync(string playerId, CancellationToken ct = default) =>
        (await LoadAsync("where p.github_id = $1", [playerId], ct)).SingleOrDefault();

    public async Task SetOnLeaderboardAsync(string playerId, string login, bool show, CancellationToken ct = default)
    {
        await using var upsert = db.CreateCommand("""
            insert into player_profiles (github_id, login, on_leaderboard) values ($1, $2, $3)
            on conflict (github_id) do update set login = excluded.login, on_leaderboard = excluded.on_leaderboard
            """);
        upsert.Parameters.Add(new() { Value = playerId });
        upsert.Parameters.Add(new() { Value = login });
        upsert.Parameters.Add(new() { Value = show });
        await upsert.ExecuteNonQueryAsync(ct);
    }

    public async Task<IReadOnlyList<PlayerRecord>> ListedAsync(CancellationToken ct = default) =>
        await LoadAsync("where p.on_leaderboard", [], ct);

    public async Task ResetAsync(string playerId, CancellationToken ct = default)
    {
        await using var delete = db.CreateCommand("delete from player_achievements where github_id = $1");
        delete.Parameters.Add(new() { Value = playerId });
        await delete.ExecuteNonQueryAsync(ct);
    }

    public async Task<Certificate?> CertificateAsync(string id, CancellationToken ct = default) =>
        await LoadCertificateAsync("id = $1", id, ct);

    public async Task<Certificate?> CertificateForAsync(string playerId, CancellationToken ct = default) =>
        await LoadCertificateAsync("github_id = $1", playerId, ct);

    public async Task<Certificate> IssueCertificateAsync(Certificate certificate, CancellationToken ct = default)
    {
        await using var upsert = db.CreateCommand("""
            insert into certificates (id, github_id, login, name, floors, score, issued_at) values ($1, $2, $3, $4, $5, $6, $7)
            on conflict (github_id) do update set name = excluded.name, login = excluded.login
            """);
        foreach (var value in new object[] { certificate.Id, certificate.PlayerId, certificate.Login, certificate.Name,
                     certificate.Floors, certificate.Score, certificate.IssuedAt })
        {
            upsert.Parameters.Add(new() { Value = value });
        }
        await upsert.ExecuteNonQueryAsync(ct);
        return (await CertificateForAsync(certificate.PlayerId, ct))!;
    }

    private async Task<Certificate?> LoadCertificateAsync(string where, string value, CancellationToken ct)
    {
        await using var select = db.CreateCommand(
            $"select id, github_id, login, name, floors, score, issued_at from certificates where {where}");
        select.Parameters.Add(new() { Value = value });
        await using var reader = await select.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct)
            ? new(reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3),
                reader.GetInt32(4), reader.GetInt32(5), reader.GetFieldValue<DateTimeOffset>(6))
            : null;
    }

    private async Task<List<PlayerRecord>> LoadAsync(string where, object[] args, CancellationToken ct)
    {
        await using var select = db.CreateCommand($"""
            select p.github_id, p.login, p.on_leaderboard, a.kind, a.slug, a.earned_at
            from player_profiles p left join player_achievements a on a.github_id = p.github_id
            {where}
            order by p.github_id, a.earned_at
            """);
        foreach (var arg in args)
        {
            select.Parameters.Add(new() { Value = arg });
        }

        var records = new Dictionary<string, PlayerRecord>();
        await using var reader = await select.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var id = reader.GetString(0);
            var record = records.TryGetValue(id, out var seen) ? seen : new(id, reader.GetString(1), reader.GetBoolean(2), []);
            if (!reader.IsDBNull(3))
            {
                record = record with
                {
                    Achievements = [.. record.Achievements, new(reader.GetString(3), reader.GetString(4), reader.GetFieldValue<DateTimeOffset>(5))],
                };
            }
            records[id] = record;
        }
        return records.Values.ToList();
    }

    private static NpgsqlBatchCommand Command(string sql, params object[] args)
    {
        var command = new NpgsqlBatchCommand(sql);
        foreach (var arg in args)
        {
            command.Parameters.Add(new() { Value = arg });
        }
        return command;
    }
}
