using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;
using System.ComponentModel.DataAnnotations.Schema;
using System.Numerics;
using Xtzkt.Data.Models.Operations.Abstract;

namespace Xtzkt.Data.Models;

public class XMichelsonEvmTransactionOperation() : TransactionOperation(Direction.XMichelsonEvm), IXManagerOperation
{
    [Column(nameof(DaFee), Order = 13)]
    public long? DaFee { get; set; } // null for internal operations

    [Column(nameof(GasFee), Order = 14)]
    public long? GasFee { get; set; } // null for internal operations

    [Column(nameof(GasFeeRefunded), Order = 15)]
    public long? GasFeeRefunded { get; set; } // null for internal operations


    [Column(nameof(StorageFee), Order = 9)]
    public long? StorageFee { get; set; }

    [Column(nameof(AllocationFee), Order = 10)]
    public long? AllocationFee { get; set; }

    [Column(nameof(StorageLimit), Order = 22)]
    public int? StorageLimit { get; set; } // null for internal operations

    [Column(nameof(StorageUsed), Order = 23)]
    public int StorageUsed { get; set; }

    [Column(nameof(Nonce), Order = 18)]
    public int? Nonce { get; set; }


    [Column(nameof(MichelsonTransactionOperation.Amount), Order = 8)]
    public long AmountSent { get; set; }

    [Column($"{nameof(XEvmTransactionOperation.Amount)}18")]
    public BigInteger AmountReceived { get; set; }


    [Column(nameof(Input))]
    public byte[]? Input { get; set; }

    [Column(nameof(Output))]
    public byte[]? Output { get; set; }

    [Column(nameof(Result))]
    public string? Result { get; set; }

    [Column(nameof(BridgeTicketTransfers), Order = 30)]
    public int? BridgeTicketTransfers { get; set; }

    // id of the deposit operation this operation claims
    [Column(nameof(XEvmTransactionOperation.ClaimDepositId), Order = 16)]
    public long? ClaimDepositId { get; set; }


    [Column(nameof(AliasId), Order = 34)]
    public int AliasId { get; set; }

    [Column(nameof(GatewayId), Order = 35)]
    public int GatewayId { get; set; }

    [Column(nameof(GatewayEntrypoint))]
    public string? GatewayEntrypoint { get; set; }

    [Column(nameof(GatewayParameters))]
    public string? GatewayParameters { get; set; }

    [Column(nameof(GatewayParametersRaw))]
    public byte[]? GatewayParametersRaw { get; set; }

    #region binary writer
    public static void Write(NpgsqlConnection conn, IEnumerable<XMichelsonEvmTransactionOperation> ops)
    {
        using var writer = conn.BeginBinaryImport($"""
            COPY "{nameof(XtzktContext.TransactionOps)}" (
                {BinaryColumns},
                "{nameof(DaFee)}",
                "{nameof(GasFee)}",
                "{nameof(GasFeeRefunded)}",
                "{nameof(StorageFee)}",
                "{nameof(AllocationFee)}",
                "{nameof(StorageLimit)}",
                "{nameof(StorageUsed)}",
                "{nameof(Nonce)}",
                "{nameof(MichelsonTransactionOperation.Amount)}",
                "{nameof(XEvmTransactionOperation.Amount)}18",
                "{nameof(Input)}",
                "{nameof(Output)}",
                "{nameof(Result)}",
                "{nameof(BridgeTicketTransfers)}",
                "{nameof(ClaimDepositId)}",
                "{nameof(AliasId)}",
                "{nameof(GatewayId)}",
                "{nameof(GatewayEntrypoint)}",
                "{nameof(GatewayParameters)}",
                "{nameof(GatewayParametersRaw)}"
            )
            FROM STDIN (FORMAT BINARY)
            """);

        foreach (var op in ops)
        {
            op.WriteBinaryBase(writer);

            writer.WriteNullable(op.DaFee, NpgsqlDbType.Bigint);
            writer.WriteNullable(op.GasFee, NpgsqlDbType.Bigint);
            writer.WriteNullable(op.GasFeeRefunded, NpgsqlDbType.Bigint);
            writer.WriteNullable(op.StorageFee, NpgsqlDbType.Bigint);
            writer.WriteNullable(op.AllocationFee, NpgsqlDbType.Bigint);
            writer.WriteNullable(op.StorageLimit, NpgsqlDbType.Integer);
            writer.Write(op.StorageUsed, NpgsqlDbType.Integer);
            writer.WriteNullable(op.Nonce, NpgsqlDbType.Integer);
            writer.Write(op.AmountSent, NpgsqlDbType.Bigint);
            writer.Write(op.AmountReceived, NpgsqlDbType.Numeric);
            writer.WriteNullable(op.Input, NpgsqlDbType.Bytea);
            writer.WriteNullable(op.Output, NpgsqlDbType.Bytea);
            writer.WriteNullable(op.Result, NpgsqlDbType.Jsonb);
            writer.WriteNullable(op.BridgeTicketTransfers, NpgsqlDbType.Integer);
            writer.WriteNullable(op.ClaimDepositId, NpgsqlDbType.Bigint);
            writer.Write(op.AliasId, NpgsqlDbType.Integer);
            writer.Write(op.GatewayId, NpgsqlDbType.Integer);
            writer.WriteNullable(op.GatewayEntrypoint, NpgsqlDbType.Text);
            writer.WriteNullable(op.GatewayParameters, NpgsqlDbType.Jsonb);
            writer.WriteNullable(op.GatewayParametersRaw, NpgsqlDbType.Bytea);
        }

        writer.Complete();
    }
    #endregion
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
