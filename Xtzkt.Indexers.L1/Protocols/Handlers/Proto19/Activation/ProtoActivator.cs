using Microsoft.EntityFrameworkCore;
using Netezos.Encoding;
using Newtonsoft.Json.Linq;
using Npgsql;
using Xtzkt.Data.Models;
using Xtzkt.Indexers.Common.Extensions;
using Xtzkt.Indexers.Common.Utils;

namespace Xtzkt.Indexers.L1.Protocols.Proto19
{
    partial class ProtoActivator(ProtocolHandler proto) : Proto18.ProtoActivator(proto)
    {
        protected override void BootstrapStakerCycles(L1Protocol protocol, List<L1Address> addresses)
        {
            Db.StakerCycles.AddRange(addresses
                .Where(x => x is L1User user && user.StakedPseudotokens != null)
                .Select(x =>
                {
                    var user = (x as L1User)!;
                    var baker = Cache.Addresses.GetBaker(x.BakerId!.Value);
                    var stakedBalance = (long)(baker.ExternalStakedBalance * user.StakedPseudotokens!.Value / baker.IssuedPseudotokens!.Value);
                    return new StakerCycle
                    {
                        Id = 0,
                        ChainId = x.ChainId,
                        Cycle = 0,
                        BakerId = x.BakerId!.Value,
                        StakerId = x.Id,
                        InitialStake = stakedBalance,
                        AvgStake = stakedBalance,
                    };
                }));
        }

        protected override void SetParameters(L1Protocol protocol, JToken parameters)
        {
            base.SetParameters(protocol, parameters);
            protocol.ConsensusRightsDelay = parameters["consensus_rights_delay"]?.Value<int>() ?? 2;
            protocol.ToleratedInactivityPeriod = protocol.ConsensusRightsDelay + 1;
            protocol.BakerParametersActivationDelay = parameters["delegate_parameters_activation_delay"]?.Value<int>() ?? 5;
            protocol.DoubleBakingSlashedPercentage = parameters["percentage_of_frozen_deposits_slashed_per_double_baking"]?.Value<int>() ?? 500;
            protocol.DoubleConsensusSlashedPercentage = parameters["percentage_of_frozen_deposits_slashed_per_double_attestation"]?.Value<int>() ?? 5000;
            protocol.NumberOfShards = parameters["dal_parametric"]?["number_of_shards"]?.Value<int>() ?? 512;
            protocol.BlocksPerSnapshot = protocol.BlocksPerCycle;
        }

        protected override void UpgradeParameters(L1Protocol protocol, L1Protocol prev)
        {
            if (protocol.ConsensusRightsDelay == 5)
            {
                protocol.ConsensusRightsDelay = 2;
                protocol.ToleratedInactivityPeriod = protocol.ConsensusRightsDelay + 1;
            }

            if (protocol.TimeBetweenBlocks >= 8)
            {
                protocol.BlocksPerCycle = protocol.BlocksPerCycle * 3 / 2;
                protocol.BlocksPerCommitment = protocol.BlocksPerCommitment * 3 / 2;
                protocol.BlocksPerVoting = protocol.BlocksPerVoting * 3 / 2;
                protocol.TimeBetweenBlocks = protocol.TimeBetweenBlocks * 2 / 3;
                protocol.HardBlockGasLimit = prev.HardBlockGasLimit * 2 / 3;
                protocol.SmartRollupCommitmentPeriod = 15 * 60 / protocol.TimeBetweenBlocks;
                protocol.SmartRollupChallengeWindow = 14 * 24 * 60 * 60 / protocol.TimeBetweenBlocks;
                protocol.SmartRollupTimeoutPeriod = 7 * 24 * 60 * 60 / protocol.TimeBetweenBlocks;
            }

            protocol.BlocksPerSnapshot = protocol.BlocksPerCycle;
            protocol.NumberOfShards = 512;
        }

        protected override async Task MigrateContext(L1Chain state)
        {
            var prevProto = await Cache.Protocols.GetAsync(state.Protocol);
            var nextProto = await Cache.Protocols.GetAsync(state.NextProtocol);

            await RemoveDeadRefutationGames(state);
            await RemoveFutureCycles(state, prevProto, nextProto);
            MigrateBakers(state, prevProto, nextProto);
            await MigrateVotingPeriods(state, nextProto);
            var cycles = await MigrateCycles(state, nextProto);
            await MigrateFutureRights(state, nextProto, cycles);

            Cache.BakerCycles.Reset();
            Cache.BakingRights.Reset();
        }

        async Task RemoveFutureCycles(L1Chain state, L1Protocol prevProto, L1Protocol nextProto)
        {
            if (prevProto.ConsensusRightsDelay == nextProto.ConsensusRightsDelay)
                return;

            var lastCycle = state.Cycle + nextProto.ConsensusRightsDelay + 1;
            var lastCycleStart = nextProto.GetCycleStart(lastCycle);
            
            await Db.Database.ExecuteSqlRawAsync("""
                DELETE FROM "BakerCycles"
                WHERE "ChainId" = {0}
                AND "Cycle" > {1}
                """, state.Id, lastCycle);

            await Db.Database.ExecuteSqlRawAsync("""
                DELETE FROM "BakingRights"
                WHERE "ChainId" = {0}
                AND ("Level" = {1} AND "Type" = {2} OR "Level" > {1})
                """,
                state.Id,
                lastCycleStart,
                (int)BakingRightType.Baking);

            var removedCycles = await Db.Database.ExecuteSqlRawAsync("""
                DELETE FROM "Cycles"
                WHERE "ChainId" = {0}
                AND "Index" > {1}
                """, state.Id, lastCycle);

            await Db.Database.ExecuteSqlRawAsync("""
                DELETE FROM "DelegatorCycles"
                WHERE "ChainId" = {0}
                AND "Cycle" > {1}
                """, state.Id, lastCycle);

            Cache.BakerCycles.Reset();
            Cache.BakingRights.Reset();

            Db.TryAttach(state);
            state.CyclesCount -= removedCycles;
        }

        void MigrateBakers(L1Chain state, L1Protocol prevProto, L1Protocol nextProto)
        {
            foreach (var baker in Cache.Addresses.GetBakers())
            {
                Db.TryAttach(baker);
                baker.MinTotalDelegated = baker.TotalDelegated;
                baker.MinTotalDelegatedLevel = state.Level;

                if (baker.DeactivationLevel > state.Level)
                    baker.DeactivationLevel = nextProto.GetCycleStart(prevProto.GetCycle(baker.DeactivationLevel));
            }
        }

        async Task MigrateVotingPeriods(L1Chain state, L1Protocol nextProto)
        {
            var newPeriod = await Cache.Periods.GetAsync(state.VotingPeriod);
            Db.TryAttach(newPeriod);
            newPeriod.LastLevel = newPeriod.FirstLevel + nextProto.BlocksPerVoting - 1;
        }

        async Task<List<Cycle>> MigrateCycles(L1Chain state, L1Protocol nextProto)
        {
            var cycles = await Db.Cycles
                .Where(x => x.ChainId == state.Id && x.Index >= state.Cycle)
                .OrderBy(x => x.Index)
                .ToListAsync();

            var res = await Proto.Rpc.GetExpectedIssuance(state.Level);
            var issuance = res.EnumerateArray().First(x => x.RequiredInt32("cycle") == cycles.First(x => x.Index > state.Cycle).Index);

            foreach (var cycle in cycles.Where(x => x.Index > state.Cycle))
            {
                cycle.BlockReward = issuance.RequiredInt64("baking_reward_fixed_portion");
                cycle.BlockBonusPerBlock = GetBlockBonusPerBlock(issuance, nextProto);
                cycle.AttestationRewardPerBlock = GetAttestationRewardPerBlock(issuance, nextProto);
                cycle.NonceRevelationReward = issuance.RequiredInt64("seed_nonce_revelation_tip");
                cycle.VdfRevelationReward = issuance.RequiredInt64("vdf_revelation_tip");

                cycle.FirstLevel = nextProto.GetCycleStart(cycle.Index);
                cycle.LastLevel = nextProto.GetCycleEnd(cycle.Index);
            }

            return cycles;
        }

        async Task MigrateFutureRights(L1Chain state, L1Protocol nextProto, List<Cycle> cycles)
        {
            await Db.Database.ExecuteSqlRawAsync("""
                DELETE FROM "BakingRights"
                WHERE "ChainId" = {0}
                AND "Level" >= {1}
                """, state.Id, cycles[1].FirstLevel);

            var conn = (Db.Database.GetDbConnection() as NpgsqlConnection)!;
            IEnumerable<RightsGenerator.AR> shifted = [];

            foreach (var cycle in cycles)
            {
                var bakerCycles = await Cache.BakerCycles.GetAsync(cycle.Index);
                var sampler = GetSampler(bakerCycles.Values
                    .Where(x => x.BakingPower > 0)
                    .Select(x => (x.BakerId, x.BakingPower))
                    .ToList());

                #region temporary diagnostics
                await sampler.Validate(Proto, state.Level, cycle.Index);
                #endregion

                if (cycle.Index == state.Cycle)
                {
                    shifted = RightsGenerator.GetAttestationRights(sampler, nextProto, cycle, cycle.LastLevel);

                    #region save shifted
                    using var writer = conn.BeginBinaryImport("""
                        COPY "BakingRights" ("ChainId", "Cycle", "Level", "BakerId", "Type", "Status", "Round", "Slots")
                        FROM STDIN (FORMAT BINARY)
                        """);

                    foreach (var ar in shifted)
                    {
                        writer.StartRow();
                        writer.Write(state.Id, NpgsqlTypes.NpgsqlDbType.Integer);
                        writer.Write(cycle.Index + 1, NpgsqlTypes.NpgsqlDbType.Integer);
                        writer.Write(ar.Level + 1, NpgsqlTypes.NpgsqlDbType.Integer);
                        writer.Write(ar.Baker, NpgsqlTypes.NpgsqlDbType.Integer);
                        writer.Write((short)BakingRightType.Attestation, NpgsqlTypes.NpgsqlDbType.Smallint);
                        writer.Write((short)BakingRightStatus.Future, NpgsqlTypes.NpgsqlDbType.Smallint);
                        writer.WriteNull();
                        writer.Write(ar.Slots, NpgsqlTypes.NpgsqlDbType.Integer);
                    }

                    writer.Complete();
                    #endregion
                }
                else
                {
                    GC.Collect();
                    var brs = await RightsGenerator.GetBakingRightsAsync(sampler, nextProto, cycle);
                    var ars = await RightsGenerator.GetAttestationRightsAsync(sampler, nextProto, cycle);

                    #region save rights
                    using (var writer = conn.BeginBinaryImport("""
                        COPY "BakingRights" ("ChainId", "Cycle", "Level", "BakerId", "Type", "Status", "Round", "Slots")
                        FROM STDIN (FORMAT BINARY)
                        """))
                    {
                        foreach (var ar in ars)
                        {
                            writer.StartRow();
                            writer.Write(state.Id, NpgsqlTypes.NpgsqlDbType.Integer);
                            writer.Write(nextProto.GetCycle(ar.Level + 1), NpgsqlTypes.NpgsqlDbType.Integer);
                            writer.Write(ar.Level + 1, NpgsqlTypes.NpgsqlDbType.Integer);
                            writer.Write(ar.Baker, NpgsqlTypes.NpgsqlDbType.Integer);
                            writer.Write((short)BakingRightType.Attestation, NpgsqlTypes.NpgsqlDbType.Smallint);
                            writer.Write((short)BakingRightStatus.Future, NpgsqlTypes.NpgsqlDbType.Smallint);
                            writer.WriteNull();
                            writer.Write(ar.Slots, NpgsqlTypes.NpgsqlDbType.Integer);
                        }

                        foreach (var br in brs)
                        {
                            writer.StartRow();
                            writer.Write(state.Id, NpgsqlTypes.NpgsqlDbType.Integer);
                            writer.Write(cycle.Index, NpgsqlTypes.NpgsqlDbType.Integer);
                            writer.Write(br.Level, NpgsqlTypes.NpgsqlDbType.Integer);
                            writer.Write(br.Baker, NpgsqlTypes.NpgsqlDbType.Integer);
                            writer.Write((short)BakingRightType.Baking, NpgsqlTypes.NpgsqlDbType.Smallint);
                            writer.Write((short)BakingRightStatus.Future, NpgsqlTypes.NpgsqlDbType.Smallint);
                            writer.Write(br.Round, NpgsqlTypes.NpgsqlDbType.Integer);
                            writer.WriteNull();
                        }

                        writer.Complete();
                    }
                    #endregion

                    #region reset baker cycles
                    var attestationRewardPerSlot = cycle.AttestationRewardPerBlock / nextProto.AttestersPerBlock;
                    var maxBlockReward = cycle.BlockReward + cycle.BlockBonusPerBlock;

                    foreach (var bakerCycle in bakerCycles.Values)
                    {
                        Db.TryAttach(bakerCycle);

                        bakerCycle.FutureBlocks = 0;
                        bakerCycle.FutureBlockRewards = 0;
                        bakerCycle.FutureAttestations = 0;

                        var expectedAttestations = (nextProto.BlocksPerCycle * nextProto.AttestersPerBlock).MulRatio(bakerCycle.BakingPower, cycle.TotalBakingPower);
                        bakerCycle.ExpectedBlocks = nextProto.BlocksPerCycle.MulRatio(bakerCycle.BakingPower, cycle.TotalBakingPower);
                        bakerCycle.ExpectedAttestations = expectedAttestations;
                        bakerCycle.FutureAttestationRewards = expectedAttestations * attestationRewardPerSlot;
                    }

                    foreach (var br in brs.Where(x => x.Round == 0))
                    {
                        if (!bakerCycles.TryGetValue(br.Baker, out var bakerCycle))
                            throw new Exception("Nonexistent baker cycle");

                        bakerCycle.FutureBlocks++;
                        bakerCycle.FutureBlockRewards += maxBlockReward;
                    }

                    foreach (var ar in shifted)
                    {
                        if (bakerCycles.TryGetValue(ar.Baker, out var bakerCycle))
                        {
                            bakerCycle.FutureAttestations += ar.Slots;
                        }
                    }

                    foreach (var ar in ars.TakeWhile(x => x.Level < cycle.LastLevel))
                    {
                        if (!bakerCycles.TryGetValue(ar.Baker, out var bakerCycle))
                            throw new Exception("Nonexistent baker cycle");

                        bakerCycle.FutureAttestations += ar.Slots;
                    }
                    #endregion

                    shifted = [..ars.Where(x => x.Level == cycle.LastLevel)];
                }
            }
        }

        protected override Sampler GetSampler(IEnumerable<(int id, long stake)> selection)
        {
            var sorted = selection.OrderByDescending(x =>
            {
                var baker = Cache.Addresses.GetBaker(x.id);
                return new byte[] { (byte)baker.PublicKey![0] }.Concat(Base58.Parse(baker.Hash));
            }, BytesComparer.Instance);

            return new Sampler([..sorted.Select(x => x.id)], [..sorted.Select(x => x.stake)]);
        }
    }
}
