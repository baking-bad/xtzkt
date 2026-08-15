using App.Metrics;
using Microsoft.EntityFrameworkCore;
using Xtzkt.Data;
using Xtzkt.Data.Models;
using Xtzkt.Indexers.Common.Exceptions;
using Xtzkt.Indexers.Common.Extensions;
using Xtzkt.Indexers.Common.Utils;
using Xtzkt.Indexers.TezosX.Protocols;
using Xtzkt.Indexers.TezosX.Protocols.Abstract;
using Xtzkt.Indexers.TezosX.Services;

namespace Xtzkt.Indexers.TezosX
{
    public abstract class ProtocolHandler(XtzktContext db, CacheService cache, IServiceProvider services, IConfiguration config, ILogger logger, IMetrics metrics)
    {
        public readonly XtzktContext Db = db;
        public readonly CacheService Cache = cache;
        public readonly ILogger Logger = logger;
        public BlockContext Context = null!;

        protected readonly IMetrics Metrics = metrics;
        protected readonly IServiceProvider Services = services;
        protected readonly TezosProtocolsConfig Config = config.GetTezosProtocolsConfig();

        #region abstract
        public abstract int Version { get; }
        public abstract IEvmRpc EvmRpc { get; }
        public abstract IMichelsonRpc MichelsonRpc { get; }

        protected abstract IActivator Activator { get; }
        protected abstract IMigrator Migrator { get; }
        protected abstract IHelpers Helpers { get; }

        protected abstract Task Commit(IMetaBlock block);
        protected abstract Task Revert();
        #endregion

        public virtual async Task<XChain> ApplyNextBlock(int head, bool migrating = false)
        {
            var state = Cache.Chain.Get();
            Db.TryAttach(state);

            if (state.MichelsonActivationLevel is null)
            {
                var michelsonActivationLevel = await EvmRpc.GetMichelsonActivationLevel();
                state.MichelsonActivationLevel = michelsonActivationLevel.OptionalInt32();
            }

            Logger.LogDebug("Begin DB transaction");
            using var tx = await Db.Database.BeginTransactionAsync();
            var txClosed = false;
            try
            {
                for (int i = 0; i < 1 && state.Level < head; i++)
                {
                    if (state.KernelUpgrade != null && !migrating)
                    {
                        Logger.LogDebug("Check for upgrade at {level}", state.Level + 1);
                        var nextProtocol = Services.GetProtocolHandler(state.KernelUpgrade);
                        if (await nextProtocol.HasUpgraded())
                        {
                            Logger.LogDebug("Save changes");
                            using (Metrics.Measure.Timer.Time(MetricsRegistry.SaveChangesTime))
                            {
                                Context?.Apply(Db);
                                await Db.SaveChangesAsync();
                            }

                            Logger.LogDebug("Commit DB transaction");
                            await tx.CommitAsync();
                            txClosed = true;

                            return await nextProtocol.ApplyNextBlock(head, true);
                        }
                    }

                    IMetaBlock block;
                    Logger.LogDebug("Load block {level}", state.Level + 1);
                    using (Metrics.Measure.Timer.Time(MetricsRegistry.RpcResponseTime))
                    {
                        block = await Helpers.GetMetaBlock(state);
                        ValidateBlock(block, state);
                    }

                    Logger.LogDebug("Init block context");
                    Context = state.BlocksCount == 0
                        ? await ActivateContext(state, block)
                        : await InitContext(state, block);

                    if (migrating)
                    {
                        Logger.LogDebug("Migrate kernel to {hash}", state.KernelUpgrade);
                        await Migrator.MigrateContext(state, block);

                        migrating = false;
                        state.KernelUpgrade = null;
                        state.KernelUpgradeTime = null;
                    }

                    if (block.KernelUpgrade != null)
                    {
                        Logger.LogDebug("Kernel upgrade to {version} scheduled at {timestamp}", state.KernelUpgrade, state.KernelUpgradeTime);
                        state.KernelUpgrade = block.KernelUpgrade;
                        state.KernelUpgradeTime = block.KernelUpgradeTime;
                    }

                    if (state.BlocksCount == 0)
                    {
                        Logger.LogDebug("Activate EVM context");
                        await Activator.ActivateEvmContext(state);
                    }

                    if (state.MichelsonActivationLevel == block.Level)
                    {
                        Logger.LogDebug("Activate Michelson context");
                        await Activator.ActivateMichelsonContext(state);
                    }

                    Logger.LogDebug("Process block");
                    using (Metrics.Measure.Timer.Time(MetricsRegistry.ProcessingTime))
                    {
                        await Commit(block);
                    }

                    #region debug
                    if (false)
                    {
                        if (block.Batches.Count != 0)
                        {
                            var addresses = Db.ChangeTracker.Entries()
                                .Where(x => x.Entity is XAddress)
                                .Select(x => x.Entity)
                                .OfType<XAddress>()
                                .ToList();

                            var evmAddresses = addresses
                                .Where(x => x is XEvmAddress)
                                .OfType<XEvmAddress>()
                                .ToList();

                            if (evmAddresses.Count != 0)
                            {
                                var balances = await EvmRpc.DebugBalances(evmAddresses.Select(x => x.Hash), state.Level);
                                for (int j = 0; j < balances.Length; j++)
                                {
                                    var addr = evmAddresses[j].Hash;
                                    var diff = evmAddresses[j].Balance - balances[j];
                                    if (diff != 0)
                                        ;
                                }
                            }

                            var michAddresses = addresses
                                .Where(x => x is XMichelsonAddress)
                                .OfType<XMichelsonAddress>()
                                .ToList();

                            if (michAddresses.Count != 0)
                            {
                                var balances = await MichelsonRpc.DebugBalances(michAddresses.Select(x => x.Hash), state.Level);
                                for (int j = 0; j < balances.Length; j++)
                                {
                                    var addr = michAddresses[j].Hash;
                                    var diff = michAddresses[j].Balance - balances[j];
                                    if (diff != 0)
                                        ;
                                }
                            }
                        }
                    }
                    #endregion
                }

                Logger.LogDebug("Save changes");
                using (Metrics.Measure.Timer.Time(MetricsRegistry.SaveChangesTime))
                {
                    Context.Apply(Db);
                    await Db.SaveChangesAsync();
                }

                Logger.LogDebug("Commit DB transaction");
                await tx.CommitAsync();
            }
            catch (Exception)
            {
                if (!txClosed) await tx.RollbackAsync();
                throw;
            }

            Cache.Trim();
            return Cache.Chain.Get();
        }
        
        public virtual async Task<XChain> RevertLastBlock()
        {
            Logger.LogDebug("Begin DB transaction");
            using var tx = await Db.Database.BeginTransactionAsync();
            try
            {
                var state = Cache.Chain.Get();
                Db.TryAttach(state);

                Logger.LogDebug("Init block context");
                Context = await InitContext(state);

                Logger.LogDebug("Revert block");
                using (Metrics.Measure.Timer.Time(MetricsRegistry.RevertProcessingTime))
                {
                    await Revert();
                }

                if (state.MichelsonActivationLevel == Context.Block.Level)
                {
                    Logger.LogDebug("Deactivate Michelson context");
                    await Activator.DeactivateMichelsonContext(state);
                }

                if (state.BlocksCount == 0)
                {
                    Logger.LogDebug("Deactivate EVM context");
                    await Activator.DeactivateEvmContext(state);
                }

                if (Context.Protocol.FirstLevel == Context.Block.Level && state.ProtocolsCount > 1)
                {
                    Logger.LogDebug("Revert kernel migration from {hash}", state.Kernel);
                    await Migrator.RevertContext(state);
                }

                Logger.LogDebug("Save changes");
                using (Metrics.Measure.Timer.Time(MetricsRegistry.RevertSaveChangesTime))
                {
                    await Context.Revert(Db);
                    await Db.SaveChangesAsync();
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

        public virtual async Task<bool> HasUpgraded()
        {
            var state = Cache.Chain.Get();
            var blueprint = await EvmRpc.GetBlueprint(state.Level + 1);
            var timestamp = blueprint.Required("blueprint").RequiredDateTime("timestamp");
            return state.KernelUpgradeTime <= timestamp;
        }

        protected void ValidateBlock(IMetaBlock block, XChain state)
        {
            if (block.Level == 0) return;

            if (block.EvmBlock.RequiredString("parentHash") != state.Hash)
                throw new ValidationException("Invalid EVM predecessor", true);

            if (block.Level == state.MichelsonActivationLevel) return;

            if (block.MichelsonBlock?.RequiredString("chain_id") != state.MichelsonChainId)
                throw new ValidationException("Invalid Michelson chain");

            if (block.MichelsonBlock?.RequiredString("protocol") != state.MichelsonProtocol && block.Level >= state.MichelsonActivationLevel + 2)
                throw new ValidationException("Invalid Michelson protocol");

            if (block.MichelsonBlock?.Required("header").RequiredString("predecessor") != state.MichelsonBlock)
                throw new ValidationException("Invalid Michelson predecessor", true);
        }

        protected ProtocolHandler WithContext(BlockContext context)
        {
            Context = context;
            return this;
        }

        async Task<BlockContext> ActivateContext(XChain state, IMetaBlock block)
        {
            return new BlockContext
            {
                Block = new()
                {
                    Id = Cache.Chain.NextOperationId(),
                    ChainId = state.Id,
                    Hash = block.Hash,
                    Level = block.Level,
                    Timestamp = block.Timestamp,
                    MichelsonHash = block.MichelsonBlock?.RequiredString("hash"),
                    ProtocolId = -1, // set in proto activator
                },
                Protocol = null!, // set in proto activator
                Statistics = new()
                {
                    ChainId = state.Id,
                    Level = block.Level,
                    Timestamp = block.Timestamp,
                },
            };
        }

        async Task<BlockContext> InitContext(XChain state, IMetaBlock block)
        {
            var protocol = await Cache.Protocols.GetAsync(state.Kernel);
            var timestamp = state.Timestamp == block.Timestamp
                ? block.Timestamp.AddMilliseconds(protocol.MinBlockTimeMs)
                : block.Timestamp;
            
            var prevStats = Cache.Statistics.Current;
            
            return new BlockContext
            {
                Block = new()
                {
                    Id = Cache.Chain.NextOperationId(),
                    ChainId = state.Id,
                    Hash = block.Hash,
                    Level = block.Level,
                    Timestamp = timestamp,
                    MichelsonHash = block.MichelsonBlock?.RequiredString("hash"),
                    ProtocolId = protocol.Id,
                },
                Protocol = protocol,
                Statistics = new()
                {
                    ChainId = state.Id,
                    Level = block.Level,
                    Timestamp = block.Timestamp,
                    TotalBootstrapped = prevStats.TotalBootstrapped,
                    TotalBurned = prevStats.TotalBurned,
                    TotalBanished = prevStats.TotalBanished,
                    TotalCreated = prevStats.TotalCreated,
                    TotalLost = prevStats.TotalLost,
                },
            };
        }

        async Task<BlockContext> InitContext(XChain state)
        {
            var block = await Cache.Blocks.GetAsync(state.Level);
            var protocol = await Cache.Protocols.GetAsync(block.ProtocolId);
            var sequencerPool = await Cache.Addresses.GetAsync(block.SequencerPoolId) as XEvmAddress;
            Db.TryAttach(sequencerPool);

            var context = new BlockContext
            {
                Block = block,
                Protocol = protocol,
                Statistics = null!, // unused in reverts
                SequencerPool = sequencerPool,
            };

            if (block.Operations.HasFlag(XOperations.Deposit))
                context.DepositOps = await Db.DepositOps.AsNoTracking().Where(x => x.ChainId == block.ChainId && x.Level == block.Level).ToListAsync();

            if (block.Operations.HasFlag(XOperations.IncreasePaidStorage))
                context.IncreasePaidStorageOps = await Db.IncreasePaidStorageOps.AsNoTracking().Where(x => x.ChainId == block.ChainId && x.Level == block.Level).ToListAsync();

            if (block.Operations.HasFlag(XOperations.Origination))
                context.OriginationOps = await Db.OriginationOps.AsNoTracking().Where(x => x.ChainId == block.ChainId && x.Level == block.Level).ToListAsync();

            if (block.Operations.HasFlag(XOperations.RegisterConstant))
                context.RegisterConstantOps = await Db.RegisterConstantOps.AsNoTracking().Where(x => x.ChainId == block.ChainId && x.Level == block.Level).ToListAsync();

            if (block.Operations.HasFlag(XOperations.Reveal))
                context.RevealOps = await Db.RevealOps.AsNoTracking().Where(x => x.ChainId == block.ChainId && x.Level == block.Level).ToListAsync();

            if (block.Operations.HasFlag(XOperations.Transaction))
                context.TransactionOps = await Db.TransactionOps.AsNoTracking().Where(x => x.ChainId == block.ChainId && x.Level == block.Level).ToListAsync();

            if (block.Operations.HasFlag(XOperations.TransferTicket))
                context.TransferTicketOps = await Db.TransferTicketOps.AsNoTracking().Where(x => x.ChainId == block.ChainId && x.Level == block.Level).ToListAsync();

            if (block.Operations.HasFlag(XOperations.Migration))
                context.MigrationOps = await Db.MigrationOps.AsNoTracking().Where(x => x.ChainId == block.ChainId && x.Level == block.Level).ToListAsync();

            if (block.Events.HasFlag(XBlockEvents.NewAddresses))
            {
                var createdAddresses = await Db.Addresses
                    .OfType<XAddress>()
                    .Where(x => x.ChainId == state.Id && x.FirstLevel == block.Level)
                    .ToListAsync();

                foreach (var address in createdAddresses)
                    Cache.Addresses.Add(address);
            }

            return context;
        }
    }
}
