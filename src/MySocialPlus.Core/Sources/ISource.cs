using MySocialPlus.Core.Model;

namespace MySocialPlus.Core.Sources;

/// <summary>
/// One network. A source reads an account — a friend's posts, or the activity on Ron's own posts —
/// through the network's official API (rule 1: never scraping).
/// </summary>
public interface ISource
{
    Network Network { get; }

    /// <summary>
    /// Fills in what the network knows about an account before its first read: its stable id, name
    /// and picture. Changes the account in place.
    /// </summary>
    Task PrepareAsync(Account account, CancellationToken ct);

    /// <summary>The newest items for the account. Throws <see cref="SourceException"/> with words for Ron.</summary>
    Task<IReadOnlyList<Item>> FetchAsync(Account account, DateTimeOffset now, CancellationToken ct);
}

public enum SourceProblem
{
    /// <summary>Anything else: the network is down, the answer made no sense.</summary>
    Failed,
    /// <summary>The network said "slow down".</summary>
    RateLimited,
    /// <summary>The sign-in is missing or no longer works; Ron has to sign in again.</summary>
    SignInNeeded,
    /// <summary>The account does not exist (or no longer does).</summary>
    NotFound,
}

/// <summary>A read that did not work, with a message a first-time user can act on.</summary>
public sealed class SourceException : Exception
{
    public SourceException(SourceProblem problem, string message, TimeSpan? retryAfter = null, Exception? inner = null)
        : base(message, inner)
    {
        Problem = problem;
        RetryAfter = retryAfter;
    }

    public SourceProblem Problem { get; }
    public TimeSpan? RetryAfter { get; }
}
