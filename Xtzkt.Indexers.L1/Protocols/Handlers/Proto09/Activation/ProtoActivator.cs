using Microsoft.EntityFrameworkCore;
using Xtzkt.Data.Models;
using Xtzkt.Indexers.Common.Extensions;

namespace Xtzkt.Indexers.L1.Protocols.Proto09
{
    class ProtoActivator(ProtocolHandler proto) : Proto08.ProtoActivator(proto)
    {
        // Proposal invoice

        protected override async Task MigrateContext(L1Chain state)
        {
            var block = await Cache.Blocks.CurrentAsync();
            Db.TryAttach(block);

            var address = (await Cache.Addresses.GetAsync("tz1abmz7jiCV2GH2u81LRrGgAFFgvQgiDiaf", Context.Block))!;
            Db.TryAttach(address);
            Receive(address, 100_000_000);
            address.MigrationsCount++;
            address.LastLevel = block.Level;
            address.LastTimestamp = block.Timestamp;

            block.Operations |= L1Operations.Migration;

            var migration = new MichelsonMigrationOperation
            {
                Id = Cache.Chain.NextOperationId(),
                ChainId = block.ChainId,
                Level = block.Level,
                Timestamp = block.Timestamp,
                AddressId = address.Id,
                Kind = MigrationKind.ProposalInvoice,
                BalanceChange = 100_000_000
            };
            Db.MigrationOps.Add(migration);
            Context.MigrationOps.Add(migration);

            Db.TryAttach(state);
            state.MigrationOpsCount++;

            var stats = Cache.Statistics.Current;
            Db.TryAttach(stats);
            stats.TotalCreated += 100_000_000;
        }

        protected override async Task RevertContext(L1Chain state)
        {
            var block = await Cache.Blocks.CurrentAsync();

            var invoice = await Db.MigrationOps
                .AsNoTracking()
                .OfType<MichelsonMigrationOperation>()
                .FirstAsync(x => x.ChainId == block.ChainId && x.Level == block.Level && x.Kind == MigrationKind.ProposalInvoice);

            var address = await Cache.Addresses.GetAsync(invoice.AddressId);
            Db.TryAttach(address);
            RevertReceive(address, 100_000_000);
            address.MigrationsCount--;

            Db.MigrationOps.Remove(invoice);
            Cache.Chain.ReleaseOperationId();

            state.MigrationOpsCount--;
        }
    }
}
