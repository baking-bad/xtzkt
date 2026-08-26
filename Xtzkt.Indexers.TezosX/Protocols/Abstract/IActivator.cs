using Xtzkt.Data.Models;
using Xtzkt.Indexers.TezosX.Protocols.Models;

namespace Xtzkt.Indexers.TezosX.Protocols
{
    public interface IActivator
    {
        Task ActivateEvmContext(XChain state);
        Task ActivateMichelsonContext(XChain state, MetaBlock block);
        Task DeactivateEvmContext(XChain state);
        Task DeactivateMichelsonContext(XChain state);
    }
}
