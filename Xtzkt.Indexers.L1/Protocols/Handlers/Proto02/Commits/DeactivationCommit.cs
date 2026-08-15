using System.Text.Json;
using Xtzkt.Data.Models;
using Xtzkt.Indexers.Common.Extensions;

namespace Xtzkt.Indexers.L1.Protocols.Proto02
{
    class DeactivationCommit : ProtocolCommit
    {
        public DeactivationCommit(ProtocolHandler protocol) : base(protocol) { }

        public virtual async Task Apply(L1Block block, JsonElement rawBlock)
        {
            #region init
            List<L1Baker>? bakers = null;
            if (block.Events.HasFlag(L1BlockEvents.Deactivations))
            {
                var deactivated = rawBlock
                    .Required("metadata")
                    .RequiredArray("deactivated")
                    .EnumerateArray()
                    .Select(x => x.RequiredString())
                    .ToHashSet();

                bakers = [..Cache.Addresses.GetBakers().Where(x => x.Staked && deactivated.Contains(x.Hash))];
            }
            else if (block.Events.HasFlag(L1BlockEvents.CycleBegin))
            {
                bakers = [..Cache.Addresses.GetBakers().Where(x => x.Staked && x.DeactivationLevel == block.Level)];
            }
            #endregion

            if (bakers == null) return;

            foreach (var baker in bakers)
            {
                Db.TryAttach(baker);
                baker.DeactivationLevel = block.Level;
                await DeactivateBaker(baker);
            }
        }

        public virtual async Task Revert(L1Block block)
        {
            #region init
            List<L1Baker>? bakers = null;
            if (block.Events.HasFlag(L1BlockEvents.Deactivations) || block.Events.HasFlag(L1BlockEvents.CycleBegin))
            {
                bakers = [..Cache.Addresses.GetBakers().Where(x => x.DeactivationLevel == block.Level)];
            }
            #endregion

            if (bakers == null) return;

            foreach (var baker in bakers)
            {
                Db.TryAttach(baker);
                baker.DeactivationLevel = block.Events.HasFlag(L1BlockEvents.CycleEnd) ? block.Level + 1 : block.Level;
                await ActivateBaker(baker);
            }
        }
    }
}
