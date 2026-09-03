using Microsoft.EntityFrameworkCore;
using Xtzkt.Data.Models.Operations.Abstract;

namespace Xtzkt.Data.Models;

public class XMichelsonOriginationOperation() : MichelsonOriginationOperation(Env.XMichelson), IXManagerOperation
{
    public long? DaFee { get; set; } // null for internal operations
    public long? GasFee { get; set; } // null for internal operations
    public long? GasFeeRefunded { get; set; } // null for internal operations
}

public static class XMichelsonOriginationOperationModel
{
    public static void BuildXMichelsonOriginationOperationModel(this ModelBuilder modelBuilder)
    {
    }
}
