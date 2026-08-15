using Xtzkt.Data.Models;

namespace Xtzkt.Indexers.TezosX.Protocols
{
    public interface IActivator
    {
        Task ActivateEvmContext(XChain state);
        Task ActivateMichelsonContext(XChain state);
        Task DeactivateEvmContext(XChain state);
        Task DeactivateMichelsonContext(XChain state);
    }
}
