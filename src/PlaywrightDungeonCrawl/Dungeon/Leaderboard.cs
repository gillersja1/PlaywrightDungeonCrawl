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

    /// <summary>The player's certificate for one tier of the path, if they have it.</summary>
    Task<Certificate?> CertificateForAsync(string playerId, CertificateTier tier = CertificateTier.Apprentice, CancellationToken ct = default);

    /// <summary>All the player's certificates, one per tier earned.</summary>
    Task<IReadOnlyList<Certificate>> CertificatesForAsync(string playerId, CancellationToken ct = default);

    /// <summary>
    /// Issues the player's certificate for its tier, or reprints their existing one: the name and ID may change, the date doesn't.
    /// <see cref="CertificateAsync"/> also finds a certificate by its previous ID.
    /// </summary>
    Task<Certificate> IssueCertificateAsync(Certificate certificate, CancellationToken ct = default);
}

public sealed record Standing(string Login, int FloorsCleared, int Score, DateTimeOffset? ReachedAt)
{
    /// <summary>Set for a dungeon resident (see <see cref="Rivals"/>); null for a real player.</summary>
    public Rival? Npc { get; init; }

    public bool IsNpc => Npc is not null;

    /// <summary>
    /// The highest N where floors 1 to N are all cleared. Certificate tiers count this, not <see cref="FloorsCleared"/>,
    /// so a tier can't be earned by clearing other floors instead of its boss.
    /// </summary>
    public int ClearedThrough { get; init; }

    /// <summary>A title bought in the loot shop and worn on the board (see <see cref="Shop.VerifiedTitle"/>).</summary>
    public string? Title { get; init; }

    /// <summary>Confirmed bug reports: shown as the Bugslayer badge, never counted in the score or rank.</summary>
    public int BugsConfirmed { get; init; }

    /// <summary>Curses broken in Cursed Runs: a badge too, never counted in the score or rank.</summary>
    public int CursesBroken { get; init; }
}

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

        var through = catalog.Floors.OrderBy(f => f.Number).TakeWhile(cleared.Contains).Count();
        return new(record.Login, cleared.Count, score, reached) { ClearedThrough = through };
    }

    /// <summary>
    /// Floors cleared, then score, then whoever got there first. Real players win ties against NPCs.
    /// Equal standings share a rank.
    /// </summary>
    public static IReadOnlyList<(int Rank, Standing Standing)> Rank(IEnumerable<Standing> standings)
    {
        var ordered = standings
            .OrderByDescending(s => s.FloorsCleared)
            .ThenByDescending(s => s.Score)
            .ThenBy(s => s.IsNpc)
            .ThenBy(s => s.ReachedAt ?? DateTimeOffset.MaxValue)
            .ThenBy(s => s.Login, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var ranked = new List<(int, Standing)>();
        for (var i = 0; i < ordered.Count; i++)
        {
            var same = i > 0 && ordered[i].FloorsCleared == ordered[i - 1].FloorsCleared
                && ordered[i].Score == ordered[i - 1].Score && ordered[i].ReachedAt == ordered[i - 1].ReachedAt
                && ordered[i].IsNpc == ordered[i - 1].IsNpc;
            ranked.Add((same ? ranked[i - 1].Item1 : i + 1, ordered[i]));
        }
        return ranked;
    }

    /// <param name="includeRivals">Whether to rank the NPC rivals alongside the players.</param>
    /// <param name="bugslayers">The Hall of Bugslayers, for the badge; it never changes the order.</param>
    public static async Task<IReadOnlyList<(int Rank, Standing Standing)>> BuildAsync(
        IPlayerRecords records, IProgressStore progress, DungeonCatalog catalog, bool includeRivals = false,
        IReadOnlyList<Bugslayer>? bugslayers = null, CancellationToken ct = default)
    {
        var standings = includeRivals ? Rivals.StandingsFor(catalog).ToList() : [];
        var bugs = (bugslayers ?? []).ToDictionary(b => b.PlayerId, b => b.Confirmed);
        foreach (var record in await records.ListedAsync(ct))
        {
            var saved = await progress.GetAsync(record.PlayerId, ct);
            var standing = Score(record, saved, catalog);
            standings.Add(standing with
            {
                Title = Shop.VerifiedTitle(saved, standing.Score),
                BugsConfirmed = bugs.GetValueOrDefault(record.PlayerId),
                CursesBroken = record.Achievements.Count(a => a.Kind == Curses.AchievementKind),
            });
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
        Task.FromResult(certificates.Values.FirstOrDefault(c => c.Id == id) ?? certificates.Values.FirstOrDefault(c => c.PreviousId == id));

    public Task<Certificate?> CertificateForAsync(string playerId, CertificateTier tier = CertificateTier.Apprentice, CancellationToken ct = default) =>
        Task.FromResult(certificates.TryGetValue(Key(playerId, tier), out var certificate) ? certificate : null);

    public Task<IReadOnlyList<Certificate>> CertificatesForAsync(string playerId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<Certificate>>(certificates.Values.Where(c => c.PlayerId == playerId).OrderBy(c => c.Tier).ToList());

    private static string Key(string playerId, CertificateTier tier) => $"{playerId}:{tier}";

    public Task<Certificate> IssueCertificateAsync(Certificate certificate, CancellationToken ct = default) =>
        Task.FromResult(certificates.AddOrUpdate(Key(certificate.PlayerId, certificate.Tier), certificate,
            (_, saved) => saved with
            {
                Id = certificate.Id, Name = certificate.Name, Login = certificate.Login,
                Floors = certificate.Floors, Score = certificate.Score, UpdatedAt = certificate.UpdatedAt ?? saved.UpdatedAt,
                PreviousId = certificate.PreviousId ?? saved.PreviousId, Language = certificate.Language,
            }));
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
        // Its current ID first, then an ID it had before an upgrade.
        (await LoadCertificatesAsync("id = $1 or previous_id = $1 order by (id = $1) desc limit 1", [id], ct)).FirstOrDefault();

    public async Task<Certificate?> CertificateForAsync(string playerId, CertificateTier tier = CertificateTier.Apprentice, CancellationToken ct = default) =>
        (await LoadCertificatesAsync("github_id = $1 and tier = $2", [playerId, CertificateRules.For(tier).Code], ct)).SingleOrDefault();

    public async Task<IReadOnlyList<Certificate>> CertificatesForAsync(string playerId, CancellationToken ct = default) =>
        (await LoadCertificatesAsync("github_id = $1", [playerId], ct)).OrderBy(c => c.Tier).ToList();

    public async Task<Certificate> IssueCertificateAsync(Certificate certificate, CancellationToken ct = default)
    {
        await using var upsert = db.CreateCommand("""
            insert into certificates (id, github_id, login, name, floors, score, issued_at, previous_id, language, tier, updated_at)
            values ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11)
            on conflict (github_id, tier) do update set id = excluded.id, name = excluded.name, login = excluded.login,
                floors = excluded.floors, score = excluded.score, updated_at = coalesce(excluded.updated_at, certificates.updated_at),
                previous_id = coalesce(excluded.previous_id, certificates.previous_id), language = excluded.language
            """);
        foreach (var value in new object?[] { certificate.Id, certificate.PlayerId, certificate.Login, certificate.Name,
                     certificate.Floors, certificate.Score, certificate.IssuedAt, certificate.PreviousId, certificate.Language.Code(),
                     CertificateRules.For(certificate.Tier).Code, certificate.UpdatedAt })
        {
            upsert.Parameters.Add(new() { Value = value ?? DBNull.Value });
        }
        await upsert.ExecuteNonQueryAsync(ct);
        return (await CertificateForAsync(certificate.PlayerId, certificate.Tier, ct))!;
    }

    private async Task<List<Certificate>> LoadCertificatesAsync(string where, string[] values, CancellationToken ct)
    {
        await using var select = db.CreateCommand(
            $"select id, github_id, login, name, floors, score, issued_at, previous_id, language, tier, updated_at from certificates where {where}");
        foreach (var value in values)
        {
            select.Parameters.Add(new() { Value = value });
        }
        await using var reader = await select.ExecuteReaderAsync(ct);
        var found = new List<Certificate>();
        while (await reader.ReadAsync(ct))
        {
            found.Add(new(reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3),
                reader.GetInt32(4), reader.GetInt32(5), reader.GetFieldValue<DateTimeOffset>(6),
                reader.IsDBNull(7) ? null : reader.GetString(7),
                Languages.Parse(reader.GetString(8)) ?? CodeLanguage.CSharp,
                CertificateRules.ParseTier(reader.GetString(9)) ?? CertificateTier.Apprentice,
                reader.IsDBNull(10) ? null : reader.GetFieldValue<DateTimeOffset>(10)));
        }
        return found;
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
