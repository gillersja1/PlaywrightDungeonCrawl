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
            (string slug, Guid id, AnswerRequest body, AttemptStore attempts, FlagService flags) =>
                attempts.Answer(slug, id, body.Answer ?? "") switch
                {
                    AnswerOutcome.Correct => Results.Ok(new { Outcome = "correct", Flag = flags.ForRoom(slug) }),
                    AnswerOutcome.UnknownAttempt => Results.NotFound(new { Outcome = "unknown-attempt" }),
                    AnswerOutcome.TooSlow => Results.UnprocessableEntity(new { Outcome = "too-slow" }),
                    _ => Results.UnprocessableEntity(new { Outcome = "wrong" }),
                });

        api.MapPost("/treasure/claim", (ClaimRequest body, DungeonCatalog catalog, FlagService flags) =>
        {
            var flag = body.Flag?.Trim();
            // Accept a flag on its own: find which room it belongs to.
            var floor = catalog.Floors.FirstOrDefault(f =>
                (body.Room is null || f.Room.Slug == body.Room) && flags.IsValid(f.Room.Slug, flag));
            return floor is null
                ? Results.Ok(new { Valid = false })
                : Results.Ok(new { Valid = true, Room = floor.Room.Slug, Floor = floor.Number, Treasure = floor.Room.Name });
        });
    }
}
