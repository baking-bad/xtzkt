using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;
using System.Numerics;
using Xtzkt.Data.Models.Operations.Abstract;

namespace Xtzkt.Data.Models;

public class XMichelsonEvmTransactionOperation() : TransactionOperation(Direction.XMichelsonEvm), IXManagerOperation
{
    [Column(nameof(DaFee))]
    public long DaFee { get; set; }

    [Column(nameof(GasFee))]
    public long GasFee { get; set; }

    [Column(nameof(GasRefund))]
    public long GasRefund { get; set; }


    [Column(nameof(StorageFee))]
    public long? StorageFee { get; set; }

    [Column(nameof(AllocationFee))]
    public long? AllocationFee { get; set; }

    [Column(nameof(StorageLimit))]
    public int StorageLimit { get; set; }

    [Column(nameof(StorageUsed))]
    public int StorageUsed { get; set; }

    [Column(nameof(Nonce))]
    public int? Nonce { get; set; }


    [Column(nameof(MichelsonTransactionOperation.Amount))]
    public long AmountSent { get; set; }

    [Column($"{nameof(XEvmTransactionOperation.Amount)}18")]
    public BigInteger AmountReceived { get; set; }


    [Column(nameof(Input))]
    public byte[]? Input { get; set; }

    [Column(nameof(Output))]
    public byte[]? Output { get; set; }

    [Column(nameof(Result))]
    public string? Result { get; set; }

    [Column(nameof(BridgeTicketTransfers))]
    public int? BridgeTicketTransfers { get; set; }

    // id of the deposit operation this operation claims
    [Column(nameof(XEvmTransactionOperation.ClaimDepositId))]
    public long? ClaimDepositId { get; set; }


    [Column(nameof(AliasId))]
    public int AliasId { get; set; }

    [Column(nameof(GatewayId))]
    public int GatewayId { get; set; }

    [Column(nameof(GatewayEntrypoint))]
    public string? GatewayEntrypoint { get; set; }

    [Column(nameof(GatewayParameters))]
    public string? GatewayParameters { get; set; }

    [Column(nameof(GatewayParametersRaw))]
    public byte[]? GatewayParametersRaw { get; set; }
}

public static class XMichelsonEvmTransactionOperationModel
{
    public static void BuildXMichelsonEvmTransactionOperationModel(this ModelBuilder modelBuilder)
    {
        #region props
        modelBuilder.Entity<XMichelsonEvmTransactionOperation>()
            .Property(x => x.GatewayParameters)
            .HasColumnType("jsonb");

        modelBuilder.Entity<XMichelsonEvmTransactionOperation>()
            .Property(x => x.Result)
            .HasColumnType("jsonb");
        #endregion
    }
}
