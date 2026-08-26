namespace Xtzkt.Indexers.TezosX.Protocols.Models;

public sealed class CracOperation : MetaOperation
{
    public required MetaOperation GatewayCall { get; init; }
    public required MetaInternalOperation TargetCall { get; init; }
}

public sealed class InternalCracOperation : MetaInternalOperation
{
    public required MetaInternalOperation GatewayCall { get; init; }
    public required MetaInternalOperation TargetCall { get; init; }
}
