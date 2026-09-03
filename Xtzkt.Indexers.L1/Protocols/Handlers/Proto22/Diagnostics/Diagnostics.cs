using Microsoft.EntityFrameworkCore;
using Xtzkt.Data.Models;
using Xtzkt.Indexers.Common.Extensions;

namespace Xtzkt.Indexers.L1.Protocols.Proto22
{
    class Diagnostics(ProtocolHandler handler) : Proto21.Diagnostics(handler)
    {
        protected override async Task TestDalParticipation(L1Chain state)
        {
            var bakers = Cache.Addresses.GetBakers().ToList();
            var bakerCycles = Db.ChangeTracker.Entries()
                .Where(x => x.Entity is BakerCycle bc && bc.Cycle == state.Cycle)
                .Select(x => (x.Entity as BakerCycle)!)
                .ToDictionary(x => x.BakerId);

            foreach (var baker in bakers)
            {
                var remote = await Rpc.GetDelegateDalParticipationAsync(state.Level, baker.Hash);

                if (!bakerCycles.TryGetValue(baker.Id, out var bakerCycle))
                    bakerCycle = await Db.BakerCycles.FirstOrDefaultAsync(x => x.ChainId == state.Id && x.Cycle == state.Cycle && x.BakerId == baker.Id);

                if (bakerCycle != null)
                {
                    if (remote.RequiredInt64("expected_assigned_shards_per_slot") != bakerCycle.ExpectedDalAttestations)
                        throw new Exception($"Invalid baker ExpectedDalAttestations {baker.Hash}");

                    if (remote.RequiredInt64("expected_dal_rewards") != bakerCycle.FutureDalAttestationRewards)
                    {
                        if (remote.RequiredInt64("expected_dal_rewards") != 0 || remote.RequiredBool("sufficient_dal_participation") && !remote.RequiredBool("denounced"))
                            throw new Exception($"Invalid baker FutureDalAttestationRewards {baker.Hash}");
                    }
                }
                else
                {
                    if (remote.RequiredInt64("expected_assigned_shards_per_slot") != 0)
                        throw new Exception($"Invalid baker ExpectedDalAttestations {baker.Hash}");

                    if (remote.RequiredInt64("expected_dal_rewards") != 0)
                        throw new Exception($"Invalid baker FutureDalAttestationRewards {baker.Hash}");
                }
            }
        }
    }
}
