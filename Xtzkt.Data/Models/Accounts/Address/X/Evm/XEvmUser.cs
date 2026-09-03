using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace Xtzkt.Data.Models;

public class XEvmUser() : XEvmAddress(AddressType.XEvmUser)
{
    [Column(nameof(Eip7702DelegateId))]
    public int? Eip7702DelegateId { get; set; }
}

public static class XEvmUserModel
{
    public static void BuildXEvmUserModel(this ModelBuilder modelBuilder)
    {
    }
}
