using System.Text.Json;
using Xtzkt.Indexers.Common.Extensions;

namespace Xtzkt.Indexers.TezosX.Utils;

public static class EvmGas
{
    // constants of EIP2, EIP2028, EIP2930, EIP3860, EIP7702
    const int BaseCost = 21_000;
    const int ZeroByteCost = 4;
    const int NonZeroByteCost = 16;
    const int CreationCost = 32_000;
    const int InitCodeWordCost = 2;
    const int WordSize = 32;
    const int AccessListAddressCost = 2_400;
    const int AccessListStorageKeyCost = 1_900;
    const int Eip7702EmptyAccountCost = 25_000;

    // the `initial_gas` term of revm's `calculate_initial_tx_gas`, not the EIP7623 `floor_gas` one
    public static int GetIntrinsicGas(JsonElement tx)
    {
        var gas = BaseCost;

        var input = tx.RequiredHexBytes("input");
        foreach (var b in input)
            gas += b == 0 ? ZeroByteCost : NonZeroByteCost;

        foreach (var item in tx.OptionalArray("accessList")?.EnumerateArray() ?? [])
            gas += AccessListAddressCost + AccessListStorageKeyCost * item.RequiredArray("storageKeys").Count();

        var authorizations = tx.OptionalArray("authorizationList")?.Count() ?? 0;
        gas += authorizations * Eip7702EmptyAccountCost;

        if (tx.OptionalString("to") == null)
            gas += CreationCost + InitCodeWordCost * ((input.Length + WordSize - 1) / WordSize);

        return gas;
    }
}
