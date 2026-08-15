using System.Text.Json;
using Xtzkt.Data.Models;
using Xtzkt.Indexers.Common.Extensions;

namespace Xtzkt.Indexers.L1.Protocols.Proto12
{
    class FreezerCommit : ProtocolCommit
    {
        public FreezerCommit(ProtocolHandler protocol) : base(protocol) { }

        public void Apply(L1Block block, JsonElement rawBlock)
        {
            if (!block.Events.HasFlag(L1BlockEvents.CycleEnd))
                return;

            foreach (var update in rawBlock.Required("metadata").RequiredArray("balance_updates").EnumerateArray()
                .Where(x => x.RequiredString("origin") == "block" &&
                            x.RequiredString("kind") == "freezer" &&
                            x.RequiredString("category") == "deposits"))
            {
                Cache.Statistics.Current.TotalFrozen += update.RequiredInt64("change");
            }
        }

        public void Revert()
        {
            // there is nothing to revert
        }
    }
}
