using Microsoft.EntityFrameworkCore;

namespace Xtzkt.Data.Models;

public class Eip7702Delegation
{
    public required long Id { get; set; }
    public required int ChainId { get; set; }
    public required int Level { get; set; }
    public required DateTime Timestamp { get; set; }
    public required long TransactionId { get; set; }
    public required int SenderId { get; set; }
    public required int AuthorityId { get; set; }
    public required int Nonce { get; set; }
    public int? PrevDelegateId { get; set; }
    public int? DelegateId { get; set; }
}

public static class Eip7702DelegationModel
{
    public static void BuildEip7702DelegationModel(this ModelBuilder modelBuilder)
    {
        #region keys
        modelBuilder.Entity<Eip7702Delegation>()
            .HasKey(x => x.Id);
        #endregion

        #region indexes
        modelBuilder.Entity<Eip7702Delegation>()
            .HasIndex(x => new { x.TransactionId, x.Id });
        #endregion
    }
}
