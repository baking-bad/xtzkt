using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Xtzkt.Data.Models;
using Xtzkt.Indexers.Common.Extensions;

namespace Xtzkt.Indexers.L1.Protocols.Proto24
{
    class TransactionsCommit(ProtocolHandler protocol) : Proto14.TransactionsCommit(protocol)
    {
        protected override async Task ApplyAddressRegistryDiffs(L1TransactionOperation transaction, JsonElement result)
        {
            if (result.TryGetProperty("address_registry_diff", out var diffs))
            {
                var minIndex = int.MaxValue;
                foreach (var diff in diffs.EnumerateArray())
                {
                    var addressHash = diff.RequiredString("address");
                    var index = diff.RequiredInt32("index");

                    var address = await Cache.Addresses.GetOrCreateAsync(addressHash, Context.Block);
                    if (address.Index != null)
                    {
                        if (address.Index != index)
                            throw new Exception("Address registry contains duplicates");

                        continue;
                    }

                    Db.TryAttach(address);
                    address.Index = index;

                    if (index < minIndex)
                        minIndex = index;
                }

                if (minIndex != int.MaxValue)
                    transaction.AddressRegistryIndex = minIndex;
            }
        }

        protected override async Task RevertAddressRegistryDiffs(L1TransactionOperation transaction)
        {
            if (transaction.AddressRegistryIndex is int minIndex)
            {
                var addresses = await Db.Addresses
                    .OfType<L1Address>()
                    .Where(x => x.ChainId == Cache.Chain.Get().Id && x.Index != null && x.Index >= minIndex)
                    .ToListAsync();

                foreach (var address in addresses)
                {
                    Cache.Addresses.Add(address);
                    address.Index = null;
                }
            }
        }
    }
}
