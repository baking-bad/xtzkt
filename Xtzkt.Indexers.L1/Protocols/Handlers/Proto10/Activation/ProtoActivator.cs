using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Netezos.Contracts;
using Netezos.Encoding;
using Newtonsoft.Json.Linq;
using Npgsql;
using Xtzkt.Data.Models;
using Xtzkt.Data.Utils;
using Xtzkt.Indexers.Common.Exceptions;
using Xtzkt.Indexers.Common.Extensions;
using Xtzkt.Indexers.Common.Helpers;
using Xtzkt.Indexers.Common.Utils;
using Xtzkt.Utils;

namespace Xtzkt.Indexers.L1.Protocols.Proto10
{
    class ProtoActivator(ProtocolHandler proto) : Proto09.ProtoActivator(proto)
    {
        public const string CpmmContract = "KT1TxqZ8QtKvLu3V3JH7Gx58n7Co8pgtpQU5";
        public const string LiquidityToken = "KT1AafHA1C1vk959wvHWBispY9Y2f3fxBUUo";
        public const string FallbackToken = "KT1VqarPDicMFn1ejmQqqshUkUXTCTXwmkCN";
        public const string Tzbtc = "KT1PWx2mnDueood7fEmfbBDKx1D9BAnnXitn";

        protected override void SetParameters(L1Protocol protocol, JToken parameters)
        {
            #region unchanged
            protocol.RampUpCycles = parameters["security_deposit_ramp_up_cycles"]?.Value<int>() ?? 0;
            protocol.NoRewardCycles = parameters["no_reward_cycles"]?.Value<int>() ?? 0;
            protocol.ByteCost = parameters["cost_per_byte"]?.Value<int>() ?? 250;
            protocol.HardOperationGasLimit = parameters["hard_gas_limit_per_operation"]?.Value<int>() ?? 1_040_000;
            protocol.HardOperationStorageLimit = parameters["hard_storage_limit_per_operation"]?.Value<int>() ?? 60_000;
            protocol.OriginationSize = parameters["origination_size"]?.Value<int>() ?? 257;
            protocol.ConsensusRightsDelay = parameters["preserved_cycles"]?.Value<int>() ?? 5;
            protocol.ToleratedInactivityPeriod = protocol.ConsensusRightsDelay + 1;
            protocol.MinimalStake = parameters["tokens_per_roll"]?.Value<long>() ?? 8_000_000_000;
            protocol.BallotQuorumMin = parameters["quorum_min"]?.Value<int>() ?? 2000;
            protocol.BallotQuorumMax = parameters["quorum_max"]?.Value<int>() ?? 7000;
            protocol.ProposalQuorum = parameters["min_proposal_quorum"]?.Value<int>() ?? 500;
            #endregion

            var br = parameters["baking_reward_per_endorsement"] as JArray;
            var ar = parameters["endorsement_reward"] as JArray;

            protocol.BlockDeposit = parameters["block_security_deposit"]?.Value<long>() ?? 640_000_000;
            protocol.AttestationDeposit = parameters["endorsement_security_deposit"]?.Value<long>() ?? 2_500_000;
            protocol.BlockReward0 = br == null ? 78_125 : br.Count > 0 ? br[0].Value<long>() : 0;
            protocol.BlockReward1 = br == null ? 11_719 : br.Count > 1 ? br[1].Value<long>() : protocol.BlockReward0;
            protocol.AttestationReward0 = ar == null ? 78_125 : ar.Count > 0 ? ar[0].Value<long>() : 0;
            protocol.AttestationReward1 = ar == null ? 52_083 : ar.Count > 1 ? ar[1].Value<long>() : protocol.AttestationReward0;

            protocol.BlocksPerCycle = parameters["blocks_per_cycle"]?.Value<int>() ?? 8192;
            protocol.BlocksPerCommitment = parameters["blocks_per_commitment"]?.Value<int>() ?? 64;
            protocol.BlocksPerSnapshot = parameters["blocks_per_roll_snapshot"]?.Value<int>() ?? 512;
            protocol.BlocksPerVoting = parameters["blocks_per_voting_period"]?.Value<int>() ?? 40960;

            protocol.AttestersPerBlock = parameters["endorsers_per_block"]?.Value<int>() ?? 256;
            protocol.HardBlockGasLimit = parameters["hard_gas_limit_per_block"]?.Value<int>() ?? 5_200_000;
            protocol.TimeBetweenBlocks = parameters["minimal_block_delay"]?.Value<int>() ?? 30;

            protocol.LBToggleThreshold = (parameters["liquidity_baking_escape_ema_threshold"]?.Value<int>() ?? 1_000_000) * 1000;
        }

        protected override void UpgradeParameters(L1Protocol protocol, L1Protocol prev)
        {
            protocol.BlockDeposit = 640_000_000;
            protocol.AttestationDeposit = 2_500_000;
            protocol.BlockReward0 = 78_125;
            protocol.BlockReward1 = 11_719;
            protocol.AttestationReward0 = 78_125;
            protocol.AttestationReward1 = 52_083;

            protocol.BlocksPerCycle *= 2;
            protocol.BlocksPerCommitment *= 2;
            protocol.BlocksPerSnapshot *= 2;
            protocol.BlocksPerVoting *= 2;

            protocol.AttestersPerBlock = 256;
            protocol.HardBlockGasLimit = 5_200_000;
            protocol.TimeBetweenBlocks /= 2;

            protocol.LBToggleThreshold = 1_000_000_000;
        }

        protected override async Task ActivateContext(L1Chain chain)
        {
            var block = await Cache.Blocks.CurrentAsync();
            await OriginateContract(block, CpmmContract);
            await OriginateContract(block, LiquidityToken);
            if (!await Cache.Addresses.ExistsAsync(Tzbtc))
                await OriginateContract(block, FallbackToken);
        }

        protected override async Task DeactivateContext(L1Chain chain)
        {
            chain.TokensCount--;
            chain.TokenBalancesCount--;
            chain.TokenTransfersCount--;

            await Db.Database.ExecuteSqlRawAsync("""
                DELETE FROM "BigMapUpdates" WHERE "ChainId" = {0};
                DELETE FROM "BigMapKeys" WHERE "ChainId" = {0};
                DELETE FROM "BigMaps" WHERE "ChainId" = {0};
                DELETE FROM "Tokens" WHERE "ChainId" = {0};
                DELETE FROM "TokenBalances" WHERE "ChainId" = {0};
                DELETE FROM "TokenTransfers" WHERE "ChainId" = {0};
                """, chain.Id);
            Cache.BigMapKeys.Reset();
            Cache.BigMaps.Reset();
            Cache.Tokens.Reset();
            Cache.TokenBalances.Reset();

            Cache.Chain.Get().BigMapCounter = 0;
            Cache.Chain.Get().BigMapKeyCounter = 0;
            Cache.Chain.Get().BigMapUpdateCounter = 0;
        }

        protected override async Task MigrateContext(L1Chain state)
        {
            var prevProto = await Cache.Protocols.GetAsync(state.Protocol);
            var nextProto = await Cache.Protocols.GetAsync(state.NextProtocol);

            #region update voting period
            var newPeriod = await Cache.Periods.GetAsync(state.VotingPeriod);
            Db.TryAttach(newPeriod);
            newPeriod.LastLevel = newPeriod.FirstLevel + nextProto.BlocksPerVoting; // - 1 + 1
            #endregion

            var cycles = await MigrateCycles(state, nextProto);
            await Db.Database.ExecuteSqlRawAsync("""
                DELETE FROM "BakingRights"
                WHERE "ChainId" = {0}
                AND "Level" >= {1}
                """, state.Id, cycles[0].FirstLevel);
            await MigrateCurrentRights(state, prevProto, nextProto, state.Level);
            await MigrateFutureRights(cycles, state, nextProto, state.Level);
            MigrateBakers(state, prevProto, nextProto);

            Cache.BakingRights.Reset();
            Cache.BakerCycles.Reset();
            Cache.Periods.Reset();

            var block = await Cache.Blocks.CurrentAsync();
            await OriginateContract(block, CpmmContract);
            await OriginateContract(block, LiquidityToken);
            if (!await Cache.Addresses.ExistsAsync(Tzbtc))
                await OriginateContract(block, FallbackToken);
        }

        protected override async Task RevertContext(L1Chain state)
        {
            var prevProto = await Cache.Protocols.GetAsync(state.Protocol);
            var nextProto = await Cache.Protocols.GetAsync(state.NextProtocol);

            #region update voting periods
            var newPeriod = await Cache.Periods.GetAsync(state.VotingPeriod);
            Db.TryAttach(newPeriod);
            newPeriod.LastLevel = newPeriod.FirstLevel + prevProto.BlocksPerVoting - 1;
            #endregion

            var cycles = await MigrateCycles(state, prevProto);
            await Db.Database.ExecuteSqlRawAsync("""
                DELETE FROM "BakingRights"
                WHERE "ChainId" = {0}
                AND "Level" >= {1}
                """, state.Id, cycles[0].FirstLevel);
            await MigrateCurrentRights(state, nextProto, prevProto, state.Level - 1);
            await MigrateFutureRights(cycles, state, prevProto, state.Level - 1);
            MigrateBakers(state, nextProto, prevProto);

            Cache.BakingRights.Reset();
            Cache.BakerCycles.Reset();
            Cache.Periods.Reset();

            await RemoveContract(CpmmContract);
            await RemoveContract(LiquidityToken);
            if (await Cache.Addresses.ExistsAsync(FallbackToken))
                await RemoveContract(FallbackToken);
        }

        async Task<List<Cycle>> MigrateCycles(L1Chain state, L1Protocol nextProto)
        {
            var cycles = await Db.Cycles
                .Where(x => x.ChainId == state.Id && x.Index > state.Cycle)
                .OrderBy(x => x.Index)
                .ToListAsync();

            foreach (var cycle in cycles)
            {
                cycle.FirstLevel = nextProto.GetCycleStart(cycle.Index);
                cycle.LastLevel = nextProto.GetCycleEnd(cycle.Index);
            }

            return cycles;
        }

        async Task MigrateCurrentRights(L1Chain state, L1Protocol prevProto, L1Protocol nextProto, int block)
        {
            var rights = await Db.BakingRights
                .AsNoTracking()
                .Where(x => x.ChainId == state.Id && x.Level > state.Level && x.Cycle == state.Cycle)
                .ToListAsync();

            foreach (var br in rights.Where(x => x.Type == BakingRightType.Baking && x.Round == 0))
            {
                var bakerCycle = await Cache.BakerCycles.GetAsync(state.Cycle, br.BakerId);
                Db.TryAttach(bakerCycle);

                bakerCycle.FutureBlockRewards -= GetFutureBlockReward(prevProto, state.Cycle);
                bakerCycle.FutureBlockRewards += GetFutureBlockReward(nextProto, state.Cycle);
            }

            foreach (var ar in rights.Where(x => x.Type == BakingRightType.Attestation))
            {
                var bakerCycle = await Cache.BakerCycles.GetAsync(state.Cycle, ar.BakerId);
                Db.TryAttach(bakerCycle);

                bakerCycle.FutureAttestationRewards -= GetFutureAttestationReward(prevProto, state.Cycle, ar.Slots!.Value);
                bakerCycle.FutureAttestations -= ar.Slots.Value;
            }

            await Db.Database.ExecuteSqlRawAsync("""
                DELETE FROM "BakingRights"
                WHERE "ChainId" = {0}
                AND  "Level" > {1}
                AND "Type" = {2}
                """, state.Id, state.Level, (int)BakingRightType.Attestation);

            var newArs = new List<BakingRight>();
            for (int level = state.Level + 1; level < nextProto.GetCycleStart(state.Cycle + 1); level++)
            {
                foreach (var ar in (await Proto.Rpc.GetLevelAttestationRightsAsync(block, level - 1)).EnumerateArray())
                {
                    newArs.Add(new BakingRight
                    {
                        Id = 0,
                        ChainId = state.Id,
                        Type = BakingRightType.Attestation,
                        Status = BakingRightStatus.Future,
                        BakerId = Cache.Addresses.GetExistingBaker(ar.RequiredString("delegate")).Id,
                        Cycle = state.Cycle,
                        Level = level,
                        Slots = ar.RequiredArray("slots").Count()
                    });
                }
            }

            await Db.Database.ExecuteSqlRawAsync("""
                INSERT INTO "BakingRights" ("ChainId", "Cycle", "Level", "BakerId", "Type", "Status", "Slots")
                SELECT {0}, {1}, q.level, q.baker, {2}, {3}, q.slots
                FROM unnest({4}::int[], {5}::int[], {6}::int[]) AS q(level, baker, slots)
                """,
                state.Id, state.Cycle, (int)BakingRightType.Attestation, (int)BakingRightStatus.Future,
                newArs.Select(x => x.Level).ToArray(),
                newArs.Select(x => x.BakerId).ToArray(),
                newArs.Select(x => x.Slots).ToArray());

            foreach (var ar in newArs)
            {
                var bakerCycle = await Cache.BakerCycles.GetAsync(state.Cycle, ar.BakerId);
                Db.TryAttach(bakerCycle);

                bakerCycle.FutureAttestationRewards += GetFutureAttestationReward(nextProto, state.Cycle, ar.Slots!.Value);
                bakerCycle.FutureAttestations += ar.Slots.Value;
            }
        }

        async Task MigrateFutureRights(List<Cycle> cycles, L1Chain state, L1Protocol nextProto, int block)
        {
            var nextCycle = state.Cycle + 1;
            var nextCycleStart = nextProto.GetCycleStart(nextCycle);
            var shiftedRights = (await Proto.Rpc.GetLevelAttestationRightsAsync(block, nextCycleStart - 1))
                .EnumerateArray()
                .ToList();

            await Db.Database.ExecuteSqlRawAsync("""
                INSERT INTO "BakingRights" ("ChainId", "Cycle", "Level", "BakerId", "Type", "Status", "Slots")
                SELECT {0}, {1}, {2}, q.baker, {3}, {4}, q.slots
                FROM unnest({5}::int[], {6}::int[]) AS q(baker, slots)
                """,
                state.Id, nextCycle, nextCycleStart, (int)BakingRightType.Attestation, (int)BakingRightStatus.Future,
                shiftedRights.Select(ar => Cache.Addresses.GetExistingBaker(ar.RequiredString("delegate")).Id).ToArray(),
                shiftedRights.Select(ar => ar.RequiredArray("slots").Count()).ToArray());

            foreach (var cycle in cycles)
            {
                var bakerCycles = (await Db.BakerCycles.Where(x => x.ChainId == state.Id && x.Cycle == cycle.Index).ToListAsync())
                    .ToDictionary(x => x.BakerId);

                foreach (var bc in bakerCycles.Values)
                {
                    var share = (double)bc.BakingPower / cycle.TotalBakingPower;
                    bc.ExpectedBlocks = nextProto.BlocksPerCycle * share;
                    bc.ExpectedAttestations = nextProto.AttestersPerBlock * nextProto.BlocksPerCycle * share;
                    bc.FutureBlockRewards = 0;
                    bc.FutureBlocks = 0;
                    bc.FutureAttestationRewards = 0;
                    bc.FutureAttestations = 0;
                }

                await FetchBakingRights(nextProto, block, cycle, bakerCycles);
                shiftedRights = await FetchAttestationRights(nextProto, block, cycle, bakerCycles, shiftedRights);
            }
        }

        async Task FetchBakingRights(L1Protocol protocol, int block, Cycle cycle, Dictionary<int, BakerCycle> bakerCycles)
        {
            GC.Collect();
            var rights = (await Proto.Rpc.GetBakingRightsAsync(block, cycle.Index)).RequiredArray().EnumerateArray();
            if (!rights.Any() || rights.Count(x => x.RequiredInt32("priority") == 0) != protocol.BlocksPerCycle)
                throw new ValidationException("Rpc returned less baking rights (with priority 0) than it should be");

            var conn = (Db.Database.GetDbConnection() as NpgsqlConnection)!;
            using var writer = conn.BeginBinaryImport(@"COPY ""BakingRights"" (""ChainId"", ""Cycle"", ""Level"", ""BakerId"", ""Type"", ""Status"", ""Round"", ""Slots"") FROM STDIN (FORMAT BINARY)");

            foreach (var br in rights)
            {
                var bakerId = Cache.Addresses.GetExistingBaker(br.RequiredString("delegate")).Id;
                var round = br.RequiredInt32("priority");
                if (round == 0)
                {
                    var bakerCycle = bakerCycles[bakerId];
                    bakerCycle.FutureBlockRewards += GetFutureBlockReward(protocol, cycle.Index);
                    bakerCycle.FutureBlocks++;
                }

                writer.StartRow();
                writer.Write(protocol.ChainId, NpgsqlTypes.NpgsqlDbType.Integer);
                writer.Write(cycle.Index, NpgsqlTypes.NpgsqlDbType.Integer);
                writer.Write(br.RequiredInt32("level"), NpgsqlTypes.NpgsqlDbType.Integer);
                writer.Write(bakerId, NpgsqlTypes.NpgsqlDbType.Integer);
                writer.Write((short)BakingRightType.Baking, NpgsqlTypes.NpgsqlDbType.Smallint);
                writer.Write((short)BakingRightStatus.Future, NpgsqlTypes.NpgsqlDbType.Smallint);
                writer.Write(round, NpgsqlTypes.NpgsqlDbType.Integer);
                writer.WriteNull();
            }

            writer.Complete();
        }

        async Task<List<JsonElement>> FetchAttestationRights(L1Protocol protocol, int block, Cycle cycle, Dictionary<int, BakerCycle> bakerCycles, List<JsonElement> shiftedRights)
        {
            GC.Collect();
            var rights = (await Proto.Rpc.GetAttestationRightsAsync(block, cycle.Index)).RequiredArray().EnumerateArray();
            //var rights = new List<JsonElement>(protocol.BlocksPerCycle * protocol.AttestersPerBlock / 2);
            //var attempts = 0;

            //for (int level = cycle.FirstLevel; level <= cycle.LastLevel; level++)
            //{
            //    try
            //    {
            //        rights.AddRange((await Proto.Rpc.GetLevelAttestationRightsAsync(block, level)).RequiredArray().EnumerateArray());
            //        attempts = 0;
            //    }
            //    catch (Exception ex)
            //    {
            //        Logger.LogError(ex, "Failed to fetch attestation rights for level {level}", level);
            //        if (++attempts >= 10) throw new Exception("Too many RPC errors when fetching attestation rights");
            //        await Task.Delay(3000);
            //        level--;
            //    }
            //}

            if (!rights.Any() || rights.Sum(x => x.RequiredArray("slots").Count()) != protocol.BlocksPerCycle * protocol.AttestersPerBlock)
                throw new ValidationException("Rpc returned less attestation rights (slots) than it should be");

            #region save rights
            var conn = (Db.Database.GetDbConnection() as NpgsqlConnection)!;
            using var writer = conn.BeginBinaryImport(@"COPY ""BakingRights"" (""ChainId"", ""Cycle"", ""Level"", ""BakerId"", ""Type"", ""Status"", ""Round"", ""Slots"") FROM STDIN (FORMAT BINARY)");

            foreach (var ar in rights)
            {
                writer.StartRow();
                writer.Write(protocol.ChainId, NpgsqlTypes.NpgsqlDbType.Integer);
                writer.Write(protocol.GetCycle(ar.RequiredInt32("level") + 1), NpgsqlTypes.NpgsqlDbType.Integer);
                writer.Write(ar.RequiredInt32("level") + 1, NpgsqlTypes.NpgsqlDbType.Integer);
                writer.Write(Cache.Addresses.GetExistingBaker(ar.RequiredString("delegate")).Id, NpgsqlTypes.NpgsqlDbType.Integer);
                writer.Write((short)BakingRightType.Attestation, NpgsqlTypes.NpgsqlDbType.Smallint);
                writer.Write((short)BakingRightStatus.Future, NpgsqlTypes.NpgsqlDbType.Smallint);
                writer.WriteNull();
                writer.Write(ar.RequiredArray("slots").Count(), NpgsqlTypes.NpgsqlDbType.Integer);
            }

            writer.Complete();
            #endregion

            foreach (var ar in rights.Where(x => x.RequiredInt32("level") != cycle.LastLevel))
            {
                var baker = Cache.Addresses.GetExistingBaker(ar.RequiredString("delegate"));
                var slots = ar.RequiredArray("slots").Count();

                if (!bakerCycles.TryGetValue(baker.Id, out var bakerCycle))
                    throw new Exception("Nonexistent baker cycle");

                bakerCycle.FutureAttestationRewards += GetFutureAttestationReward(protocol, cycle.Index, slots);
                bakerCycle.FutureAttestations += slots;
            }

            foreach (var ar in shiftedRights)
            {
                var baker = Cache.Addresses.GetExistingBaker(ar.RequiredString("delegate"));
                var slots = ar.RequiredArray("slots").Count();

                if (!bakerCycles.TryGetValue(baker.Id, out var bakerCycle))
                {
                    #region shifting hack
                    var snapshottedBaker = await Proto.Rpc.GetDelegateAsync(cycle.SnapshotLevel, baker.Hash);
                    var delegators = snapshottedBaker
                        .RequiredArray("delegated_contracts")
                        .EnumerateArray()
                        .Select(x => x.RequiredString())
                        .Where(x => x != baker.Hash);

                    var stakingBalance = snapshottedBaker.RequiredInt64("staking_balance");
                    var delegatedBalance = snapshottedBaker.RequiredInt64("delegated_balance");

                    bakerCycle = new BakerCycle
                    {
                        ChainId = baker.ChainId,
                        Cycle = cycle.Index,
                        BakerId = baker.Id,
                        OwnDelegatedBalance = stakingBalance - delegatedBalance,
                        ExternalDelegatedBalance = delegatedBalance,
                        DelegatorsCount = delegators.Count(),
                        OwnStakedBalance = 0,
                        ExternalStakedBalance = 0,
                        StakersCount = 0,
                        IssuedPseudotokens = null,
                        BakingPower = 0,
                        TotalBakingPower = cycle.TotalBakingPower,
                        ExpectedBlocks = 0,
                        ExpectedAttestations = 0
                    };
                    bakerCycles.Add(baker.Id, bakerCycle);
                    Db.BakerCycles.Add(bakerCycle);

                    foreach (var delegatorAddress in delegators)
                    {
                        var snapshottedDelegator = await Proto.Rpc.GetContractAsync(cycle.SnapshotLevel, delegatorAddress);
                        Db.DelegatorCycles.Add(new DelegatorCycle
                        {
                            Id = 0,
                            ChainId = baker.ChainId,
                            Cycle = cycle.Index,
                            DelegatorId = (await Cache.Addresses.GetExistingAsync(delegatorAddress)).Id,
                            BakerId = baker.Id,
                            DelegatedBalance = snapshottedDelegator.RequiredInt64("balance"),
                            StakedPseudotokens = null
                        });
                    }
                    #endregion
                }

                bakerCycle.FutureAttestationRewards += GetFutureAttestationReward(protocol, cycle.Index, slots);
                bakerCycle.FutureAttestations += slots;
            }

            return [..rights.Where(x => x.RequiredInt32("level") == cycle.LastLevel)];
        }

        void MigrateBakers(L1Chain state, L1Protocol prevProto, L1Protocol nextProto)
        {
            foreach (var baker in Cache.Addresses.GetBakers().Where(x => x.DeactivationLevel > state.Level))
            {
                Db.TryAttach(baker);
                baker.DeactivationLevel = nextProto.GetCycleStart(prevProto.GetCycle(baker.DeactivationLevel));
            }
        }

        async Task OriginateContract(L1Block block, string address)
        {
            var rawContract = await Proto.Rpc.GetContractAsync(block.Level, address);

            #region contract
            L1Contract contract;
            var creator = await Cache.Addresses.GetExistingAsync(NullAddress.Hash);
            var ghost = await Cache.Addresses.GetAsync(address, Context.Block);
            if (ghost != null)
            {
                contract = new L1Contract
                {
                    Id = ghost.Id,
                    ChainId = ghost.ChainId,
                    Index = ghost.Index,
                    FirstLevel = ghost.FirstLevel,
                    FirstTimestamp = ghost.FirstTimestamp,
                    LastLevel = block.Level,
                    LastTimestamp = block.Timestamp,
                    Hash = address,
                    CreatorId = creator.Id,
                    Kind = L1ContractKind.SmartContract,
                    MigrationsCount = 1,
                    ActiveTokensCount = ghost.ActiveTokensCount,
                    TokenBalancesCount = ghost.TokenBalancesCount,
                    TokenTransfersCount = ghost.TokenTransfersCount
                };
                var isAdded = Db.Entry(ghost).State == EntityState.Added;
                Db.Entry(ghost).State = EntityState.Detached;
                Db.Entry(contract).State = isAdded ? EntityState.Added : EntityState.Modified;
            }
            else
            {
                contract = new L1Contract
                {
                    Id = Cache.Chain.NextAddressId(),
                    ChainId = Cache.Chain.Get().Id,
                    FirstLevel = block.Level,
                    FirstTimestamp = block.Timestamp,
                    LastLevel = block.Level,
                    LastTimestamp = block.Timestamp,
                    Hash = address,
                    CreatorId = creator.Id,
                    Kind = L1ContractKind.SmartContract,
                    MigrationsCount = 1,
                };
                Db.Addresses.Add(contract);
            }
            Receive(contract, rawContract.RequiredInt64("balance"));
            Cache.Addresses.Add(contract);

            Db.TryAttach(creator);
            creator.ContractsCount++;
            #endregion

            #region script
            var code = (rawContract.Required("script").RequiredMicheline("code") as MichelineArray)!;
            var micheParameter = code.First(x => x is MichelinePrim p && p.Prim == PrimType.parameter);
            var micheStorage = code.First(x => x is MichelinePrim p && p.Prim == PrimType.storage);
            var micheCode = code.First(x => x is MichelinePrim p && p.Prim == PrimType.code);
            var micheViews = code.Where(x => x is MichelinePrim p && p.Prim == PrimType.view);
            var script = new MichelsonScript
            {
                Id = Cache.Chain.NextScriptId(),
                ChainId = contract.ChainId,
                Level = block.Level,
                ContractId = contract.Id,
                ParameterSchema = micheParameter.ToBytes(),
                StorageSchema = micheStorage.ToBytes(),
                CodeSchema = micheCode.ToBytes(),
                Views = micheViews.Any()
                    ? [..micheViews.Select(x => x.ToBytes())]
                    : null,
                Current = true
            };

            var viewsBytes = script.Views?
                .OrderBy(x => x, BytesComparer.Instance)
                .SelectMany(x => x)
                .ToArray()
                ?? [];
            var typeSchema = script.ParameterSchema.Concat(script.StorageSchema).Concat(viewsBytes);
            var fullSchema = typeSchema.Concat(script.CodeSchema);
            contract.TypeHash = script.TypeHash = MichelsonScript.GetHash(typeSchema);
            contract.CodeHash = script.CodeHash = MichelsonScript.GetHash(fullSchema);

            if (script.Schema.IsFA1())
            {
                if (script.Schema.IsFA12())
                    contract.Tags |= L1ContractTags.FA12;

                contract.Tags |= L1ContractTags.FA1;
                contract.Kind = L1ContractKind.Asset;
            }
            if (script.Schema.IsFA2())
            {
                contract.Tags |= L1ContractTags.FA2;
                contract.Kind = L1ContractKind.Asset;
            }

            Db.Scripts.Add(script);
            #endregion

            #region storage
            var storageValue = rawContract.Required("script").RequiredMicheline("storage");
            var storage = new Storage
            {
                Id = Cache.Chain.NextStorageId(),
                ChainId = contract.ChainId,
                Level = block.Level,
                ContractId = contract.Id,
                RawValue = script.Schema.OptimizeStorage(storageValue, false).ToBytes(),
                JsonValue = Regexes.RestrictedUnicode().Replace(script.Schema.HumanizeStorage(storageValue), Regexes.NullEscapeString),
                Current = true
            };

            Db.Storages.Add(storage);
            #endregion

            #region migration
            var migration = new MichelsonMigrationOperation
            {
                Id = Cache.Chain.NextOperationId(),
                ChainId = block.ChainId,
                Level = block.Level,
                Timestamp = block.Timestamp,
                Kind = MigrationKind.Origination,
                AddressId = contract.Id,
                BalanceChange = contract.Balance,
                ScriptId = script.Id,
                StorageId = storage.Id,
            };

            script.MigrationId = migration.Id;
            storage.MigrationId = migration.Id;

            Db.TryAttach(block);
            block.Operations |= L1Operations.Migration;

            var state = Cache.Chain.Get();
            Db.TryAttach(state);
            state.MigrationOpsCount++;

            var stats = Cache.Statistics.Current;
            Db.TryAttach(stats);
            stats.TotalCreated += contract.Balance;

            Db.MigrationOps.Add(migration);
            Context.MigrationOps.Add(migration);
            #endregion

            #region bigmaps
            var storageScript = new ContractStorage(micheStorage);
            var storageTree = storageScript.Schema.ToTreeView(storageValue);
            var bigmaps = storageTree.Nodes()
                .Where(x => x.Schema is BigMapSchema)
                .Select(x => (x, (x.Schema as BigMapSchema)!, (int)(x.Value as MichelineInt)!.Value));

            foreach (var (bigmap, schema, ptr) in bigmaps)
            {
                block.Events |= L1BlockEvents.Bigmaps;

                var allocated = new BigMap
                {
                    Id = Cache.Chain.NextBigMapId(),
                    ChainId = Cache.Chain.Get().Id,
                    Ptr = ptr,
                    ContractId = contract.Id,
                    StoragePath = bigmap.Path,
                    KeyType = schema.Key.ToMicheline().ToBytes(),
                    ValueType = schema.Value.ToMicheline().ToBytes(),
                    Active = true,
                    FirstLevel = block.Level,
                    FirstTimestamp = block.Timestamp,
                    LastLevel = block.Level,
                    LastTimestamp = block.Timestamp,
                    ActiveKeys = 0,
                    TotalKeys = 0,
                    Updates = 1,
                    Tags = BigMaps.GetTags(contract, bigmap)
                };
                Db.BigMaps.Add(allocated);

                Db.BigMapUpdates.Add(new BigMapUpdate
                {
                    Id = Cache.Chain.NextBigMapUpdateId(),
                    ChainId = Cache.Chain.Get().Id,
                    Action = BigMapAction.Allocate,
                    BigMapId = allocated.Id,
                    Level = block.Level,
                    Timestamp = block.Timestamp,
                    MigrationId = migration.Id
                });

                migration.BigMapUpdates = (migration.BigMapUpdates ?? 0) + 1;

                if (address == LiquidityToken && allocated.StoragePath == "tokens")
                {
                    var rawKey = new MichelineString(NullAddress.Hash);
                    var rawValue = new MichelineInt(100);

                    allocated.Tags |= BigMapTag.Ledger1;
                    allocated.ActiveKeys++;
                    allocated.TotalKeys++;
                    allocated.Updates++;
                    var key = new BigMapKey
                    {
                        Id = Cache.Chain.NextBigMapKeyId(),
                        ChainId = Cache.Chain.Get().Id,
                        Active = true,
                        BigMapId = allocated.Id,
                        FirstLevel = block.Level,
                        FirstTimestamp = block.Timestamp,
                        LastLevel = block.Level,
                        LastTimestamp = block.Timestamp,
                        JsonKey = Regexes.RestrictedUnicode().Replace(schema.Key.Humanize(rawKey), Regexes.NullEscapeString),
                        JsonValue = Regexes.RestrictedUnicode().Replace(schema.Value.Humanize(rawValue), Regexes.NullEscapeString),
                        RawKey = schema.Key.Optimize(rawKey).ToBytes(),
                        RawValue = schema.Value.Optimize(rawValue).ToBytes(),
                        KeyHash = Hashes.ParseExprHash(schema.GetKeyHash(rawKey)),
                        Updates = 1
                    };
                    Db.BigMapKeys.Add(key);

                    migration.BigMapUpdates++;
                    Db.BigMapUpdates.Add(new BigMapUpdate
                    {
                        Id = Cache.Chain.NextBigMapUpdateId(),
                        ChainId = Cache.Chain.Get().Id,
                        Action = BigMapAction.AddKey,
                        BigMapKeyId = key.Id,
                        BigMapId = key.BigMapId,
                        JsonValue = key.JsonValue,
                        RawValue = key.RawValue,
                        Level = block.Level,
                        Timestamp = block.Timestamp,
                        MigrationId = migration.Id
                    });

                    #region tokens
                    var token = new Token
                    {
                        Id = Cache.Chain.NextSubId(migration),
                        ChainId = contract.ChainId,
                        Tags = TokenTags.Fa12,
                        BalancesCount = 1,
                        ContractId = contract.Id,
                        FirstMinterId = contract.Id,
                        FirstLevel = migration.Level,
                        FirstTimestamp = migration.Timestamp,
                        HoldersCount = 1,
                        LastLevel = migration.Level,
                        LastTimestamp = migration.Timestamp,
                        TokenId = 0,
                        TotalBurned = 0,
                        TotalMinted = 100,
                        TotalSupply = 100,
                        TransfersCount = 1
                    };
                    var tokenBalance = new TokenBalance
                    {
                        Id = Cache.Chain.NextSubId(migration),
                        ChainId = migration.ChainId,
                        AddressId = NullAddress.Id,
                        Entrypoint = null,
                        Balance = 100,
                        FirstLevel = migration.Level,
                        FirstTimestamp = migration.Timestamp,
                        LastLevel = migration.Level,
                        LastTimestamp = migration.Timestamp,
                        TokenId = token.Id,
                        ContractId = token.ContractId,
                        TransfersCount = 1
                    };
                    var tokenTransfer = new TokenTransfer
                    {
                        Id = Cache.Chain.NextSubId(migration),
                        ChainId = migration.ChainId,
                        Amount = 100,
                        Level = migration.Level,
                        Timestamp = migration.Timestamp,
                        MigrationId = migration.Id,
                        ToId = NullAddress.Id,
                        ToEntrypoint = null,
                        TokenId = token.Id,
                        ContractId = token.ContractId
                    };

                    Db.Tokens.Add(token);
                    Db.TokenBalances.Add(tokenBalance);
                    Db.TokenTransfers.Add(tokenTransfer);

                    migration.TokenTransfers = 1;

                    state.TokensCount++;
                    state.TokenBalancesCount++;
                    state.TokenTransfersCount++;

                    contract.TokensCount++;
                    creator.ActiveTokensCount++;
                    creator.TokenBalancesCount++;
                    creator.TokenTransfersCount++;
                    creator.LastLevel = migration.Level;
                    creator.LastTimestamp = migration.Timestamp;

                    block.Events |= L1BlockEvents.Tokens;
                    #endregion
                }
                else if (address == FallbackToken && allocated.StoragePath == "tokens")
                {
                    allocated.Tags |= BigMapTag.Ledger1;
                }
            }
            #endregion
        }

        async Task RemoveContract(string address)
        {
            var contract = (await Cache.Addresses.GetExistingAsync(address) as L1Contract)!;
            Db.TryAttach(contract);

            var bigmaps = await Db.BigMaps.AsNoTracking()
                .Where(x => x.ContractId == contract.Id)
                .ToListAsync();

            var state = Cache.Chain.Get();
            Db.TryAttach(state);
            state.MigrationOpsCount--;

            var creator = await Cache.Addresses.GetAsync(contract.CreatorId);
            Db.TryAttach(creator);
            creator.ContractsCount--;

            if (address == LiquidityToken)
            {
                var token = await Db.Tokens
                    .AsNoTracking()
                    .Where(x => x.ContractId == contract.Id)
                    .SingleAsync();

                await Db.Database.ExecuteSqlRawAsync("""
                    DELETE FROM "TokenTransfers" WHERE "TokenId" = {0};
                    DELETE FROM "TokenBalances" WHERE "TokenId" = {0};
                    DELETE FROM "Tokens" WHERE "Id" = {0};
                    """, token.Id);

                state.TokenTransfersCount--;
                state.TokenBalancesCount--;
                state.TokensCount--;

                contract.TokensCount--;

                creator.ActiveTokensCount--;
                creator.TokenBalancesCount--;
                creator.TokenTransfersCount--;
            }

            await Db.Database.ExecuteSqlRawAsync("""
                DELETE FROM "MigrationOps" WHERE "AddressId" = {0};
                DELETE FROM "Storages" WHERE "ContractId" = {0};
                DELETE FROM "Scripts" WHERE "ContractId" = {0};
                DELETE FROM "BigMapUpdates" WHERE "BigMapId" = ANY({1});
                DELETE FROM "BigMapKeys" WHERE "BigMapId" = ANY({1});
                DELETE FROM "BigMaps" WHERE "Id" = ANY({1});
                """, contract.Id, bigmaps.Select(x => x.Id).ToList());

            Cache.Chain.ReleaseOperationId();
            Cache.Chain.ReleaseScriptId();
            Cache.Chain.ReleaseStorageId();
            Cache.Storages.Remove(contract);
            Cache.Schemas.Remove(contract);
            Cache.BigMapKeys.Reset();
            foreach (var bigmap in bigmaps)
            {
                Cache.BigMaps.Remove(bigmap);
                Cache.Chain.ReleaseBigMapId();
                Cache.Chain.ReleaseBigMapKeyId(bigmap.TotalKeys);
                Cache.Chain.ReleaseBigMapUpdateId(bigmap.Updates);
            }

            if (contract.TokenTransfersCount != 0)
            {
                var ghost = new L1Ghost
                {
                    Id = contract.Id,
                    ChainId = contract.ChainId,
                    Index = contract.Index,
                    Hash = contract.Hash,
                    FirstLevel = contract.FirstLevel,
                    FirstTimestamp = contract.FirstTimestamp,
                    LastLevel = contract.LastLevel,
                    LastTimestamp = contract.LastTimestamp,
                    ActiveTokensCount = contract.ActiveTokensCount,
                    TokenBalancesCount = contract.TokenBalancesCount,
                    TokenTransfersCount = contract.TokenTransfersCount
                };

                Db.Entry(contract).State = EntityState.Detached;
                Db.Entry(ghost).State = EntityState.Modified;
                Cache.Addresses.Add(ghost);
            }
        }

        protected override long GetFutureBlockReward(L1Protocol protocol, int cycle)
            => cycle < protocol.NoRewardCycles ? 0 : (protocol.BlockReward0 * protocol.AttestersPerBlock);

        protected override long GetFutureAttestationReward(L1Protocol protocol, int cycle, int slots)
            => cycle < protocol.NoRewardCycles ? 0 : (slots * protocol.AttestationReward0);
    }
}
