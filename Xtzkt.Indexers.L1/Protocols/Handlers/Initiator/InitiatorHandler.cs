using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using App.Metrics;
using Xtzkt.Data;
using Xtzkt.Data.Models;
using Xtzkt.Indexers.Common.Extensions;
using Xtzkt.Indexers.Common.Services;
using Xtzkt.Indexers.L1.Services;
using Xtzkt.Indexers.L1.Protocols.Initiator;
using Xtzkt.Data.Utils;

namespace Xtzkt.Indexers.L1.Protocols
{
    class InitiatorHandler : ProtocolHandler
    {
        public override IDiagnostics Diagnostics { get; }
        public override IHelpers Helpers { get; }
        public override IValidator Validator { get; }
        public override IRpc Rpc { get; }
        public override string VersionName => "genesis";
        public override int VersionNumber => 0;

        public InitiatorHandler(TezosNode node, XtzktContext db, CacheService cache, QuotesService quotes, IServiceProvider services, IConfiguration config, ILogger<InitiatorHandler> logger, IMetrics metrics)
            : base(node, db, cache, quotes, services, config, logger, metrics)
        {
            Diagnostics = new Diagnostics();
            Helpers = new Helpers();
            Validator = new Validator(this);
            Rpc = new Rpc(node);
        }

        public override Task Activate(L1Chain state, JsonElement block) => Task.CompletedTask;
        public override Task Deactivate(L1Chain state) => Task.CompletedTask;

        public override Task WarmUpCache(JsonElement block) => Task.CompletedTask;

        public override Task Commit(JsonElement rawBlock)
        {
            #region add protocol
            var chain = Cache.Chain.Get();
            var protocol = new L1Protocol
            {
                Id = Cache.Chain.NextProtocolId(),
                ChainId = chain.Id,
                Hash = rawBlock.RequiredString("protocol"),
                Version = VersionNumber,
                FirstLevel = 1,
                LastLevel = 1,
                FirstCycle = 0,
                FirstCycleLevel = 1
            };
            Db.Protocols.Add(protocol);
            Cache.Protocols.Add(protocol);
            Context.Protocol = protocol;
            #endregion

            #region add block
            var hash = rawBlock.RequiredString("hash");
            var timestamp = rawBlock.Required("header").RequiredDateTime("timestamp");
            var block = new L1Block
            {
                Id = Cache.Chain.NextOperationId(),
                ChainId = chain.Id,
                Hash = Hashes.ParseMichelsonBlockHash(hash),
                Cycle = 0,
                Level = rawBlock.Required("header").RequiredInt32("level"),
                ProtocolId = protocol.Id,
                Timestamp = timestamp,
                Events = L1BlockEvents.CycleBegin
                    | L1BlockEvents.ProtocolBegin
                    | L1BlockEvents.ProtocolEnd
                    | L1BlockEvents.BalanceSnapshot
            };
            Db.Blocks.Add(block);
            Cache.Blocks.Add(block);
            Context.Block = block;
            #endregion

            #region add empty stats
            var stats = new L1Statistics
            {
                ChainId = block.ChainId,
                Level = block.Level,
                Timestamp = block.Timestamp,
            };
            Db.Statistics.Add(stats);
            Cache.Statistics.SetCurrent(stats);
            #endregion

            #region update state
            chain.Cycle = 0;
            chain.Level = block.Level;
            chain.Timestamp = block.Timestamp;
            chain.Protocol = protocol.Hash;
            chain.NextProtocol = rawBlock.Required("metadata").RequiredString("next_protocol");
            chain.Hash = hash;
            chain.BlocksCount++;
            chain.VotingEpoch = 0;
            chain.VotingPeriod = 0;
            #endregion

            return Task.CompletedTask;
        }

        public override async Task Revert()
        {
            var chain = Cache.Chain.Get();

            var curr = Cache.Blocks.Current();
            var currProtocol = await Cache.Protocols.GetAsync(curr.ProtocolId);

            var prev = await Cache.Blocks.PreviousAsync();
            var prevProtocol = await Cache.Protocols.GetAsync(prev.ProtocolId);

            await Db.Database.ExecuteSqlRawAsync("""
                DELETE FROM "Statistics" WHERE "ChainId" = {0} AND "Level" = {1};
                DELETE FROM "Protocols" WHERE "ChainId" = {0} AND "FirstLevel" = {1};
                DELETE FROM "Blocks" WHERE "ChainId" = {0} AND "Level" = {1};
                """, chain.Id, curr.Level);

            await Cache.Statistics.ResetAsync();
            await Cache.Protocols.ResetAsync();
            Cache.Blocks.Reset();

            #region update state
            chain.Cycle = -1;
            chain.Level = prev.Level;
            chain.Timestamp = prev.Timestamp;
            chain.Protocol = prevProtocol.Hash;
            chain.NextProtocol = currProtocol.Hash;
            chain.Hash = Hashes.FormatMichelsonBlockHash(prev.Hash);
            chain.BlocksCount--;

            Cache.Chain.ReleaseOperationId();
            Cache.Chain.ReleaseProtocolId();
            #endregion
        }
    }
}
