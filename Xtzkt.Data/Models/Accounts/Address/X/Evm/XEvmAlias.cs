using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace Xtzkt.Data.Models;

public class XEvmAlias() : XEvmAddress(AddressType.XEvmAlias)
{
    [Column(nameof(OwnerId))]
    public int OwnerId { get; set; }

    [Column(nameof(Eip7702DelegateId))]
    public int? Eip7702DelegateId { get; set; }
}

public static class XEvmAliasModel
{
    public static void BuildXEvmAliasModel(this ModelBuilder modelBuilder)
    {
    }
}
