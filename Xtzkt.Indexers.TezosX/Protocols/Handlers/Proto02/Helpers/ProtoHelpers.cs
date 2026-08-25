using Xtzkt.Data.Models;
using Xtzkt.Indexers.TezosX.Protocols.Abstract;
using Xtzkt.Indexers.TezosX.Services;
using Xtzkt.Indexers.TezosX.Protocols.Proto02.Helpers.MetaBlock;

namespace Xtzkt.Indexers.TezosX.Protocols.Proto02.Helpers
{
    public class ProtoHelpers(ProtocolHandler protocol) : IHelpers
    {
        protected readonly IEvmRpc EvmRpc = protocol.EvmRpc;
        protected readonly IEvmRuntime EvmRuntime = protocol.EvmRuntime;
        protected readonly CacheService Cache = protocol.Cache;
        protected readonly ILogger Logger = protocol.Logger;

        public Task<IMetaBlock> GetMetaBlock(XChain state)
            => new MetaBlockBuilder(EvmRpc, EvmRuntime, Cache, Logger).GetNextBlock(state);
    }
}
