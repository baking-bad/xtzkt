using Microsoft.EntityFrameworkCore;

namespace Xtzkt.Data.Models;

public class L1RegisterConstantOperation() : RegisterConstantOperation(Layer.L1)
{
    public long BakerFee { get; set; }
}

public static class L1RegisterConstantOperationModel
{
    public static void BuildL1RegisterConstantOperationModel(this ModelBuilder modelBuilder)
    {
    }
}
