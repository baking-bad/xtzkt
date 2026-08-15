using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace Xtzkt.Data.Models;

public class XMichelsonUser() : XMichelsonAddress(AddressType.XMichelsonUser)
{
    [Column(nameof(Counter))]
    public int Counter { get; set; }

    [Column(nameof(Revealed))]
    public bool Revealed { get; set; }

    [Column(nameof(PublicKey))]
    public string? PublicKey { get; set; }

    #region counters
    [Column(nameof(RevealsCount))]
    public int RevealsCount { get; set; }

    [Column(nameof(RegisterConstantsCount))]
    public int RegisterConstantsCount { get; set; }
    #endregion

    public override bool IsEmpty() => 
        base.IsEmpty() &&
        RevealsCount == 0 &&
        RegisterConstantsCount == 0;
}

public static class XMichelsonUserModel
{
    public static void BuildXMichelsonUserModel(this ModelBuilder modelBuilder)
    {
    }
}
