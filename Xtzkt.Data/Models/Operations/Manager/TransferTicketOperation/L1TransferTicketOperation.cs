using Microsoft.EntityFrameworkCore;

namespace Xtzkt.Data.Models;

public class L1TransferTicketOperation() : TransferTicketOperation(Layer.L1)
{
    public long BakerFee { get; set; }
}

public static class L1TransferTicketOperationModel
{
    public static void BuildL1TransferTicketOperationModel(this ModelBuilder modelBuilder)
    {
    }
}
