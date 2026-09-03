using Xtzkt.Data.Models;
using Xtzkt.Indexers.TezosX.Protocols.Models;

namespace Xtzkt.Indexers.TezosX.Protocols
{
    public interface IMigrator
    {
        Task MigrateContext(XChain state, MetaBlock block);

        Task RevertContext(XChain state);
    }
}
