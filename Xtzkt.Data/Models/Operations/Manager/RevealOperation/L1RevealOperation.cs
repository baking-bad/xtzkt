using Microsoft.EntityFrameworkCore;
using Xtzkt.Data.Models.Operations.Abstract;

namespace Xtzkt.Data.Models;

public class L1RevealOperation() : RevealOperation(Layer.L1), IL1ManagerOperation
{
    public long BakerFee { get; set; }

    #region IL1ManagerOperation
    int? IL1ManagerOperation.GasLimit { get => GasLimit; set => GasLimit = value ?? throw new InvalidOperationException($"{nameof(GasLimit)} cannot be null"); }
    long? IL1ManagerOperation.BakerFee { get => BakerFee; set => BakerFee = value ?? throw new InvalidOperationException($"{nameof(BakerFee)} cannot be null"); }
    #endregion
}

public static class L1RevealOperationModel
{
    public static void BuildL1RevealOperationModel(this ModelBuilder modelBuilder)
    {
    }
}
