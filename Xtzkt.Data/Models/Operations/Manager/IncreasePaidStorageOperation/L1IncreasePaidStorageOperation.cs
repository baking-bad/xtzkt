using Microsoft.EntityFrameworkCore;

namespace Xtzkt.Data.Models;

public class L1IncreasePaidStorageOperation() : IncreasePaidStorageOperation(Layer.L1)
{
    public long BakerFee { get; set; }
}

public static class L1IncreasePaidStorageOperationModel
{
    public static void BuildL1IncreasePaidStorageOperationModel(this ModelBuilder modelBuilder)
    {
    }
}
