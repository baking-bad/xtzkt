using Microsoft.EntityFrameworkCore;
using Netezos.Contracts;
using Netezos.Encoding;
using Newtonsoft.Json.Linq;
using Xtzkt.Data.Models;
using Xtzkt.Indexers.Common.Extensions;
using Xtzkt.Indexers.Common.Utils;
using Xtzkt.Utils;

namespace Xtzkt.Indexers.L1.Protocols.Proto13
{
    partial class ProtoActivator(ProtocolHandler proto) : Proto12.ProtoActivator(proto)
    {
        protected override void SetParameters(L1Protocol protocol, JToken parameters)
        {
            base.SetParameters(protocol, parameters);
            protocol.LBToggleThreshold = parameters["liquidity_baking_toggle_ema_threshold"]?.Value<int>() ?? 1_000_000_000;
            protocol.BlocksPerVoting = (parameters["cycles_per_voting_period"]?.Value<int>() ?? 5) * protocol.BlocksPerCycle;
        }

        protected override void UpgradeParameters(L1Protocol protocol, L1Protocol prev)
        {
            protocol.LBToggleThreshold = 1_000_000_000;
        }

        protected override Sampler GetSampler(IEnumerable<(int id, long stake)> selection)
        {
            var sorted = selection.OrderByDescending(x =>
                Base58.Parse(Cache.Addresses.GetBaker(x.id).Hash), BytesComparer.Instance);

            return new Sampler(sorted.Select(x => x.id).ToArray(), sorted.Select(x => x.stake).ToArray());
        }

        protected override async Task MigrateContext(L1Chain state)
        {
            var block = await Cache.Blocks.CurrentAsync();
            var nextProto = await Cache.Protocols.GetAsync(state.NextProtocol);

            #region voting power
            UpdateBakersPower();
            #endregion

            #region voting snapshots
            await Db.Database.ExecuteSqlRawAsync("""
                DELETE FROM "VotingSnapshots"
                WHERE "ChainId" = {0}
                AND "Period" = {1}
                """, state.Id, state.VotingPeriod);

            var snapshots = Cache.Addresses.GetBakers()
                .Where(x => x.VotingPower != 0)
                .Select(x => new VotingSnapshot
                {
                    ChainId = state.Id,
                    Level = state.Level,
                    Period = state.VotingPeriod,
                    BakerId = x.Id,
                    VotingPower = x.VotingPower,
                    Status = VoterStatus.None
                });

            var period = await Cache.Periods.GetAsync(state.VotingPeriod);
            Db.TryAttach(period);

            period.TotalBakers = snapshots.Count();
            period.TotalVotingPower = snapshots.Sum(x => x.VotingPower);

            Db.VotingSnapshots.AddRange(snapshots);
            #endregion

            #region patch contracts
            Db.TryAttach(block);
            Db.TryAttach(state);

            var patched = File.ReadAllLines("./Protocols/Handlers/Proto13/Activation/patched.contracts");
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
                    
                    block.Operations |= L1Operations.Migration;

                    Db.MigrationOps.Add(migration);
                    Context.MigrationOps.Add(migration);

                    Db.Scripts.Add(newScript);
                    Cache.Schemas.Add(contract, newScript.Schema);

                    Db.Storages.Add(newStorage);
                    Cache.Storages.Add(contract, newStorage);
                }
            }
            #endregion

            #region empty contracts
            // Address emptying has been significatnly changed, so  that its behavoir is completely incompatible with previous protocols.
            // Instead of adding a lot of code crutches to support both the new and old behavior, we just use the new one for all protocols
            // and simply patch the address broken in previous protocols.
            if (state.ChainId == "NetXdQprcVkpaWU") // mainnet
            {
                var emptied = File.ReadAllLines("./Protocols/Handlers/Proto13/Activation/emptied.contracts");
                foreach (var address in emptied)
                {
                    if (await Cache.Addresses.GetAsync(address, Context.Block) is L1User user)
                    {
                        Db.TryAttach(user);
                        var rawUser = await Proto.Rpc.GetContractAsync(state.Level, user.Hash);
                        user.Counter = rawUser.RequiredInt32("counter");
                        user.Revealed = false;
                    }
                }
            }
            #endregion
        }

        protected override Task RevertContext(L1Chain state) => throw new NotImplementedException();
    }
}
