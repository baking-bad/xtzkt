using System.Numerics;
using System.Text.Json;
using Xtzkt.Indexers.Common.Extensions;

namespace Xtzkt.Indexers.TezosX.Protocols.Proto01.Helpers;

partial class ProtoHelpers
{
    public BigInteger GetDaFee(JsonElement tx, bool isDelayedOp)
    {
        return isDelayedOp ? BigInteger.Zero : GetDaFee(tx);
    }

    protected virtual BigInteger GetDaFee(JsonElement tx)
    {
        // the authorization list is not charged in this era, it starts being charged with Farfadet 6.2
        var size = 150
            + tx.RequiredHexBytes("input").Length
            + (tx.OptionalArray("accessList")?.EnumerateArray().Sum(x => 20 + 32 * x.RequiredArray("storageKeys").Count()) ?? 0);

        return size * Context.Protocol.DaFeePerByte18;
    }

    public virtual int GetDaGas(BigInteger effectiveGasPrice, BigInteger daFee)
    {
        if (daFee.IsZero || effectiveGasPrice.IsZero)
            return 0;

        // the kernel reserves whole gas units for the da fee: `ceil_div(exec * base + da, base)`
        return (int)((daFee + effectiveGasPrice - 1) / effectiveGasPrice);
    }

    public virtual BigInteger GetGasFee(BigInteger effectiveGasPrice, int gasUsed, BigInteger daFee)
    {
        // gasUsed already covers the da fee, rounded up to a whole gas unit
        return effectiveGasPrice * gasUsed - daFee;
    }
}
