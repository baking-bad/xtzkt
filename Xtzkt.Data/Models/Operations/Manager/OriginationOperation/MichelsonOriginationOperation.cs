using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Xtzkt.Data.Models.Operations.Abstract;

namespace Xtzkt.Data.Models;

public abstract class MichelsonOriginationOperation(Env env) : OriginationOperation(env), IContractOperation
{
    public long? StorageFee { get; set; }
    public long? AllocationFee { get; set; }
    public int StorageLimit { get; set; }
    public int StorageUsed { get; set; }
    public int? Nonce { get; set; }
    public long? StorageId { get; set; }
    public int? BigMapUpdates { get; set; }

    [Column(nameof(Balance))]
    public long Balance { get; set; }
}

public static class MichelsonOriginationOperationModel
{
    public static void BuildMichelsonOriginationOperationModel(this ModelBuilder modelBuilder)
    {
        #region inheritance
        // enable OfType<T>()
        modelBuilder.Entity<MichelsonOriginationOperation>();
        #endregion
    }
}
