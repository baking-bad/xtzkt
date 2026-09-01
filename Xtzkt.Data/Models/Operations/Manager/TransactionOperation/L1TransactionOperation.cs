using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;

namespace Xtzkt.Data.Models
{
    public class L1TransactionOperation() : MichelsonTransactionOperation(Direction.L1)
    {
        public long BakerFee { get; set; }
        public int? ResetDeactivation { get; set; }

        #region binary writer
        public static void Write(NpgsqlConnection conn, IEnumerable<L1TransactionOperation> ops)
        {
            using var writer = conn.BeginBinaryImport($"""
                COPY "{nameof(XtzktContext.TransactionOps)}" (
                    "{nameof(Id)}",
                    "{nameof(Direction)}",
                    "{nameof(ChainId)}",
                    "{nameof(SenderCodeHash)}",
                    "{nameof(TargetId)}",
                    "{nameof(TargetCodeHash)}",
                    "{nameof(ResetDeactivation)}",
                    "{nameof(Amount)}",
                    "{nameof(Entrypoint)}",
                    "{nameof(ParametersRaw)}",
                    "{nameof(Parameters)}",
                    "{nameof(Guessed)}",
                    "{nameof(InternalOperations)}",
                    "{nameof(LogsCount)}",
                    "{nameof(TicketTransfers)}",
                    "{nameof(AddressRegistryIndex)}",
                    "{nameof(Level)}",
                    "{nameof(Timestamp)}",
                    "{nameof(Hash)}",
                    "{nameof(SenderId)}",
                    "{nameof(Counter)}",
                    "{nameof(BakerFee)}",
                    "{nameof(StorageFee)}",
                    "{nameof(AllocationFee)}",
                    "{nameof(GasLimit)}",
                    "{nameof(GasUsed)}",
                    "{nameof(StorageLimit)}",
                    "{nameof(StorageUsed)}",
                    "{nameof(Status)}",
                    "{nameof(Errors)}",
                    "{nameof(InitiatorId)}",
                    "{nameof(Nonce)}",
                    "{nameof(StorageId)}",
                    "{nameof(BigMapUpdates)}",
                    "{nameof(TokenTransfers)}",
                    "{nameof(SubsCounter)}"
                )
                FROM STDIN (FORMAT BINARY)
                """);

            foreach (var op in ops)
            {
                writer.StartRow();

                writer.Write(op.Id, NpgsqlDbType.Bigint);
                writer.Write((int)op.Direction, NpgsqlDbType.Integer);
                writer.Write(op.ChainId, NpgsqlDbType.Integer);
                writer.WriteNullable(op.SenderCodeHash, NpgsqlDbType.Integer);
                writer.Write(op.TargetId, NpgsqlDbType.Integer);
                writer.WriteNullable(op.TargetCodeHash, NpgsqlDbType.Integer);
                writer.WriteNullable(op.ResetDeactivation, NpgsqlDbType.Integer);
                writer.Write(op.Amount, NpgsqlDbType.Bigint);
                writer.WriteNullable(op.Entrypoint, NpgsqlDbType.Text);
                writer.WriteNullable(op.ParametersRaw, NpgsqlDbType.Bytea);
                writer.WriteNullable(op.Parameters, NpgsqlDbType.Jsonb);
                writer.WriteNullable(op.Guessed, NpgsqlDbType.Boolean);
                writer.WriteNullable(op.InternalOperations, NpgsqlDbType.Integer);
                writer.WriteNullable(op.LogsCount, NpgsqlDbType.Integer);
                writer.WriteNullable(op.TicketTransfers, NpgsqlDbType.Integer);
                writer.WriteNullable(op.AddressRegistryIndex, NpgsqlDbType.Integer);
                writer.Write(op.Level, NpgsqlDbType.Integer);
                writer.Write(op.Timestamp, NpgsqlDbType.TimestampTz);
                writer.Write(op.Hash, NpgsqlDbType.Bytea);
                writer.Write(op.SenderId, NpgsqlDbType.Integer);
                writer.Write(op.Counter, NpgsqlDbType.Integer);
                writer.Write(op.BakerFee, NpgsqlDbType.Bigint);
                writer.WriteNullable(op.StorageFee, NpgsqlDbType.Bigint);
                writer.WriteNullable(op.AllocationFee, NpgsqlDbType.Bigint);
                writer.Write(op.GasLimit, NpgsqlDbType.Integer);
                writer.Write(op.GasUsed, NpgsqlDbType.Integer);
                writer.Write(op.StorageLimit, NpgsqlDbType.Integer);
                writer.Write(op.StorageUsed, NpgsqlDbType.Integer);
                writer.Write((int)op.Status, NpgsqlDbType.Smallint);
                writer.WriteNullable(op.Errors, NpgsqlDbType.Text);
                writer.WriteNullable(op.InitiatorId, NpgsqlDbType.Integer);
                writer.WriteNullable(op.Nonce, NpgsqlDbType.Integer);
                writer.WriteNullable(op.StorageId, NpgsqlDbType.Bigint);
                writer.WriteNullable(op.BigMapUpdates, NpgsqlDbType.Integer);
                writer.WriteNullable(op.TokenTransfers, NpgsqlDbType.Integer);
                writer.WriteNullable(op.SubsCounter, NpgsqlDbType.Integer);
            }

            writer.Complete();
        }
        #endregion
    }

    public static class L1TransactionOperationModel
    {
        public static void BuildL1TransactionOperationModel(this ModelBuilder modelBuilder)
        {
        }
    }
}
