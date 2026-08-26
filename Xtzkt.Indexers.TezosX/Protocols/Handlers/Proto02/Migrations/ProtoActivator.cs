using Xtzkt.Data.Models;
using Xtzkt.Indexers.TezosX.Protocols.Models;

namespace Xtzkt.Indexers.TezosX.Protocols.Proto02;

class ProtoActivator(ProtocolHandler proto) : ProtocolCommit(proto), IActivator
{
    public Task ActivateEvmContext(XChain state)
    {
        // activation logic for old protocols isn't really needed
        throw new NotImplementedException();
    }

    public Task DeactivateEvmContext(XChain state)
    {
        // deactivation logic for old protocols isn't really needed
        throw new NotImplementedException();
    }

    public Task ActivateMichelsonContext(XChain state, MetaBlock block)
    {
        // activation logic for old protocols isn't really needed
        throw new NotImplementedException();
    }

    public Task DeactivateMichelsonContext(XChain state)
    {
        // deactivation logic for old protocols isn't really needed
        throw new NotImplementedException();
    }
}
