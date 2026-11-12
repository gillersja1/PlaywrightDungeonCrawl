using Microsoft.Extensions.Caching.Memory;

namespace PlaywrightDungeonCrawl.Dungeon;

public sealed record AnswerRequest(string? Answer);
public sealed record LoginRequest(string? Guest, string? Password);
public sealed record QuestionRequest(string? Question);
public sealed record CutRequest(Guid? Attempt);
public sealed record PetitionRequest(string? Office);
public sealed record SignInRequest(string? Role, string? Password);

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

        // ?curse=haste practices a Cursed Run (no badge); ?run=<token> is a run link, played for its owner.
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

        // Floor 10's dispatch: a real file download, so the seal never appears on the page.
        api.MapGet("/rooms/couriers-counter/attempts/{id:guid}/dispatch", (Guid id, AttemptStore attempts) =>
            attempts.Peek("couriers-counter", id)?.Puzzle.Reveal is CouriersCounter.Dispatch dispatch
                ? Results.File(System.Text.Encoding.UTF8.GetBytes(dispatch.Text), "text/plain", dispatch.FileName)
                : Results.NotFound());

        // Floor 11's ball: each guest signs in with a cookie of the same name, so two guests need two contexts.
        const string ball = "/rooms/masked-ball/attempts/{id:guid}";
        static MaskedBall.Ball? BallOf(AttemptStore attempts, Guid id) =>
            attempts.Peek("masked-ball", id)?.Puzzle.Reveal as MaskedBall.Ball;
        api.MapPost(ball + "/login", (Guid id, LoginRequest body, AttemptStore attempts, HttpContext context) =>
        {
            if (BallOf(attempts, id) is not { } b) return Results.NotFound();
            var mask = Guid.NewGuid().ToString("N");
            lock (b.Gate)
            {
                switch (body.Guest)
                {
                    case "Fox" when body.Password == b.FoxPassword: b.FoxMask = mask; break;
                    case "Owl" when body.Password == b.OwlPassword: b.OwlMask = mask; break;
                    default: return Results.Json(new { error = "The doorman checks the list twice and shakes his head." }, statusCode: 403);
                }
            }
            context.Response.Cookies.Append(MaskedBall.Cookie, mask, new CookieOptions
            {
                HttpOnly = true, SameSite = SameSiteMode.Strict, Path = $"/api/rooms/masked-ball/attempts/{id}",
            });
            return Results.Ok(new { guest = body.Guest });
        });
        api.MapPost(ball + "/curtsy", (Guid id, AttemptStore attempts, HttpContext context) =>
        {
            if (BallOf(attempts, id) is not { } b) return Results.NotFound();
            var mask = context.Request.Cookies[MaskedBall.Cookie];
            lock (b.Gate)
            {
                if (mask is null || mask != b.OwlMask) return Results.Json(new { error = "Only Owl can return the bow, and you aren't wearing Owl's mask." }, statusCode: 403);
                if (b.FoxMask is null) return Results.Json(new { error = "Owl curtsies to an empty ballroom. Fox hasn't arrived." }, statusCode: 409);
                b.CurtsiedTo = b.FoxMask;
            }
            return Results.Ok(new { whisper = b.Whisper });
        });
        api.MapPost(ball + "/unmask", (Guid id, AnswerRequest body, AttemptStore attempts, HttpContext context) =>
        {
            if (attempts.Peek("masked-ball", id) is not { Puzzle.Reveal: MaskedBall.Ball b } attempt) return Results.NotFound();
            var mask = context.Request.Cookies[MaskedBall.Cookie];
            lock (b.Gate)
            {
                if (mask is null || mask != b.FoxMask) return Results.Json(new { error = "The host only unmasks for Fox, and you aren't wearing Fox's mask." }, statusCode: 403);
                if (b.CurtsiedTo != mask) return Results.Json(new { error = "Owl hasn't returned this Fox's bow." }, statusCode: 409);
            }
            return body.Answer?.Trim() == b.Whisper
                ? Results.Ok(new { phrase = attempt.Puzzle.Answer })
                : Results.Json(new { error = "That isn't what Owl whispered." }, statusCode: 403);
        });

        // Floor 17's gallery: the portraits are PNGs. Hanging 2 (after the forger strikes) has one forgery in it.
        api.MapGet("/rooms/curators-eye/attempts/{id:guid}/portraits/{index:int}", (Guid id, int index, int? hanging,
            AttemptStore attempts, TimeProvider clock, HttpContext context) =>
        {
            if (attempts.Peek("curators-eye", id) is not { Puzzle.Reveal: CuratorsEye.Gallery g } attempt
                || index is < 0 or >= CuratorsEye.Count) return Results.NotFound();
            var second = hanging == 2;
            if (second && clock.GetUtcNow() - attempt.StartedAt < TimeSpan.FromMilliseconds(g.StrikeMs - 250))
                return Results.StatusCode(425); // the forger hasn't been yet
            context.Response.Headers.CacheControl = "no-store";
            var flaw = second && index == g.Forged ? g.Flaw : null;
            return Results.File(Portraits.Paint(g.Seeds[index], flaw), "image/png");
        });

        // Floor 16's pit: one hunt (the setup call), five heads, each faced and cut by its own crawler in its own context.
        const string pit = "/rooms/severing-pit/hunts";
        static SeveringPit.Hunt? HuntOf(IMemoryCache cache, Guid hunt) =>
            cache.TryGetValue($"hunt:{hunt}", out SeveringPit.Hunt? h) ? h : null;
        static IResult Refuse(string outcome, string error, int status) => Results.Json(new { outcome, error }, statusCode: status);
        api.MapPost(pit, (IMemoryCache cache) =>
        {
            var hunt = new SeveringPit.Hunt();
            cache.Set($"hunt:{hunt.Id}", hunt, TimeSpan.FromMinutes(5));
            events.LogInformation("Hunt started in {Room}", "severing-pit");
            return Results.Created($"/api/rooms/severing-pit/hunts/{hunt.Id}",
                new { huntId = hunt.Id, heads = SeveringPit.Heads, windowSeconds = SeveringPit.Window.TotalSeconds });
        });
        api.MapPost(pit + "/{hunt:guid}/heads/{head:int}/face", (Guid hunt, int head, CutRequest body, IMemoryCache cache,
            AttemptStore attempts, TimeProvider clock, HttpContext context) =>
        {
            if (HuntOf(cache, hunt) is not { } h || head is < 1 or > SeveringPit.Heads) return Results.NotFound();
            if (attempts.Peek("severing-pit", body.Attempt ?? Guid.Empty) is not { } attempt) return Results.NotFound();
            if (context.Request.Cookies[SeveringPit.ScarCookie] == hunt.ToString())
                return Refuse("scarred", "This hand is already at the pit, facing another head. Each head needs a crawler of their own.", 409);
            var now = clock.GetUtcNow();
            if (h.StateAt(now) is SeveringPit.HuntState.Regrown or SeveringPit.HuntState.Slain)
                return Refuse("regrown", "This hunt is over.", 409);
            int faced;
            lock (h.Gate)
            {
                if (!h.Faced.TryAdd(head, (attempt.Id, now)))
                    return Refuse("already-cut", "Another crawler is already facing that head.", 409);
                faced = h.Faced.Count;
            }
            context.Response.Cookies.Append(SeveringPit.ScarCookie, hunt.ToString(), new CookieOptions
            {
                HttpOnly = true, SameSite = SameSiteMode.Strict, Path = $"/api/rooms/severing-pit/hunts/{hunt}",
            });
            return Results.Ok(new { faced });
        });
        api.MapPost(pit + "/{hunt:guid}/heads/{head:int}/cut", (Guid hunt, int head, CutRequest body, IMemoryCache cache,
            TimeProvider clock) =>
        {
            if (HuntOf(cache, hunt) is not { } h || head is < 1 or > SeveringPit.Heads) return Results.NotFound();
            var now = clock.GetUtcNow();
            var state = h.StateAt(now);
            if (state == SeveringPit.HuntState.Regrown) return Refuse("regrown", "Too late: the heads have grown back.", 409);
            if (state == SeveringPit.HuntState.Gathering) return Refuse("too-soon", "The heads are still coiled, waiting for every crawler.", 425);
            int fallen;
            lock (h.Gate)
            {
                if (!h.Faced.TryGetValue(head, out var facer) || facer.Attempt != body.Attempt)
                    return Refuse("already-cut", "You aren't the crawler facing that head.", 409);
                if (!h.Cuts.TryAdd(head, (facer.Attempt, now)))
                    return Refuse("already-cut", "That head is already off.", 409);
                fallen = h.Cuts.Count;
            }
            return Results.Ok(new { fallen });
        });
        api.MapGet(pit + "/{hunt:guid}", (Guid hunt, Guid? attempt, IMemoryCache cache, AttemptStore attempts, TimeProvider clock) =>
        {
            if (HuntOf(cache, hunt) is not { } h) return Results.NotFound();
            var state = h.StateAt(clock.GetUtcNow());
            int faced, fallen;
            lock (h.Gate) (faced, fallen) = (h.Faced.Count, h.Cuts.Count);
            // The fang goes only to a crawler who cut one of the heads of a slain hydra.
            var fang = state == SeveringPit.HuntState.Slain && attempt is { } a && h.WasCutBy(a)
                ? attempts.Peek("severing-pit", a)?.Puzzle.Answer
                : null;
            return Results.Ok(new { state = state.ToString().ToLowerInvariant(), faced, fallen, heads = SeveringPit.Heads, fang });
        });

        // Floor 12's well: an API with no buttons. POST a question with the offering, follow the 201's Location.
        const string well = "/rooms/oracles-well/attempts/{id:guid}/questions";
        static bool Offered(OraclesWell.Well w, HttpContext context) =>
            context.Request.Headers[OraclesWell.OfferingHeader] == w.Offering;
        api.MapPost(well, (Guid id, QuestionRequest body, AttemptStore attempts, HttpContext context) =>
        {
            if (attempts.Peek("oracles-well", id)?.Puzzle.Reveal is not OraclesWell.Well w) return Results.NotFound();
            if (!Offered(w, context)) return Results.Json(new { error = "The well stays silent. It wants your offering, in the X-Offering header." }, statusCode: 401);
            if (string.IsNullOrWhiteSpace(body.Question)) return Results.BadRequest(new { error = "The Oracle can't answer a question you didn't ask." });
            var q = Guid.NewGuid();
            w.Questions[q] = body.Question.Trim();
            return Results.Created($"/api/rooms/oracles-well/attempts/{id}/questions/{q}", new { status = "The Oracle is pondering. Your answer is elsewhere." });
        });
        api.MapGet(well + "/{q:guid}", (Guid id, Guid q, AttemptStore attempts, HttpContext context) =>
        {
            if (attempts.Peek("oracles-well", id) is not { Puzzle.Reveal: OraclesWell.Well w } attempt || !w.Questions.TryGetValue(q, out var question)) return Results.NotFound();
            return Offered(w, context)
                ? Results.Ok(new { question, prophecy = attempt.Puzzle.Answer })
                : Results.Json(new { error = "The well stays silent. It wants your offering, in the X-Offering header." }, statusCode: 401);
        });

        // Floor 13, the second boss: a chain where every link is checked here, so none can be skipped or faked.
        const string chancellery = "/rooms/chancellors-seal/attempts/{id:guid}";
        static ChancellorsSeal.Chancellery? ChancelleryOf(AttemptStore attempts, Guid id) =>
            attempts.Peek("chancellors-seal", id)?.Puzzle.Reveal as ChancellorsSeal.Chancellery;
        static IResult Refused(string error, int status) => Results.Json(new { error }, statusCode: status);

        // Link 1, the API: the Chancellor's password is only ever filed with the Registry.
        api.MapPost(chancellery + "/registry", (Guid id, PetitionRequest body, AttemptStore attempts, HttpContext context) =>
        {
            if (ChancelleryOf(attempts, id) is not { } c) return Results.NotFound();
            if (context.Request.Headers[ChancellorsSeal.PetitionHeader] != c.Petition) return Refused("The Registry won't read a petition without its number, in the X-Petition header.", 401);
            if (body.Office?.Trim() != ChancellorsSeal.Office) return Refused("The Registry only keeps records for the office of Chancellor.", 422);
            var p = Guid.NewGuid();
            c.Petitions[p] = ChancellorsSeal.Office;
            return Results.Created($"/api/rooms/chancellors-seal/attempts/{id}/registry/{p}", new { status = "Filed. The record is elsewhere." });
        });
        api.MapGet(chancellery + "/registry/{p:guid}", (Guid id, Guid p, AttemptStore attempts, HttpContext context) =>
        {
            if (ChancelleryOf(attempts, id) is not { } c || !c.Petitions.ContainsKey(p)) return Results.NotFound();
            return context.Request.Headers[ChancellorsSeal.PetitionHeader] == c.Petition
                ? Results.Ok(new { office = ChancellorsSeal.Office, password = c.ChancellorPassword })
                : Refused("The Registry won't read a petition without its number, in the X-Petition header.", 401);
        });

        // Link 2, contexts: the Clerk and the Chancellor wear a cookie of the same name.
        api.MapPost(chancellery + "/login", (Guid id, SignInRequest body, AttemptStore attempts, HttpContext context) =>
        {
            if (ChancelleryOf(attempts, id) is not { } c) return Results.NotFound();
            var seal = Guid.NewGuid().ToString("N");
            lock (c.Gate)
            {
                switch (body.Role)
                {
                    case "Clerk" when body.Password == c.ClerkPassword: c.ClerkSeal = seal; break;
                    case ChancellorsSeal.Office when body.Password == c.ChancellorPassword: c.ChancellorSeal = seal; break;
                    default: return Refused("The usher checks the roll twice and shakes his head.", 403);
                }
            }
            context.Response.Cookies.Append(ChancellorsSeal.Cookie, seal, new CookieOptions
            {
                HttpOnly = true, SameSite = SameSiteMode.Strict, Path = $"/api/rooms/chancellors-seal/attempts/{id}",
            });
            return Results.Ok(new { role = body.Role });
        });

        // Link 4, the mouse: the strongroom page sends this when the seal is dropped on the charter.
        api.MapPost(chancellery + "/seal", (Guid id, AttemptStore attempts, HttpContext context) =>
        {
            if (ChancelleryOf(attempts, id) is not { } c) return Results.NotFound();
            var seal = context.Request.Cookies[ChancellorsSeal.Cookie];
            lock (c.Gate)
            {
                if (seal is null || seal != c.ChancellorSeal) return Refused("Only the Chancellor can press the seal, and you aren't wearing the Chancellor's.", 403);
                if (c.ClerkSeal is null) return Refused("There's no Clerk on duty to receive the charter.", 409);
                c.CharterSeal = Guid.NewGuid().ToString("N")[..10].ToUpperInvariant();
                c.SealedForClerk = c.ClerkSeal;
                c.SealedByChancellor = c.ChancellorSeal;
            }
            return Results.Ok(new { charter = $"/api/rooms/chancellors-seal/attempts/{id}/charter" });
        });

        // Link 5, files: the sealed charter is a real download, and only the Chancellor who sealed it can collect it.
        api.MapGet(chancellery + "/charter", (Guid id, AttemptStore attempts, HttpContext context) =>
        {
            if (ChancelleryOf(attempts, id) is not { } c) return Results.NotFound();
            lock (c.Gate)
            {
                if (c.CharterSeal is null) return Refused("The charter hasn't been sealed yet.", 409);
                if (context.Request.Cookies[ChancellorsSeal.Cookie] != c.SealedByChancellor) return Refused("The strongroom only opens for the Chancellor who sealed it.", 403);
                return Results.File(System.Text.Encoding.UTF8.GetBytes(ChancellorsSeal.CharterText(c)), "text/plain", "sealed-charter.txt");
            }
        });
        api.MapPost(chancellery + "/file", (Guid id, AnswerRequest body, AttemptStore attempts, HttpContext context) =>
        {
            if (attempts.Peek("chancellors-seal", id) is not { Puzzle.Reveal: ChancellorsSeal.Chancellery c } attempt) return Results.NotFound();
            var seal = context.Request.Cookies[ChancellorsSeal.Cookie];
            lock (c.Gate)
            {
                if (seal is null || seal != c.ClerkSeal) return Refused("Only the Clerk files charters, and you aren't wearing the Clerk's seal.", 403);
                if (c.CharterSeal is null || c.SealedForClerk != seal) return Refused("No charter has been sealed for this Clerk.", 409);
                // Both still at their posts: a Chancellor who signed in again isn't the one who sealed it.
                if (c.ChancellorSeal != c.SealedByChancellor) return Refused("The Chancellor who sealed this charter has left the building.", 409);
            }
            return ChancellorsSeal.IsFiled(c, body.Answer ?? "")
                ? Results.Ok(new { phrase = attempt.Puzzle.Answer })
                : Refused("The Clerk reads it twice. That isn't the charter, or it isn't filed under the right name.", 422);
        });

        // Floor 14's stage: the real act keeps the curtain down, and the heckler always turns up. Tests change both.
        api.MapGet("/rooms/illusionists-stage/attempts/{id:guid}/act", (Guid id, AttemptStore attempts) =>
            attempts.Peek("illusionists-stage", id) is { } attempt
                ? Results.Ok(new { curtain = "down", word = attempt.Puzzle.Answer })
                : Results.NotFound());
        api.MapGet("/rooms/illusionists-stage/attempts/{id:guid}/heckler", (Guid id, AttemptStore attempts) =>
            attempts.Peek("illusionists-stage", id) is not null
                ? Results.Ok(new { heckle = "I can see the wires!" })
                : Results.NotFound());

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
