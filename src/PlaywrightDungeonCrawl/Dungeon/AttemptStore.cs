using Microsoft.Extensions.Caching.Memory;

namespace PlaywrightDungeonCrawl.Dungeon;

/// <param name="Curse">Set for a Cursed Run, practice or for keeps.</param>
/// <param name="Run">Set when the attempt came from a run link: breaking the curse is recorded for its owner.</param>
public sealed record Attempt(Guid Id, Room Room, RoomPuzzle Puzzle, DateTimeOffset StartedAt, DateTimeOffset Deadline,
    Curse? Curse = null, CursedRun? Run = null)
{
    /// <summary>How many of the puzzle's trials have been cleared, in order.</summary>
    public int TrialsCleared { get; set; }
}

public enum AnswerOutcome { Correct, Wrong, TooSlow, UnknownAttempt }

/// <param name="Unlocks">What the cleared trial hands over, when <see cref="Outcome"/> is Correct.</param>
public sealed record TrialResult(AnswerOutcome Outcome, int Trial = 0, object? Unlocks = null);

/// <summary>
/// Holds in-flight room attempts. Attempts are single-use: answering removes
/// them, so an answer can't be brute-forced against one puzzle.
/// </summary>
public sealed class AttemptStore(IMemoryCache cache, TimeProvider clock)
{
    private static readonly TimeSpan Retention = TimeSpan.FromMinutes(5);

    public Attempt Start(Room room, Random rng, Curse? curse = null, CursedRun? run = null)
    {
        var now = clock.GetUtcNow();
        var attempt = new Attempt(Guid.NewGuid(), room, room.Generate(rng), now, now + Curses.TimeLimit(room, curse), curse, run);
        cache.Set(Key(room.Slug, attempt.Id), attempt, Retention);
        return attempt;
    }

    /// <summary>The attempt, without using it up (e.g. for the portal tab to know the room's curse).</summary>
    public Attempt? Peek(string roomSlug, Guid attemptId) =>
        cache.TryGetValue(Key(roomSlug, attemptId), out Attempt? attempt) ? attempt : null;

    /// <summary>
    /// The room's hidden data, once its reveal delay has passed; null before
    /// that, or if the attempt doesn't exist.
    /// </summary>
    public object? Reveal(string roomSlug, Guid attemptId)
    {
        if (!cache.TryGetValue(Key(roomSlug, attemptId), out Attempt? attempt) || attempt!.Puzzle.Reveal is null)
        {
            return null;
        }
        return clock.GetUtcNow() >= attempt.StartedAt + attempt.Puzzle.RevealAfter ? attempt.Puzzle.Reveal : null;
    }

    /// <summary>
    /// Checks the attempt's next trial. A wrong or late answer springs the trap
    /// and uses the attempt up, like a wrong final answer does.
    /// </summary>
    public TrialResult Trial(string roomSlug, Guid attemptId, string answer)
    {
        var key = Key(roomSlug, attemptId);
        if (!cache.TryGetValue(key, out Attempt? attempt))
        {
            return new(AnswerOutcome.UnknownAttempt);
        }
        lock (attempt!)
        {
            var trials = attempt.Puzzle.Trials;
            if (attempt.TrialsCleared >= trials.Count)
            {
                // No trials left (or none at all): only the final answer is open.
                return new(AnswerOutcome.Wrong);
            }
            if (clock.GetUtcNow() > attempt.Deadline)
            {
                cache.Remove(key);
                return new(AnswerOutcome.TooSlow);
            }
            var trial = trials[attempt.TrialsCleared];
            if (!trial.Accepts(answer))
            {
                cache.Remove(key);
                return new(AnswerOutcome.Wrong, attempt.TrialsCleared + 1);
            }
            attempt.TrialsCleared++;
            return new(AnswerOutcome.Correct, attempt.TrialsCleared, trial.Unlocks);
        }
    }

    public AnswerOutcome Answer(string roomSlug, Guid attemptId, string answer) => Answer(roomSlug, attemptId, answer, out _);

    /// <param name="answered">The attempt that was answered, if there was one.</param>
    public AnswerOutcome Answer(string roomSlug, Guid attemptId, string answer, out Attempt? answered)
    {
        var key = Key(roomSlug, attemptId);
        if (!cache.TryGetValue(key, out Attempt? attempt))
        {
            answered = null;
            return AnswerOutcome.UnknownAttempt;
        }
        answered = attempt;
        cache.Remove(key);

        if (clock.GetUtcNow() > attempt!.Deadline)
        {
            return AnswerOutcome.TooSlow;
        }
        lock (attempt)
        {
            if (attempt.TrialsCleared < attempt.Puzzle.Trials.Count)
            {
                // Skipping ahead to the final answer doesn't count.
                return AnswerOutcome.Wrong;
            }
        }
        return attempt.Room.IsCorrect(attempt.Puzzle, answer) ? AnswerOutcome.Correct : AnswerOutcome.Wrong;
    }

    private static string Key(string roomSlug, Guid id) => $"attempt:{roomSlug}:{id}";
}
