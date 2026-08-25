using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Xtzkt.Data.Models;
using Xtzkt.Data.Models.Operations.Abstract;
using Xtzkt.Indexers.Common.Extensions;
using Xtzkt.Indexers.TezosX.Utils.Abi;

namespace Xtzkt.Indexers.TezosX.Protocols.Proto01;

class LogCommit(ProtocolHandler protocol) : Proto01Commit(protocol)
{
    public async Task ApplyEvmLogs(ILogsOperation op, IEnumerable<JsonElement> logs)
    {
        if (op.Status != OperationStatus.Applied)
            return;

        foreach (var log in logs)
        {
            // the emitter is often unknown, because contracts deployed by other contracts are
            // invisible without traces, so it's resolved (and bootstrapped) via the node
            var address = await GetOrCreateXEvmContract(log.RequiredString("address"));
            var contract = address;

            var topics = log.RequiredArray("topics").EnumerateArray().Select(x => x.RequiredHexBytes()).ToArray();
            var data = log.RequiredHexBytes("data");

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
                DepositId = (op as DepositOperation)?.Id,
                Name = name,
                Payload = payload,
                Guessed = guessed,
            };

            op.LogsCount = (op.LogsCount ?? 0) + 1;

            Db.TryAttach(address);
            address.LogsCount++;

            Cache.Chain.Get().LogsCount++;
            Context.Block.Events |= XBlockEvents.Events;

            Batch.Logs.Add(evmLog);

            if (Erc.TryParseTransfers(topics, data, out var tokenType, out var tokenTransfers))
            {
                foreach (var (tokenId, from, to, amount) in tokenTransfers)
                    Context.EvmTokenTransfers.Add(new(contract, tokenId, tokenType, from, to, amount, (op as ISourceOperation)!));
            }
        }
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
            if (address is not XEvmAddress evmAddress)
                throw new InvalidOperationException("Invalid log address type");
            evmAddress.LogsCount--;
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
