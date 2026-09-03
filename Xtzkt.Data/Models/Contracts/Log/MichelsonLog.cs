using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;
using System.ComponentModel.DataAnnotations.Schema;

namespace Xtzkt.Data.Models;

public class MichelsonLog() : Log(Runtime.Michelson)
{
    [Column(nameof(TransactionId), Order = 2)]
    public required long TransactionId { get; set; }

    public byte[]? Type { get; set; }
    public byte[]? PayloadRaw { get; set; }

    #region binary writer
    public static void Write(NpgsqlConnection conn, IEnumerable<MichelsonLog> logs)
    {
        using var writer = conn.BeginBinaryImport($"""
            COPY "{nameof(XtzktContext.Logs)}" (
                {BinaryColumns},
                "{nameof(TransactionId)}",
                "{nameof(Type)}",
                "{nameof(PayloadRaw)}"
            )
            FROM STDIN (FORMAT BINARY)
            """);

        foreach (var log in logs)
        {
            log.WriteBinaryBase(writer);

            writer.Write(log.TransactionId, NpgsqlDbType.Bigint);
            writer.WriteNullable(log.Type, NpgsqlDbType.Bytea);
            writer.WriteNullable(log.PayloadRaw, NpgsqlDbType.Bytea);
        }

        writer.Complete();
    }
    #endregion
}

public static class MichelsonLogModel
{
    public static void BuildMichelsonLogModel(this ModelBuilder modelBuilder)
    {
    }
}
