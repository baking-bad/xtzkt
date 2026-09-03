using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Xtzkt.Data.Models;

public class XMichelsonDepositOperation() : DepositOperation(Runtime.Michelson)
{
    [Column(nameof(Amount))]
    public required long Amount { get; set; }
}

public static class XMichelsonDepositOperationModel
{
    public static void BuildXMichelsonDepositOperationModel(this ModelBuilder modelBuilder)
    {
    }
}
