using System.ComponentModel.DataAnnotations.Schema;
using System.Numerics;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;
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

    #region binary writer
    public static void Write(NpgsqlConnection conn, IEnumerable<XEvmMichelsonTransactionOperation> ops)
    {
        using var writer = conn.BeginBinaryImport($"""
            COPY "{nameof(XtzktContext.TransactionOps)}" (
                {BinaryColumns},
                "{nameof(OpType)}",
                "{nameof(OpCode)}",
                "{nameof(GasPrice)}",
                "{nameof(MaxFeePerGas)}",
                "{nameof(MaxPriorityFeePerGas)}",
                "{nameof(EffectiveGasPrice)}",
                "{nameof(DaFee)}18",
                "{nameof(GasFee)}18",
                "{nameof(XEvmTransactionOperation.Amount)}18",
                "{nameof(RoundingLoss)}",
                "{nameof(XMichelsonTransactionOperation.Amount)}",
                "{nameof(StorageId)}",
                "{nameof(BigMapUpdates)}",
                "{nameof(TicketTransfers)}",
                "{nameof(AddressRegistryIndex)}",
                "{nameof(ParametersRaw)}",
                "{nameof(AliasId)}",
                "{nameof(GatewayId)}",
                "{nameof(GatewayEntrypoint)}",
                "{nameof(GatewayParameters)}",
                "{nameof(GatewayInput)}",
                "{nameof(Eip7702DelegationCount)}"
            )
            FROM STDIN (FORMAT BINARY)
            """);

        foreach (var op in ops)
        {
            op.WriteBinaryBase(writer);

            writer.Write((int)op.OpType, NpgsqlDbType.Integer);
            writer.Write((int)op.OpCode, NpgsqlDbType.Integer);
            writer.WriteNullable(op.GasPrice, NpgsqlDbType.Numeric);
            writer.WriteNullable(op.MaxFeePerGas, NpgsqlDbType.Numeric);
            writer.WriteNullable(op.MaxPriorityFeePerGas, NpgsqlDbType.Numeric);
            writer.WriteNullable(op.EffectiveGasPrice, NpgsqlDbType.Numeric);
            writer.Write(op.DaFee, NpgsqlDbType.Numeric);
            writer.Write(op.GasFee, NpgsqlDbType.Numeric);
            writer.Write(op.AmountSent, NpgsqlDbType.Numeric);
            writer.Write(op.RoundingLoss, NpgsqlDbType.Numeric);
            writer.Write(op.AmountReceived, NpgsqlDbType.Bigint);
            writer.WriteNullable(op.StorageId, NpgsqlDbType.Bigint);
            writer.WriteNullable(op.BigMapUpdates, NpgsqlDbType.Integer);
            writer.WriteNullable(op.TicketTransfers, NpgsqlDbType.Integer);
            writer.WriteNullable(op.AddressRegistryIndex, NpgsqlDbType.Integer);
            writer.WriteNullable(op.ParametersRaw, NpgsqlDbType.Bytea);
            writer.Write(op.AliasId, NpgsqlDbType.Integer);
            writer.Write(op.GatewayId, NpgsqlDbType.Integer);
            writer.WriteNullable(op.GatewayEntrypoint, NpgsqlDbType.Text);
            writer.WriteNullable(op.GatewayParameters, NpgsqlDbType.Jsonb);
            writer.WriteNullable(op.GatewayInput, NpgsqlDbType.Bytea);
            writer.WriteNullable(op.Eip7702DelegationCount, NpgsqlDbType.Integer);
        }

        writer.Complete();
    }
    #endregion
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
