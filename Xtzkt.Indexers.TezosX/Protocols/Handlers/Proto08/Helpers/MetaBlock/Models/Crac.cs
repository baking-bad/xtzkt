using Xtzkt.Indexers.TezosX.Protocols.Abstract;

namespace Xtzkt.Indexers.TezosX.Protocols.Proto08.Helpers.MetaBlock;

public class CracOperation(IMetaOperationContent gatewayCall, IMetaInternalOperationContent targetCall) : IMetaOperationContent
{
    public IMetaOperationContent GatewayCall { get; } = gatewayCall;
    public IMetaInternalOperationContent TargetCall { get; } = targetCall;

    public override string ToString()
    {
        return $"{GatewayCall}  ||  {TargetCall}";
    }
}

public class InternalCracOperation(IMetaInternalOperationContent gatewayCall, IMetaInternalOperationContent targetCall) : IMetaInternalOperationContent
{
    public IMetaInternalOperationContent GatewayCall { get; } = gatewayCall;
    public IMetaInternalOperationContent TargetCall { get; } = targetCall;

    public override string ToString()
    {
        return $"{GatewayCall}  ||  {TargetCall}";
    }
}
