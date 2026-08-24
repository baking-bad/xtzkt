using Microsoft.EntityFrameworkCore;
using Xtzkt.Data;
using Xtzkt.Data.Models;
using Xtzkt.Indexers.Common.Extensions;
using Xtzkt.Indexers.TezosX.Protocols.Abstract;
using Xtzkt.Indexers.TezosX.Services;

namespace Xtzkt.Indexers.TezosX.Protocols.Proto01;

public class ProtoMigrator(ProtocolHandler proto) : IMigrator
{
    protected readonly ProtocolHandler Proto = proto;
    protected readonly IMichelsonRpc MichelsonRpc = proto.MichelsonRpc;
    protected readonly XtzktContext Db = proto.Db;
    protected readonly CacheService Cache = proto.Cache;
    protected readonly BlockContext Context = proto.Context;
    protected readonly ILogger Logger = proto.Logger;

    public async Task MigrateContext(XChain state, IMetaBlock block)
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
            MichelsonHash = prev.MichelsonHash,
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
    }

    public async Task RevertContext(XChain state)
    {
        #region protocol
        await Db.Protocols
            .Where(x => x.ChainId == state.Id && x.Hash == state.Kernel)
            .ExecuteDeleteAsync();

        Cache.Chain.ReleaseProtocolId();

        var prev = await Db.Protocols
            .OfType<XProtocol>()
            .Where(x => x.ChainId == state.Id)
            .OrderByDescending(x => x.Id)
            .FirstAsync();

        prev.LastLevel = -1;

        state.KernelUpgrade = state.Kernel;
        state.KernelUpgradeTime = Context.Block.Timestamp;
        state.Kernel = prev.Hash;
        state.MichelsonProtocol = prev.MichelsonHash;

        await Cache.Protocols.ResetAsync();
        #endregion
    }
}
