using Microsoft.EntityFrameworkCore;

namespace Xtzkt.Data.Models;

public class XChain() : Chain(Layer.TezosX)
{
    public required string RollupAddress { get; set; }
    public required string Kernel { get; set; }
    public string? KernelUpgrade { get; set; }
    public DateTime? KernelUpgradeTime { get; set; }

    public int? MichelsonActivationLevel { get; set; }
    public string? MichelsonChainId { get; set; }
    public string? MichelsonProtocol { get; set; }
    public string? MichelsonBlock { get; set; }

    #region counts
    public long DepositOpsCount { get; set; }
    public int Eip7702DelegationCount { get; set; }

    public int BridgeTicketsCount { get; set; }
    public int BridgeTicketBalancesCount { get; set; }
    public int BridgeTicketTransfersCount { get; set; }
    #endregion
}

public static class XChainModel
{
    public static void BuildXChainModel(this ModelBuilder modelBuilder)
    {
    }
}
