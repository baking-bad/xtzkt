using System.Text.Json;
using Xtzkt.Data.Models;
using Xtzkt.Indexers.TezosX.Protocols.Proto01.Helpers.MetaBlock;

namespace Xtzkt.Indexers.TezosX.Protocols.Abstract;

public interface IHelpers
{
    Task<IMetaBlock> GetMetaBlock(XChain state);
}

public interface IMetaBlock
{
    int Level { get; }
    DateTime Timestamp { get; }
    string Hash { get; }
    List<IMetaBatch> Batches { get; }
    List<DelayedTransaction> Delayed { get; }

    JsonElement EvmBlock { get; }
    JsonElement? MichelsonBlock { get; }

    string? KernelUpgrade { get; }
    DateTime? KernelUpgradeTime { get; }
}

public interface IMetaBatch
{
    string Hash { get; }
    bool Delayed { get; }
    List<IMetaOperation> Operations { get; }
}

public interface IMetaOperation
{
    IMetaOperationContent Content { get; }
    List<IMetaInternalOperation> Internals { get; }
}

public interface IMetaOperationContent : IMetaContent { }

public interface IMetaInternalOperation
{
    IMetaInternalOperationContent Content { get; }
    IMetaContent? CracParent { get; }
}

public interface IMetaInternalOperationContent : IMetaContent { }

public interface IMetaContent { }
