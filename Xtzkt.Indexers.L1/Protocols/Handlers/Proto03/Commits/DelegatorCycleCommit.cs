using Xtzkt.Data.Models;

namespace Xtzkt.Indexers.L1.Protocols.Proto03
{
    class DelegatorCycleCommit(ProtocolHandler protocol) : Proto01.DelegatorCycleCommit(protocol)
    {
        public override async Task Apply(L1Block block, Cycle? futureCycle)
        {
            if (!block.Events.HasFlag(L1BlockEvents.CycleBegin))
                return;

            await CreateFromSnapshots(futureCycle!);
        }
    }
}
