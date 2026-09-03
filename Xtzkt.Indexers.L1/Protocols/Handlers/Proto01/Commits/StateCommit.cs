using System.Text.Json;
using Xtzkt.Data.Models;
using Xtzkt.Data.Utils;
using Xtzkt.Indexers.Common.Extensions;

namespace Xtzkt.Indexers.L1.Protocols.Proto01
{
    class StateCommit(ProtocolHandler protocol) : ProtocolCommit(protocol)
    {
        public virtual Task Apply(L1Block block, JsonElement rawBlock)
        {
            var nextProtocol = rawBlock.Required("metadata").RequiredString("next_protocol");
            var appState = Cache.Chain.Get();

            #region entities
            var state = appState;
            #endregion

            state.Cycle = block.Cycle;
            state.Level = block.Level;
            state.Timestamp = block.Timestamp;
            state.Protocol = Context.Protocol.Hash;
            state.NextProtocol = nextProtocol;
            state.Hash = Hashes.FormatMichelsonBlockHash(block.Hash);

            if (block.Events.HasFlag(L1BlockEvents.CycleBegin)) state.CyclesCount++;

            return Task.CompletedTask;
        }

        public virtual async Task Revert(L1Block block)
        {
            var nextProtocol = Context.Protocol.Hash;
            var appState = Cache.Chain.Get();

            #region entities
            var state = appState;
            var prevBlock = await Cache.Blocks.PreviousAsync();
            var prevProtocol = await Cache.Protocols.GetAsync(prevBlock.ProtocolId);
            #endregion

            state.Cycle = prevBlock.Cycle;
            state.Level = prevBlock.Level;
            state.Timestamp = prevBlock.Timestamp;
            state.Protocol = prevProtocol.Hash;
            state.NextProtocol = nextProtocol;
            state.Hash = Hashes.FormatMichelsonBlockHash(prevBlock.Hash);

            if (block.Events.HasFlag(L1BlockEvents.CycleBegin)) state.CyclesCount--;

            Cache.Blocks.Remove(block);
        }
    }
}
