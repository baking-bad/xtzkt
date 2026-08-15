using Xtzkt.Data.Models;
using Xtzkt.Indexers.TezosX.Protocols.Abstract;

namespace Xtzkt.Indexers.TezosX.Protocols
{
    public interface IMigrator
    {
        Task MigrateContext(XChain state, IMetaBlock block);

        Task RevertContext(XChain state);
    }
}
