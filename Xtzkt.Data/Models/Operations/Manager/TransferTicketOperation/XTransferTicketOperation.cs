using Microsoft.EntityFrameworkCore;
using Xtzkt.Data.Models.Operations.Abstract;

namespace Xtzkt.Data.Models;

public class XTransferTicketOperation() : TransferTicketOperation(Layer.TezosX), IXManagerOperation
{
    public long DaFee { get; set; }
    public long GasFee { get; set; }
    public long GasRefund { get; set; }
}

public static class XTransferTicketOperationModel
{
    public static void BuildXTransferTicketOperationModel(this ModelBuilder modelBuilder)
    {
    }
}
