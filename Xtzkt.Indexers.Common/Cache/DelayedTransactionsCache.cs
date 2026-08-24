namespace Xtzkt.Indexers.Common.Cache;

public readonly record struct DelayedTransaction(int Level, string Kind, byte[] Hash, byte[] Payload);

public class DelayedTransactionsCache
{
    #region static
    static int SoftCap = 0;
    static int TargetCap = 0;
    static Dictionary<string, DelayedTransaction> Cached = [];

    public static void Configure(CacheSize? size)
    {
        SoftCap = size?.SoftCap ?? 8_192;
        TargetCap = size?.TargetCap ?? 4_096;
        Cached = new(SoftCap + 1024);
    }
    #endregion

    public void Reset()
    {
        Cached.Clear();
    }

    public void Trim()
    {
        if (Cached.Count > SoftCap)
        {
            var toRemove = Cached
                .OrderBy(x => x.Value.Level)
                .Take(Cached.Count - TargetCap)
                .Select(x => x.Key)
                .ToList();

            foreach (var key in toRemove)
                Cached.Remove(key);
        }
    }

    public void Add(string hash, DelayedTransaction delayedTransaction)
    {
        Cached[hash] = delayedTransaction;
    }

    public bool TryGet(string hash, out DelayedTransaction delayedTransaction)
    {
        return Cached.TryGetValue(hash, out delayedTransaction);
    }
}
