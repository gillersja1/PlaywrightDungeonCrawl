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

        // ?curse=haste practises a Cursed Run (no badge); ?run=<token> is a run link, played for its owner.
        api.MapPost("/rooms/{slug}/attempts", (string slug, string? curse, string? run, DungeonCatalog catalog,
            AttemptStore attempts, CursedRunStore runs) =>
        {
            if (catalog.FindByRoom(slug) is not { } floor)
            {
                return Results.NotFound();
            }
            CursedRun? cursedRun = null;
            var practice = Curses.Find(curse);
            if (!string.IsNullOrEmpty(run))
            {
                cursedRun = runs.Take(run, slug);
                if (cursedRun is null)
                {
                    // 410 Gone: expired, already used (Flawless), or for another room.
                    return Results.Json(new { Outcome = "expired-run" }, statusCode: StatusCodes.Status410Gone);
                }
            }
            else if (curse is not null && (practice is null || !Curses.Offers(floor.Room, practice)))
            {
                return Results.BadRequest(new { Outcome = "unknown-curse" });
            }
            var cursedBy = cursedRun?.Curse ?? practice;
            var attempt = attempts.Start(floor.Room, Random.Shared, cursedBy, cursedRun);
            Telemetry.AttemptStarted(slug);
            events.LogInformation("Attempt in {Room}", slug);
            return Results.Ok(new
            {
                AttemptId = attempt.Id,
                TimeLimitMs = (int)(attempt.Deadline - attempt.StartedAt).TotalMilliseconds,
                attempt.Puzzle.Briefing,
                Curse = cursedBy is null ? null : new { cursedBy.Code, cursedBy.Name, ForKeeps = cursedRun is not null, Player = cursedRun?.Login },
            });
        });

        api.MapGet("/rooms/{slug}/attempts/{id:guid}/reveal", (string slug, Guid id, AttemptStore attempts, HttpContext context) =>
        {
            // Pages the room opens elsewhere (floor 8's portal tab) need to know its curse too.
            if (attempts.Peek(slug, id)?.Curse is { } curse)
            {
                context.Response.Headers["X-Dungeon-Curse"] = curse.Code;
            }
            return attempts.Reveal(slug, id) is { } reveal
                ? Results.Ok(reveal)
                // 425 Too Early: the rune hasn't appeared yet (or never will).
                : Results.StatusCode(425);
        });

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
            async (string slug, Guid id, AnswerRequest body, AttemptStore attempts, FlagService flags, CursedRunStore runs,
                IPlayerRecords records, HttpContext context) =>
            {
                var outcome = attempts.Answer(slug, id, body.Answer ?? "", out var attempt);
                var name = OutcomeName(outcome);
                Telemetry.Answered(slug, name);
                events.LogInformation("Answer in {Room}: {Outcome}", slug, name);
                string? curseBroken = null;
                if (outcome == AnswerOutcome.Correct)
                {
                    await Leaderboard.RecordIfSignedInAsync(context, Achievement.Treasure, slug);
                    if (attempt?.Run is { } run)
                    {
                        // The run link says whose run this was, though the test's browser isn't signed in.
                        await records.RecordAsync(run.PlayerId, run.Login, Curses.AchievementKind, run.Curse.AchievementSlug(slug), context.RequestAborted);
                        runs.Finish(run);
                        curseBroken = run.Curse.Name;
                        events.LogInformation("Curse broken in {Room}: {Curse}", slug, run.Curse.Code);
                    }
                }
                return outcome switch
                {
                    AnswerOutcome.Correct => Results.Ok(new { Outcome = name, Flag = flags.ForRoom(slug), CurseBroken = curseBroken }),
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
