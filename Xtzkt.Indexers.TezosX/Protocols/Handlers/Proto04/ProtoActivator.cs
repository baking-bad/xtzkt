using Xtzkt.Data.Models;
using Xtzkt.Indexers.TezosX.Protocols.Models;

namespace Xtzkt.Indexers.TezosX.Protocols.Proto04;

public class ProtoActivator(ProtocolHandler proto) : ProtocolCommit(proto), IActivator
{
    protected readonly IMichelsonRpc MichelsonRpc = proto.MichelsonRpc;

    public async Task ActivateEvmContext(XChain state)
    {
        throw new NotImplementedException();
    }

    public Task ActivateMichelsonContext(XChain state, MetaBlock block)
    {
        throw new NotImplementedException();
    }

    public async Task DeactivateEvmContext(XChain state)
    {
        throw new NotImplementedException();
    }

    public Task DeactivateMichelsonContext(XChain state)
    {
        throw new NotImplementedException();
    }
}
