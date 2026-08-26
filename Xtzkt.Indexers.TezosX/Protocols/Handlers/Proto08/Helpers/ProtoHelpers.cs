using Xtzkt.Data;
using Xtzkt.Indexers.TezosX.Protocols.Abstract;
using Xtzkt.Indexers.TezosX.Services;

namespace Xtzkt.Indexers.TezosX.Protocols.Proto08.Helpers
{
    public partial class ProtoHelpers(ProtocolHandler protocol) : IHelpers
    {
        protected readonly ProtocolHandler Proto = protocol;
        protected readonly XtzktContext Db = protocol.Db;
        protected readonly IEvmRpc EvmRpc = protocol.EvmRpc;
        protected readonly IEvmRuntime EvmRuntime = protocol.EvmRuntime;
        protected readonly IMichelsonRpc MichelsonRpc = protocol.MichelsonRpc;
        protected readonly IMichelsonRuntime MichelsonRuntime = protocol.MichelsonRuntime;
        protected readonly CacheService Cache = protocol.Cache;
        protected readonly ILogger Logger = protocol.Logger;

        // the block context is reassigned by the handler on every block, so it must be read lazily
        protected BlockContext Context => Proto.Context;
    }
}
