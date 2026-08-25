using Xtzkt.Data.Models;
using Xtzkt.Indexers.TezosX.Protocols.Abstract;
using Xtzkt.Indexers.TezosX.Services;
using Xtzkt.Indexers.TezosX.Protocols.Proto08.Helpers.MetaBlock;

namespace Xtzkt.Indexers.TezosX.Protocols.Proto08.Helpers
{
    public class ProtoHelpers(ProtocolHandler protocol) : IHelpers
    {
        protected readonly IEvmRpc EvmRpc = protocol.EvmRpc;
        protected readonly IEvmRuntime EvmRuntime = protocol.EvmRuntime;
        protected readonly IMichelsonRpc MichelsonRpc = protocol.MichelsonRpc;
        protected readonly IMichelsonRuntime MichelsonRuntime = protocol.MichelsonRuntime;
        protected readonly CacheService Cache = protocol.Cache;
        protected readonly ILogger Logger = protocol.Logger;

        public Task<IMetaBlock> GetMetaBlock(XChain state)
            => new MetaBlockBuilder(EvmRpc, EvmRuntime, MichelsonRpc, MichelsonRuntime, Cache, Logger).GetNextBlock(state);
    }
}
