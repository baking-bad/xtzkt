using System.ComponentModel.DataAnnotations.Schema;
using System.Numerics;
using Microsoft.EntityFrameworkCore;

namespace Xtzkt.Data.Models;

public class XEvmDepositOperation() : DepositOperation(Runtime.Evm)
{
    [Column($"{nameof(Amount)}18")]
    public required BigInteger Amount { get; set; }
    public byte[]? TicketHash { get; set; }
    public int? ProxyId { get; set; }
    public BigInteger? DepositId { get; set; }
}

public static class XEvmDepositOperationModel
{
    public static void BuildXEvmDepositOperationModel(this ModelBuilder modelBuilder)
    {
    }
}
