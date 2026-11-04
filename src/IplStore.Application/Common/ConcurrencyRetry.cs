using IplStore.Domain.Exceptions;

namespace IplStore.Application.Common;

/// <summary>Retries optimistic concurrency failures with exponential backoff and jitter. Bounded, so it never spins forever.</summary>
public static class ConcurrencyRetry
{
    private const double MaxDelayMilliseconds = 250;

    public static async Task<T> RunAsync<T>(Func<Task<T>> operation, StoreMetrics metrics, int maxAttempts, CancellationToken ct)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await operation();
            }
            catch (ConcurrencyException)
            {
                metrics.ConcurrencyConflicts.Add(1);

                if (attempt >= maxAttempts)
                {
                    throw new ConflictException("concurrency_conflict", "The data changed repeatedly while processing your request. Please retry.");
                }

                // 20, 40, 80, ... ms, capped at 250 ms, with full jitter so competing requests spread out.
                var ceiling = Math.Min(MaxDelayMilliseconds, 10 * Math.Pow(2, attempt));
                var delay = TimeSpan.FromMilliseconds(Random.Shared.NextDouble() * ceiling);
                await Task.Delay(delay, ct);
            }
        }
    }
}
