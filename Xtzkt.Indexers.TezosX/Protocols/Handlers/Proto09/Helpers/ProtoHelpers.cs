using System.Numerics;
using System.Text.Json;
using Xtzkt.Indexers.Common.Extensions;

namespace Xtzkt.Indexers.TezosX.Protocols.Proto09.Helpers;

class ProtoHelpers(ProtocolHandler protocol) : Proto07.Helpers.ProtoHelpers(protocol)
{
    #region fees
    protected override BigInteger GetDaFee(JsonElement tx)
    {
        var size = 150
            + tx.RequiredHexBytes("input").Length
            + (tx.OptionalArray("accessList")?.EnumerateArray().Sum(x => 20 + 32 * x.RequiredArray("storageKeys").Count()) ?? 0)
            + 125 * (tx.OptionalArray("authorizationList")?.Count() ?? 0); // 125 - size of EIP7702 authorization

        return size * Context.Protocol.DaFeePerByte18;
    }

    public override BigInteger GetGasFee(BigInteger effectiveGasPrice, int gasUsed, BigInteger daFee)
    {
        if (daFee.IsZero)
            return effectiveGasPrice * gasUsed;

        if (effectiveGasPrice.IsZero)
            return BigInteger.Zero;

        // gasUsed covers the gas reserved for the da fee, rounded up to a whole gas unit,
        // but the remainder of that rounding is not charged to the sender
        return effectiveGasPrice * (gasUsed - GetDaGas(effectiveGasPrice, daFee));
    }
    #endregion
}
