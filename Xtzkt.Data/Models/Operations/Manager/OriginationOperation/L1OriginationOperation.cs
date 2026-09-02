using Microsoft.EntityFrameworkCore;
using Xtzkt.Data.Models.Operations.Abstract;

namespace Xtzkt.Data.Models;

public class L1OriginationOperation() : MichelsonOriginationOperation(Env.L1)
{
    public long? BakerFee { get; set; } // null for internal operations
    public int? BakerId { get; set; }
}

public static class L1OriginationOperationModel
{
    public static void BuildL1OriginationOperationModel(this ModelBuilder modelBuilder)
    {
        #region indexes
        modelBuilder.Entity<L1OriginationOperation>()
            .HasIndex(x => x.BakerId, $"IX_{nameof(XtzktContext.OriginationOps)}_{nameof(L1OriginationOperation.BakerId)}_Partial")
            .HasFilter($@"""{nameof(L1OriginationOperation.Status)}"" = {(int)OperationStatus.Applied}");
        #endregion
    }
}
