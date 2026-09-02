using Microsoft.EntityFrameworkCore;
using Xtzkt.Data.Models.Operations.Abstract;

namespace Xtzkt.Data.Models;

public class XTransferTicketOperation() : TransferTicketOperation(Layer.TezosX), IXManagerOperation
{
    public long DaFee { get; set; }
    public long GasFee { get; set; }
    public long GasRefund { get; set; }

    #region IXManagerOperation
    // this operation is always external, so none of these fields is ever null
    int? IXManagerOperation.GasLimit { get => GasLimit; set => GasLimit = value ?? throw new InvalidOperationException($"{nameof(GasLimit)} cannot be null"); }
    long? IXManagerOperation.DaFee { get => DaFee; set => DaFee = value ?? throw new InvalidOperationException($"{nameof(DaFee)} cannot be null"); }
    long? IXManagerOperation.GasFee { get => GasFee; set => GasFee = value ?? throw new InvalidOperationException($"{nameof(GasFee)} cannot be null"); }
    long? IXManagerOperation.GasRefund { get => GasRefund; set => GasRefund = value ?? throw new InvalidOperationException($"{nameof(GasRefund)} cannot be null"); }
    #endregion
}

public static class XTransferTicketOperationModel
{
    public static void BuildXTransferTicketOperationModel(this ModelBuilder modelBuilder)
    {
    }
}
