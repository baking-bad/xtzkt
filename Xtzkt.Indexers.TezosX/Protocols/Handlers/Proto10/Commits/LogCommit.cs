using System.Text.Json;
using Netezos.Contracts;
using Netezos.Encoding;
using Xtzkt.Data.Models;
using Xtzkt.Data.Models.Operations.Abstract;
using Xtzkt.Indexers.Common.Extensions;
using Xtzkt.Utils;

namespace Xtzkt.Indexers.TezosX.Protocols.Proto10;

class LogCommit(ProtocolHandler protocol) : Proto02.LogCommit(protocol)
{
    public override Task ApplyEvmLogs(ILogsOperation op, IEnumerable<JsonElement> logs)
    {
        return base.ApplyEvmLogs(op, logs.Where(x =>
        {
            // ignore gateway logs
            if (x.RequiredString("address") == EvmRuntime.MichelsonGateway)
                return false;

            var topics = x.RequiredArray("topics").EnumerateArray();
            if (topics.Any())
            {
                var topic = topics.First().RequiredString();
                // ignore alias logs
                if (topic == EvmRuntime.AliasInitializedTopic ||
                    topic == EvmRuntime.AliasForwardedTopic)
                    return false;
            }

            return true;
        }));
    }

    protected override bool IsFaBridge(XEvmAddress address)
    {
        return address.Hash == EvmRuntime.FaBridge;
    }

    public async Task ApplyMichelsonLog(IParentOperation? cracParent, JsonElement content)
    {
        #region init
        var block = Context.Block;
        var contract = (await Cache.Addresses.GetExistingAsync(content.RequiredString("source")) as XMichelsonContract)!;
        var parentTx = Context.TransactionOps
            .OrderByDescending(x => x.Id)
            .FirstOrDefault(x => x.TargetId == contract.Id)
            ?? throw new Exception("Event parent transaction not found");

        var result = content.Required("result");
        if (parentTx.Status != OperationStatus.Applied || result.RequiredString("status") != "applied")
            return;

        var consumedMilligas = result.OptionalInt64("consumed_milligas") ?? 0;
        var consumedGas = (int)((consumedMilligas + 999) / 1000);

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
        if (parentTx != cracParent)
        {
            cracParent?.GasUsed -= EvmRuntime.ConvertGas(consumedMilligas);
            parentTx.GasUsed += consumedGas;
        }
        parentTx.LogsCount = (parentTx.LogsCount ?? 0) + 1;
        contract.LogsCount++;
        contract.LastLevel = block.Level;
        contract.LastTimestamp = block.Timestamp;
        Cache.Chain.Get().LogsCount++;
        block.Events |= XBlockEvents.Events;
        #endregion

        Batch.Logs.Add(log);
    }
}
