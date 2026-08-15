using Microsoft.EntityFrameworkCore;
using Xtzkt.Data.Models.Operations.Abstract;

namespace Xtzkt.Data.Models;

public class XIncreasePaidStorageOperation() : IncreasePaidStorageOperation(Layer.TezosX), IXManagerOperation
{
    public long DaFee { get; set; }
    public long GasFee { get; set; }
    public long GasRefund { get; set; }
}

public static class XIncreasePaidStorageOperationModel
{
    public static void BuildXIncreasePaidStorageOperationModel(this ModelBuilder modelBuilder)
    {
    }
}
