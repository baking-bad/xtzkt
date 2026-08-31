using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xtzkt.Data;
using Xtzkt.Data.Models;
using Xtzkt.Data.Models.Operations.Abstract;
using Xtzkt.Indexers.Common.Utils;

namespace Xtzkt.Indexers.Common.Cache;

public class XChainCache(XtzktContext db, IConfiguration config) : IChainCache
{
    static XChain Chain = null!;
    static int ChainIdMask32 = 0;
    static long ChainIdMask64 = 0;

    readonly XtzktContext Db = db;
    readonly ChainConfig ChainConfig = config.GetChainConfig();

    public async Task ResetAsync()
    {
        if (await Db.Chains.SingleAsync(x => x.Id == ChainConfig.Id) is not XChain chain)
            throw new Exception("Invalid chain");

        Chain = chain;
        ChainIdMask32 = chain.Id << 28;
        ChainIdMask64 = (long)chain.Id << 60;
    }

    public XChain Get()
    {
        return Chain;
    }

    public int GetLevel()
    {
        return Chain.Level;
    }

    public int GetNextLevel()
    {
        return Chain.Level + 1;
    }

    public int NextAddressId()
    {
        if (Chain.AddressCounter == 0xFFFFFFF)
            throw new Exception("Addresses count limit reached");

        return ChainIdMask32 + ++Chain.AddressCounter;
    }

    public void ReleaseAddressId(int count = 1)
    {
        // this isn't fully correct as it may break the indexer in case of reorg of a block
        // with new addresses, followed by "delayed" tokens, created other new addresses retroactively
        Chain.AddressCounter -= count;
    }

    public int NextProtocolId()
    {
        if (Chain.ProtocolsCount == 0xFF)
            throw new Exception("Protocols count limit reached");

        return (Chain.Id << 8) + ++Chain.ProtocolsCount;
    }

    public void ReleaseProtocolId()
    {
        Chain.ProtocolsCount--;
    }

    public long NextOperationId()
    {
        if (Chain.OperationCounter == 0xFFFFFFFFFFL) // 40 bits
            throw new Exception("Operations count limit reached");

        return ChainIdMask64 | (++Chain.OperationCounter << 20);
    }

    public long NextSubId(ISourceOperation parent)
    {
        if (parent.SubsCounter == 0xFFFFFL) // 20 bits
            throw new Exception("Operation subs count limit reached");

        parent.SubsCounter = (parent.SubsCounter ?? 0) + 1;
        return parent.Id | (long)parent.SubsCounter.Value;
    }

    public void ReleaseOperationId(long count = 1)
    {
        Chain.OperationCounter -= count;
    }

    public int NextBigMapId()
    {
        if (Chain.BigMapCounter == 0xFFFFFFF)
            throw new Exception("BigMaps count limit reached");

        return ChainIdMask32 + ++Chain.BigMapCounter;
    }

    public void ReleaseBigMapId(int count = 1)
    {
        Chain.BigMapCounter -= count;
    }

    public long NextBigMapKeyId()
    {
        //if (Chain.BigMapKeyCounter == 0xFFFFFFFFFFFFFFFL)
        //    throw new Exception("BigMapKeys count limit reached");

        return ChainIdMask64 + ++Chain.BigMapKeyCounter;
    }

    public void ReleaseBigMapKeyId(int count = 1)
    {
        Chain.BigMapKeyCounter -= count;
    }

    public long NextBigMapUpdateId()
    {
        //if (Chain.BigMapUpdateCounter == 0xFFFFFFFFFFFFFFFL)
        //    throw new Exception("BigMapUpdates count limit reached");

        return ChainIdMask64 + ++Chain.BigMapUpdateCounter;
    }

    public void ReleaseBigMapUpdateId(int count = 1)
    {
        Chain.BigMapUpdateCounter -= count;
    }

    public long NextLogId()
    {
        //if (Chain.LogsCounter == 0xFFFFFFFFFFFFFFFL)
        //    throw new Exception("Logs count limit reached");

        return ChainIdMask64 + ++Chain.LogsCounter;
    }

    public void ReleaseLogId(int count)
    {
        Chain.LogsCounter -= count;
    }

    public long NextStorageId()
    {
        //if (Chain.StorageCounter == 0xFFFFFFFFFFFFFFFL)
        //    throw new Exception("Storages count limit reached");

        return ChainIdMask64 + ++Chain.StorageCounter;
    }

    public void ReleaseStorageId(int count = 1)
    {
        Chain.StorageCounter -= count;
    }

    public int NextScriptId()
    {
        if (Chain.ScriptCounter == 0xFFFFFFF)
            throw new Exception("Scripts count limit reached");

        return ChainIdMask32 + ++Chain.ScriptCounter;
    }

    public void ReleaseScriptId(int count = 1)
    {
        Chain.ScriptCounter -= count;
    }

    public int GetManagerCounter()
    {
        return 0;
    }
}
