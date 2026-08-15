using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Linq;
using Xtzkt.Data.Models;
using Xtzkt.Indexers.Common.Extensions;

namespace Xtzkt.Indexers.L1.Protocols.Proto04
{
    class ProtoActivator(ProtocolHandler proto) : Proto03.ProtoActivator(proto)
    {
        protected override void SetParameters(L1Protocol protocol, JToken parameters)
        {
            base.SetParameters(protocol, parameters);
            protocol.HardBlockGasLimit = parameters["hard_gas_limit_per_block"]?.Value<int>() ?? 8_000_000;
            protocol.HardOperationGasLimit = parameters["hard_gas_limit_per_operation"]?.Value<int>() ?? 800_000;
            protocol.MinimalStake = parameters["tokens_per_roll"]?.Value<long>() ?? 8_000_000_000;
        }

        protected override void UpgradeParameters(L1Protocol protocol, L1Protocol prev)
        {
            protocol.HardBlockGasLimit = 8_000_000;
            protocol.HardOperationGasLimit = 800_000;
            protocol.MinimalStake = 8_000_000_000;
        }

        // Proposal invoice

        protected override async Task MigrateContext(L1Chain state)
        {
            var block = await Cache.Blocks.CurrentAsync();
            Db.TryAttach(block);

            UpdateBakersPower();

            var address = (await Cache.Addresses.GetAsync("tz1iSQEcaGpUn6EW5uAy3XhPiNg7BHMnRSXi", Context.Block))!;
            Db.TryAttach(address);
            Receive(address, 100_000_000);
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
