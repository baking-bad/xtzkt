using Microsoft.EntityFrameworkCore;
using Netezos.Encoding;
using Newtonsoft.Json.Linq;
using Npgsql;
using Xtzkt.Data.Models;
using Xtzkt.Indexers.Common.Extensions;
using Xtzkt.Indexers.Common.Utils;

namespace Xtzkt.Indexers.L1.Protocols.Proto12
{
    partial class ProtoActivator(ProtocolHandler proto) : Proto11.ProtoActivator(proto)
    {
        protected override void SetParameters(L1Protocol protocol, JToken parameters)
        {
            base.SetParameters(protocol, parameters);
            protocol.BlocksPerSnapshot = parameters["blocks_per_stake_snapshot"]?.Value<int>() ?? 512;
            protocol.AttestersPerBlock = parameters["consensus_committee_size"]?.Value<int>() ?? 7000;
            protocol.MinimalStake = parameters["tokens_per_roll"]?.Value<long>() ?? 6_000_000_000;
            protocol.BlockDeposit = 0;
            protocol.AttestationDeposit = 0;

            var totalReward = 80_000_000 / (60 / protocol.TimeBetweenBlocks);
            protocol.BlockReward0 = parameters["baking_reward_fixed_portion"]?.Value<long>() ?? (totalReward / 4);
            protocol.BlockReward1 = parameters["baking_reward_bonus_per_slot"]?.Value<long>() ?? (totalReward / 4 / (protocol.AttestersPerBlock / 3));
            protocol.AttestationReward0 = parameters["endorsing_reward_per_slot"]?.Value<long>() ?? (totalReward / 2 / protocol.AttestersPerBlock);
            protocol.AttestationReward1 = 0;

            protocol.LBToggleThreshold = (parameters["liquidity_baking_escape_ema_threshold"]?.Value<int>() ?? 666_667) * 1000;

            protocol.ConsensusThreshold = parameters["consensus_threshold"]?.Value<int>() ?? 4667;
            protocol.MinParticipationNumerator = parameters["minimal_participation_ratio"]?["numerator"]?.Value<int>() ?? 2;
            protocol.MinParticipationDenominator = parameters["minimal_participation_ratio"]?["denominator"]?.Value<int>() ?? 3;
            protocol.DenunciationPeriod = 1;
            protocol.SlashingDelay = 1;
            protocol.MaxDelegatedOverFrozenRatio = 100 / (parameters["frozen_deposits_percentage"]?.Value<int>() ?? 10) - 1;

            protocol.MaxBakingReward = protocol.BlockReward0 + protocol.AttestersPerBlock / 3 * protocol.BlockReward1;
            protocol.MaxAttestationReward = protocol.AttestersPerBlock * protocol.AttestationReward0;
        }

        protected override void UpgradeParameters(L1Protocol protocol, L1Protocol prev)
        {
            protocol.AttestersPerBlock = 7000;
            protocol.MinimalStake = 6_000_000_000;
            protocol.BlockDeposit = 0;
            protocol.AttestationDeposit = 0;

            var totalReward = 80_000_000 / (60 / protocol.TimeBetweenBlocks);
            protocol.BlockReward0 = totalReward / 4;
            protocol.BlockReward1 = totalReward / 4 / (protocol.AttestersPerBlock / 3);
            protocol.AttestationReward0 = totalReward / 2 / protocol.AttestersPerBlock;
            protocol.AttestationReward1 = 0;

            protocol.LBToggleThreshold = 666_667_000;

            protocol.ConsensusThreshold = 4667;
            protocol.MinParticipationNumerator = 2;
            protocol.MinParticipationDenominator = 3;
            protocol.DenunciationPeriod = 1;
            protocol.SlashingDelay = 1;
            protocol.MaxDelegatedOverFrozenRatio = 9;

            protocol.MaxBakingReward = protocol.BlockReward0 + protocol.AttestersPerBlock / 3 * protocol.BlockReward1;
            protocol.MaxAttestationReward = protocol.AttestersPerBlock * protocol.AttestationReward0;
        }

        protected override async Task MigrateContext(L1Chain state)
        {
            var prevProto = await Cache.Protocols.GetAsync(state.Protocol);
            var nextProto = await Cache.Protocols.GetAsync(state.NextProtocol);

            var bakers = MigrateBakers();
            await MigrateCycles(state, bakers, nextProto);
            MigrateStatistics(bakers, nextProto);

            if (state.Level == 1) return;

            Proto.Diagnostics.TrackChanges();
            await Db.SaveChangesAsync();

            await MigrateSnapshots(state);
            await MigrateCurrentRights(state, bakers, prevProto, nextProto);
            await MigrateFutureRights(state, bakers, nextProto);

            Cache.BakingRights.Reset();
            Cache.BakerCycles.Reset();
        }

        protected override Task RevertContext(L1Chain state)
        {
            throw new NotImplementedException("Reverting Ithaca migration block is technically impossible");
        }

        List<L1Baker> MigrateBakers()
        {            
            var bakers = Cache.Addresses.GetBakers().ToList();
            foreach (var baker in bakers)
            {
                Db.TryAttach(baker);

                if (baker.Staked)
                    Cache.Statistics.Current.TotalOwnDelegated -= baker.OwnDelegatedBalance;

                baker.OwnDelegatedBalance = baker.Balance;

                if (baker.Staked)
                    Cache.Statistics.Current.TotalOwnDelegated += baker.OwnDelegatedBalance;

                UpdateBakerPower(baker);
            }
            return bakers;
        }

        async Task MigrateCycles(L1Chain state, List<L1Baker> bakers, L1Protocol nextProto)
        {
            var selectedStakes = bakers
                .Where(x => x.BakingPower != 0)
                .Select(x => x.BakingPower);

            var selectedBakers = selectedStakes.Count();
            var selectedStake = selectedStakes.Sum();

            foreach (var cycle in await Db.Cycles.Where(x => x.ChainId == state.Id && x.LastLevel > state.Level).ToListAsync())
            {
                cycle.SnapshotLevel = state.Level;
                cycle.TotalBakers = selectedBakers;
                cycle.TotalBakingPower = selectedStake;
            }
        }

        void MigrateStatistics(List<L1Baker> bakers, L1Protocol nextProto)
        {
            var stats = Cache.Statistics.Current;
            Db.TryAttach(stats);
            stats.TotalFrozen = bakers
                .Where(x => x.BakingPower != 0)
                .Sum(x => x.BakingPower / (nextProto.MaxDelegatedOverFrozenRatio + 1));
        }

        Task<int> MigrateSnapshots(L1Chain chain)
        {
            return Db.Database.ExecuteSqlRawAsync("""
                DELETE FROM "SnapshotBalances"
                WHERE "ChainId" = {0}
                AND "Level" = {1};

                INSERT INTO "SnapshotBalances" (
                    "ChainId",
                    "Level",
                    "BakerId",
                    "AddressId",
                    "OwnDelegatedBalance",
                    "ExternalDelegatedBalance",
                    "DelegatorsCount"
                )
                SELECT
                    {0},
                    {1},
                    COALESCE("BakerId", "Id"),
                    "Id",
                    COALESCE("OwnDelegatedBalance", "Balance"),
                    "ExternalDelegatedBalance",
                    "DelegatorsCount"
                FROM "Addresses"
                WHERE "ChainId" = {0}
                AND "Staked" = true;
                """, chain.Id, chain.Level);
        }

        async Task MigrateCurrentRights(L1Chain state, List<L1Baker> bakers, L1Protocol prevProto, L1Protocol nextProto)
        {
            var cycle = await Db.Cycles.AsNoTracking().FirstAsync(x => x.ChainId == state.Id && x.Index == state.Cycle);
            if (state.Level == cycle.LastLevel) return;

            var bakerCycles = await Cache.BakerCycles.GetAsync(state.Cycle);
            var selectedBakers = bakers.Where(x => x.BakingPower != 0);

            #region revert current rights
            var rights = await Db.BakingRights
                .AsNoTracking()
                .Where(x => x.ChainId == state.Id && x.Level > state.Level && x.Cycle == state.Cycle)
                .ToListAsync();

            foreach (var br in rights.Where(x => x.Type == BakingRightType.Baking && x.Round == 0))
            {
                var bakerCycle = bakerCycles[br.BakerId];
                Db.TryAttach(bakerCycle);

                bakerCycle.FutureBlocks--;
                bakerCycle.FutureBlockRewards -= GetFutureBlockReward(prevProto, state.Cycle);
            }

            foreach (var ar in rights.Where(x => x.Type == BakingRightType.Attestation))
            {
                var bakerCycle = bakerCycles[ar.BakerId];
                Db.TryAttach(bakerCycle);

                bakerCycle.FutureAttestations -= ar.Slots!.Value;
                bakerCycle.FutureAttestationRewards -= GetFutureAttestationReward(prevProto, state.Cycle, ar.Slots.Value);
            }

            await Db.Database.ExecuteSqlRawAsync("""
                DELETE FROM "BakingRights"
                WHERE "ChainId" = {0}
                AND "Level" > {1}
                AND "Cycle" = {2}
                """, state.Id, state.Level, state.Cycle);
                
            #endregion

            #region add missed baker cycles
            foreach (var baker in selectedBakers)
            {
                if (!bakerCycles.TryGetValue(baker.Id, out var bc))
                {
                    bc = new BakerCycle
                    {
                        ChainId = baker.ChainId,
                        Cycle = state.Cycle,
                        BakerId = baker.Id
                    };
                    Db.BakerCycles.Add(bc);
                    Cache.BakerCycles.Add(bc);

                    if (baker.DelegatorsCount > 0)
                    {
                        await Db.Database.ExecuteSqlRawAsync("""
                            INSERT INTO "DelegatorCycles" (
                                "ChainId",
                                "Cycle",
                                "DelegatorId",
                                "BakerId",
                                "DelegatedBalance",
                                "StakedPseudotokens"
                            )
                            SELECT
                                {0},
                                {1},
                                "AddressId",
                                "BakerId",
                                "OwnDelegatedBalance",
                                "Pseudotokens"
                            FROM "SnapshotBalances"
                            WHERE "ChainId" = {0}
                            AND "Level" = {2}
                            AND "BakerId" = {3}
                            AND "BakerId" != "AddressId"
                            """, state.Id, state.Cycle, state.Level, baker.Id);
                    }
                }
            }
            #endregion

            #region update baker cycles
            foreach (var (bakerId, bc) in bakerCycles)
            {
                var baker = Cache.Addresses.GetBaker(bakerId);

                Db.TryAttach(bc);
                bc.OwnDelegatedBalance = baker.OwnDelegatedBalance;
                bc.ExternalDelegatedBalance = baker.ExternalDelegatedBalance;
                bc.DelegatorsCount = baker.DelegatorsCount;
                bc.OwnStakedBalance = baker.OwnStakedBalance;
                bc.ExternalStakedBalance = baker.ExternalStakedBalance;
                bc.IssuedPseudotokens = baker.IssuedPseudotokens;
                bc.StakersCount = baker.StakersCount;
                bc.BakingPower = baker.BakingPower;
                bc.TotalBakingPower = cycle.TotalBakingPower;

                if (baker.BakingPower != 0)
                {
                    var expectedAttestations = (nextProto.BlocksPerCycle * nextProto.AttestersPerBlock).MulRatio(baker.BakingPower, cycle.TotalBakingPower);
                    bc.FutureAttestationRewards += expectedAttestations * nextProto.AttestationReward0;
                }
            }
            #endregion

            #region apply new rights
            var sampler = GetSampler(selectedBakers.Select(x => (x.Id, x.BakingPower)));

            #region temporary diagnostics
            await sampler.Validate(Proto, state.Level, cycle.Index);
            #endregion

            var brs = new List<RightsGenerator.BR>();
            var ars = new List<RightsGenerator.AR>();
            for (int level = state.Level + 1; level <= cycle.LastLevel; level++)
            {
                foreach (var br in RightsGenerator.GetBakingRights(sampler, cycle, level))
                {
                    brs.Add(br);
                    if (br.Round == 0)
                    {
                        var bakerCycle = bakerCycles[br.Baker];
                        Db.TryAttach(bakerCycle);
                        bakerCycle.FutureBlocks++;
                        bakerCycle.FutureBlockRewards += nextProto.MaxBakingReward;
                    }
                }
                foreach (var ar in RightsGenerator.GetAttestationRights(sampler, nextProto, cycle, level - 1))
                {
                    ars.Add(ar);
                    var bakerCycle = bakerCycles[ar.Baker];
                    Db.TryAttach(bakerCycle);
                    bakerCycle.FutureAttestations += ar.Slots;
                }
            }

            var conn = (Db.Database.GetDbConnection() as NpgsqlConnection)!;
            using var writer = conn.BeginBinaryImport(@"
                COPY ""BakingRights"" (""ChainId"", ""Cycle"", ""Level"", ""BakerId"", ""Type"", ""Status"", ""Round"", ""Slots"")
                FROM STDIN (FORMAT BINARY)");

            foreach (var ar in ars)
            {
                writer.StartRow();
                writer.Write(state.Id, NpgsqlTypes.NpgsqlDbType.Integer);
                writer.Write(cycle.Index, NpgsqlTypes.NpgsqlDbType.Integer);
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
            #endregion
        }

        async Task MigrateFutureRights(L1Chain state, List<L1Baker> bakers, L1Protocol nextProto)
        {
            var cycles = await Db.Cycles
                .AsNoTracking()
                .Where(x => x.ChainId == state.Id && x.Index >= state.Cycle)
                .OrderBy(x => x.Index)
                .ToListAsync();

            await Db.Database.ExecuteSqlRawAsync("""
                DELETE FROM "BakingRights" WHERE "ChainId" = {0} AND "Level" >= {2};
                DELETE FROM "BakerCycles" WHERE "ChainId" = {0} AND "Cycle" > {1};
                DELETE FROM "DelegatorCycles" WHERE "ChainId" = {0} AND "Cycle" > {1};
                """, state.Id, state.Cycle, cycles[1].FirstLevel);

            var seelctedBakers = bakers.Where(x => x.BakingPower != 0);
            var sampler = GetSampler(seelctedBakers.Select(x => (x.Id, x.BakingPower)));

            #region temporary diagnostics
            await sampler.Validate(Proto, state.Level, state.Cycle);
            #endregion

            var conn = (Db.Database.GetDbConnection() as NpgsqlConnection)!;

            #region save shifted
            var currentCycle = cycles[0];
            var shifted = RightsGenerator.GetAttestationRights(sampler, nextProto, currentCycle, currentCycle.LastLevel);

            using (var writer = conn.BeginBinaryImport(@"
                COPY ""BakingRights"" (""ChainId"", ""Cycle"", ""Level"", ""BakerId"", ""Type"", ""Status"", ""Round"", ""Slots"")
                FROM STDIN (FORMAT BINARY)"))
            {
                foreach (var ar in shifted)
                {
                    writer.StartRow();
                    writer.Write(state.Id, NpgsqlTypes.NpgsqlDbType.Integer);
                    writer.Write(currentCycle.Index + 1, NpgsqlTypes.NpgsqlDbType.Integer);
                    writer.Write(ar.Level + 1, NpgsqlTypes.NpgsqlDbType.Integer);
                    writer.Write(ar.Baker, NpgsqlTypes.NpgsqlDbType.Integer);
                    writer.Write((int)BakingRightType.Attestation, NpgsqlTypes.NpgsqlDbType.Integer);
                    writer.Write((int)BakingRightStatus.Future, NpgsqlTypes.NpgsqlDbType.Integer);
                    writer.WriteNull();
                    writer.Write(ar.Slots, NpgsqlTypes.NpgsqlDbType.Integer);
                }

                writer.Complete();
            }
            #endregion
            
            foreach (var cycle in cycles.Skip(1))
            {
                GC.Collect();
                var brs = await RightsGenerator.GetBakingRightsAsync(sampler, nextProto, cycle);
                var ars = await RightsGenerator.GetAttestationRightsAsync(sampler, nextProto, cycle);

                #region save rights
                using (var writer = conn.BeginBinaryImport(@"
                    COPY ""BakingRights"" (""ChainId"", ""Cycle"", ""Level"", ""BakerId"", ""Type"", ""Status"", ""Round"", ""Slots"")
                    FROM STDIN (FORMAT BINARY)"))
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

                #region save delegator cycles
                await Db.Database.ExecuteSqlRawAsync("""
                    INSERT INTO "DelegatorCycles" (
                        "ChainId",
                        "Cycle",
                        "DelegatorId",
                        "BakerId",
                        "DelegatedBalance",
                        "StakedPseudotokens"
                    )
                    SELECT
                        {0},
                        {1},
                        "AddressId",
                        "BakerId",
                        "OwnDelegatedBalance",
                        "Pseudotokens"
                    FROM "SnapshotBalances"
                    WHERE "ChainId" = {0}
                    AND "Level" = {2}
                    AND "BakerId" != "AddressId"
                    """, cycle.ChainId, cycle.Index, cycle.SnapshotLevel);
                #endregion

                #region save baker cycles
                var bakerCycles = bakers.Where(x => x.Staked).ToDictionary(x => x.Id, x =>
                {
                    var bc = new BakerCycle
                    {
                        ChainId = x.ChainId,
                        BakerId = x.Id,
                        Cycle = cycle.Index,
                        OwnDelegatedBalance = x.OwnDelegatedBalance,
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
                        var expectedAttestations = (nextProto.BlocksPerCycle * nextProto.AttestersPerBlock).MulRatio(x.BakingPower, cycle.TotalBakingPower);
                        bc.ExpectedBlocks = nextProto.BlocksPerCycle.MulRatio(x.BakingPower, cycle.TotalBakingPower);
                        bc.ExpectedAttestations = expectedAttestations;
                        bc.FutureAttestationRewards = expectedAttestations * nextProto.AttestationReward0;
                    }
                    return bc;
                });
                Db.BakerCycles.AddRange(bakerCycles.Values);
                #endregion

                #region apply future baking rights
                foreach (var br in brs.Where(x => x.Round == 0))
                {
                    if (!bakerCycles.TryGetValue(br.Baker, out var bakerCycle))
                        throw new Exception("Nonexistent baker cycle");

                    bakerCycle.FutureBlocks++;
                    bakerCycle.FutureBlockRewards += nextProto.MaxBakingReward;
                }
                #endregion

                #region apply future attestation rights
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

                shifted = ars.Where(x => x.Level == cycle.LastLevel).ToList();
            }
        }

        protected virtual Sampler GetSampler(IEnumerable<(int id, long stake)> selection)
        {
            var sorted = selection
                .OrderByDescending(x => x.stake)
                .ThenByDescending(x => Base58.Parse(Cache.Addresses.GetBaker(x.id).Hash), new BytesComparer());

            return new Sampler([..sorted.Select(x => x.id)], [..sorted.Select(x => x.stake)]);
        }
    }
}
