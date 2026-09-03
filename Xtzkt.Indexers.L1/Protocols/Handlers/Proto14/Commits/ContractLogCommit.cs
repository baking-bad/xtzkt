using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Netezos.Contracts;
using Netezos.Encoding;
using Xtzkt.Data.Models;
using Xtzkt.Data.Models.Operations.Abstract;
using Xtzkt.Indexers.Common.Extensions;
using Xtzkt.Utils;

namespace Xtzkt.Indexers.L1.Protocols.Proto14
{
    class ContractLogCommit(ProtocolHandler protocol) : ProtocolCommit(protocol)
    {
        public virtual async Task Apply(L1Block block, JsonElement content)
        {
            #region init
            var contract = (await Cache.Addresses.GetExistingAsync(content.RequiredString("source")) as L1Contract)!;
            var parentTx = Context.TransactionOps.OrderByDescending(x => x.Id).FirstOrDefault(x => x.TargetId == contract.Id)
                ?? throw new Exception("Event parent transaction not found");

            var result = content.Required("result");
            if (parentTx.Status != OperationStatus.Applied || result.RequiredString("status") != "applied")
                return;

            var consumedGas = (int)(((result.OptionalInt64("consumed_milligas") ?? 0) + 999) / 1000);

            var log = new MichelsonLog
            {
                Id = Cache.Chain.NextLogId(),
                ChainId = block.ChainId,
                Level = block.Level,
                Timestamp = block.Timestamp,
                AddressId = contract.Id,
                ContractCodeHash = contract.CodeHash,
                ContractTypeHash = contract.TypeHash,
                TransactionId = parentTx.Id,
                Name = content.OptionalString("tag")
            };

            try
            {
                var type = (content.RequiredMicheline("type") as MichelinePrim)!;
                var schema = Schema.Create(type);
                log.Type = type.ToBytes();
                log.Guessed = false;

                var rawPayload = content.OptionalMicheline("payload") ?? new MichelinePrim { Prim = PrimType.Unit };
                log.PayloadRaw = schema.Optimize(rawPayload).ToBytes();
                log.Payload = Regexes.RestrictedUnicode().Replace(schema.Humanize(rawPayload), Regexes.NullEscapeString);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to process event payload");
            }
            #endregion

            #region apply
            parentTx.GasUsed += consumedGas;
            parentTx.LogsCount = (parentTx.LogsCount ?? 0) + 1;
            contract.LogsCount++;
            Cache.Chain.Get().LogsCount++;
            block.GasUsed += consumedGas;
            block.Events |= L1BlockEvents.Events;
            #endregion

            Db.Logs.Add(log);
        }

        public virtual async Task Revert(L1Block block)
        {
            if (!block.Events.HasFlag(L1BlockEvents.Events))
                return;

            var events = await Db.Logs
                .AsNoTracking()
                .Where(x => x.ChainId == block.ChainId && x.Level == block.Level)
                .ToListAsync();

            foreach (var contractEvent in events)
            {
                var contract = (await Cache.Addresses.GetAsync(contractEvent.AddressId) as L1Contract)!;
                Db.TryAttach(contract);
                contract.LogsCount--;

                Cache.Chain.Get().LogsCount--;
            }

            Cache.Chain.ReleaseLogId(events.Count);

            await Db.Database.ExecuteSqlRawAsync("""
                DELETE FROM "Logs"
                WHERE "ChainId" = {0}
                AND "Level" = {1}
                """, block.ChainId, block.Level);
                
        }
    }
}
