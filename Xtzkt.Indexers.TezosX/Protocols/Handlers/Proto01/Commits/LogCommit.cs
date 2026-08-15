using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Netezos.Contracts;
using Netezos.Encoding;
using Xtzkt.Data.Models;
using Xtzkt.Data.Models.Operations.Abstract;
using Xtzkt.Indexers.Common.Extensions;
using Xtzkt.Indexers.TezosX.Utils.Abi;
using Xtzkt.Utils;

namespace Xtzkt.Indexers.TezosX.Protocols.Proto01;

class LogCommit(ProtocolHandler protocol) : ProtocolCommit(protocol)
{
    public async Task ApplyEvmLogs(ILogsOperation op, IEnumerable<JsonElement> logs)
    {
        if (op.Status != OperationStatus.Applied)
            return;

        var filteredLogs = logs.Where(x =>
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
        });

        foreach (var log in filteredLogs)
        {
            var address = (await Cache.Addresses.GetExistingAsync(log.RequiredString("address")) as XEvmAddress)!;
            var addressEip7702Delegate = await GetEip7702Delegate(address);

            var topics = log.RequiredArray("topics").EnumerateArray().Select(x => x.RequiredHexBytes()).ToArray();
            var data = log.RequiredHexBytes("data");

            if ((addressEip7702Delegate ?? address) is not XEvmContract contract)
                throw new Exception("Non-contract addresses shouldn't emit logs");

            string? name = null;
            string? payload = null;
            bool? guessed = null;
            if (topics.Length != 0)
            {
                if (await Cache.Abi.GetOrDefaultAsync(contract) is Abi abi && abi.TryGetEvent(topics[0], out var eventAbi))
                {
                    name = eventAbi.Name;
                    guessed = false;
                    try
                    {
                        payload = eventAbi.DecodeToJson(topics, data);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError(ex, "Failed to decode event payload");
                    }
                }
                else if (KnownTopicsAbi.TryGetEvent(topics, out var knownAbi))
                {
                    // unlike a function selector, the topic is a full signature hash, so it can't
                    // match by chance, and the name stays even if the payload doesn't decode
                    name = knownAbi.Name;
                    guessed = true;
                    try
                    {
                        payload = knownAbi.DecodeToJson(topics, data);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogDebug(ex, "Failed to guess event payload");
                    }
                }
            }
            else
            {
                // TODO: lookup anonymous events
            }

            var evmLog = new EvmLog
            {
                Id = Cache.Chain.NextLogId(),
                ChainId = Context.Block.ChainId,
                Level = Context.Block.Level,
                Timestamp = Context.Block.Timestamp,
                AddressId = address.Id,
                ContractCodeHash = contract.CodeHash,
                ContractTypeHash = contract.TypeHash,
                Topics = topics,
                Data = data,
                TransactionId = (op as TransactionOperation)?.Id,
                OriginationId = (op as XEvmOriginationOperation)?.Id,
                Name = name,
                Payload = payload,
                Guessed = guessed,
            };

            op.LogsCount = (op.LogsCount ?? 0) + 1;

            Db.TryAttach(address);
            address.LogsCount++;

            Cache.Chain.Get().LogsCount++;
            Context.Block.Events |= XBlockEvents.Events;

            Db.Logs.Add(evmLog);

            if (addressEip7702Delegate == null) // we don't index EIP7702-delegated token contracts
            {
                if (Erc.TryParseTransfers(topics, data, out var tokenType, out var tokenTransfers))
                {
                    foreach (var (tokenId, from, to, amount) in tokenTransfers)
                        Context.EvmTokenTransfers.Add(new(contract, tokenId, tokenType, from, to, amount, (op as ISourceOperation)!));
                }
            }
        }
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
        Cache.Chain.Get().LogsCount++;
        block.Events |= XBlockEvents.Events;
        #endregion

        Db.Logs.Add(log);
    }

    public async Task RevertLogs(XBlock block)
    {
        if (!block.Events.HasFlag(XBlockEvents.Events))
            return;

        var logs = await Db.Logs
            .AsNoTracking()
            .Where(x => x.ChainId == block.ChainId && x.Level == block.Level)
            .ToListAsync();

        foreach (var log in logs)
        {
            var address = await Cache.Addresses.GetAsync(log.AddressId);
            Db.TryAttach(address);
            if (address is XMichelsonContract michContract)
                michContract.LogsCount--;
            else if (address is XEvmAddress evmAddress)
                evmAddress.LogsCount--;
            else
                throw new InvalidOperationException("Invalid log address type");
            address.LastLevel = block.Level;
            address.LastTimestamp = block.Timestamp;

            Cache.Chain.Get().LogsCount--;
        }

        Cache.Chain.ReleaseLogId(logs.Count);

        await Db.Database.ExecuteSqlRawAsync("""
            DELETE FROM "Logs"
            WHERE "ChainId" = {0}
            AND "Level" = {1}
            """, block.ChainId, block.Level);

    }
}
