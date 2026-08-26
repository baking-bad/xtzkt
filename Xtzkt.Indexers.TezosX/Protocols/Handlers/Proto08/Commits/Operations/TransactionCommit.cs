using System.Numerics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Netezos.Contracts;
using Netezos.Encoding;
using Xtzkt.Data.Models;
using Xtzkt.Indexers.Common.Extensions;
using Xtzkt.Indexers.Common.Helpers;
using Xtzkt.Indexers.TezosX.Utils.Abi;
using Xtzkt.Utils;

namespace Xtzkt.Indexers.TezosX.Protocols.Proto08;

partial class TransactionCommit(ProtocolHandler protocol) : ProtocolCommit(protocol)
{
    protected async Task<(string?, byte[]?, string?, bool?)> ParseParameters(XMichelsonAddress target, JsonElement parameters)
    {
        string? rawEp = null;
        IMicheline? rawParam = null;
        try
        {
            rawEp = parameters.RequiredString("entrypoint");
            rawParam = Micheline.FromJson(parameters.Required("value"))!;
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to parse tx parameters");
            rawEp ??= string.Empty;
            rawParam ??= new MichelineArray();
            return (rawEp, rawParam.ToBytes(), null, null);
        }

        if (target is not XMichelsonContract contract)
            return (rawEp, rawParam.ToBytes(), null, null);

        var schema = await Cache.Schemas.GetAsync(contract);
        try
        {
            var (normEp, normParam) = schema.NormalizeParameter(rawEp, rawParam);
            return (
                normEp,
                schema.OptimizeParameter(normEp, normParam).ToBytes(),
                Regexes.RestrictedUnicode().Replace(schema.HumanizeParameter(normEp, normParam), Regexes.NullEscapeString),
                false);
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to humanize tx parameters");
            return (rawEp, rawParam.ToBytes(), null, false);
        }
    }

    protected async Task<(string?, string?, bool?)> ParseParameters(XEvmAddress target, byte[] input)
    {
        if (target is XEvmContract contract && await Cache.Abi.GetOrDefaultAsync(contract) is Abi abi)
        {
            if (abi.TryGetFunction(input, out var fn))
            {
                try
                {
                    return (fn.Signature, AbiDecoder.DecodeToJson(input.AsSpan()[4..], fn.Inputs), false);
                }
                catch (Exception ex)
                {
                    Logger.LogWarning(ex, "Failed to parse tx inputs");
                    return (fn.Signature, null, false);
                }
            }
        }

        if (KnownSelectorsAbi.TryGetFunction(input, out var known) && (known.Inputs.Count > 0 || input.Length == 4))
        {
            try
            {
                return (known.Signature, AbiDecoder.DecodeToJson(input.AsSpan()[4..], known.Inputs), true);
            }
            catch (Exception ex)
            {
                // most likely the 4-byte selector matched by chance, and the calldata is not what
                // we guessed, so the signature is dropped as well, unlike in the abi branch above
                Logger.LogDebug(ex, "Failed to guess tx inputs");
                return (null, null, true);
            }
        }

        return (null, null, null);
    }

    protected async Task<(string?, bool?)> ParseResult(XEvmAddress target, byte[] input, byte[] output)
    {
        if (target is XEvmContract contract && await Cache.Abi.GetOrDefaultAsync(contract) is Abi abi)
        {
            if (abi.TryGetFunction(input, out var fn))
            {
                try
                {
                    return (AbiDecoder.DecodeToJson(output, fn.Outputs), false);
                }
                catch (Exception ex)
                {
                    Logger.LogWarning(ex, "Failed to parse tx outputs");
                    return (null, false);
                }
            }
        }

        if (KnownSelectorsAbi.TryGetFunction(input, out var known) && (known.Outputs.Count > 0 || output.Length == 0))
        {
            try
            {
                return (AbiDecoder.DecodeToJson(output, known.Outputs), true);
            }
            catch (Exception ex)
            {
                Logger.LogDebug(ex, "Failed to guess tx outputs");
                return (null, true);
            }
        }

        return (null, null);
    }

    protected static bool? Guessed(bool? paramsGuessed, bool? resultGuessed)
    {
        if (paramsGuessed == null)
            return resultGuessed;

        if (resultGuessed == null)
            return paramsGuessed;

        return paramsGuessed.Value && resultGuessed.Value;
    }

    protected virtual async Task<long?> ProcessStorage(long txId, XMichelsonAddress target, JsonElement storage)
    {
        if (target is not XMichelsonContract contract)
            return null;

        var schema = await Cache.Schemas.GetAsync(contract);
        var currentStorage = await Cache.Storages.GetAsync(contract);

        var newStorageMicheline = schema.OptimizeStorage(Micheline.FromJson(storage)!, false);
        var newStorageBytes = newStorageMicheline.ToBytes();

        if (newStorageBytes.IsEqual(currentStorage.RawValue))
            return currentStorage.Id;

        Db.TryAttach(currentStorage);
        currentStorage.Current = false;

        var newStorage = new Storage
        {
            Id = Cache.Chain.NextStorageId(),
            ChainId = contract.ChainId,
            Level = Context.Block.Level,
            ContractId = contract.Id,
            TransactionId = txId,
            RawValue = newStorageBytes,
            JsonValue = Regexes.RestrictedUnicode().Replace(schema.HumanizeStorage(newStorageMicheline), Regexes.NullEscapeString),
            Current = true,
        };

        Db.Storages.Add(newStorage);
        Cache.Storages.Add(contract, newStorage);

        return newStorage.Id;
    }

    public async Task RevertStorage(long txId, XMichelsonContract contract)
    {
        var storage = await Cache.Storages.GetAsync(contract);
        if (storage.TransactionId == txId)
        {
            var prevStorage = await Db.Storages
                .Where(x => x.ContractId == contract.Id && x.Id < storage.Id)
                .OrderByDescending(x => x.Id)
                .FirstAsync();

            prevStorage.Current = true;
            Cache.Storages.Add(contract, prevStorage);

            Db.Storages.Remove(storage);
            Cache.Chain.ReleaseStorageId();
        }
    }

    protected virtual IEnumerable<BigMapDiff>? ParseBigMapDiffs(JsonElement result)
    {
        return result.TryGetProperty("lazy_storage_diff", out var diffs)
            ? BigMapDiff.ParseLazyStorage(diffs)
            : null;
    }

    protected virtual IEnumerable<TicketUpdates>? ParseTicketUpdates(JsonElement result)
    {
        if (!result.TryGetProperty("ticket_updates", out var ticketUpdates))
            return null;

        var res = new List<TicketUpdates>();
        foreach (var updates in ticketUpdates.RequiredArray().EnumerateArray())
        {
            var list = new List<TicketUpdate>();
            foreach (var update in updates.RequiredArray("updates").EnumerateArray())
            {
                var amount = update.RequiredBigInteger("amount");
                if (amount != BigInteger.Zero)
                {
                    list.Add(new TicketUpdate
                    {
                        Address = update.RequiredString("account"),
                        Amount = amount
                    });
                }
            }

            if (list.Count > 0)
            {
                var ticketToken = updates.Required("ticket_token");
                var type = Micheline.FromJson(ticketToken.Required("content_type"))!;
                var value = Micheline.FromJson(ticketToken.Required("content"))!;
                var rawType = type.ToBytes();

                byte[] rawContent;
                string? jsonContent;

                try
                {
                    var schema = Schema.Create((type as MichelinePrim)!);
                    rawContent = schema.Optimize(value).ToBytes();
                    jsonContent = Regexes.RestrictedUnicode().Replace(schema.Humanize(value), Regexes.NullEscapeString);
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Failed to parse ticket content");
                    rawContent = value.ToBytes();
                    jsonContent = null;
                }

                res.Add(new TicketUpdates
                {
                    Ticket = new TicketIdentity
                    {
                        Ticketer = ticketToken.RequiredString("ticketer"),
                        RawType = rawType,
                        RawContent = rawContent,
                        JsonContent = jsonContent,
                    },
                    Updates = list
                });
            }
        }

        return res.Count > 0 ? res : null;
    }

    protected async Task<int?> ProcessAddressRegistryDiffs(JsonElement result)
    {
        int? addressRegistryIndex = null;
        if (result.TryGetProperty("address_registry_diff", out var diffs))
        {
            var minIndex = int.MaxValue;
            foreach (var diff in diffs.EnumerateArray())
            {
                var addressHash = diff.RequiredString("address");
                var index = diff.RequiredInt32("index");

                var address = await Helpers.GetOrCreateXMichelsonAddress(addressHash);
                if (address.Index != null)
                {
                    if (address.Index != index)
                        throw new Exception("Address registry contains duplicates");

                    continue;
                }

                Db.TryAttach(address);
                address.Index = index;
                address.LastLevel = Context.Block.Level;
                address.LastTimestamp = Context.Block.Timestamp;

                if (index < minIndex)
                    minIndex = index;
            }

            if (minIndex != int.MaxValue)
                addressRegistryIndex = minIndex;
        }
        return addressRegistryIndex;
    }

    protected async Task RevertAddressRegistryDiffs(int? addressRegistryIndex)
    {
        if (addressRegistryIndex is int minIndex)
        {
            var addresses = await Db.Addresses
                .OfType<XMichelsonAddress>()
                .Where(x => x.ChainId == Context.Block.ChainId && x.Index != null && x.Index >= minIndex)
                .ToListAsync();

            foreach (var address in addresses)
            {
                Cache.Addresses.Add(address);
                address.Index = null;
            }
        }
    }
}
