using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;
using Xtzkt.Data.Models.Operations.Abstract;

namespace Xtzkt.Data.Models;

public class XMichelsonTransactionOperation() : MichelsonTransactionOperation(Direction.XMichelson), IXManagerOperation
{
    [Column(nameof(DaFee))]
    public long DaFee { get; set; }

    [Column(nameof(GasFee))]
    public long GasFee { get; set; }

    [Column(nameof(GasRefund))]
    public long GasRefund { get; set; }
}

public static class XMichelsonTransactionOperationModel
{
    public static void BuildXMichelsonTransactionOperationModel(this ModelBuilder modelBuilder)
    {
    }
}
