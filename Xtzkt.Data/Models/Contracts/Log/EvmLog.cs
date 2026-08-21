using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace Xtzkt.Data.Models;

public class EvmLog() : Log(Runtime.Evm)
{
    [Column(nameof(TransactionId))]
    public long? TransactionId { get; set; }
    public long? OriginationId { get; set; }
    public long? DepositId { get; set; }

    public required byte[][] Topics { get; set; }
    public required byte[] Data { get; set; }
}

public static class EvmLogModel
{
    public static void BuildEvmLogModel(this ModelBuilder modelBuilder)
    {
    }
}
