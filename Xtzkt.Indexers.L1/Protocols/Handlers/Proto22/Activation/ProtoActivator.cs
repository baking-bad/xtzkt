using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Linq;
using Npgsql;
using Xtzkt.Data.Models;
using Xtzkt.Indexers.Common.Extensions;

namespace Xtzkt.Indexers.L1.Protocols.Proto22
{
    partial class ProtoActivator(ProtocolHandler proto) : Proto21.ProtoActivator(proto)
    {
        protected override long GetDalAttestationRewardPerShard(JsonElement issuance)
        {
            return issuance.RequiredInt64("dal_attesting_reward_per_shard");
        }

        protected override void SetParameters(L1Protocol protocol, JToken parameters)
        {
            base.SetParameters(protocol, parameters);
            protocol.ConsensusThreshold = parameters["consensus_threshold_size"]?.Value<int>() ?? 4667;
            protocol.DenunciationPeriod = parameters["denunciation_period"]?.Value<int>() ?? 1;
            protocol.SlashingDelay = parameters["slashing_delay"]?.Value<int>() ?? 1;
            protocol.ToleratedInactivityPeriod = parameters["tolerated_inactivity_period"]?.Value<int>() ?? 2;
        }

        protected override void UpgradeParameters(L1Protocol protocol, L1Protocol prev)
        {
            protocol.ToleratedInactivityPeriod = 2;

            if (Cache.Chain.GetChainId() == "NetXdQprcVkpaWU" &&
                protocol.BlocksPerCycle > 10_800 && protocol.TimeBetweenBlocks == 8)
            {
                protocol.BlocksPerCycle = 10_800;
                protocol.BlocksPerVoting = protocol.BlocksPerCycle * 14;
                protocol.BlocksPerSnapshot = protocol.BlocksPerCycle;
            }
            else if (protocol.BlocksPerCycle > 10_800 && protocol.TimeBetweenBlocks == 4)
            {
                protocol.BlocksPerCycle = 10_800;
                protocol.BlocksPerVoting = protocol.BlocksPerCycle * (prev.BlocksPerVoting / prev.BlocksPerCycle);
                protocol.BlocksPerSnapshot = protocol.BlocksPerCycle;
            }

            // for nextnet
            if (protocol.MaxExternalOverOwnStakeRatio != 9)
            {
                protocol.MaxExternalOverOwnStakeRatio = 9;
            }
        }

        protected override async Task MigrateContext(L1Chain state)
        {
            var prevProto = await Cache.Protocols.GetAsync(state.Protocol);
            var nextProto = await Cache.Protocols.GetAsync(state.NextProtocol);

            await RemoveDeadRefutationGames(state);
            await MigrateSlashing(state, nextProto);
            MigrateBakers(state, prevProto, nextProto);
            await MigrateVotingPeriods(state, nextProto);
            var cycles = await MigrateCycles(state, prevProto, nextProto);
            await MigrateFutureRights(state, nextProto, cycles);

            Cache.BakerCycles.Reset();
            Cache.BakingRights.Reset();
        }

        protected override async Task RevertContext(L1Chain state)
        {
            var prevProto = await Cache.Protocols.GetAsync(state.Protocol);
            var nextProto = await Cache.Protocols.GetAsync(state.NextProtocol);

            await MigrateSlashing(state, prevProto);
            MigrateBakers(state, nextProto, prevProto);

            Cache.BakerCycles.Reset();
            Cache.BakingRights.Reset();
        }

        async Task MigrateSlashing(L1Chain state, L1Protocol nextProto)
        {
            foreach (var op in await Db.DoubleBakingOps.Where(x => x.ChainId == state.Id && x.SlashedLevel > state.Level).ToListAsync())
            {
                var proto = await Cache.Protocols.FindByLevelAsync(op.AccusedLevel);
                op.SlashedLevel = nextProto.GetCycleEnd(proto.GetCycle(op.AccusedLevel) + proto.SlashingDelay);
            }

            foreach (var op in await Db.DoubleConsensusOps.Where(x => x.ChainId == state.Id && x.SlashedLevel > state.Level).ToListAsync())
            {
                var proto = await Cache.Protocols.FindByLevelAsync(op.AccusedLevel);
                op.SlashedLevel = nextProto.GetCycleEnd(proto.GetCycle(op.AccusedLevel) + proto.SlashingDelay);
            }
        }

        void MigrateBakers(L1Chain state, L1Protocol prevProto, L1Protocol nextProto)
        {
            UpdateBakersPower();

            foreach (var baker in Cache.Addresses.GetBakers().Where(x => x.DeactivationLevel > state.Level))
            {
                Db.TryAttach(baker);
                baker.DeactivationLevel = nextProto.GetCycleStart(prevProto.GetCycle(baker.DeactivationLevel));
            }
        }

        async Task MigrateVotingPeriods(L1Chain state, L1Protocol nextProto)
        {
            var newPeriod = await Cache.Periods.GetAsync(state.VotingPeriod);
            Db.TryAttach(newPeriod);
            newPeriod.LastLevel = newPeriod.FirstLevel + nextProto.BlocksPerVoting - 1;
        }

        async Task<List<Cycle>> MigrateCycles(L1Chain state, L1Protocol prevProto, L1Protocol nextProto)
        {
            var cycles = await Db.Cycles
                .Where(x => x.ChainId == state.Id && x.Index >= state.Cycle)
                .OrderBy(x => x.Index)
                .ToListAsync();

            var res = prevProto.ConsensusRightsDelay != nextProto.ConsensusRightsDelay
                ? await Proto.Rpc.GetExpectedIssuance(state.Level + 1) // Crutch for buggy ghostnet
                : await Proto.Rpc.GetExpectedIssuance(state.Level);

            foreach (var cycle in cycles.Where(x => x.Index > state.Cycle))
            {
                var issuance = res.EnumerateArray().First(x => x.RequiredInt32("cycle") == cycle.Index);

                cycle.BlockReward = issuance.RequiredInt64("baking_reward_fixed_portion");
                cycle.BlockBonusPerBlock = GetBlockBonusPerBlock(issuance, nextProto);
                cycle.AttestationRewardPerBlock = GetAttestationRewardPerBlock(issuance, nextProto);
                cycle.NonceRevelationReward = issuance.RequiredInt64("seed_nonce_revelation_tip");
                cycle.VdfRevelationReward = issuance.RequiredInt64("vdf_revelation_tip");
                cycle.DalAttestationRewardPerShard = issuance.RequiredInt64("dal_attesting_reward_per_shard");

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
                        writer.Write((int)BakingRightType.Attestation, NpgsqlTypes.NpgsqlDbType.Integer);
                        writer.Write((int)BakingRightStatus.Future, NpgsqlTypes.NpgsqlDbType.Integer);
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
                            writer.Write((int)BakingRightType.Attestation, NpgsqlTypes.NpgsqlDbType.Integer);
                            writer.Write((int)BakingRightStatus.Future, NpgsqlTypes.NpgsqlDbType.Integer);
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
                            writer.Write((int)BakingRightType.Baking, NpgsqlTypes.NpgsqlDbType.Integer);
                            writer.Write((int)BakingRightStatus.Future, NpgsqlTypes.NpgsqlDbType.Integer);
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
                        var expectedDalAttestations = (nextProto.BlocksPerCycle * nextProto.NumberOfShards).MulRatio(bakerCycle.BakingPower, cycle.TotalBakingPower);
                        bakerCycle.ExpectedBlocks = nextProto.BlocksPerCycle.MulRatio(bakerCycle.BakingPower, cycle.TotalBakingPower);
                        bakerCycle.ExpectedAttestations = expectedAttestations;
                        bakerCycle.FutureAttestationRewards = expectedAttestations * attestationRewardPerSlot;
                        bakerCycle.ExpectedDalAttestations = expectedDalAttestations;
                        bakerCycle.FutureDalAttestationRewards = expectedDalAttestations * cycle.DalAttestationRewardPerShard;
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

                    shifted = [.. ars.Where(x => x.Level == cycle.LastLevel)];
                }
            }
        }
    }
}
