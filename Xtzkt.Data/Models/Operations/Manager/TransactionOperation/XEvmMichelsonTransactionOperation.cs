using System.ComponentModel.DataAnnotations.Schema;
using System.Numerics;
using Microsoft.EntityFrameworkCore;
using Xtzkt.Data.Models.Operations.Abstract;

namespace Xtzkt.Data.Models;

public class XEvmMichelsonTransactionOperation() : TransactionOperation(Direction.XEvmMichelson), IBigmapOperation
{
    [Column(nameof(OpType))]
    public EvmOpType OpType { get; set; }

    [Column(nameof(OpCode))]
    public EvmOpCode OpCode { get; set; }

    [Column(nameof(GasPrice))]
    public BigInteger? GasPrice { get; set; }

    [Column(nameof(MaxFeePerGas))]
    public BigInteger? MaxFeePerGas { get; set; }

    [Column(nameof(MaxPriorityFeePerGas))]
    public BigInteger? MaxPriorityFeePerGas { get; set; }

    [Column(nameof(EffectiveGasPrice))]
    public BigInteger? EffectiveGasPrice { get; set; }

    [Column($"{nameof(DaFee)}18")]
    public BigInteger DaFee { get; set; }

    [Column($"{nameof(GasFee)}18")]
    public BigInteger GasFee { get; set; }


    [Column($"{nameof(XEvmTransactionOperation.Amount)}18")]
    public BigInteger AmountSent { get; set; }

    public BigInteger RoundingLoss { get; set; }

    [Column(nameof(XMichelsonTransactionOperation.Amount))]
    public long AmountReceived { get; set; }


    [Column(nameof(StorageId))]
    public long? StorageId { get; set; }

    [Column(nameof(BigMapUpdates))]
    public int? BigMapUpdates { get; set; }

    [Column(nameof(TicketTransfers))]
    public int? TicketTransfers { get; set; }

    [Column(nameof(AddressRegistryIndex))]
    public int? AddressRegistryIndex { get; set; }

    [Column(nameof(ParametersRaw))]
    public byte[]? ParametersRaw { get; set; }


    [Column(nameof(AliasId))]
    public int AliasId { get; set; }

    [Column(nameof(GatewayId))]
    public int GatewayId { get; set; }

    [Column(nameof(GatewayEntrypoint))]
    public string? GatewayEntrypoint { get; set; }

    [Column(nameof(GatewayParameters))]
    public string? GatewayParameters { get; set; }

    [Column(nameof(GatewayInput))]
    public byte[]? GatewayInput { get; set; }


    [Column(nameof(Eip7702DelegationCount))]
    public int? Eip7702DelegationCount { get; set; }
}

public static class XEvmMichelsonTransactionOperationModel
{
    public static void BuildXEvmMichelsonTransactionOperationModel(this ModelBuilder modelBuilder)
    {
        #region props
        modelBuilder.Entity<XEvmMichelsonTransactionOperation>()
            .Property(x => x.GatewayParameters)
            .HasColumnType("jsonb");
        #endregion
    }
}
