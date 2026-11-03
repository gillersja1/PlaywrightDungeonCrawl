using System.Collections.Concurrent;
using System.Security.Claims;
using Npgsql;

namespace PlaywrightDungeonCrawl.Dungeon;

public enum BugStatus { Pending, Confirmed, Rejected }

/// <summary>A bug a signed-in player found in the dungeon itself. Confirmed ones earn a place in the Hall of Bugslayers.</summary>
public sealed record BugReport(long Id, string PlayerId, string Login, string Title, string Details, string? Page,
    BugStatus Status, DateTimeOffset SubmittedAt, DateTimeOffset? ReviewedAt = null);

/// <summary>A player in the Hall of Bugslayers: how many of their reports were confirmed, and when the first was.</summary>
public sealed record Bugslayer(string PlayerId, string Login, int Confirmed, DateTimeOffset FirstConfirmedAt);

/// <summary>
/// Bug reports and their reviews. Kept apart from <see cref="IPlayerRecords"/> on purpose: resetting your
/// progress clears your achievements, but a confirmed bug stays found.
/// </summary>
public interface IBugReports
{
    Task<BugReport> SubmitAsync(string playerId, string login, string title, string details, string? page, CancellationToken ct = default);

    /// <summary>Every report, pending first, then newest first.</summary>
    Task<IReadOnlyList<BugReport>> ListAsync(CancellationToken ct = default);

    Task<IReadOnlyList<BugReport>> ForPlayerAsync(string playerId, CancellationToken ct = default);

    /// <summary>Confirms or rejects a report; false if there's no such report.</summary>
    Task<bool> ReviewAsync(long id, BugStatus status, CancellationToken ct = default);

    /// <summary>Everyone with at least one confirmed report, most confirmed first, then whoever got there first.</summary>
    Task<IReadOnlyList<Bugslayer>> HallAsync(CancellationToken ct = default);
}

public static class BugRules
{
    public const int MaxTitle = 120;
    public const int MaxDetails = 4000;

    /// <summary>Reports a player can have waiting for review at once; enough for real finds, not for spam.</summary>
    public const int MaxPending = 5;

    /// <summary>The title a confirmed report earns. Worn as a badge, not bought, and worth no XP.</summary>
    public const string Title = "Bugslayer";

    /// <summary>
    /// Dungeon__Admins: GitHub account ids (the numeric NameIdentifier, which never changes) allowed to review
    /// reports, separated by commas.
    /// </summary>
    public static bool IsAdmin(ClaimsPrincipal user, IConfiguration config) =>
        user.Identity?.IsAuthenticated == true
        && user.FindFirstValue(ClaimTypes.NameIdentifier) is { } id
        && (config["Dungeon:Admins"] ?? "").Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Contains(id);

    public static IReadOnlyList<Bugslayer> Rank(IEnumerable<BugReport> reports) => reports
        .Where(r => r.Status == BugStatus.Confirmed)
        .GroupBy(r => r.PlayerId)
        .Select(g => new Bugslayer(g.Key, g.OrderByDescending(r => r.SubmittedAt).First().Login, g.Count(),
            g.Min(r => r.ReviewedAt ?? r.SubmittedAt)))
        .OrderByDescending(b => b.Confirmed)
        .ThenBy(b => b.FirstConfirmedAt)
        .ToList();
}

/// <summary>For local play and tests: reports last until the site restarts.</summary>
public sealed class InMemoryBugReports(TimeProvider clock) : IBugReports
{
    private readonly ConcurrentDictionary<long, BugReport> reports = new();
    private long nextId;

    public Task<BugReport> SubmitAsync(string playerId, string login, string title, string details, string? page, CancellationToken ct = default)
    {
        var report = new BugReport(Interlocked.Increment(ref nextId), playerId, login, title, details, page, BugStatus.Pending, clock.GetUtcNow());
        reports[report.Id] = report;
        return Task.FromResult(report);
    }

    public Task<IReadOnlyList<BugReport>> ListAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<BugReport>>(reports.Values
            .OrderBy(r => r.Status != BugStatus.Pending).ThenByDescending(r => r.SubmittedAt).ThenByDescending(r => r.Id).ToList());

    public Task<IReadOnlyList<BugReport>> ForPlayerAsync(string playerId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<BugReport>>(reports.Values.Where(r => r.PlayerId == playerId)
            .OrderByDescending(r => r.SubmittedAt).ThenByDescending(r => r.Id).ToList());

    public Task<bool> ReviewAsync(long id, BugStatus status, CancellationToken ct = default)
    {
        if (!reports.TryGetValue(id, out var report))
        {
            return Task.FromResult(false);
        }
        reports[id] = report with { Status = status, ReviewedAt = clock.GetUtcNow() };
        return Task.FromResult(true);
    }

    public Task<IReadOnlyList<Bugslayer>> HallAsync(CancellationToken ct = default) =>
        Task.FromResult(BugRules.Rank(reports.Values));
}

public sealed class PostgresBugReports(NpgsqlDataSource db) : IBugReports
{
    private const string Columns = "id, github_id, login, title, details, page, status, submitted_at, reviewed_at";

    public async Task<BugReport> SubmitAsync(string playerId, string login, string title, string details, string? page, CancellationToken ct = default)
    {
        await using var insert = db.CreateCommand($"""
            insert into bug_reports (github_id, login, title, details, page) values ($1, $2, $3, $4, $5)
            returning {Columns}
            """);
        foreach (var value in new object?[] { playerId, login, title, details, page })
        {
            insert.Parameters.Add(new() { Value = value ?? DBNull.Value });
        }
        return (await ReadAsync(insert, ct)).Single();
    }

    public async Task<IReadOnlyList<BugReport>> ListAsync(CancellationToken ct = default)
    {
        await using var select = db.CreateCommand(
            $"select {Columns} from bug_reports order by (status <> 'pending'), submitted_at desc, id desc");
        return await ReadAsync(select, ct);
    }

    public async Task<IReadOnlyList<BugReport>> ForPlayerAsync(string playerId, CancellationToken ct = default)
    {
        await using var select = db.CreateCommand($"select {Columns} from bug_reports where github_id = $1 order by submitted_at desc, id desc");
        select.Parameters.Add(new() { Value = playerId });
        return await ReadAsync(select, ct);
    }

    public async Task<bool> ReviewAsync(long id, BugStatus status, CancellationToken ct = default)
    {
        await using var update = db.CreateCommand("update bug_reports set status = $2, reviewed_at = now() where id = $1");
        update.Parameters.Add(new() { Value = id });
        update.Parameters.Add(new() { Value = status.ToString().ToLowerInvariant() });
        return await update.ExecuteNonQueryAsync(ct) == 1;
    }

    public async Task<IReadOnlyList<Bugslayer>> HallAsync(CancellationToken ct = default)
    {
        await using var select = db.CreateCommand($"select {Columns} from bug_reports where status = 'confirmed'");
        return BugRules.Rank(await ReadAsync(select, ct));
    }

    private static async Task<List<BugReport>> ReadAsync(NpgsqlCommand command, CancellationToken ct)
    {
        await using var reader = await command.ExecuteReaderAsync(ct);
        var found = new List<BugReport>();
        while (await reader.ReadAsync(ct))
        {
            found.Add(new(reader.GetInt64(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetString(5),
                Enum.TryParse<BugStatus>(reader.GetString(6), ignoreCase: true, out var status) ? status : BugStatus.Pending,
                reader.GetFieldValue<DateTimeOffset>(7),
                reader.IsDBNull(8) ? null : reader.GetFieldValue<DateTimeOffset>(8)));
        }
        return found;
    }
}
