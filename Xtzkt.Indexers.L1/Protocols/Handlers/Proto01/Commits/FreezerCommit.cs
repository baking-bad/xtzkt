using System.Text.Json;
using Xtzkt.Data.Models;
using Xtzkt.Indexers.Common.Extensions;

namespace Xtzkt.Indexers.L1.Protocols.Proto01
{
    class FreezerCommit(ProtocolHandler protocol) : ProtocolCommit(protocol)
    {
        public void Apply(L1Block block, JsonElement rawBlock)
        {
            if (!block.Events.HasFlag(L1BlockEvents.CycleEnd))
                return;

            foreach (var update in GetFreezerUpdates(block, Context.Protocol, rawBlock))
            {
                var change = update.RequiredInt64("change");
                switch (update.RequiredString("category")[0])
                {
                    case 'd':
                        break;
                    case 'r':
                        var baker = Cache.Addresses.GetExistingBaker(update.RequiredString("delegate"));
                        Db.TryAttach(baker);
                        UnlockRewards(baker, -change);
                        break;
                    case 'f':
                        break;
                    default:
                        throw new Exception("unexpected freezer balance update type");
                }

                Cache.Statistics.Current.TotalFrozen += change;
            }

            return;
        }

        public async Task Revert(L1Block block)
        {
            if (!block.Events.HasFlag(L1BlockEvents.CycleEnd))
                return;

            var rawBlock = await Proto.Rpc.GetBlockAsync(block.Level);

            foreach (var update in GetFreezerUpdates(block, Context.Protocol, rawBlock))
            {
                var change = update.RequiredInt64("change");
                switch (update.RequiredString("category")[0])
                {
                    case 'd':
                        break;
                    case 'r':
                        var baker = Cache.Addresses.GetExistingBaker(update.RequiredString("delegate"));
                        Db.TryAttach(baker);
                        RevertUnlockRewards(baker, -change);
                        break;
                    case 'f':
                        break;
                    default:
                        throw new Exception("unexpected freezer balance update type");
                }
            }
        }

        protected virtual int GetFreezerCycle(JsonElement el) => el.RequiredInt32("level");

        protected virtual IEnumerable<JsonElement> GetFreezerUpdates(L1Block block, L1Protocol protocol, JsonElement rawBlock)
        {
            return rawBlock
                .Required("metadata")
                .Required("balance_updates")
                .EnumerateArray()
                .Where(x => x.RequiredString("kind")[0] == 'f' &&
                            x.RequiredInt64("change") < 0 &&
                            GetFreezerCycle(x) == block.Cycle - protocol.ConsensusRightsDelay);
        }
    }
}
