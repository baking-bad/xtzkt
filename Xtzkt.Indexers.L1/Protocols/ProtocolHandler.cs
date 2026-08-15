using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using App.Metrics;
using Xtzkt.Data;
using Xtzkt.Data.Models;
using Xtzkt.Indexers.Common.Extensions;
using Xtzkt.Indexers.Common.Services;
using Xtzkt.Indexers.Common.Utils;
using Xtzkt.Indexers.L1.Protocols;
using Xtzkt.Indexers.L1.Services;

namespace Xtzkt.Indexers.L1
{
    public abstract class ProtocolHandler
    {
        public abstract IDiagnostics Diagnostics { get; }
        public abstract IHelpers Helpers { get; }
        public abstract IValidator Validator { get; }
        public abstract IRpc Rpc { get; }
        public abstract string VersionName { get; }
        public abstract int VersionNumber { get; }

        public readonly TezosNode Node;
        public readonly XtzktContext Db;
        public readonly CacheService Cache;
        public readonly QuotesService Quotes;
        public readonly IServiceProvider Services;
        public readonly TezosProtocolsConfig Config;
        public readonly ILogger Logger;
        public readonly IMetrics Metrics;
        public readonly ManagerContext Manager;
        public readonly InboxContext Inbox;
        public BlockContext Context { get; private set; }

        bool _ForceDiagnostics = false;

        public ProtocolHandler(TezosNode node, XtzktContext db, CacheService cache, QuotesService quotes, IServiceProvider services, IConfiguration config, ILogger logger, IMetrics metrics)
        {
            Node = node;
            Db = db;
            Cache = cache;
            Quotes = quotes;
            Services = services;
            Config = config.GetTezosProtocolsConfig();
            Logger = logger;
            Metrics = metrics;
            Manager = new(this);
            Inbox = new();
            Context = new();
        }

        public ProtocolHandler WithContext(BlockContext context)
        {
            Context = context;
            return this;
        }

        public virtual async Task<L1Chain> CommitNextBlock()
        {
            var state = Cache.Chain.Get();
            Db.TryAttach(state);

            JsonElement block;
            Logger.LogDebug("Load block {level}", state.Level + 1);
            using (Metrics.Measure.Timer.Time(MetricsRegistry.RpcResponseTime))
            {
                block = await Rpc.GetBlockAsync(state.Level + 1);
            }

            Logger.LogDebug("Begin DB transaction");
            using var tx = await Db.Database.BeginTransactionAsync();
            try
            {
                Logger.LogDebug("Warm up cache");
                using (Metrics.Measure.Timer.Time(MetricsRegistry.CacheWarmUpTime))
                {
                    await WarmUpCache(block);
                }

                if (Config.Validation)
                {
                    Logger.LogDebug("Validate block");
                    using (Metrics.Measure.Timer.Time(MetricsRegistry.ValidationTime))
                    {
                        await Validator.ValidateBlock(block);
                    }
                }

                Logger.LogDebug("Process block");
                using (Metrics.Measure.Timer.Time(MetricsRegistry.ProcessingTime))
                {
                    await Commit(block);
                }

                Logger.LogDebug("Touch addresses");
                TouchAddresses();

                var nextProtocol = this;
                if (state.Protocol != state.NextProtocol)
                    nextProtocol = Services.GetProtocolHandler(state.Level + 1, state.NextProtocol).WithContext(Context);

                Logger.LogDebug("Save changes");
                using (Metrics.Measure.Timer.Time(MetricsRegistry.SaveChangesTime))
                {
                    if (Config.Diagnostics || _ForceDiagnostics)
                        nextProtocol.Diagnostics.TrackChanges();
                    Context.Apply(Db);
                    await Db.SaveChangesAsync();
                }

                Logger.LogDebug("Save post-changes");
                using (Metrics.Measure.Timer.Time(MetricsRegistry.PostProcessingTime))
                {
                    await AfterCommit(block);
                    if (Config.Diagnostics || _ForceDiagnostics)
                        nextProtocol.Diagnostics.TrackChanges();
                    await Db.SaveChangesAsync();
                }

                Logger.LogDebug("Process quotes");
                using (Metrics.Measure.Timer.Time(MetricsRegistry.QuotesProcessingTime))
                {
                    await Quotes.Commit();
                }

                if (state.Protocol != state.NextProtocol)
                {
                    Logger.LogDebug("Activate protocol {hash}", state.NextProtocol);
                    await nextProtocol.Activate(state, block);
                    if (Config.Diagnostics || _ForceDiagnostics)
                        nextProtocol.Diagnostics.TrackChanges();
                    await Db.SaveChangesAsync();
                }

                if (Config.Diagnostics || _ForceDiagnostics)
                {
                    Logger.LogDebug("Diagnostics");
                    using (Metrics.Measure.Timer.Time(MetricsRegistry.DiagnosticsTime))
                    {
                        await nextProtocol.Diagnostics.Run(block);
                    }
                    _ForceDiagnostics = false;
                }

                Logger.LogDebug("Commit DB transaction");
                await tx.CommitAsync();
            }
            catch (Exception)
            {
                await tx.RollbackAsync();
                throw;
            }

            Cache.Trim();
            return Cache.Chain.Get();
        }
        
        public virtual async Task<L1Chain> RevertLastBlock(string predecessor)
        {
            Logger.LogDebug("Begin DB transaction");
            using var tx = await Db.Database.BeginTransactionAsync();
            try
            {
                var state = Cache.Chain.Get();
                Db.TryAttach(state);

                Logger.LogDebug("Init block context");
                await InitContext(state);
                Db.TryAttach(Context.Proposer);

                var nextProtocol = this;
                if (state.Protocol != state.NextProtocol)
                {
                    nextProtocol = Services.GetProtocolHandler(state.Level + 1, state.NextProtocol);

                    Logger.LogDebug("Deactivate protocol {hash}", state.NextProtocol);
                    await nextProtocol.Deactivate(state);

                    nextProtocol.Diagnostics.TrackChanges();
                    await Db.SaveChangesAsync();
                }

                Logger.LogDebug("Revert quotes");
                using (Metrics.Measure.Timer.Time(MetricsRegistry.RevertQuotesProcessingTime))
                {
                    await Quotes.Revert();
                }

                Logger.LogDebug("Revert post-changes");
                using (Metrics.Measure.Timer.Time(MetricsRegistry.RevertPostProcessingTime))
                {
                    await BeforeRevert();

                    nextProtocol.Diagnostics.TrackChanges();
                    await Db.SaveChangesAsync();
                }

                Logger.LogDebug("Revert block");
                using (Metrics.Measure.Timer.Time(MetricsRegistry.RevertProcessingTime))
                {
                    await Revert();
                }

                Logger.LogDebug("Touch addresses");
                ClearAddresses();

                Logger.LogDebug("Save changes");
                using (Metrics.Measure.Timer.Time(MetricsRegistry.RevertSaveChangesTime))
                {
                    nextProtocol.Diagnostics.TrackChanges();
                    await Context.Revert(Db);
                    await Db.SaveChangesAsync();
                }

                if ((Config.Diagnostics || _ForceDiagnostics) && state.Hash == predecessor)
                {
                    Logger.LogDebug("Diagnostics");
                    using (Metrics.Measure.Timer.Time(MetricsRegistry.RevertDiagnosticsTime))
                    {
                        await Diagnostics.Run(state.Level);
                    }
                    _ForceDiagnostics = false;
                }

                Logger.LogDebug("Commit DB transaction");
                await tx.CommitAsync();
            }
            catch (Exception)
            {
                await tx.RollbackAsync();
                throw;
            }

            Cache.Trim();
            return Cache.Chain.Get();
        }

        public virtual async Task WarmUpCache(JsonElement block)
        {
            var addresses = new HashSet<string>(64);
            var contracts = new HashSet<string>(64);
            var operations = block.RequiredArray("operations", 4);

            foreach (var op in operations[2].RequiredArray().EnumerateArray())
            {
                var content = op.RequiredArray("contents", 1)[0];
                if (content.RequiredString("kind") == "activate_account")
                    addresses.Add(content.RequiredString("pkh"));
            }

            foreach (var op in operations[3].RequiredArray().EnumerateArray())
            {
                foreach (var content in op.RequiredArray("contents").EnumerateArray())
                {
                    addresses.Add(content.RequiredString("source"));
                    if (content.RequiredString("kind") == "transaction")
                    {
                        if (content.RequiredString("destination") is string dest)
                        {
                            addresses.Add(dest);
                            if (dest[0] == 'K')
                                contracts.Add(dest);
                        }

                        if (content.Required("metadata").TryGetProperty("internal_operation_results", out var internalResults))
                            foreach (var internalContent in internalResults.RequiredArray().EnumerateArray())
                            {
                                addresses.Add(internalContent.RequiredString("source"));
                                if (internalContent.RequiredString("kind") == "transaction")
                                {
                                    if (internalContent.RequiredString("destination") is string internalDest)
                                    {
                                        addresses.Add(internalDest);
                                        if (internalDest[0] == 'K')
                                            contracts.Add(internalDest);
                                    }
                                }
                            }
                    }
                }
            }

            if (addresses.Count != 0)
            {
                var header = block.Required("header");
                await Cache.Addresses.LoadAsync(addresses, header.RequiredInt32("level"), header.RequiredDateTime("timestamp"));
            }
            if (contracts.Count != 0)
            {
                var contractIds = new List<int>();
                foreach (var contract in contracts)
                    if (Cache.Addresses.TryGetCached(contract, out var _contract))
                        contractIds.Add(_contract.Id);

                if (contractIds.Count != 0)
                {
                    await Cache.Storages.PreloadAsync(contractIds);
                    await Cache.Schemas.PreloadAsync(contractIds);
                }
            }
        }

        public virtual Task Activate(L1Chain state, JsonElement block) => Task.CompletedTask;

        public virtual Task Deactivate(L1Chain state) => Task.CompletedTask;

        public virtual Task AfterCommit(JsonElement block) => Task.CompletedTask;

        public virtual Task BeforeRevert() => Task.CompletedTask;

        public abstract Task Commit(JsonElement block);

        public abstract Task Revert();

        public void ForceDiagnostics() => _ForceDiagnostics = true;

        async Task InitContext(L1Chain state)
        {
            var currBlock = Cache.Blocks.Get(state.Level);
            Context.Block = currBlock;
            Context.Proposer = Cache.Addresses.GetBaker(currBlock.ProposerId!.Value);
            Context.Protocol = await Cache.Protocols.GetAsync(currBlock.ProtocolId);

            if (currBlock.Operations.HasFlag(L1Operations.Attestation))
                Context.AttestationOps = await Db.AttestationOps.AsNoTracking().Where(x => x.ChainId == currBlock.ChainId && x.Level == currBlock.Level).ToListAsync();

            if (currBlock.Operations.HasFlag(L1Operations.Preattestation))
                Context.PreattestationOps = await Db.PreattestationOps.AsNoTracking().Where(x => x.ChainId == currBlock.ChainId && x.Level == currBlock.Level).ToListAsync();

            if (currBlock.Operations.HasFlag(L1Operations.Proposal))
                Context.ProposalOps = await Db.ProposalOps.AsNoTracking().Where(x => x.ChainId == currBlock.ChainId && x.Level == currBlock.Level).ToListAsync();

            if (currBlock.Operations.HasFlag(L1Operations.Ballot))
                Context.BallotOps = await Db.BallotOps.AsNoTracking().Where(x => x.ChainId == currBlock.ChainId && x.Level == currBlock.Level).ToListAsync();

            if (currBlock.Operations.HasFlag(L1Operations.Activation))
                Context.ActivationOps = await Db.ActivationOps.AsNoTracking().Where(x => x.ChainId == currBlock.ChainId && x.Level == currBlock.Level).ToListAsync();

            if (currBlock.Operations.HasFlag(L1Operations.DalEntrapmentEvidence))
                Context.DalEntrapmentEvidenceOps = await Db.DalEntrapmentEvidenceOps.AsNoTracking().Where(x => x.ChainId == currBlock.ChainId && x.Level == currBlock.Level).ToListAsync();

            if (currBlock.Operations.HasFlag(L1Operations.DoubleBaking))
                Context.DoubleBakingOps = await Db.DoubleBakingOps.AsNoTracking().Where(x => x.ChainId == currBlock.ChainId && x.Level == currBlock.Level).ToListAsync();

            if (currBlock.Operations.HasFlag(L1Operations.DoubleConsensus))
                Context.DoubleConsensusOps = await Db.DoubleConsensusOps.AsNoTracking().Where(x => x.ChainId == currBlock.ChainId && x.Level == currBlock.Level).ToListAsync();

            if (currBlock.Operations.HasFlag(L1Operations.NonceRevelation))
                Context.NonceRevelationOps = await Db.NonceRevelationOps.AsNoTracking().Where(x => x.ChainId == currBlock.ChainId && x.Level == currBlock.Level).ToListAsync();

            if (currBlock.Operations.HasFlag(L1Operations.VdfRevelation))
                Context.VdfRevelationOps = await Db.VdfRevelationOps.AsNoTracking().Where(x => x.ChainId == currBlock.ChainId && x.Level == currBlock.Level).ToListAsync();

            if (currBlock.Operations.HasFlag(L1Operations.DrainDelegate))
                Context.DrainDelegateOps = await Db.DrainDelegateOps.AsNoTracking().Where(x => x.ChainId == currBlock.ChainId && x.Level == currBlock.Level).ToListAsync();

            if (currBlock.Operations.HasFlag(L1Operations.Delegation))
                Context.DelegationOps = await Db.DelegationOps.AsNoTracking().Where(x => x.ChainId == currBlock.ChainId && x.Level == currBlock.Level).ToListAsync();

            if (currBlock.Operations.HasFlag(L1Operations.Origination))
                Context.OriginationOps = await Db.OriginationOps
                    .AsNoTracking()
                    .OfType<L1OriginationOperation>()
                    .Where(x => x.ChainId == currBlock.ChainId && x.Level == currBlock.Level)
                    .ToListAsync();

            if (currBlock.Operations.HasFlag(L1Operations.Transaction))
                Context.TransactionOps = await Db.TransactionOps
                    .AsNoTracking()
                    .OfType<L1TransactionOperation>()
                    .Where(x => x.ChainId == currBlock.ChainId && x.Level == currBlock.Level)
                    .ToListAsync();

            if (currBlock.Operations.HasFlag(L1Operations.Reveal))
                Context.RevealOps = await Db.RevealOps
                    .AsNoTracking()
                    .OfType<L1RevealOperation>()
                    .Where(x => x.ChainId == currBlock.ChainId && x.Level == currBlock.Level)
                    .ToListAsync();

            if (currBlock.Operations.HasFlag(L1Operations.RegisterConstant))
                Context.RegisterConstantOps = await Db.RegisterConstantOps
                    .AsNoTracking()
                    .OfType<L1RegisterConstantOperation>()
                    .Where(x => x.ChainId == currBlock.ChainId && x.Level == currBlock.Level)
                    .ToListAsync();

            if (currBlock.Operations.HasFlag(L1Operations.SetDepositsLimits))
                Context.SetDepositsLimitOps = await Db.SetDepositsLimitOps.AsNoTracking().Where(x => x.ChainId == currBlock.ChainId && x.Level == currBlock.Level).ToListAsync();

            if (currBlock.Operations.HasFlag(L1Operations.IncreasePaidStorage))
                Context.IncreasePaidStorageOps = await Db.IncreasePaidStorageOps
                    .AsNoTracking()
                    .OfType<L1IncreasePaidStorageOperation>()
                    .Where(x => x.ChainId == currBlock.ChainId && x.Level == currBlock.Level)
                    .ToListAsync();

            if (currBlock.Operations.HasFlag(L1Operations.UpdateSecondaryKey))
                Context.UpdateSecondaryKeyOps = await Db.UpdateSecondaryKeyOps.AsNoTracking().Where(x => x.ChainId == currBlock.ChainId && x.Level == currBlock.Level).ToListAsync();

            if (currBlock.Operations.HasFlag(L1Operations.TransferTicket))
                Context.TransferTicketOps = await Db.TransferTicketOps
                    .AsNoTracking()
                    .OfType<L1TransferTicketOperation>()
                    .Where(x => x.ChainId == currBlock.ChainId && x.Level == currBlock.Level)
                    .ToListAsync();

            if (currBlock.Operations.HasFlag(L1Operations.SetDelegateParameters))
                Context.SetDelegateParametersOps = await Db.SetDelegateParametersOps.AsNoTracking().Where(x => x.ChainId == currBlock.ChainId && x.Level == currBlock.Level).ToListAsync();

            if (currBlock.Operations.HasFlag(L1Operations.DalPublishCommitment))
                Context.DalPublishCommitmentOps = await Db.DalPublishCommitmentOps.AsNoTracking().Where(x => x.ChainId == currBlock.ChainId && x.Level == currBlock.Level).ToListAsync();

            if (currBlock.Operations.HasFlag(L1Operations.Staking))
                Context.StakingOps = await Db.StakingOps.AsNoTracking().Where(x => x.ChainId == currBlock.ChainId && x.Level == currBlock.Level).ToListAsync();

            if (currBlock.Operations.HasFlag(L1Operations.SmartRollupAddMessages))
                Context.SmartRollupAddMessagesOps = await Db.SmartRollupAddMessagesOps.AsNoTracking().Where(x => x.ChainId == currBlock.ChainId && x.Level == currBlock.Level).ToListAsync();

            if (currBlock.Operations.HasFlag(L1Operations.SmartRollupCement))
                Context.SmartRollupCementOps = await Db.SmartRollupCementOps.AsNoTracking().Where(x => x.ChainId == currBlock.ChainId && x.Level == currBlock.Level).ToListAsync();

            if (currBlock.Operations.HasFlag(L1Operations.SmartRollupExecute))
                Context.SmartRollupExecuteOps = await Db.SmartRollupExecuteOps.AsNoTracking().Where(x => x.ChainId == currBlock.ChainId && x.Level == currBlock.Level).ToListAsync();

            if (currBlock.Operations.HasFlag(L1Operations.SmartRollupOriginate))
                Context.SmartRollupOriginateOps = await Db.SmartRollupOriginateOps.AsNoTracking().Where(x => x.ChainId == currBlock.ChainId && x.Level == currBlock.Level).ToListAsync();

            if (currBlock.Operations.HasFlag(L1Operations.SmartRollupPublish))
                Context.SmartRollupPublishOps = await Db.SmartRollupPublishOps.AsNoTracking().Where(x => x.ChainId == currBlock.ChainId && x.Level == currBlock.Level).ToListAsync();

            if (currBlock.Operations.HasFlag(L1Operations.SmartRollupRecoverBond))
                Context.SmartRollupRecoverBondOps = await Db.SmartRollupRecoverBondOps.AsNoTracking().Where(x => x.ChainId == currBlock.ChainId && x.Level == currBlock.Level).ToListAsync();

            if (currBlock.Operations.HasFlag(L1Operations.SmartRollupRefute))
                Context.SmartRollupRefuteOps = await Db.SmartRollupRefuteOps.AsNoTracking().Where(x => x.ChainId == currBlock.ChainId && x.Level == currBlock.Level).ToListAsync();

            if (currBlock.Operations.HasFlag(L1Operations.Migration))
                Context.MigrationOps = await Db.MigrationOps
                    .AsNoTracking()
                    .OfType<MichelsonMigrationOperation>()
                    .Where(x => x.ChainId == currBlock.ChainId && x.Level == currBlock.Level)
                    .ToListAsync();

            if (currBlock.Operations.HasFlag(L1Operations.Subsidy))
                Context.SubsidyOps = await Db.SubsidyOps.AsNoTracking().Where(x => x.ChainId == currBlock.ChainId && x.Level == currBlock.Level).ToListAsync();

            if (currBlock.Operations.HasFlag(L1Operations.RevelationPenalty))
                Context.RevelationPenaltyOps = await Db.RevelationPenaltyOps.AsNoTracking().Where(x => x.ChainId == currBlock.ChainId && x.Level == currBlock.Level).ToListAsync();

            if (currBlock.Operations.HasFlag(L1Operations.AttestationRewards))
                Context.AttestationRewardOps = await Db.AttestationRewardOps.AsNoTracking().Where(x => x.ChainId == currBlock.ChainId && x.Level == currBlock.Level).ToListAsync();

            if (currBlock.Operations.HasFlag(L1Operations.DalAttestationReward))
                Context.DalAttestationRewardOps = await Db.DalAttestationRewardOps.AsNoTracking().Where(x => x.ChainId == currBlock.ChainId && x.Level == currBlock.Level).ToListAsync();

            if (currBlock.Operations.HasFlag(L1Operations.Autostaking))
                Context.AutostakingOps = await Db.AutostakingOps.AsNoTracking().Where(x => x.ChainId == currBlock.ChainId && x.Level == currBlock.Level).ToListAsync();

            if (currBlock.Events.HasFlag(L1BlockEvents.NewAddresses))
            {
                var createdAddresses = await Db.Addresses
                    .OfType<L1Address>()
                    .Where(x => x.ChainId == state.Id && x.FirstLevel == currBlock.Level)
                    .ToListAsync();

                foreach (var address in createdAddresses)
                    Cache.Addresses.Add(address);
            }
        }

        void TouchAddresses()
        {
            var state = Cache.Chain.Get();
            var block = (Db.ChangeTracker.Entries()
                .First(x => x.Entity is L1Block block && block.Level == state.Level).Entity as L1Block)!;

            foreach (var entry in Db.ChangeTracker.Entries().Where(x => x.Entity is L1Address).ToList())
            {
                var address = (entry.Entity as L1Address)!;

                if (entry.State == EntityState.Modified)
                {
                    address.LastLevel = state.Level;
                    address.LastTimestamp = state.Timestamp;
                }
                else if (entry.State == EntityState.Added)
                {
                    if (address.FirstLevel == block.Level)
                        block.Events |= L1BlockEvents.NewAddresses;
                }
            }
        }

        void ClearAddresses()
        {
            var state = Cache.Chain.Get();

            foreach (var entry in Db.ChangeTracker.Entries().Where(x => x.Entity is L1Address).ToList())
            {
                var address = (entry.Entity as L1Address)!;

                if (entry.State == EntityState.Modified)
                {
                    address.LastLevel = Context.Block.Level;
                    address.LastTimestamp = Context.Block.Timestamp;
                }

                if (address.FirstLevel == Context.Block.Level)
                {
                    Db.Addresses.Remove(address);
                    Cache.Addresses.Remove(address);
                    Cache.Chain.ReleaseAddressId();
                }
            }
        }
    }
}
