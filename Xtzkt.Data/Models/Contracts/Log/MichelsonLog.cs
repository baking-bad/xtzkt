using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace Xtzkt.Data.Models;

public class MichelsonLog() : Log(Runtime.Michelson)
{
    [Column(nameof(TransactionId))]
    public required long TransactionId { get; set; }

    public byte[]? Type { get; set; }
    public byte[]? PayloadRaw { get; set; }
}

public static class MichelsonLogModel
{
    public static void BuildMichelsonLogModel(this ModelBuilder modelBuilder)
    {
    }
}
