using Microsoft.EntityFrameworkCore;
using Netezos.Contracts;
using Netezos.Encoding;
using Newtonsoft.Json.Linq;
using Npgsql;
using Xtzkt.Data.Models;
using Xtzkt.Indexers.Common.Extensions;
using Xtzkt.Indexers.Common.Utils;
using Xtzkt.Utils;

namespace Xtzkt.Indexers.L1.Protocols.Proto15
{
    partial class ProtoActivator(ProtocolHandler proto) : Proto14.ProtoActivator(proto)
    {
        protected override void SetParameters(L1Protocol protocol, JToken parameters)
        {
            base.SetParameters(protocol, parameters);
            protocol.MinimalStake = parameters["minimal_stake"]?.Value<long>() ?? 6_000_000L;
        }

        protected override void UpgradeParameters(L1Protocol protocol, L1Protocol prev) { }

        protected override async Task MigrateContext(L1Chain state)
        {
            var prevProto = await Cache.Protocols.GetAsync(state.Protocol);
            var nextProto = await Cache.Protocols.GetAsync(state.NextProtocol);

            await AddInvoice(state, "tz1MidLyXXvKWMmbRvKKeusDtP95NDJ5gAUx", 10_000_000_000L);
            await AddInvoice(state, "tz1X81bCXPtMiHu1d4UZF4GPhMPkvkp56ssb", 15_000_000_000L);
            await MigrateCurrentRights(state, prevProto, nextProto);
            await MigrateFutureRights(state, nextProto);
            await PatchContracts(state);

            Cache.BakingRights.Reset();
            Cache.BakerCycles.Reset();

            if (state.ChainId == "NetXnHfVqm9iesp") // ghostnet: amend broken voting period
            {
                await RestartVotingPeriod(state, nextProto);
            }
        }

        protected override Task RevertContext(L1Chain state)
        {
            throw new NotImplementedException("Reverting Lima migration block is not implemented, because likely won't be needed");
        }

        async Task RestartVotingPeriod(L1Chain state, L1Protocol nextProto)
        {
            var currentPeriod = await Cache.Periods.GetAsync(58);
            Db.TryAttach(currentPeriod);

            #region update current period
            currentPeriod.LastLevel = state.Level;
            currentPeriod.Status = currentPeriod.ProposalsCount == 0
                ? PeriodStatus.NoProposals
                : PeriodStatus.NoQuorum;
            #endregion

            #region update proposals status
            if (currentPeriod.ProposalsCount > 0)
            {
                var proposals = await Db.Proposals
                    .Where(x => x.ChainId == state.Id && x.Status == ProposalStatus.Active)
                    .ToListAsync();

                var pendings = Db.ChangeTracker.Entries()
                    .Where(x => x.Entity is Proposal p && p.Status == ProposalStatus.Active)
                    .Select(x => (x.Entity as Proposal)!)
                    .ToList();

                foreach (var pending in pendings)
                    if (!proposals.Any(x => x.Id == pending.Id))
                        proposals.Add(pending);

                foreach (var proposal in proposals)
                    proposal.Status = ProposalStatus.Skipped;
            }
            #endregion

            #region update snapshots
            await Db.Database.ExecuteSqlRawAsync("""
                DELETE FROM "VotingSnapshots"
                WHERE "ChainId" = {0}
                AND "Period" > 58;
                """, state.Id);

            Db.VotingSnapshots.AddRange((await Db.VotingSnapshots
                .AsNoTracking()
                .Where(x => x.ChainId == state.Id && x.Period == 58)
                .ToListAsync())
                .Select(x => new VotingSnapshot
                {
                    ChainId = x.ChainId,
                    BakerId = x.BakerId,
                    Level = x.Level,
                    Period = x.Period + 1,
                    Status = VoterStatus.None,
                    VotingPower = x.VotingPower
                }));
            #endregion

            #region add next period
            await Db.Database.ExecuteSqlRawAsync("""
                DELETE FROM "VotingPeriods"
                WHERE "ChainId" = {0}
                AND "Index" > 58;
                """, state.Id);

            Db.VotingPeriods.Add(new VotingPeriod
            {
                ChainId = state.Id,
                Index = currentPeriod.Index + 1,
                Epoch = currentPeriod.Epoch + 1,
                FirstLevel = currentPeriod.LastLevel + 1,
                LastLevel = currentPeriod.LastLevel + nextProto.BlocksPerVoting,
                Kind = PeriodKind.Proposal,
                Status = PeriodStatus.Active,
                TotalBakers = currentPeriod.TotalBakers,
                TotalVotingPower = currentPeriod.TotalVotingPower,
                UpvotesQuorum = nextProto.ProposalQuorum,
                ProposalsCount = 0,
                TopUpvotes = 0,
                TopVotingPower = 0,
                SingleWinner = false,
            });
            #endregion

            state.VotingPeriod = currentPeriod.Index + 1;
            state.VotingEpoch = currentPeriod.Epoch + 1;
            Cache.Periods.Reset();
        }

        async Task AddInvoice(L1Chain state, string addressHash, long amount)
        {
            var block = await Cache.Blocks.CurrentAsync();
            Db.TryAttach(block);

            var address = (await Cache.Addresses.GetAsync(addressHash, Context.Block))!;
            Db.TryAttach(address);
            Receive(address, amount);
            address.LastLevel = state.Level;
            address.LastTimestamp = state.Timestamp;
            address.MigrationsCount++;

            block.Operations |= L1Operations.Migration;

            var migration = new MichelsonMigrationOperation
            {
                Id = Cache.Chain.NextOperationId(),
                ChainId = block.ChainId,
                Level = block.Level,
                Timestamp = block.Timestamp,
                AddressId = address.Id,
                Kind = MigrationKind.ProposalInvoice,
                BalanceChange = amount
            };
            Db.MigrationOps.Add(migration);
            Context.MigrationOps.Add(migration);

            Db.TryAttach(state);
            state.MigrationOpsCount++;

            var stats = Cache.Statistics.Current;
            Db.TryAttach(stats);
            stats.TotalCreated += amount;
        }

        async Task MigrateCurrentRights(L1Chain state, L1Protocol prevProto, L1Protocol nextProto)
        {
            var cycle = await Db.Cycles.AsNoTracking().FirstAsync(x => x.ChainId == state.Id && x.Index == state.Cycle);
            if (state.Level == cycle.LastLevel) return;

            var bakerCycles = await Cache.BakerCycles.GetAsync(state.Cycle);

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
                bakerCycle.FutureBlockRewards -= prevProto.MaxBakingReward;
            }

            foreach (var ar in rights.Where(x => x.Type == BakingRightType.Attestation))
            {
                var bakerCycle = bakerCycles[ar.BakerId];
                Db.TryAttach(bakerCycle);

                bakerCycle.FutureAttestations -= ar.Slots!.Value;
            }

            await Db.Database.ExecuteSqlRawAsync("""
                DELETE FROM "BakingRights"
                WHERE "ChainId" = {0}
                AND "Level" > {1}
                AND "Cycle" = {2}
                """, state.Id, state.Level, state.Cycle);
                
            #endregion

            #region apply new rights
            var sampler = GetOldSampler(bakerCycles.Values
                .Where(x => x.BakingPower > 0)
                .Select(x => (x.BakerId, x.BakingPower))
                .ToList());

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
            #endregion
        }

        async Task MigrateFutureRights(L1Chain state, L1Protocol nextProto)
        {
            var cycles = await Db.Cycles
                .AsNoTracking()
                .Where(x => x.ChainId == state.Id && x.Index >= state.Cycle)
                .OrderBy(x => x.Index)
                .ToListAsync();

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
                var sampler = GetOldSampler(bakerCycles.Values
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
                    using (var writer = conn.BeginBinaryImport(@"
                        COPY ""BakingRights"" (""ChainId"", ""Cycle"", ""Level"", ""BakerId"", ""Type"", ""Status"", ""Round"", ""Slots"")
                        FROM STDIN (FORMAT BINARY)"))
                    {
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
                    }
                    #endregion
                }
                else
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
                    foreach (var bakerCycle in bakerCycles.Values)
                    {
                        bakerCycle.FutureBlocks = 0;
                        bakerCycle.FutureBlockRewards = 0;
                        bakerCycle.FutureAttestations = 0;
                    }

                    foreach (var br in brs.Where(x => x.Round == 0))
                    {
                        if (!bakerCycles.TryGetValue(br.Baker, out var bakerCycle))
                            throw new Exception("Nonexistent baker cycle");

                        bakerCycle.FutureBlocks++;
                        bakerCycle.FutureBlockRewards += nextProto.MaxBakingReward;
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
                    
                    shifted = ars.Where(x => x.Level == cycle.LastLevel).ToList();
                }
            }
        }

        async Task PatchContracts(L1Chain state)
        {
            var block = await Cache.Blocks.CurrentAsync();
            var patched = File.ReadAllLines("./Protocols/Handlers/Proto15/Activation/patched.contracts");
            foreach (var address in patched)
            {
                if (await Cache.Addresses.GetAsync(address, Context.Block) is L1Contract contract)
                {
                    Db.TryAttach(contract);

                    var oldScript = await Db.Scripts
                        .OfType<MichelsonScript>()
                        .FirstAsync(x => x.ContractId == contract.Id && x.Current);
                    var oldStorage = await Cache.Storages.GetAsync(contract);

                    var rawContract = await Proto.Rpc.GetContractAsync(state.Level, contract.Hash);

                    var code = (rawContract.Required("script").RequiredMicheline("code") as MichelineArray)!;
                    var micheParameter = code.First(x => x is MichelinePrim p && p.Prim == PrimType.parameter).ToBytes();
                    var micheStorage = code.First(x => x is MichelinePrim p && p.Prim == PrimType.storage).ToBytes();
                    var micheCode = code.First(x => x is MichelinePrim p && p.Prim == PrimType.code).ToBytes();
                    var micheViews = code.Where(x => x is MichelinePrim p && p.Prim == PrimType.view);

                    var newSchema = new ContractScript(code);
                    var newStorageValue = rawContract.Required("script").RequiredMicheline("storage");
                    var newRawStorageValue = newSchema.OptimizeStorage(newStorageValue, false).ToBytes();

                    if (oldScript.ParameterSchema.IsEqual(micheParameter) &&
                        oldScript.StorageSchema.IsEqual(micheStorage) &&
                        oldScript.CodeSchema.IsEqual(micheCode) &&
                        oldStorage.RawValue.IsEqual(newRawStorageValue))
                        continue;

                    Db.TryAttach(oldScript);
                    oldScript.Current = false;

                    Db.TryAttach(oldStorage);
                    oldStorage.Current = false;

                    var migration = new MichelsonMigrationOperation
                    {
                        Id = Cache.Chain.NextOperationId(),
                        ChainId = block.ChainId,
                        Level = block.Level,
                        Timestamp = block.Timestamp,
                        AddressId = contract.Id,
                        Kind = MigrationKind.CodeChange
                    };
                    var newScript = new MichelsonScript
                    {
                        Id = Cache.Chain.NextScriptId(),
                        ChainId = contract.ChainId,
                        Level = migration.Level,
                        ContractId = contract.Id,
                        MigrationId = migration.Id,
                        ParameterSchema = micheParameter,
                        StorageSchema = micheStorage,
                        CodeSchema = micheCode,
                        Views = micheViews.Any()
                            ? micheViews.Select(x => x.ToBytes()).ToArray()
                            : null,
                        Current = true
                    };
                    var newStorage = new Storage
                    {
                        Id = Cache.Chain.NextStorageId(),
                        ChainId = contract.ChainId,
                        Level = migration.Level,
                        ContractId = contract.Id,
                        MigrationId = migration.Id,
                        RawValue = newRawStorageValue,
                        JsonValue = Regexes.RestrictedUnicode().Replace(newScript.Schema.HumanizeStorage(newStorageValue), Regexes.NullEscapeString),
                        Current = true
                    };

                    var viewsBytes = newScript.Views?
                        .OrderBy(x => x, BytesComparer.Instance)
                        .SelectMany(x => x)
                        .ToArray()
                        ?? [];
                    var typeSchema = newScript.ParameterSchema.Concat(newScript.StorageSchema).Concat(viewsBytes);
                    var fullSchema = typeSchema.Concat(newScript.CodeSchema);
                    contract.TypeHash = newScript.TypeHash = MichelsonScript.GetHash(typeSchema);
                    contract.CodeHash = newScript.CodeHash = MichelsonScript.GetHash(fullSchema);

                    migration.ScriptId = newScript.Id;
                    migration.StorageId = newStorage.Id;

                    contract.MigrationsCount++;
                    contract.LastLevel = migration.Level;
                    contract.LastTimestamp = migration.Timestamp;

                    state.MigrationOpsCount++;

                    Db.MigrationOps.Add(migration);
                    Context.MigrationOps.Add(migration);

                    Db.Scripts.Add(newScript);
                    Cache.Schemas.Add(contract, newScript.Schema);

                    Db.Storages.Add(newStorage);
                    Cache.Storages.Add(contract, newStorage);
                }
            }
        }

        Sampler GetOldSampler(IEnumerable<(int id, long stake)> selection)
        {
            var sorted = selection
                .OrderByDescending(x => x.stake)
                .ThenByDescending(x =>
                {
                    var baker = Cache.Addresses.GetBaker(x.id);
                    return new byte[] { (byte)baker.PublicKey![0] }.Concat(Base58.Parse(baker.Hash));
                }, BytesComparer.Instance);

            return new Sampler(sorted.Select(x => x.id).ToArray(), sorted.Select(x => x.stake).ToArray());
        }
    }
}
