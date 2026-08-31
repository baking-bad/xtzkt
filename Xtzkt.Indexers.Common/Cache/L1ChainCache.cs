using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xtzkt.Data;
using Xtzkt.Data.Models;
using Xtzkt.Data.Models.Operations.Abstract;
using Xtzkt.Indexers.Common.Utils;

namespace Xtzkt.Indexers.Common.Cache;

public interface IChainCache
{
    int GetLevel();
    int GetNextLevel();
    int NextAddressId();
    int GetManagerCounter();
}

public class L1ChainCache(XtzktContext db, IConfiguration config) : IChainCache
{
    static L1Chain Chain = null!;
    static int ChainIdMask32 = 0;
    static long ChainIdMask64 = 0;

    readonly XtzktContext Db = db;
    readonly ChainConfig ChainConfig = config.GetChainConfig();

    public async Task ResetAsync()
    {
        Chain = (await Db.Chains.SingleAsync(x => x.Id == ChainConfig.Id) as L1Chain)!;
        ChainIdMask32 = Chain.Id << 28;
        ChainIdMask64 = (long)Chain.Id << 60;
    }

    public L1Chain Get()
    {
        return Chain;
    }

    public string GetChainId()
    {
        return Chain.ChainId;
    }

    public int GetLevel()
    {
        return Chain.Level;
    }

    public int GetNextLevel()
    {
        return Chain.Level + 1;
    }

    public string GetHead()
    {
        return Chain.Hash;
    }

    public string GetNextProtocol()
    {
        return Chain.NextProtocol;
    }

    public int NextAddressId()
    {
        if (Chain.AddressCounter == 0xFFFFFFF)
            throw new Exception("Address count limit reached");

        return ChainIdMask32 + ++Chain.AddressCounter;
    }

    public void ReleaseAddressId(int count = 1)
    {
        // this isn't fully correct as it may break the indexer in case of reorg of a block
        // with new addresses, followed by "delayed" tokens, created other new addresses retroactively
        Chain.AddressCounter -= count;
    }

    public int NextStakingUpdateId()
    {
        if (Chain.StakingUpdatesCount == 0xFFFFFFF)
            throw new Exception("StakingUpdates count limit reached");

        return ChainIdMask32 + ++Chain.StakingUpdatesCount;
    }

    public void ReleaseStakingUpdateId()
    {
        Chain.StakingUpdatesCount--;
    }

    public int NextUnstakeReqeuestId()
    {
        if (Chain.UnstakeRequestsCount == 0xFFFFFFF)
            throw new Exception("UnstakeReqeuests count limit reached");

        return ChainIdMask32 + ++Chain.UnstakeRequestsCount;
    }

    public void ReleaseUnstakeReqeuestId()
    {
        Chain.UnstakeRequestsCount--;
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

    public int NextSmartRollupCommitmentId()
    {
        if (Chain.SmartRollupCommitmentCounter == 0xFFFFFFF)
            throw new Exception("SmartRollupCommitments count limit reached");

        return ChainIdMask32 + ++Chain.SmartRollupCommitmentCounter;
    }

    public void ReleaseSmartRollupCommitmentId()
    {
        Chain.SmartRollupCommitmentCounter--;
    }

    public int NextRefutationGameId()
    {
        if (Chain.RefutationGameCounter == 0xFFFFFFF)
            throw new Exception("RefutationGames count limit reached");

        return ChainIdMask32 + ++Chain.RefutationGameCounter;
    }

    public void ReleaseRefutationGameId()
    {
        Chain.RefutationGameCounter--;
    }

    public int NextInboxMessageId()
    {
        if (Chain.InboxMessageCounter == 0xFFFFFFF)
            throw new Exception("InboxMessages count limit reached");

        return ChainIdMask32 + ++Chain.InboxMessageCounter;
    }

    public void ReleaseInboxMessageId(int count)
    {
        Chain.InboxMessageCounter -= count;
    }

    public int NextProposalId()
    {
        if (Chain.ProposalCounter == 0xFFFFFFF)
            throw new Exception("Proposals count limit reached");

        return ChainIdMask32 + ++Chain.ProposalCounter;
    }

    public void ReleaseProposalId()
    {
        Chain.ProposalCounter--;
    }

    public int NextSoftwareId()
    {
        if (Chain.SoftwareCounter == 0xFFFF)
            throw new Exception("Software count limit reached");

        return (Chain.Id << 16) + ++Chain.SoftwareCounter;
    }

    public void ReleaseSoftwareId()
    {
        Chain.SoftwareCounter--;
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
        return Chain.ManagerCounter;
    }

    public void IncreaseManagerCounter(int value)
    {
        Chain.ManagerCounter += value;
    }

    public void ReleaseManagerCounter()
    {
        --Chain.ManagerCounter;
    }
}
