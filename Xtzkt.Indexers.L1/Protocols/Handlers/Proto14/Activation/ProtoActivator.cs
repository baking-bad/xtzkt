using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Linq;
using Xtzkt.Data.Models;
using Xtzkt.Indexers.Common.Extensions;

namespace Xtzkt.Indexers.L1.Protocols.Proto14
{
    partial class ProtoActivator : Proto13.ProtoActivator
    {
        public ProtoActivator(ProtocolHandler proto) : base(proto) { }

        protected override void SetParameters(L1Protocol protocol, JToken parameters)
        {
            base.SetParameters(protocol, parameters);
            protocol.Dictator = parameters["testnet_dictator"]?.Value<string>();
        }

        protected override void UpgradeParameters(L1Protocol protocol, L1Protocol prev)
        {
            if (Cache.Chain.GetChainId() == "NetXnHfVqm9iesp") // ghostnet
            {
                protocol.BlocksPerVoting = prev.BlocksPerCycle;
                protocol.Dictator = "tz1Xf8zdT3DbAX9cHw3c3CXh79rc4nK4gCe8"; // oxhead_testnet_baker
            }
        }

        protected override async Task MigrateContext(L1Chain state)
        {
            var block = await Cache.Blocks.CurrentAsync();
            Db.TryAttach(block);

            var address = (await Cache.Addresses.GetAsync("tz1X81bCXPtMiHu1d4UZF4GPhMPkvkp56ssb", Context.Block))!;
            Db.TryAttach(address);
            Receive(address, 3_000_000_000L);
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
                BalanceChange = 3_000_000_000L
            };
            Db.MigrationOps.Add(migration);
            Context.MigrationOps.Add(migration);

            Db.TryAttach(state);
            state.MigrationOpsCount++;

            var stats = Cache.Statistics.Current;
            Db.TryAttach(stats);
            stats.TotalCreated += 3_000_000_000L;

            if (state.ChainId == "NetXnHfVqm9iesp") // ghostnet
            {
                var votingPeriod = await Cache.Periods.GetAsync(58);
                Db.TryAttach(votingPeriod);
                votingPeriod.LastLevel = 0;
                state.VotingPeriod = 58;
                state.VotingEpoch = 58;
            }
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

            RevertReceive(address, invoice.BalanceChange);
            address.MigrationsCount--;

            Db.MigrationOps.Remove(invoice);
            Cache.Chain.ReleaseOperationId();

            state.MigrationOpsCount--;
        }
    }
}
