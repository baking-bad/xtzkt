using System.Text.Json;
using Xtzkt.Data.Models;
using Xtzkt.Data.Models.Operations.Abstract;
using Xtzkt.Indexers.Common.Extensions;

namespace Xtzkt.Indexers.L1.Protocols.Proto01
{
    class RevealsCommit(ProtocolHandler protocol) : ProtocolCommit(protocol)
    {
        public virtual async Task Apply(L1Block block, byte[] opHash, JsonElement content)
        {
            #region init
            var sender = await Cache.Addresses.GetExistingAsync(content.RequiredString("source"));

            var pubKey = content.RequiredString("public_key");
            var result = content.Required("metadata").Required("operation_result");
            var reveal = new L1RevealOperation
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
                GasUsed = GetConsumedGas(result)
            };
            #endregion

            #region apply operation
            Db.TryAttach(sender);
            PayFee(sender, reveal.BakerFee);
            sender.Counter = reveal.Counter;
            sender.RevealsCount++;

            block.GasUsed += reveal.GasUsed;
            block.Operations |= L1Operations.Reveal;

            Cache.Chain.Get().RevealOpsCount++;
            #endregion

            #region apply result
            ApplyResult(reveal, sender, pubKey);
            #endregion

            Proto.Manager.Set(sender);
            Proto.Manager.Add(reveal);
            Db.RevealOps.Add(reveal);
            Context.RevealOps.Add(reveal);
        }

        public virtual async Task Revert(L1Block block, L1RevealOperation reveal)
        {
            #region entities
            var sender = await Cache.Addresses.GetAsync(reveal.SenderId);

            Db.TryAttach(sender);
            #endregion

            #region revert result
            RevertResult(reveal, sender);
            #endregion

            #region revert operation
            RevertPayFee(sender, reveal.BakerFee);
            sender.Counter = reveal.Counter - 1;
            sender.RevealsCount--;

            Cache.Chain.Get().RevealOpsCount--;
            #endregion

            Db.RevealOps.Remove(reveal);
            Cache.Chain.ReleaseManagerCounter();
            Cache.Chain.ReleaseOperationId();
        }

        protected virtual int GetConsumedGas(JsonElement result)
        {
            return result.OptionalInt32("consumed_gas") ?? 0;
        }

        protected virtual void ApplyResult(L1RevealOperation op, L1Address sender, string pubKey)
        {
            if (sender is L1User user)
            {
                user.PublicKey = pubKey;
                if (user.Balance > 0) user.Revealed = true;
            }
        }

        protected virtual void RevertResult(L1RevealOperation op, L1Address sender)
        {
            if (sender is L1User user)
            {
                if (user.RevealsCount == 1)
                    user.PublicKey = null;

                user.Revealed = false;
            }
        }
    }
}
