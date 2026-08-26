using Microsoft.EntityFrameworkCore;
using Xtzkt.Data;
using Xtzkt.Data.Models;
using Xtzkt.Indexers.Common.Utils;

namespace Xtzkt.Indexers.Common.Cache;

public class XProtocolsCache(XtzktContext db, ChainConfig chain)
{
    #region static
    static readonly Dictionary<int, XProtocol> CachedById = new(37);
    static readonly Dictionary<string, XProtocol> CachedByHash = new(37);
    #endregion

    readonly XtzktContext Db = db;
    readonly ChainConfig Chain = chain;

    public async Task ResetAsync()
    {
        CachedById.Clear();
        CachedByHash.Clear();

        foreach (var protocol in await Db.XProtocols.Where(x => x.ChainId == Chain.Id).ToListAsync())
            Add(protocol);
    }

    public void Add(XProtocol protocol)
    {
        CachedById[protocol.Id] = protocol;
        CachedByHash[protocol.Hash] = protocol;
    }

    public void Remove(XProtocol protocol)
    {
        CachedById.Remove(protocol.Id);
        CachedByHash.Remove(protocol.Hash);
    }

    public async Task<XProtocol> GetAsync(int id)
    {
        if (!CachedById.TryGetValue(id, out var protocol))
        {
            protocol = await Db.XProtocols.FirstOrDefaultAsync(x => x.Id == id)
                ?? throw new Exception($"Protocol #{id} doesn't exist");

            Add(protocol);
        }

        return protocol;
    }

    public async Task<XProtocol> GetAsync(string hash)
    {
        if (!CachedByHash.TryGetValue(hash, out var protocol))
        {
            protocol = await Db.XProtocols.FirstOrDefaultAsync(x => x.Hash == hash)
                ?? throw new Exception($"Protocol {hash} doesn't exist");

            Add(protocol);
        }

        return protocol;
    }
}
