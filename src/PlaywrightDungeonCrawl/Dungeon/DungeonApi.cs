namespace PlaywrightDungeonCrawl.Dungeon;

public sealed record AnswerRequest(string? Answer);

public sealed record ClaimRequest(string? Room, string? Flag);

/// <summary>
/// The JSON API behind the trap rooms and the treasure chest.
/// </summary>
public static class DungeonApi
{
    public static void MapDungeonApi(this WebApplication app)
    {
        var api = app.MapGroup("/api");

        api.MapPost("/rooms/{slug}/attempts", (string slug, DungeonCatalog catalog, AttemptStore attempts) =>
        {
            if (catalog.FindByRoom(slug) is not { } floor)
            {
                return Results.NotFound();
            }
            var attempt = attempts.Start(floor.Room, Random.Shared);
            Telemetry.AttemptStarted(slug);
            return Results.Ok(new
            {
                AttemptId = attempt.Id,
                TimeLimitMs = (int)floor.Room.TimeLimit.TotalMilliseconds,
                attempt.Puzzle.Briefing,
            });
        });

        api.MapGet("/rooms/{slug}/attempts/{id:guid}/reveal", (string slug, Guid id, AttemptStore attempts) =>
            attempts.Reveal(slug, id) is { } reveal
                ? Results.Ok(reveal)
                // 425 Too Early: the rune hasn't appeared yet (or never will).
                : Results.StatusCode(425));

        api.MapPost("/rooms/{slug}/attempts/{id:guid}/answer",
            (string slug, Guid id, AnswerRequest body, AttemptStore attempts, FlagService flags, ILoggerFactory logs) =>
            {
                var outcome = attempts.Answer(slug, id, body.Answer ?? "");
                var name = outcome switch
                {
                    AnswerOutcome.Correct => "correct",
                    AnswerOutcome.UnknownAttempt => "unknown-attempt",
                    AnswerOutcome.TooSlow => "too-slow",
                    _ => "wrong",
                };
                Telemetry.Answered(slug, name);
                logs.CreateLogger("PlaywrightDungeonCrawl.Rooms").LogInformation("Answer in {Room}: {Outcome}", slug, name);
                return outcome switch
                {
                    AnswerOutcome.Correct => Results.Ok(new { Outcome = name, Flag = flags.ForRoom(slug) }),
                    AnswerOutcome.UnknownAttempt => Results.NotFound(new { Outcome = name }),
                    _ => Results.UnprocessableEntity(new { Outcome = name }),
                };
            });

        api.MapPost("/treasure/claim", (ClaimRequest body, DungeonCatalog catalog, FlagService flags) =>
        {
            var flag = body.Flag?.Trim();
            // Accept a flag on its own: find which room it belongs to.
            var floor = catalog.Floors.FirstOrDefault(f =>
                (body.Room is null || f.Room.Slug == body.Room) && flags.IsValid(f.Room.Slug, flag));
            Telemetry.TreasureClaimed(floor?.Room.Slug);
            return floor is null
                ? Results.Ok(new { Valid = false })
                : Results.Ok(new { Valid = true, Room = floor.Room.Slug, Floor = floor.Number, Treasure = floor.Room.Name });
        });
    }
}
