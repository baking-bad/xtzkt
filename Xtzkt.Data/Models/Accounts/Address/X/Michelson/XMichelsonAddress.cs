using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace Xtzkt.Data.Models;

public abstract class XMichelsonAddress(AddressType type) : XAddress(Runtime.Michelson, type)
{
    [Column(nameof(Balance))]
    public long Balance { get; set; }

    [Column(nameof(Index))]
    public int? Index { get; set; }

    #region counters
    [Column(nameof(TransferTicketCount))]
    public int TransferTicketCount { get; set; }

    [Column(nameof(IncreasePaidStorageCount))]
    public int IncreasePaidStorageCount { get; set; }
    #endregion

    #region helpers
    public override bool IsEmpty() =>
        base.IsEmpty() &&
        Index == null &&
        TransferTicketCount == 0 &&
        IncreasePaidStorageCount == 0;
    #endregion
}

public static class XMichelsonAddressModel
{
    public static void BuildXMichelsonAddressModel(this ModelBuilder modelBuilder)
    {
        #region inheritance
        // enable OfType<T>()
        modelBuilder.Entity<XMichelsonAddress>();
        #endregion
    }
}
