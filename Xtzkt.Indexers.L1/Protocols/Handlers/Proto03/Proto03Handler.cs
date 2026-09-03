using System.Text.Json;
using App.Metrics;
using Xtzkt.Data;
using Xtzkt.Data.Models;
using Xtzkt.Indexers.Common.Extensions;
using Xtzkt.Indexers.Common.Services;
using Xtzkt.Indexers.L1.Services;
using Xtzkt.Indexers.L1.Protocols.Proto03;

namespace Xtzkt.Indexers.L1.Protocols
{
    class Proto03Handler : ProtocolHandler
    {
        public override IDiagnostics Diagnostics { get; }
        public override IHelpers Helpers { get; }
        public override IValidator Validator { get; }
        public override IRpc Rpc { get; }
        public override string VersionName => "alpha_003";
        public override int VersionNumber => 3;

        public Proto03Handler(TezosNode node, XtzktContext db, CacheService cache, QuotesService quotes, IServiceProvider services, IConfiguration config, ILogger<Proto03Handler> logger, IMetrics metrics)
            : base(node, db, cache, quotes, services, config, logger, metrics)
        {
            Rpc = new Rpc(node);
            Diagnostics = new Diagnostics(this);
            Helpers = new Helpers(this);
            Validator = new Validator(this);
        }

        public override Task Activate(L1Chain state, JsonElement block) => new ProtoActivator(this).Activate(state, block);
        public override Task Deactivate(L1Chain state) => new ProtoActivator(this).Deactivate(state);

        public override async Task Commit(JsonElement block)
        {
            await new StatisticsCommit(this).Apply(block);

            var blockCommit = new BlockCommit(this);
            await blockCommit.Apply(block);

            new FreezerCommit(this).Apply(blockCommit.Block, block);
            await new RevelationPenaltyCommit(this).Apply(blockCommit.Block, block);
            await new DeactivationCommit(this).Apply(blockCommit.Block, block);

            var operations = block.RequiredArray("operations", 4);

            #region operations 0
            foreach (var operation in operations[0].EnumerateArray())
            {
                var opHash = operation.RequiredMichelsonOperationHashBytes("hash");
                foreach (var content in operation.RequiredArray("contents", 1).EnumerateArray())
                {
                    switch (content.RequiredString("kind"))
                    {
                        case "endorsement":
                            await new AttestationsCommit(this).Apply(blockCommit.Block, opHash, content);
                            break;
                        default:
                            throw new NotImplementedException($"'{content.RequiredString("kind")}' is not allowed in operations[0]");
                    }
                }
            }
            #endregion

            #region operations 1
            foreach (var operation in operations[1].EnumerateArray())
            {
                var opHash = operation.RequiredMichelsonOperationHashBytes("hash");
                foreach (var content in operation.RequiredArray("contents", 1).EnumerateArray())
                {
                    switch (content.RequiredString("kind"))
                    {
                        case "proposals":
                            await new ProposalsCommit(this).Apply(blockCommit.Block, opHash, content);
                            break;
                        case "ballot":
                            await new BallotsCommit(this).Apply(blockCommit.Block, opHash, content);
                            break;
                        default:
                            throw new NotImplementedException($"'{content.RequiredString("kind")}' is not allowed in operations[1]");
                    }
                }
            }
            #endregion

            #region operations 2
            foreach (var operation in operations[2].EnumerateArray())
            {
                var opHash = operation.RequiredMichelsonOperationHashBytes("hash");
                foreach (var content in operation.RequiredArray("contents", 1).EnumerateArray())
                {
                    switch (content.RequiredString("kind"))
                    {
                        case "activate_account":
                            await new ActivationsCommit(this).Apply(blockCommit.Block, opHash, content);
                            break;
                        case "double_baking_evidence":
                            await new DoubleBakingCommit(this).Apply(blockCommit.Block, opHash, content);
                            break;
                        case "seed_nonce_revelation":
                            await new NonceRevelationsCommit(this).Apply(blockCommit.Block, opHash, content);
                            break;
                        default:
                            throw new NotImplementedException($"'{content.RequiredString("kind")}' is not allowed in operations[2]");
                    }
                }
            }
            #endregion

            var bigMapCommit = new BigMapCommit(this);

            #region operations 3
            foreach (var operation in operations[3].EnumerateArray())
            {
                var opHash = operation.RequiredMichelsonOperationHashBytes("hash");
                Manager.Init(operation);
                foreach (var content in operation.RequiredArray("contents").EnumerateArray())
                {
                    switch (content.RequiredString("kind"))
                    {
                        case "reveal":
                            await new RevealsCommit(this).Apply(blockCommit.Block, opHash, content);
                            break;
                        case "delegation":
                            await new DelegationsCommit(this).Apply(blockCommit.Block, opHash, content);
                            break;
                        case "origination":
                            var orig = new OriginationsCommit(this);
                            await orig.Apply(blockCommit.Block, opHash, content);
                            if (orig.BigMapDiffs != null)
                                bigMapCommit.Append(orig.Origination, orig.Contract!, orig.BigMapDiffs);
                            break;
                        case "transaction":
                            var parent = new TransactionsCommit(this);
                            await parent.Apply(blockCommit.Block, opHash, content);
                            if (parent.BigMapDiffs != null)
                                bigMapCommit.Append(parent.Transaction, (parent.Target as L1Contract)!, parent.BigMapDiffs);

                            if (content.Required("metadata").TryGetProperty("internal_operation_results", out var internalResult))
                            {
                                foreach (var internalContent in internalResult.EnumerateArray())
                                {
                                    switch (internalContent.RequiredString("kind"))
                                    {
                                        case "transaction":
                                            var internalTx = new TransactionsCommit(this);
                                            await internalTx.ApplyInternal(blockCommit.Block, parent.Transaction, internalContent);
                                            if (internalTx.BigMapDiffs != null)
                                                bigMapCommit.Append(internalTx.Transaction, (internalTx.Target as L1Contract)!, internalTx.BigMapDiffs);
                                            break;
                                        default:
                                            throw new NotImplementedException($"internal '{internalContent.RequiredString("kind")}' is not implemented");
                                    }
                                }
                            }
                            break;
                        default:
                            throw new NotImplementedException($"'{content.RequiredString("kind")}' is not expected in operations[3]");
                    }
                }
                Manager.Apply();
            }
            #endregion

            await bigMapCommit.Apply();

            var brCommit = new BakingRightsCommit(this);
            await brCommit.Apply(blockCommit.Block);

            var cycleCommit = new CycleCommit(this);
            await cycleCommit.Apply(blockCommit.Block);

            await new DelegatorCycleCommit(this).Apply(blockCommit.Block, cycleCommit.FutureCycle);

            await new BakerCycleCommit(this).Apply(
                blockCommit.Block,
                cycleCommit.FutureCycle,
                brCommit.FutureBakingRights,
                brCommit.FutureAttestationRights,
                cycleCommit.BakerSnapshots,
                brCommit.CurrentRights);

            await new VotingCommit(this).Apply(blockCommit.Block, block);
            await new StateCommit(this).Apply(blockCommit.Block, block);
        }

        public override async Task AfterCommit(JsonElement rawBlock)
        {
            var block = await Cache.Blocks.CurrentAsync();
            await new SnapshotBalanceCommit(this).Apply(rawBlock, block);
        }

        public override async Task BeforeRevert()
        {
            var block = await Cache.Blocks.CurrentAsync();
            await new SnapshotBalanceCommit(this).Revert(block);
        }

        public override async Task Revert()
        {
            var currBlock = await Cache.Blocks.CurrentAsync();
            Db.TryAttach(currBlock);

            await new VotingCommit(this).Revert(currBlock);
            await new StatisticsCommit(this).Revert(currBlock);

            await new BakerCycleCommit(this).Revert(currBlock);
            await new DelegatorCycleCommit(this).Revert(currBlock);
            await new CycleCommit(this).Revert(currBlock);
            await new BakingRightsCommit(this).Revert(currBlock);
            await new BigMapCommit(this).Revert(currBlock);

            foreach (var operation in Context.EnumerateOps().OrderByDescending(x => x.Id).ToList())
            {
                switch (operation)
                {
                    case AttestationOperation op:
                        await new AttestationsCommit(this).Revert(currBlock, op);
                        break;
                    case ProposalOperation op:
                        await new ProposalsCommit(this).Revert(currBlock, op);
                        break;
                    case BallotOperation op:
                        await new BallotsCommit(this).Revert(currBlock, op);
                        break;
                    case ActivationOperation op:
                        await new ActivationsCommit(this).Revert(currBlock, op);
                        break;
                    case DoubleBakingOperation op:
                        await new DoubleBakingCommit(this).Revert(currBlock, op);
                        break;
                    case NonceRevelationOperation op:
                        await new NonceRevelationsCommit(this).Revert(currBlock, op);
                        break;
                    case L1RevealOperation op:
                        await new RevealsCommit(this).Revert(currBlock, op);
                        break;
                    case DelegationOperation op:
                        await new DelegationsCommit(this).Revert(currBlock, op);
                        break;
                    case L1OriginationOperation op:
                        await new OriginationsCommit(this).Revert(currBlock, op);
                        break;
                    case L1TransactionOperation op:
                        if (op.InitiatorId == null)
                            await new TransactionsCommit(this).Revert(currBlock, op);
                        else
                            await new TransactionsCommit(this).RevertInternal(currBlock, op);
                        break;
                    default:
                        throw new NotImplementedException($"'{operation.GetType()}' is not implemented");
                }
            }

            await new DeactivationCommit(this).Revert(currBlock);
            await new RevelationPenaltyCommit(this).Revert(currBlock);
            await new FreezerCommit(this).Revert(currBlock);
            await new BlockCommit(this).Revert(currBlock);

            await new StateCommit(this).Revert(currBlock);
        }
    }
}
