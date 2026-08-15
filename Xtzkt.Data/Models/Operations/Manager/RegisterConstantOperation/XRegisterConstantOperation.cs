using Microsoft.EntityFrameworkCore;
using Xtzkt.Data.Models.Operations.Abstract;

namespace Xtzkt.Data.Models;

public class XRegisterConstantOperation() : RegisterConstantOperation(Layer.TezosX), IXManagerOperation
{
    public long DaFee { get; set; }
    public long GasFee { get; set; }
    public long GasRefund { get; set; }
}

public static class XRegisterConstantOperationModel
{
    public static void BuildXRegisterConstantOperationModel(this ModelBuilder modelBuilder)
    {
    }
}
