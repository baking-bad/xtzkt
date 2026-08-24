using Xtzkt.Data.Models;

namespace Xtzkt.Indexers.TezosX.Protocols.Abstract;

public interface IHelpers
{
    Task<IMetaBlock> GetMetaBlock(XChain state);
}
