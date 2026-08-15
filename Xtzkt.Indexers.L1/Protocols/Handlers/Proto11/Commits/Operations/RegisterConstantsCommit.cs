using System.Text.Json;
using Netezos.Encoding;
using Xtzkt.Data.Models;
using Xtzkt.Data.Models.Operations.Abstract;
using Xtzkt.Indexers.Common.Extensions;

namespace Xtzkt.Indexers.L1.Protocols.Proto11
{
    class RegisterConstantsCommit(ProtocolHandler protocol) : ProtocolCommit(protocol)
    {
        public virtual async Task Apply(L1Block block, JsonElement op, JsonElement content)
        {
            #region init
            var sender = (L1User)await Cache.Addresses.GetExistingAsync(content.RequiredString("source"));

            var result = content.Required("metadata").Required("operation_result");
            var registerConstant = new L1RegisterConstantOperation
            {
                Id = Cache.Chain.NextOperationId(),
                ChainId = block.ChainId,
                Hash = op.RequiredString("hash"),
                Level = block.Level,
                Timestamp = block.Timestamp,
                BakerFee = content.RequiredInt64("fee"),
                Counter = content.RequiredInt32("counter"),
                GasLimit = content.RequiredInt32("gas_limit"),
                StorageLimit = content.RequiredInt32("storage_limit"),
                SenderId = sender.Id,
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
                GasUsed = GetConsumedGas(result),
                StorageUsed = result.OptionalInt32("storage_size") ?? 0,
                StorageFee = result.OptionalInt32("storage_size") > 0
                    ? result.OptionalInt32("storage_size") * Context.Protocol.ByteCost
                    : null,
            };
            #endregion

            #region apply operation
            Db.TryAttach(sender);
            PayFee(sender, registerConstant.BakerFee);
            sender.Counter = registerConstant.Counter;
            sender.RegisterConstantsCount++;

            block.Operations |= L1Operations.RegisterConstant;

            Cache.Chain.Get().RegisterConstantOpsCount++;
            #endregion

            #region apply result
            if (registerConstant.Status == OperationStatus.Applied)
            {
                var burned = registerConstant.StorageFee ?? 0;
                Proto.Manager.Burn(burned);
                BurnFee(sender, burned);

                registerConstant.Address = result.RequiredString("global_address");
                registerConstant.Value = content.RequiredMicheline("value").ToBytes();
                registerConstant.Refs = 0;

                Cache.Chain.Get().ConstantsCount++;
            }
            #endregion

            Proto.Manager.Set(sender);
            Db.RegisterConstantOps.Add(registerConstant);
            Context.RegisterConstantOps.Add(registerConstant);
        }

        public virtual async Task Revert(L1Block block, L1RegisterConstantOperation registerConstant)
        {
            #region entities
            var sender = (L1User)await Cache.Addresses.GetAsync(registerConstant.SenderId);

            Db.TryAttach(sender);
            #endregion

            #region revert result
            if (registerConstant.Status == OperationStatus.Applied)
            {
                RevertBurnFee(sender, registerConstant.StorageFee ?? 0);

                Cache.Chain.Get().ConstantsCount--;
            }
            #endregion

            #region revert operation
            RevertPayFee(sender, registerConstant.BakerFee);
            sender.Counter = registerConstant.Counter - 1;
            sender.RegisterConstantsCount--;
            sender.Revealed = true;

            Cache.Chain.Get().RegisterConstantOpsCount--;
            #endregion

            Db.RegisterConstantOps.Remove(registerConstant);
            Cache.Chain.ReleaseManagerCounter();
            Cache.Chain.ReleaseOperationId();
        }

        protected virtual int GetConsumedGas(JsonElement result)
        {
            return result.OptionalInt32("consumed_gas") ?? 0;
        }
    }
}
