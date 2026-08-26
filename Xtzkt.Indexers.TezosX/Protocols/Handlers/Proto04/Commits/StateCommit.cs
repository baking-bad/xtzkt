using Xtzkt.Indexers.TezosX.Protocols.Models;

namespace Xtzkt.Indexers.TezosX.Protocols.Proto04
{
    class StateCommit(ProtocolHandler protocol) : ProtocolCommit(protocol)
    {
        public virtual Task Apply(MetaBlock block)
        {
            var state = Cache.Chain.Get();

            state.Level = block.Level;
            state.Timestamp = block.Timestamp;
            state.Hash = block.Hash;

            return Task.CompletedTask;
        }

        public virtual async Task Revert()
        {
            var state = Cache.Chain.Get();
            if (state.Level != 0)
            {
                var prevBlock = await Cache.Blocks.PreviousAsync();

                state.Level = prevBlock.Level;
                state.Timestamp = prevBlock.Timestamp;
                state.Hash = prevBlock.Hash;

                state.MichelsonBlock = prevBlock.MichelsonHash;
            }
            else
            {
                state.Level = -1;
                state.Timestamp = DateTimeOffset.MinValue.UtcDateTime;
                state.Hash = string.Empty;
                state.MichelsonBlock = null;
            }
        }
    }
}
