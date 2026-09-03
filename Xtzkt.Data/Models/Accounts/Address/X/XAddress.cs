using Microsoft.EntityFrameworkCore;

namespace Xtzkt.Data.Models;

public abstract class XAddress(Runtime runtime, AddressType type) : Address(Layer.TezosX, runtime, type)
{
    #region counters
    public int AliasesCount { get; set; }
    public int DepositOpsCount { get; set; }
    #endregion

    #region helpers
    public override bool IsEmpty() =>
        base.IsEmpty() &&
        DepositOpsCount == 0;
    #endregion
}

public static class XAddressModel
{
    public static void BuildXAddressModel(this ModelBuilder modelBuilder)
    {
        #region inheritance
        // enable OfType<T>()
        modelBuilder.Entity<XAddress>();
        #endregion
    }
}
