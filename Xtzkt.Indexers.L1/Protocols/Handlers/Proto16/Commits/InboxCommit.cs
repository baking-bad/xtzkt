using Microsoft.EntityFrameworkCore;
using Xtzkt.Data.Models;
using Npgsql;

namespace Xtzkt.Indexers.L1.Protocols.Proto16
{
    public class InboxCommit(ProtocolHandler protocol) : ProtocolCommit(protocol)
    {
        public void Init(L1Block block)
        {
            var conn = (Db.Database.GetDbConnection() as NpgsqlConnection)!;
            using var writer = conn.BeginBinaryImport("""
                COPY "InboxMessages" ("Id", "ChainId", "Level", "Index", "Type", "PredecessorLevel", "OperationId", "Payload", "Protocol")
                FROM STDIN (FORMAT BINARY)
                """);

            var index = 0;

            writer.StartRow();
            writer.Write(Cache.Chain.NextInboxMessageId(), NpgsqlTypes.NpgsqlDbType.Integer);
            writer.Write(Cache.Chain.Get().Id, NpgsqlTypes.NpgsqlDbType.Integer);
            writer.Write(block.Level, NpgsqlTypes.NpgsqlDbType.Integer);
            writer.Write(index++, NpgsqlTypes.NpgsqlDbType.Integer);
            writer.Write((int)InboxMessageType.LevelStart, NpgsqlTypes.NpgsqlDbType.Integer);
            writer.WriteNull();
            writer.WriteNull();
            writer.WriteNull();
            writer.WriteNull();

            writer.StartRow();
            writer.Write(Cache.Chain.NextInboxMessageId(), NpgsqlTypes.NpgsqlDbType.Integer);
            writer.Write(Cache.Chain.Get().Id, NpgsqlTypes.NpgsqlDbType.Integer);
            writer.Write(block.Level, NpgsqlTypes.NpgsqlDbType.Integer);
            writer.Write(index++, NpgsqlTypes.NpgsqlDbType.Integer);
            writer.Write((int)InboxMessageType.LevelInfo, NpgsqlTypes.NpgsqlDbType.Integer);
            writer.Write(block.Level - 1, NpgsqlTypes.NpgsqlDbType.Integer);
            writer.WriteNull();
            writer.WriteNull();
            writer.WriteNull();

            WriteMigrationMessage(writer, block, ref index);

            writer.StartRow();
            writer.Write(Cache.Chain.NextInboxMessageId(), NpgsqlTypes.NpgsqlDbType.Integer);
            writer.Write(Cache.Chain.Get().Id, NpgsqlTypes.NpgsqlDbType.Integer);
            writer.Write(block.Level, NpgsqlTypes.NpgsqlDbType.Integer);
            writer.Write(index, NpgsqlTypes.NpgsqlDbType.Integer);
            writer.Write((int)InboxMessageType.LevelEnd, NpgsqlTypes.NpgsqlDbType.Integer);
            writer.WriteNull();
            writer.WriteNull();
            writer.WriteNull();
            writer.WriteNull();

            writer.Complete();
        }

        public void Apply(L1Block block)
        {
            var conn = (Db.Database.GetDbConnection() as NpgsqlConnection)!;
            using var writer = conn.BeginBinaryImport("""
                COPY "InboxMessages" ("Id", "ChainId", "Level", "Index", "Type", "PredecessorLevel", "OperationId", "Payload", "Protocol")
                FROM STDIN (FORMAT BINARY)
                """);

            var index = 0;

            writer.StartRow();
            writer.Write(Cache.Chain.NextInboxMessageId(), NpgsqlTypes.NpgsqlDbType.Integer);
            writer.Write(Cache.Chain.Get().Id, NpgsqlTypes.NpgsqlDbType.Integer);
            writer.Write(block.Level, NpgsqlTypes.NpgsqlDbType.Integer);
            writer.Write(index++, NpgsqlTypes.NpgsqlDbType.Integer);
            writer.Write((int)InboxMessageType.LevelStart, NpgsqlTypes.NpgsqlDbType.Integer);
            writer.WriteNull();
            writer.WriteNull();
            writer.WriteNull();
            writer.WriteNull();

            writer.StartRow();
            writer.Write(Cache.Chain.NextInboxMessageId(), NpgsqlTypes.NpgsqlDbType.Integer);
            writer.Write(Cache.Chain.Get().Id, NpgsqlTypes.NpgsqlDbType.Integer);
            writer.Write(block.Level, NpgsqlTypes.NpgsqlDbType.Integer);
            writer.Write(index++, NpgsqlTypes.NpgsqlDbType.Integer);
            writer.Write((int)InboxMessageType.LevelInfo, NpgsqlTypes.NpgsqlDbType.Integer);
            writer.Write(block.Level - 1, NpgsqlTypes.NpgsqlDbType.Integer);
            writer.WriteNull();
            writer.WriteNull();
            writer.WriteNull();

            if (block.Events.HasFlag(L1BlockEvents.ProtocolBegin))
                WriteMigrationMessage(writer, block, ref index);

            foreach (var (operationId, payload) in Proto.Inbox.Messages)
            {
                writer.StartRow();
                writer.Write(Cache.Chain.NextInboxMessageId(), NpgsqlTypes.NpgsqlDbType.Integer);
                writer.Write(Cache.Chain.Get().Id, NpgsqlTypes.NpgsqlDbType.Integer);
                writer.Write(block.Level, NpgsqlTypes.NpgsqlDbType.Integer);
                writer.Write(index++, NpgsqlTypes.NpgsqlDbType.Integer);
                if (payload == null)
                {
                    writer.Write((int)InboxMessageType.Transfer, NpgsqlTypes.NpgsqlDbType.Integer);
                    writer.WriteNull();
                    writer.Write(operationId, NpgsqlTypes.NpgsqlDbType.Bigint);
                    writer.WriteNull();
                }
                else
                {
                    writer.Write((int)InboxMessageType.External, NpgsqlTypes.NpgsqlDbType.Integer);
                    writer.WriteNull();
                    writer.Write(operationId, NpgsqlTypes.NpgsqlDbType.Bigint);
                    writer.Write(payload, NpgsqlTypes.NpgsqlDbType.Bytea);
                }
                writer.WriteNull();
            }

            writer.StartRow();
            writer.Write(Cache.Chain.NextInboxMessageId(), NpgsqlTypes.NpgsqlDbType.Integer);
            writer.Write(Cache.Chain.Get().Id, NpgsqlTypes.NpgsqlDbType.Integer);
            writer.Write(block.Level, NpgsqlTypes.NpgsqlDbType.Integer);
            writer.Write(index, NpgsqlTypes.NpgsqlDbType.Integer);
            writer.Write((int)InboxMessageType.LevelEnd, NpgsqlTypes.NpgsqlDbType.Integer);
            writer.WriteNull();
            writer.WriteNull();
            writer.WriteNull();
            writer.WriteNull();

            writer.Complete();
        }

        public async Task Revert(L1Block block)
        {
            var cnt = await Db.Database.ExecuteSqlRawAsync("""
                DELETE FROM "InboxMessages"
                WHERE "ChainId" = {0}
                AND "Level" = {1}
                """, block.ChainId, block.Level);

            Cache.Chain.ReleaseInboxMessageId(cnt);
        }

        protected virtual void WriteMigrationMessage(NpgsqlBinaryImporter writer, L1Block block, ref int index)
        {
            // migration messages were added in Proto17
        }
    }
}
