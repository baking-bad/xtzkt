using Xtzkt.Data.Models;
using Xtzkt.Indexers.TezosX.Protocols.Abstract;

namespace Xtzkt.Indexers.TezosX.Protocols.Proto02;

public class ProtoActivator(ProtocolHandler proto) : Proto02Commit(proto), IActivator
{
    protected readonly IMichelsonRpc MichelsonRpc = proto.MichelsonRpc;

    public async Task ActivateEvmContext(XChain state)
    {
        throw new NotImplementedException();
    }

    public Task ActivateMichelsonContext(XChain state, IMetaBlock block)
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
