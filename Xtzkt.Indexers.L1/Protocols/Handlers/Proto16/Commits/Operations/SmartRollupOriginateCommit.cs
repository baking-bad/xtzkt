using Microsoft.EntityFrameworkCore;
using Netezos.Encoding;
using System.Diagnostics.Contracts;
using System.Text.Json;
using Xtzkt.Data.Models;
using Xtzkt.Data.Models.Operations.Abstract;
using Xtzkt.Indexers.Common.Extensions;

namespace Xtzkt.Indexers.L1.Protocols.Proto16
{
    class SmartRollupOriginateCommit(ProtocolHandler protocol) : ProtocolCommit(protocol)
    {
        public virtual async Task Apply(L1Block block, JsonElement op, JsonElement content)
        {
            #region init
            var sender = await Cache.Addresses.GetExistingAsync(content.RequiredString("source"));
            Db.TryAttach(sender);

            var pvmKind = content.RequiredString("pvm_kind") switch
            {
                "arith" => PvmKind.Arith,
                "wasm_2_0_0" => PvmKind.Wasm,
                _ => throw new NotImplementedException()
            };
            var result = content.Required("metadata").Required("operation_result");

            L1SmartRollup? rollup = null;
            if (result.RequiredString("status") == "applied")
            {
                var address = result.RequiredString("address");
                var ghost = await Cache.Addresses.GetAsync(address, block);
                if (ghost != null)
                {
                    rollup = new()
                    {
                        Id = ghost.Id,
                        ChainId = ghost.ChainId,
                        Index = ghost.Index,
                        FirstLevel = ghost.FirstLevel,
                        FirstTimestamp = ghost.FirstTimestamp,
                        LastLevel = ghost.LastLevel,
                        LastTimestamp = ghost.LastTimestamp,
                        Hash = address,
                        Counter = 0,
                        CreatorId = sender.Id,
                        PvmKind = pvmKind,
                        ParameterSchema = content.RequiredMicheline("parameters_ty").ToBytes(),
                        GenesisCommitment = result.RequiredString("genesis_commitment_hash"),
                        LastCommitment = result.RequiredString("genesis_commitment_hash"),
                        InboxLevel = 0,
                        TotalStakers = 0,
                        ActiveStakers = 0,
                        ExecutedCommitments = 0,
                        CementedCommitments = 0,
                        PendingCommitments = 0,
                        RefutedCommitments = 0,
                        OrphanCommitments = 0,
                        SmartRollupBonds = 0,
                        ActiveTokensCount = ghost.ActiveTokensCount,
                        TokenBalancesCount = ghost.TokenBalancesCount,
                        TokenTransfersCount = ghost.TokenTransfersCount,
                        ActiveTicketsCount = ghost.ActiveTicketsCount,
                        TicketBalancesCount = ghost.TicketBalancesCount,
                        TicketTransfersCount = ghost.TicketTransfersCount
                    };
                    var isAdded = Db.Entry(ghost).State == EntityState.Added;
                    Db.Entry(ghost).State = EntityState.Detached;
                    Db.Entry(rollup).State = isAdded ? EntityState.Added : EntityState.Modified;
                }
                else
                {
                    rollup = new()
                    {
                        Id = Cache.Chain.NextAddressId(),
                        ChainId = Cache.Chain.Get().Id,
                        FirstLevel = block.Level,
                        FirstTimestamp = block.Timestamp,
                        LastLevel = block.Level,
                        LastTimestamp = block.Timestamp,
                        Hash = address,
                        Counter = 0,
                        CreatorId = sender.Id,
                        PvmKind = pvmKind,
                        ParameterSchema = content.RequiredMicheline("parameters_ty").ToBytes(),
                        GenesisCommitment = result.RequiredString("genesis_commitment_hash"),
                        LastCommitment = result.RequiredString("genesis_commitment_hash"),
                        InboxLevel = 0,
                        TotalStakers = 0,
                        ActiveStakers = 0,
                        ExecutedCommitments = 0,
                        CementedCommitments = 0,
                        PendingCommitments = 0,
                        RefutedCommitments = 0,
                        OrphanCommitments = 0,
                        SmartRollupBonds = 0
                    };
                    Db.Addresses.Add(rollup);
                }
                Cache.Addresses.Add(rollup);
            }

            var operation = new SmartRollupOriginateOperation
            {
                Id = Cache.Chain.NextOperationId(),
                ChainId = block.ChainId,
                Level = block.Level,
                Timestamp = block.Timestamp,
                Hash = op.RequiredMichelsonOperationHashBytes("hash"),
                BakerFee = content.RequiredInt64("fee"),
                Counter = content.RequiredInt32("counter"),
                GasLimit = content.RequiredInt32("gas_limit"),
                StorageLimit = content.RequiredInt32("storage_limit"),
                SenderId = sender.Id,
                PvmKind = pvmKind,
                Kernel = Hex.Parse(content.RequiredString("kernel")),
                GenesisCommitment = result.OptionalString("genesis_commitment_hash"),
                SmartRollupId = rollup?.Id,
                Status = result.RequiredString("status") switch
                {
                    "applied" => OperationStatus.Applied,
                    "backtracked" => OperationStatus.Backtracked,
                    "failed" => OperationStatus.Failed,
                    "skipped" => OperationStatus.Skipped,
                    _ => throw new NotImplementedException()
                },
                Errors = result.TryGetProperty("errors", out var errors)
                    ? OperationErrors.Parse(content, errors)
                    : null,
                GasUsed = (int)(((result.OptionalInt64("consumed_milligas") ?? 0) + 999) / 1000),
                StorageUsed = result.OptionalInt32("size") ?? 0,
                StorageFee = result.OptionalInt32("size") > 0
                    ? result.OptionalInt32("size") * Context.Protocol.ByteCost
                    : null,
                AllocationFee = null
            };

            try
            {
                operation.ParameterType = content.RequiredMicheline("parameters_ty").ToBytes();
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to parse smart rollup parameter type");
            }
            #endregion

            #region apply operation
            PayFee(sender, operation.BakerFee);

            sender.SmartRollupOriginateCount++;
            if (rollup != null) rollup.SmartRollupOriginateCount++;

            block.Operations |= L1Operations.SmartRollupOriginate;

            sender.Counter = operation.Counter;

            Cache.Chain.Get().SmartRollupOriginateOpsCount++;
            #endregion

            #region apply result
            if (operation.Status == OperationStatus.Applied)
            {
                var burned = operation.StorageFee ?? 0;
                Proto.Manager.Burn(burned);
                BurnFee(sender, burned);

                sender.SmartRollupsCount++;
            }
            #endregion

            Proto.Manager.Set(sender);
            Db.SmartRollupOriginateOps.Add(operation);
            Context.SmartRollupOriginateOps.Add(operation);
        }

        public virtual async Task Revert(L1Block block, SmartRollupOriginateOperation operation)
        {
            #region entities
            var sender = await Cache.Addresses.GetAsync(operation.SenderId);
            var rollup = await Cache.Addresses.GetAsync(operation.SmartRollupId) as L1SmartRollup;

            Db.TryAttach(sender);
            Db.TryAttach(rollup);
            #endregion

            #region revert result
            if (operation.Status == OperationStatus.Applied)
            {
                RevertBurnFee(sender, operation.StorageFee ?? 0);

                sender.SmartRollupsCount--;

                if (rollup!.OriginationsCount == 0 &&
                    rollup.TransactionsCount == 0 &&
                    rollup.TransferTicketCount == 0 &&
                    rollup.IncreasePaidStorageCount == 0 &&
                    rollup.TokenTransfersCount == 0 &&
                    rollup.TicketTransfersCount == 0 &&
                    rollup.Index is null)
                {
                    Db.Addresses.Remove(rollup);
                    Cache.Addresses.Remove(rollup);
                }
                else
                {
                    var ghost = new L1Ghost
                    {
                        Id = rollup.Id,
                        ChainId = rollup.ChainId,
                        Index = rollup.Index,
                        Hash = rollup.Hash,
                        FirstLevel = rollup.FirstLevel,
                        FirstTimestamp = rollup.FirstTimestamp,
                        LastLevel = rollup.LastLevel,
                        LastTimestamp = rollup.LastTimestamp,
                        OriginationsCount = rollup.OriginationsCount,
                        TransactionsCount = rollup.TransactionsCount,
                        TransferTicketCount = rollup.TransferTicketCount,
                        IncreasePaidStorageCount = rollup.IncreasePaidStorageCount,
                        ActiveTokensCount = rollup.ActiveTokensCount,
                        TokenBalancesCount = rollup.TokenBalancesCount,
                        TokenTransfersCount = rollup.TokenTransfersCount,
                        ActiveTicketsCount = rollup.ActiveTicketsCount,
                        TicketBalancesCount = rollup.TicketBalancesCount,
                        TicketTransfersCount = rollup.TicketTransfersCount,
                    };

                    Db.Entry(rollup).State = EntityState.Detached;
                    Db.Entry(ghost).State = EntityState.Modified;
                    Cache.Addresses.Add(ghost);
                }

                Cache.Schemas.Remove(rollup);
            }
            #endregion

            #region revert operation
            RevertPayFee(sender, operation.BakerFee);

            sender.SmartRollupOriginateCount--;

            sender.Counter = operation.Counter - 1;
            (sender as L1User)!.Revealed = true;

            Cache.Chain.Get().SmartRollupOriginateOpsCount--;
            #endregion

            Db.SmartRollupOriginateOps.Remove(operation);
            Cache.Chain.ReleaseManagerCounter();
            Cache.Chain.ReleaseOperationId();
        }
    }
}
