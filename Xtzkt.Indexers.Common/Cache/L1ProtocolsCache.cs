using Microsoft.EntityFrameworkCore;
using Xtzkt.Data;
using Xtzkt.Data.Models;
using Xtzkt.Indexers.Common.Utils;

namespace Xtzkt.Indexers.Common.Cache;

public class L1ProtocolsCache(XtzktContext db, ChainConfig chain)
{
    #region static
    static readonly Dictionary<int, L1Protocol> CachedById = new(37);
    static readonly Dictionary<string, L1Protocol> CachedByHash = new(37);
    #endregion

    readonly XtzktContext Db = db;
    readonly ChainConfig Chain = chain;

    public async Task ResetAsync()
    {
        CachedById.Clear();
        CachedByHash.Clear();

        foreach (var protocol in await Db.L1Protocols.Where(x => x.ChainId == Chain.Id).ToListAsync())
            Add(protocol);
    }

    public void Add(L1Protocol protocol)
    {
        CachedById[protocol.Id] = protocol;
        CachedByHash[protocol.Hash] = protocol;
    }

    public async Task<L1Protocol> GetAsync(int id)
    {
        if (!CachedById.TryGetValue(id, out var protocol))
        {
            protocol = await Db.L1Protocols.FirstOrDefaultAsync(x => x.Id == id)
                ?? throw new Exception($"Protocol #{id} doesn't exist");

            Add(protocol);
        }

        return protocol;
    }

    public async Task<L1Protocol> GetAsync(string hash)
    {
        if (!CachedByHash.TryGetValue(hash, out var protocol))
        {
            protocol = await Db.L1Protocols.FirstOrDefaultAsync(x => x.ChainId == Chain.Id && x.Hash == hash)
                ?? throw new Exception($"Protocol {hash} doesn't exist");

            Add(protocol);
        }

        return protocol;
    }

    public async Task<L1Protocol> FindByCycleAsync(int cycle)
    {
        var protocol = CachedById.Values
            .OrderByDescending(x => x.Id)
            .FirstOrDefault(x => x.FirstCycle <= cycle);

        if (protocol == null)
        {
            protocol = await Db.L1Protocols
                .OrderByDescending(x => x.Id)
                .FirstOrDefaultAsync(x => x.ChainId == Chain.Id && x.FirstCycle <= cycle)
                    ?? throw new Exception($"Protocol for cycle {cycle} doesn't exist");

            Add(protocol);
        }

        return protocol;
    }

    public async Task<L1Protocol> FindByLevelAsync(int level)
    {
        var protocol = CachedById.Values
            .OrderByDescending(x => x.Id)
            .FirstOrDefault(x => x.FirstLevel <= level);

        if (protocol == null)
        {
            protocol = await Db.L1Protocols
                .OrderByDescending(x => x.Id)
                .FirstOrDefaultAsync(x => x.ChainId == Chain.Id && x.FirstLevel <= level)
                    ?? throw new Exception($"Protocol for level {level} doesn't exist");

            Add(protocol);
        }

        return protocol;
    }

    public L1Protocol FindByCycle(int cycle)
    {
        return CachedById.Values
            .OrderByDescending(x => x.Id)
            .FirstOrDefault(x => x.FirstCycle <= cycle)
                ?? throw new Exception($"Protocol for cycle {cycle} doesn't exist");
    }

    public int GetCycleStart(int cycle)
    {
        return FindByCycle(cycle).GetCycleStart(cycle);
    }

    public int GetCycleEnd(int cycle)
    {
        return FindByCycle(cycle).GetCycleEnd(cycle);
    }
}
