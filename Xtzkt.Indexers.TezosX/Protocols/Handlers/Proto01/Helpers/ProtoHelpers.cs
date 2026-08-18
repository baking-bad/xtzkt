using Xtzkt.Data.Models;
using Xtzkt.Indexers.TezosX.Protocols.Abstract;
using Xtzkt.Indexers.TezosX.Protocols.Proto01.Helpers.MetaBlock;

namespace Xtzkt.Indexers.TezosX.Protocols.Proto01.Helpers
{
    public class ProtoHelpers(ProtocolHandler protocol) : IHelpers
    {
        protected readonly IEvmRpc EvmRpc = protocol.EvmRpc;
        protected readonly IMichelsonRpc MichelsonRpc = protocol.MichelsonRpc;
        protected readonly ILogger Logger = protocol.Logger;

        public Task<IMetaBlock> GetMetaBlock(XChain state)
            => new MetaBlockBuilder(EvmRpc, MichelsonRpc, Logger).GetNextBlock(state);
    }
}
