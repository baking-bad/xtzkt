using Microsoft.EntityFrameworkCore;
using Xtzkt.Data.Models.Operations.Abstract;

namespace Xtzkt.Data.Models;

public class XMichelsonOriginationOperation() : MichelsonOriginationOperation(Env.XMichelson), IXManagerOperation
{
    public long DaFee { get; set; }
    public long GasFee { get; set; }
    public long GasRefund { get; set; }
}

public static class XMichelsonOriginationOperationModel
{
    public static void BuildXMichelsonOriginationOperationModel(this ModelBuilder modelBuilder)
    {
    }
}
