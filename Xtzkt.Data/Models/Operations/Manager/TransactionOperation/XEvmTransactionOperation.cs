using System.ComponentModel.DataAnnotations.Schema;
using System.Numerics;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;
using Xtzkt.Data.Models.Operations.Abstract;

namespace Xtzkt.Data.Models;

public class XEvmTransactionOperation() : TransactionOperation(Direction.XEvm), IParentOperation
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
    public BigInteger? DaFee { get; set; } // null for internal operations

    [Column($"{nameof(GasFee)}18")]
    public BigInteger? GasFee { get; set; } // null for internal operations


    [Column($"{nameof(Amount)}18")]
    public BigInteger Amount { get; set; }


    [Column(nameof(Input))]
    public byte[]? Input { get; set; }

    [Column(nameof(Output))]
    public byte[]? Output { get; set; }

    [Column(nameof(Result))]
    public string? Result { get; set; }


    [Column(nameof(Eip7702DelegationCount))]
    public int? Eip7702DelegationCount { get; set; }

    [Column(nameof(BridgeTicketTransfers))]
    public int? BridgeTicketTransfers { get; set; }

    // id of the deposit operation this operation claims
    [Column(nameof(ClaimDepositId))]
    public long? ClaimDepositId { get; set; }

    #region binary writer
    public static void Write(NpgsqlConnection conn, IEnumerable<XEvmTransactionOperation> ops)
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
                "{nameof(Amount)}18",
                "{nameof(Input)}",
                "{nameof(Output)}",
                "{nameof(Result)}",
                "{nameof(Eip7702DelegationCount)}",
                "{nameof(BridgeTicketTransfers)}",
                "{nameof(ClaimDepositId)}"
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
            writer.WriteNullable(op.DaFee, NpgsqlDbType.Numeric);
            writer.WriteNullable(op.GasFee, NpgsqlDbType.Numeric);
            writer.Write(op.Amount, NpgsqlDbType.Numeric);
            writer.WriteNullable(op.Input, NpgsqlDbType.Bytea);
            writer.WriteNullable(op.Output, NpgsqlDbType.Bytea);
            writer.WriteNullable(op.Result, NpgsqlDbType.Jsonb);
            writer.WriteNullable(op.Eip7702DelegationCount, NpgsqlDbType.Integer);
            writer.WriteNullable(op.BridgeTicketTransfers, NpgsqlDbType.Integer);
            writer.WriteNullable(op.ClaimDepositId, NpgsqlDbType.Bigint);
        }

        writer.Complete();
    }
    #endregion
}

public enum EvmOpType
{
    Legacy,
    AccessList,
    DynamicFee,
    Blob,
    SetCode,
    Trace = 255
}

public enum EvmOpCode
{
    Create,
    Create2,
    Call,
    CallCode,
    DelegateCall,
    StaticCall,
    SelfDestruct,
    Suicide,
}

public static class XEvmTransactionOperationModel
{
    public static void BuildXEvmTransactionOperationModel(this ModelBuilder modelBuilder)
    {
        #region props
        modelBuilder.Entity<XEvmTransactionOperation>()
            .Property(x => x.Result)
            .HasColumnType("jsonb");
        #endregion
    }
}
