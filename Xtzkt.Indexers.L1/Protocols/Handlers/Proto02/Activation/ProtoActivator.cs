using Microsoft.EntityFrameworkCore;
using Xtzkt.Data.Models;
using Xtzkt.Data.Models.Operations.Abstract;
using Xtzkt.Indexers.Common.Extensions;

namespace Xtzkt.Indexers.L1.Protocols.Proto02
{
    class ProtoActivator(ProtocolHandler proto) : Proto01.ProtoActivator(proto)
    {
        protected override async Task MigrateContext(L1Chain state)
        {
            var block = await Cache.Blocks.CurrentAsync();
            var protocol = await Cache.Protocols.GetAsync(block.ProtocolId);
            
            var weirdBakers = (await Db.OriginationOps
                .AsNoTracking()
                .OfType<L1OriginationOperation>()
                .Join(Db.Addresses, x => x.BakerId, x => x.Id, (op, baker) => new { op, baker = (baker as L1User)! })
                .Join(Db.Addresses, x => x.op.ContractId, x => x.Id, (opDelegat, contract) => new { opDelegat.op, opDelegat.baker, contract = (contract as L1Address)! })
                .Where(x =>
                    x.op.ChainId == block.ChainId &&
                    x.op.Status == OperationStatus.Applied &&
                    x.op.BakerId != null &&
                    x.baker.Type != AddressType.L1Baker &&
                    x.baker.Balance > 0 &&
                    x.contract.BakerId == null)
                .Select(x => new
                {
                    Contract = x.contract,
                    WeirdBaker = x.baker
                })
                .ToListAsync())
                .GroupBy(x => x.WeirdBaker.Id);

            var activatedBakers = new Dictionary<int, L1Baker>(weirdBakers.Count());

            Db.TryAttach(block);
            Db.TryAttach(state);

            foreach (var weirds in weirdBakers)
            {
                var baker = RegisterBaker(weirds.First().WeirdBaker, protocol);
                activatedBakers.Add(baker.Id, baker);

                baker.MigrationsCount++;
                baker.LastLevel = block.Level;
                baker.LastTimestamp = block.Timestamp;

                block.Operations |= L1Operations.Migration;

                var migration = new MichelsonMigrationOperation
                {
                    Id = Cache.Chain.NextOperationId(),
                    ChainId = block.ChainId,
                    Level = block.Level,
                    Timestamp = block.Timestamp,
                    AddressId = baker.Id,
                    Kind = MigrationKind.ActivateBaker
                };
                Db.MigrationOps.Add(migration);
                Context.MigrationOps.Add(migration);
                
                state.MigrationOpsCount++;

                foreach (var weird in weirds)
                {
                    var delegator = weird.Contract;
                    if (delegator.BakerId != null)
                        throw new Exception("migration error");

                    Db.TryAttach(delegator);
                    Cache.Addresses.Add(delegator);

                    Delegate(delegator, baker, delegator.FirstLevel, delegator.FirstTimestamp);
                }
            }
        }

        protected override async Task RevertContext(L1Chain state)
        {
            var block = await Cache.Blocks.CurrentAsync();

            var bakers = await Db.Addresses
                .AsNoTracking()
                .Where(x => x.ChainId == state.Id && x.Type == AddressType.L1Baker)
                .GroupJoin(Db.Addresses, x => x.Id, x => (x as L1Address)!.BakerId, (baker, delegators) => new
                {
                    baker = (baker as L1Baker)!,
                    delegators = delegators as IEnumerable<L1Address> ?? Enumerable.Empty<L1Address>()
                })
                .Where(x => x.baker.ActivationLevel == block.Level)
                .ToListAsync();

            foreach (var row in bakers)
            {
                foreach (var delegator in row.delegators)
                {
                    Db.TryAttach(delegator);
                    Cache.Addresses.Add(delegator);

                    Undelegate(delegator, row.baker);
                }

                if (row.baker.ExternalDelegatedBalance != 0 || row.baker.DelegatorsCount > 0)
                    throw new Exception("migration error");

                var user = UnregisterBaker(row.baker);
                user.MigrationsCount--;
            }

            var migrationOps = await Db.MigrationOps
                .AsNoTracking()
                .OfType<MichelsonMigrationOperation>()
                .Where(x => x.ChainId == block.ChainId && x.Level == state.Level && x.Kind == MigrationKind.ActivateBaker)
                .ToListAsync();

            Db.MigrationOps.RemoveRange(migrationOps);
            Cache.Chain.ReleaseOperationId(migrationOps.Count);

            state.MigrationOpsCount -= migrationOps.Count;
        }
    }
}
