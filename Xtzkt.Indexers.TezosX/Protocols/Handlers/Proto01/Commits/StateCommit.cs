using System.Text.Json;
using Xtzkt.Indexers.Common.Extensions;
using Xtzkt.Indexers.TezosX.Protocols.Abstract;

namespace Xtzkt.Indexers.TezosX.Protocols.Proto01
{
    class StateCommit(ProtocolHandler protocol) : ProtocolCommit(protocol)
    {
        public virtual Task Apply(IMetaBlock block)
        {
            var state = Cache.Chain.Get();

            state.Level = block.Level;
            state.Timestamp = block.Timestamp;
            state.Hash = block.Hash;

            if (block.MichelsonBlock is JsonElement mb)
                state.MichelsonBlock = mb.RequiredString("hash");

            return Task.CompletedTask;
        }

        public virtual async Task Revert()
        {
            var state = Cache.Chain.Get();
            var prevBlock = await Cache.Blocks.PreviousAsync();

            state.Level = prevBlock.Level;
            state.Timestamp = prevBlock.Timestamp;
            state.Hash = prevBlock.Hash;

            state.MichelsonBlock = prevBlock.MichelsonHash;
        }
    }
}
