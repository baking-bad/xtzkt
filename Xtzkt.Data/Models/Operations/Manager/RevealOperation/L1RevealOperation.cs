using Microsoft.EntityFrameworkCore;

namespace Xtzkt.Data.Models;

public class L1RevealOperation() : RevealOperation(Layer.L1)
{
    public long BakerFee { get; set; }
}

public static class L1RevealOperationModel
{
    public static void BuildL1RevealOperationModel(this ModelBuilder modelBuilder)
    {
    }
}
