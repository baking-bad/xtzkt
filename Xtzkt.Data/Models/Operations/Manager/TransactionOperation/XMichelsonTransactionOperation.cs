using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;
using System.ComponentModel.DataAnnotations.Schema;
using Xtzkt.Data.Models.Operations.Abstract;

namespace Xtzkt.Data.Models;

public class XMichelsonTransactionOperation() : MichelsonTransactionOperation(Direction.XMichelson), IXManagerOperation
{
    [Column(nameof(DaFee))]
    public long? DaFee { get; set; } // null for internal operations

    [Column(nameof(GasFee))]
    public long? GasFee { get; set; } // null for internal operations

    [Column(nameof(GasRefund))]
    public long? GasRefund { get; set; } // null for internal operations

    #region binary writer
    public static void Write(NpgsqlConnection conn, IEnumerable<XMichelsonTransactionOperation> ops)
    {
        using var writer = conn.BeginBinaryImport($"""
            COPY "{nameof(XtzktContext.TransactionOps)}" (
                {BinaryColumns},
                "{nameof(StorageFee)}",
                "{nameof(AllocationFee)}",
                "{nameof(StorageLimit)}",
                "{nameof(StorageUsed)}",
                "{nameof(Nonce)}",
                "{nameof(Amount)}",
                "{nameof(StorageId)}",
                "{nameof(BigMapUpdates)}",
                "{nameof(TicketTransfers)}",
                "{nameof(AddressRegistryIndex)}",
                "{nameof(ParametersRaw)}",
                "{nameof(DaFee)}",
                "{nameof(GasFee)}",
                "{nameof(GasRefund)}"
            )
            FROM STDIN (FORMAT BINARY)
            """);

        foreach (var op in ops)
        {
            op.WriteBinaryBase(writer);

            writer.WriteNullable(op.StorageFee, NpgsqlDbType.Bigint);
            writer.WriteNullable(op.AllocationFee, NpgsqlDbType.Bigint);
            writer.WriteNullable(op.StorageLimit, NpgsqlDbType.Integer);
            writer.Write(op.StorageUsed, NpgsqlDbType.Integer);
            writer.WriteNullable(op.Nonce, NpgsqlDbType.Integer);
            writer.Write(op.Amount, NpgsqlDbType.Bigint);
            writer.WriteNullable(op.StorageId, NpgsqlDbType.Bigint);
            writer.WriteNullable(op.BigMapUpdates, NpgsqlDbType.Integer);
            writer.WriteNullable(op.TicketTransfers, NpgsqlDbType.Integer);
            writer.WriteNullable(op.AddressRegistryIndex, NpgsqlDbType.Integer);
            writer.WriteNullable(op.ParametersRaw, NpgsqlDbType.Bytea);
            writer.WriteNullable(op.DaFee, NpgsqlDbType.Bigint);
            writer.WriteNullable(op.GasFee, NpgsqlDbType.Bigint);
            writer.WriteNullable(op.GasRefund, NpgsqlDbType.Bigint);
        }

        writer.Complete();
    }
    #endregion
}

public static class XMichelsonTransactionOperationModel
{
    public static void BuildXMichelsonTransactionOperationModel(this ModelBuilder modelBuilder)
    {
    }
}
