using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace Xtzkt.Data.Models;

public class XMichelsonAlias() : XMichelsonAddress(AddressType.XMichelsonAlias)
{
    [Column(nameof(OwnerId))]
    public int OwnerId { get; set; }
}

public static class XMichelsonAliasModel
{
    public static void BuildXMichelsonAliasModel(this ModelBuilder modelBuilder)
    {
    }
}
