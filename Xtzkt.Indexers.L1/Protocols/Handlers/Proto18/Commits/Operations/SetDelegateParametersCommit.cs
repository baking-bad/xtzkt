using System.Numerics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Netezos.Contracts;
using Netezos.Encoding;
using Xtzkt.Data.Models;
using Xtzkt.Data.Models.Operations.Abstract;
using Xtzkt.Indexers.Common.Extensions;

namespace Xtzkt.Indexers.L1.Protocols.Proto18
{
    class SetDelegateParametersCommit(ProtocolHandler protocol) : ProtocolCommit(protocol)
    {
        #region static
        public static readonly string Entrypoint = "set_delegate_parameters";
        static readonly Schema Parameters = Schema.Create(new MichelinePrim
        {
            Prim = PrimType.pair,
            Args =
            [
                new MichelinePrim { Prim = PrimType.@int },
                new MichelinePrim { Prim = PrimType.@int },
                new MichelinePrim { Prim = PrimType.unit },
            ]
        });
        #endregion

        public async Task Apply(L1Block block, byte[] opHash, JsonElement content)
        {
            #region init
            var sender = (await Cache.Addresses.GetExistingAsync(content.RequiredString("source")) as L1User)!;

            var result = content.Required("metadata").Required("operation_result");
            var status = result.RequiredString("status") switch
            {
                "applied" => OperationStatus.Applied,
                "backtracked" => OperationStatus.Backtracked,
                "failed" => OperationStatus.Failed,
                "skipped" => OperationStatus.Skipped,
                _ => throw new NotImplementedException()
            };

            var limit = BigInteger.Zero;
            var edge = BigInteger.Zero;
            try
            {
                var param = Parameters.Optimize(content.Required("parameters").RequiredMicheline("value"));
                limit = ((param as MichelinePrim)!.Args![0] as MichelineInt)!.Value;
                edge = (((param as MichelinePrim)!.Args![1] as MichelinePrim)!.Args![0] as MichelineInt)!.Value;
            }
            catch when (status != OperationStatus.Applied) { }

            var operation = new SetDelegateParametersOperation
            {
                Id = Cache.Chain.NextOperationId(),
                ChainId = block.ChainId,
                Hash = opHash,
                Level = block.Level,
                Timestamp = block.Timestamp,
                BakerFee = content.RequiredInt64("fee"),
                Counter = content.RequiredInt32("counter"),
                GasLimit = content.RequiredInt32("gas_limit"),
                StorageLimit = content.RequiredInt32("storage_limit"),
                SenderId = sender.Id,
                ActivationCycle = block.Cycle + Context.Protocol.BakerParametersActivationDelay + 1,
                LimitOfStakingOverBaking = limit.TrimToInt64(),
                EdgeOfBakingOverStaking = (long)edge,
                Status = status,
                Errors = result.TryGetProperty("errors", out var errors)
                    ? OperationErrors.Parse(content, errors)
                    : null,
                GasUsed = (int)(((result.OptionalInt64("consumed_milligas") ?? 0) + 999) / 1000),
                AllocationFee = null,
                StorageFee = null,
                StorageUsed = 0
            };
            #endregion

            #region apply operation
            Db.TryAttach(sender);
            PayFee(sender, operation.BakerFee);
            sender.Counter = operation.Counter;
            sender.SetDelegateParametersOpsCount++;

            block.GasUsed += operation.GasUsed;
            block.Operations |= L1Operations.SetDelegateParameters;

            Cache.Chain.Get().SetDelegateParametersOpsCount++;
            #endregion

            #region apply result
            if (operation.Status == OperationStatus.Applied)
            {
                Cache.Chain.Get().PendingBakerParameters++;
            }
            #endregion

            Proto.Manager.Set(sender);
            Proto.Manager.Add(operation);
            Db.SetDelegateParametersOps.Add(operation);
            Context.SetDelegateParametersOps.Add(operation);
        }

        public async Task Revert(L1Block block, SetDelegateParametersOperation operation)
        {
            var sender = (await Cache.Addresses.GetAsync(operation.SenderId) as L1User)!;
            Db.TryAttach(sender);

            #region revert result
            if (operation.Status == OperationStatus.Applied)
            {
                Cache.Chain.Get().PendingBakerParameters--;
            }
            #endregion

            #region revert operation
            RevertPayFee(sender, operation.BakerFee);
            sender.Counter = operation.Counter - 1;
            sender.SetDelegateParametersOpsCount--;

            Cache.Chain.Get().SetDelegateParametersOpsCount--;
            #endregion

            Db.SetDelegateParametersOps.Remove(operation);
            Cache.Chain.ReleaseManagerCounter();
            Cache.Chain.ReleaseOperationId();
        }

        public async Task ActivateStakingParameters(L1Block block)
        {
            if (!block.Events.HasFlag(L1BlockEvents.CycleBegin) || Cache.Chain.Get().PendingBakerParameters == 0)
                return;

            var ops = await Db.SetDelegateParametersOps
                .AsNoTracking()
                .Where(x => x.ChainId == block.ChainId && x.ActivationCycle == block.Cycle && x.Status == OperationStatus.Applied)
                .ToListAsync();

            foreach (var op in ops.OrderBy(x => x.Id))
            {
                var baker = Cache.Addresses.GetBaker(op.SenderId);
                Db.TryAttach(baker);
                baker.EdgeOfBakingOverStaking = op.EdgeOfBakingOverStaking;
                baker.LimitOfStakingOverBaking = op.LimitOfStakingOverBaking;
                UpdateBakerPower(baker);
                Cache.Chain.Get().PendingBakerParameters--;
            }
        }

        public async Task DeactivateStakingParameters(L1Block block)
        {
            if (!block.Events.HasFlag(L1BlockEvents.CycleBegin))
                return;

            var ops = await Db.SetDelegateParametersOps
                .AsNoTracking()
                .Where(x => x.ChainId == block.ChainId && x.ActivationCycle == block.Cycle && x.Status == OperationStatus.Applied)
                .ToListAsync();

            foreach (var op in ops.OrderByDescending(x => x.Id))
            {
                var baker = Cache.Addresses.GetBaker(op.SenderId);

                var prevOp = await Db.SetDelegateParametersOps
                    .AsNoTracking()
                    .Where(x =>
                        x.SenderId == baker.Id &&
                        x.ActivationCycle < op.ActivationCycle &&
                        x.Status == OperationStatus.Applied)
                    .OrderByDescending(x => x.Id)
                    .FirstOrDefaultAsync();

                Db.TryAttach(baker);
                baker.EdgeOfBakingOverStaking = prevOp?.EdgeOfBakingOverStaking;
                baker.LimitOfStakingOverBaking = prevOp?.LimitOfStakingOverBaking;
                RevertBakerPower(baker);
                Cache.Chain.Get().PendingBakerParameters++;
            }
        }
    }
}
