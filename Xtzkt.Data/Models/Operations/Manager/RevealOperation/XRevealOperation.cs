using Microsoft.EntityFrameworkCore;
using Xtzkt.Data.Models.Operations.Abstract;

namespace Xtzkt.Data.Models;

public class XRevealOperation() : RevealOperation(Layer.TezosX), IXManagerOperation
{
    public long DaFee { get; set; }
    public long GasFee { get; set; }
    public long GasRefund { get; set; }
}

public static class XRevealOperationModel
{
    public static void BuildXRevealOperationModel(this ModelBuilder modelBuilder)
    {
    }
}
