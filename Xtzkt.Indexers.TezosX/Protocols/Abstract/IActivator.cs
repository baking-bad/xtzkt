using Xtzkt.Data.Models;
using Xtzkt.Indexers.TezosX.Protocols.Abstract;

namespace Xtzkt.Indexers.TezosX.Protocols
{
    public interface IActivator
    {
        Task ActivateEvmContext(XChain state);
        Task ActivateMichelsonContext(XChain state, IMetaBlock block);
        Task DeactivateEvmContext(XChain state);
        Task DeactivateMichelsonContext(XChain state);
    }
}
