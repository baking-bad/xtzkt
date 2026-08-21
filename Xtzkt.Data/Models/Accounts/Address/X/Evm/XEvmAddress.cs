using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;
using System.Numerics;

namespace Xtzkt.Data.Models;

public abstract class XEvmAddress(AddressType type) : XAddress(Runtime.Evm, type)
{
    [Column(nameof(Counter))]
    public int Counter { get; set; }

    [Column($"{nameof(Balance)}18")]
    public BigInteger Balance { get; set; }

    [Column(nameof(BlocksCount))]
    public int BlocksCount { get; set; }

    [Column(nameof(Eip7702DelegationCount))]
    public int Eip7702DelegationCount { get; set; }

    [Column(nameof(LogsCount))]
    public int LogsCount { get; set; }

    public int ActiveBridgeTicketsCount { get; set; }
    public int BridgeTicketBalancesCount { get; set; }
    public int BridgeTicketTransfersCount { get; set; }

    #region helpers
    public override bool IsEmpty() =>
        base.IsEmpty() &&
        BlocksCount == 0 &&
        Eip7702DelegationCount == 0 &&
        LogsCount == 0 &&
        BridgeTicketTransfersCount == 0;
    #endregion
}

public static class XEvmAddressModel
{
    public static void BuildXEvmAddressModel(this ModelBuilder modelBuilder)
    {
        #region inheritance
        // enable OfType<T>()
        modelBuilder.Entity<XEvmAddress>();
        #endregion
    }
}