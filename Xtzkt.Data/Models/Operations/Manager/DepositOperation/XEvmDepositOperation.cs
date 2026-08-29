using System.ComponentModel.DataAnnotations.Schema;
using System.Numerics;
using Microsoft.EntityFrameworkCore;
using Xtzkt.Data.Models.Operations.Abstract;

namespace Xtzkt.Data.Models;

public class XEvmDepositOperation() : DepositOperation(Runtime.Evm), IParentOperation, ISourceOperation, ILogsOperation
{
    [Column($"{nameof(Amount)}18")]
    public required BigInteger Amount { get; set; }
    public byte[]? TicketHash { get; set; }
    public int? ProxyId { get; set; }
    public BigInteger? DepositId { get; set; }

    // id of the claim transaction (for queued deposits only)
    public long? ClaimTransactionId { get; set; }

    public int? SubsCounter { get; set; }
    public int? LogsCount { get; set; }
    public int? BridgeTicketTransfers { get; set; }

    public int GasUsed { get; set; }

    #region crutch for nested proxy calls in old etherlink
    [NotMapped]
    public int SenderId { get; set; }

    [NotMapped]
    public int Counter { get; set; }

    [NotMapped]
    public int? InternalOperations { get; set; }
    #endregion
}

public static class XEvmDepositOperationModel
{
    public static void BuildXEvmDepositOperationModel(this ModelBuilder modelBuilder)
    {
        #region indexes
        modelBuilder.Entity<XEvmDepositOperation>()
            .HasIndex(x => x.DepositId, $"IX_{nameof(XtzktContext.DepositOps)}_{nameof(XEvmDepositOperation.DepositId)}_Partial")
            .HasFilter($@"""{nameof(XEvmDepositOperation.DepositId)}"" is not null");
        #endregion
    }
}
