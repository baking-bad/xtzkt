using Microsoft.EntityFrameworkCore;
using Xtzkt.Data.Models;

namespace Xtzkt.Indexers.L1.Protocols.Proto01
{
    partial class ProtoActivator : ProtocolCommit
    {
        public virtual void BootstrapBakerCycles(
            L1Protocol protocol,
            List<L1Address> addresses,
            List<Cycle> cycles,
            List<IEnumerable<RightsGenerator.BR>> bakingRights,
            List<IEnumerable<RightsGenerator.AR>> attestationRights)
        {
            var bakers = addresses
                .Where(x => x.Type == AddressType.L1Baker)
                .OfType<L1Baker>();

            foreach (var cycle in cycles)
            {
                var bakerCycles = bakers.ToDictionary(x => x.Id, x =>
                {
                    var share = (double)x.BakingPower / cycle.TotalBakingPower;
                    return new BakerCycle
                    {
                        ChainId = x.ChainId,
                        Cycle = cycle.Index,
                        BakerId = x.Id,
                        OwnDelegatedBalance = x.Balance,
                        ExternalDelegatedBalance = x.ExternalDelegatedBalance,
                        DelegatorsCount = x.DelegatorsCount,
                        OwnStakedBalance = x.OwnStakedBalance,
                        ExternalStakedBalance = x.ExternalStakedBalance,
                        StakersCount = x.StakersCount,
                        IssuedPseudotokens = x.IssuedPseudotokens,
                        BakingPower = x.BakingPower,
                        TotalBakingPower = cycle.TotalBakingPower,
                        ExpectedBlocks = protocol.BlocksPerCycle * share, 
                        ExpectedAttestations = protocol.AttestersPerBlock * protocol.BlocksPerCycle * share
                    };
                });

                #region future baking rights
                foreach (var br in bakingRights[cycle.Index].SkipWhile(x => x.Level == 1)) // skip bootstrap block rights
                {
                    if (br.Round > 0)
                        continue;

                    if (!bakerCycles.TryGetValue(br.Baker, out var bakerCycle))
                        throw new Exception("Unknown baking right recipient");

                    bakerCycle.FutureBlocks++;
                    bakerCycle.FutureBlockRewards += GetFutureBlockReward(protocol, cycle.Index);
                }
                #endregion

                #region future attestation rights
                var skipLevel = attestationRights[cycle.Index].Last().Level; // skip shifted rights
                foreach (var ar in attestationRights[cycle.Index].TakeWhile(x => x.Level < skipLevel))
                {
                    if (!bakerCycles.TryGetValue(ar.Baker, out var bakerCycle))
                        throw new Exception("Unknown attestation right recipient");

                    bakerCycle.FutureAttestations += ar.Slots;
                    bakerCycle.FutureAttestationRewards += GetFutureAttestationReward(protocol, cycle.Index, ar.Slots);
                }
                #endregion

                #region shifted future endirsing rights
                if (cycle.Index > 0)
                {
                    foreach (var ar in attestationRights[cycle.Index - 1].Reverse().TakeWhile(x => x.Level == cycle.FirstLevel - 1))
                    {
                        if (!bakerCycles.TryGetValue(ar.Baker, out var bakerCycle))
                            throw new Exception("Unknown attestation right recipient");

                        bakerCycle.FutureAttestations += ar.Slots;
                        bakerCycle.FutureAttestationRewards += GetFutureAttestationReward(protocol, cycle.Index, ar.Slots);
                    }
                }
                #endregion

                Db.BakerCycles.AddRange(bakerCycles.Values);
            }
        }

        public async Task ClearBakerCycles()
        {
            await Db.Database.ExecuteSqlRawAsync("""
                DELETE FROM "BakerCycles"
                WHERE "ChainId" = {0}
                """, Cache.Chain.Get().Id);
            Cache.BakerCycles.Reset();
        }

        #region helpers
        protected virtual long GetFutureBlockReward(L1Protocol protocol, int cycle)
            => cycle < protocol.NoRewardCycles ? 0 : protocol.BlockReward0;

        protected virtual long GetFutureAttestationReward(L1Protocol protocol, int cycle, int slots)
            => cycle < protocol.NoRewardCycles ? 0 : (slots * protocol.AttestationReward0);
        #endregion
    }
}
