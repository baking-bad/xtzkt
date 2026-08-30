using Microsoft.EntityFrameworkCore;
using Xtzkt.Data.Models;
using Xtzkt.Indexers.Common.Extensions;
using Xtzkt.Indexers.TezosX.Protocols.Models;

namespace Xtzkt.Indexers.TezosX.Protocols.Proto01;

class ProtoMigrator(ProtocolHandler proto) : ProtocolCommit(proto), IMigrator
{
    public async Task MigrateContext(XChain state, MetaBlock block)
    {
        #region protocol
        var prev = await Cache.Protocols.GetAsync(state.Kernel);
        Db.TryAttach(prev);
        prev.LastLevel = state.Level;

        var protocol = new XProtocol
        {
            Id = Cache.Chain.NextProtocolId(),
            ChainId = state.Id,
            Hash = state.KernelUpgrade!,
            Version = Proto.Version,
            FirstLevel = Context.Block.Level,
            LastLevel = -1,
            // in Tezos X `next_protocol` is actually the current protocol
            MichelsonHash = block.MichelsonBlock?.Required("metadata").RequiredString("next_protocol"),
            MinBlockTimeMs = prev.MinBlockTimeMs,
            MaxBlockTimeMs = prev.MaxBlockTimeMs,
            ByteCost = prev.ByteCost,
            OriginationSize = prev.OriginationSize,
            DaFeePerByte = prev.DaFeePerByte,
            DaFeePerByte18 = prev.DaFeePerByte18,
            HardEvmBlockGasLimit = prev.HardEvmBlockGasLimit,
            HardEvmOperationGasLimit = prev.HardEvmOperationGasLimit,
            HardMichelsonBlockGasLimit = prev.HardMichelsonBlockGasLimit,
            HardMichelsonOperationGasLimit = prev.HardMichelsonOperationGasLimit,
            HardMichelsonOperationStorageLimit = prev.HardMichelsonOperationStorageLimit,
        };

        state.Kernel = protocol.Hash;
        state.MichelsonProtocol = protocol.MichelsonHash;
        Context.Block.ProtocolId = protocol.Id;
        Context.Protocol = protocol;

        Cache.Protocols.Add(protocol);
        Db.Protocols.Add(protocol);
        #endregion

        if (prev.Version != protocol.Version)
            await ApplyMigrations(state, block);
    }

    public async Task RevertContext(XChain state)
    {
        var protocols = await Db.Protocols
            .OfType<XProtocol>()
            .Where(x => x.ChainId == state.Id)
            .OrderByDescending(x => x.Id)
            .Take(2)
            .ToListAsync();
        var protocol = protocols[0];
        var prev = protocols[1];

        if (prev.Version != protocol.Version)
            await RevertMigrations(state);

        #region protocol
        state.KernelUpgrade = state.Kernel;
        state.KernelUpgradeTime = Context.Block.Timestamp;
        state.Kernel = prev.Hash;
        state.MichelsonProtocol = prev.MichelsonHash;
        
        prev.LastLevel = -1;
        Cache.Protocols.Add(prev);

        Cache.Chain.ReleaseProtocolId();
        Cache.Protocols.Remove(protocol);
        Db.Protocols.Remove(protocol);
        #endregion

    }

    protected virtual Task ApplyMigrations(XChain state, MetaBlock block)
    {
        return Task.CompletedTask;
    }

    protected virtual Task RevertMigrations(XChain state)
    {
        return Task.CompletedTask;
    }
}
