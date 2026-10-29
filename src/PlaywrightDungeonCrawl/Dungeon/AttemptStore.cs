using Microsoft.Extensions.Caching.Memory;

namespace PlaywrightDungeonCrawl.Dungeon;

public sealed record Attempt(Guid Id, Room Room, RoomPuzzle Puzzle, DateTimeOffset StartedAt, DateTimeOffset Deadline);

public enum AnswerOutcome { Correct, Wrong, TooSlow, UnknownAttempt }

/// <summary>
/// Holds in-flight room attempts. Attempts are single-use: answering removes
/// them, so an answer can't be brute-forced against one puzzle.
/// </summary>
public sealed class AttemptStore(IMemoryCache cache, TimeProvider clock)
{
    private static readonly TimeSpan Retention = TimeSpan.FromMinutes(5);

    public Attempt Start(Room room, Random rng)
    {
        var now = clock.GetUtcNow();
        var attempt = new Attempt(Guid.NewGuid(), room, room.Generate(rng), now, now + room.TimeLimit);
        cache.Set(Key(room.Slug, attempt.Id), attempt, Retention);
        return attempt;
    }

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

    public AnswerOutcome Answer(string roomSlug, Guid attemptId, string answer)
    {
        var key = Key(roomSlug, attemptId);
        if (!cache.TryGetValue(key, out Attempt? attempt))
        {
            return AnswerOutcome.UnknownAttempt;
        }
        cache.Remove(key);

        if (clock.GetUtcNow() > attempt!.Deadline)
        {
            return AnswerOutcome.TooSlow;
        }
        return attempt.Room.IsCorrect(attempt.Puzzle, answer) ? AnswerOutcome.Correct : AnswerOutcome.Wrong;
    }

    private static string Key(string roomSlug, Guid id) => $"attempt:{roomSlug}:{id}";
}
