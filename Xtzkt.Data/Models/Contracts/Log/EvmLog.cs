using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;
using System.ComponentModel.DataAnnotations.Schema;

namespace Xtzkt.Data.Models;

public class EvmLog() : Log(Runtime.Evm)
{
    [Column(nameof(TransactionId))]
    public long? TransactionId { get; set; }
    public long? OriginationId { get; set; }
    public long? DepositId { get; set; }

    public byte[]? Topic0 { get; set; }
    public byte[]? Topic1 { get; set; }
    public byte[]? Topic2 { get; set; }
    public byte[]? Topic3 { get; set; }

    public required byte[] Data { get; set; }

    #region binary writer
    public static void Write(NpgsqlConnection conn, IEnumerable<EvmLog> logs)
    {
        using var writer = conn.BeginBinaryImport($"""
            COPY "{nameof(XtzktContext.Logs)}" (
                {BinaryColumns},
                "{nameof(TransactionId)}",
                "{nameof(OriginationId)}",
                "{nameof(DepositId)}",
                "{nameof(Topic0)}",
                "{nameof(Topic1)}",
                "{nameof(Topic2)}",
                "{nameof(Topic3)}",
                "{nameof(Data)}"
            )
            FROM STDIN (FORMAT BINARY)
            """);

        foreach (var log in logs)
        {
            log.WriteBinaryBase(writer);

            writer.WriteNullable(log.TransactionId, NpgsqlDbType.Bigint);
            writer.WriteNullable(log.OriginationId, NpgsqlDbType.Bigint);
            writer.WriteNullable(log.DepositId, NpgsqlDbType.Bigint);
            writer.WriteNullable(log.Topic0, NpgsqlDbType.Bytea);
            writer.WriteNullable(log.Topic1, NpgsqlDbType.Bytea);
            writer.WriteNullable(log.Topic2, NpgsqlDbType.Bytea);
            writer.WriteNullable(log.Topic3, NpgsqlDbType.Bytea);
            writer.Write(log.Data, NpgsqlDbType.Bytea);
        }

        writer.Complete();
    }
    #endregion
}

public static class EvmLogModel
{
    public static void BuildEvmLogModel(this ModelBuilder modelBuilder)
    {
    }
}
