using System.Text.Json;
using Xtzkt.Data.Models;
using Xtzkt.Indexers.Common.Extensions;

namespace Xtzkt.Indexers.L1.Protocols.Proto05
{
    class Diagnostics(ProtocolHandler handler) : Proto01.Diagnostics(handler)
    {
        protected override void TestDelegatorsCount(JsonElement remote, L1Baker local)
        {
            var delegators = remote.RequiredArray("delegated_contracts").Count();

            if (delegators != local.DelegatorsCount && delegators != local.DelegatorsCount + 1)
                throw new Exception($"Diagnostics failed: wrong delegators count {local.Hash}");
        }

        protected override void TestAddressBaker(JsonElement remote, L1Address local)
        {
            if (local.Type == AddressType.L1Baker)
                return;
             
            var baker = Cache.Addresses.GetBakerOrDefault(local.BakerId);
            if (baker?.Hash != remote.OptionalString("delegate"))
                throw new Exception($"Diagnostics failed: wrong baker {local.Hash}");
        }

        protected override void TestAddressCounter(JsonElement remote, L1Address local)
        {
            if (local.Type == AddressType.L1Contract) return;

            if (remote.RequiredInt64("balance") > 0 && remote.RequiredInt32("counter") != local.Counter)
                throw new Exception($"Diagnostics failed: wrong counter {local.Hash}");
        }
    }
}
