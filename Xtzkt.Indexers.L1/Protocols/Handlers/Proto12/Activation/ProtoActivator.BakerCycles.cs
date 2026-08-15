using Microsoft.EntityFrameworkCore;
using Xtzkt.Data.Models;
using Xtzkt.Indexers.Common.Extensions;

namespace Xtzkt.Indexers.L1.Protocols.Proto12
{
    partial class ProtoActivator : Proto11.ProtoActivator
    {
        public override void BootstrapBakerCycles(
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
                    var bakerCycle = new BakerCycle
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
                        TotalBakingPower = cycle.TotalBakingPower
                    };
                    if (x.BakingPower != 0)
                    {
                        var expectedAttestations = (protocol.BlocksPerCycle * protocol.AttestersPerBlock).MulRatio(x.BakingPower, cycle.TotalBakingPower);
                        bakerCycle.ExpectedBlocks = protocol.BlocksPerCycle.MulRatio(x.BakingPower, cycle.TotalBakingPower);
                        bakerCycle.ExpectedAttestations = expectedAttestations;
                        bakerCycle.FutureAttestationRewards = expectedAttestations * protocol.AttestationReward0;
                    }
                    return bakerCycle;
                });

                #region future baking rights
                foreach (var br in bakingRights[cycle.Index].SkipWhile(x => x.Level == 1).Where(x => x.Round == 0))
                {
                    if (!bakerCycles.TryGetValue(br.Baker, out var bakerCycle))
                        throw new Exception("Unknown baking right recipient");

                    bakerCycle.FutureBlocks++;
                    bakerCycle.FutureBlockRewards += protocol.MaxBakingReward;
                }
                #endregion

                #region future attestation rights
                foreach (var ar in attestationRights[cycle.Index].TakeWhile(x => x.Level < cycle.LastLevel))
                {
                    if (!bakerCycles.TryGetValue(ar.Baker, out var bakerCycle))
                        throw new Exception("Unknown attestation right recipient");

                    bakerCycle.FutureAttestations += ar.Slots;
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
                    }
                }
                #endregion

                Db.BakerCycles.AddRange(bakerCycles.Values);
            }
        }
    }
}
