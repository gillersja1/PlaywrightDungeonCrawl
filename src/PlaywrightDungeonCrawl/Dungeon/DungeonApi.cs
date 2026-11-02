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
        // One log line per game event: Grafana counts these for plays, solve rate
        // and claims, which stays exact even though Render restarts the app.
        var events = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("PlaywrightDungeonCrawl.Rooms");

        api.MapPost("/rooms/{slug}/attempts", (string slug, DungeonCatalog catalog, AttemptStore attempts) =>
        {
            if (catalog.FindByRoom(slug) is not { } floor)
            {
                return Results.NotFound();
            }
            var attempt = attempts.Start(floor.Room, Random.Shared);
            Telemetry.AttemptStarted(slug);
            events.LogInformation("Attempt in {Room}", slug);
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

        // Multi-step rooms (the boss) clear their trials here, one at a time, before the final answer.
        api.MapPost("/rooms/{slug}/attempts/{id:guid}/trials", (string slug, Guid id, AnswerRequest body, AttemptStore attempts) =>
        {
            var result = attempts.Trial(slug, id, body.Answer ?? "");
            var name = OutcomeName(result.Outcome);
            events.LogInformation("Trial {Trial} in {Room}: {Outcome}", result.Trial, slug, name);
            if (result.Outcome != AnswerOutcome.Correct)
            {
                Telemetry.Answered(slug, name);
            }
            return result.Outcome switch
            {
                AnswerOutcome.Correct => Results.Ok(new { Outcome = name, result.Trial, result.Unlocks }),
                AnswerOutcome.UnknownAttempt => Results.NotFound(new { Outcome = name }),
                _ => Results.UnprocessableEntity(new { Outcome = name, result.Trial }),
            };
        });

        api.MapPost("/rooms/{slug}/attempts/{id:guid}/answer",
            async (string slug, Guid id, AnswerRequest body, AttemptStore attempts, FlagService flags, HttpContext context) =>
            {
                var outcome = attempts.Answer(slug, id, body.Answer ?? "");
                var name = OutcomeName(outcome);
                Telemetry.Answered(slug, name);
                events.LogInformation("Answer in {Room}: {Outcome}", slug, name);
                if (outcome == AnswerOutcome.Correct)
                {
                    await Leaderboard.RecordIfSignedInAsync(context, Achievement.Treasure, slug);
                }
                return outcome switch
                {
                    AnswerOutcome.Correct => Results.Ok(new { Outcome = name, Flag = flags.ForRoom(slug) }),
                    AnswerOutcome.UnknownAttempt => Results.NotFound(new { Outcome = name }),
                    _ => Results.UnprocessableEntity(new { Outcome = name }),
                };
            });

        api.MapPost("/treasure/claim", async (ClaimRequest body, DungeonCatalog catalog, FlagService flags, HttpContext context) =>
        {
            var flag = body.Flag?.Trim();
            // Accept a flag on its own: find which room it belongs to.
            var floor = catalog.Floors.FirstOrDefault(f =>
                (body.Room is null || f.Room.Slug == body.Room) && flags.IsValid(f.Room.Slug, flag));
            Telemetry.TreasureClaimed(floor?.Room.Slug);
            events.LogInformation("Treasure claim for {Room}", floor?.Room.Slug ?? "invalid");
            if (floor is not null)
            {
                // The flag is signed by this server, so a valid one proves the room was cleared.
                await Leaderboard.RecordIfSignedInAsync(context, Achievement.Treasure, floor.Room.Slug);
            }
            return floor is null
                ? Results.Ok(new { Valid = false })
                : Results.Ok(new { Valid = true, Room = floor.Room.Slug, Floor = floor.Number, Treasure = floor.Room.Name });
        });
    }

    private static string OutcomeName(AnswerOutcome outcome) => outcome switch
    {
        AnswerOutcome.Correct => "correct",
        AnswerOutcome.UnknownAttempt => "unknown-attempt",
        AnswerOutcome.TooSlow => "too-slow",
        _ => "wrong",
    };
}
