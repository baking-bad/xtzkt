using System.Diagnostics;

namespace Xtzkt.Utils.Network;

public sealed class RateLimiter(int rps)
{
    readonly Lock _crit = new();
    readonly long _interval = Math.Max(1, Stopwatch.Frequency / Math.Max(1, rps));
    long _nextSlot = Stopwatch.GetTimestamp();

    public Task AcquireAsync(CancellationToken ct)
    {
        long now, slot;
        lock (_crit)
        {
            now = Stopwatch.GetTimestamp();
            slot = Math.Max(now, _nextSlot);
            _nextSlot = slot + _interval;
        }

        return slot > now
            ? Task.Delay(Stopwatch.GetElapsedTime(now, slot), ct)
            : Task.CompletedTask;
    }
}
