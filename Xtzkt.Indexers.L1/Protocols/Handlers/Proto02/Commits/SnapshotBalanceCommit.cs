using System.Text.Json;
using Xtzkt.Data.Models;

namespace Xtzkt.Indexers.L1.Protocols.Proto02
{
    class SnapshotBalanceCommit : Proto01.SnapshotBalanceCommit
    {
        public SnapshotBalanceCommit(ProtocolHandler protocol) : base(protocol) { }

        public override async Task Apply(JsonElement rawBlock, L1Block block)
        {
            if (!block.Events.HasFlag(L1BlockEvents.BalanceSnapshot))
                return;

            await RemoveOutdated(block, Context.Protocol);
            await TakeSnapshot(block);
            await TakeDeactivatedSnapshot(block);
            await SubtractCycleRewards(rawBlock, block);
        }
    }
}
