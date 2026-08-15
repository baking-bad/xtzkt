using Npgsql;
using Xtzkt.Data.Models;

namespace Xtzkt.Indexers.L1.Protocols.Proto17
{
    public class InboxCommit(ProtocolHandler protocol) : Proto16.InboxCommit(protocol)
    {
        protected override void WriteMigrationMessage(NpgsqlBinaryImporter writer, L1Block block, ref int index)
        {
            writer.StartRow();
            writer.Write(Cache.Chain.NextInboxMessageId(), NpgsqlTypes.NpgsqlDbType.Integer);
            writer.Write(Cache.Chain.Get().Id, NpgsqlTypes.NpgsqlDbType.Integer);
            writer.Write(block.Level, NpgsqlTypes.NpgsqlDbType.Integer);
            writer.Write(index++, NpgsqlTypes.NpgsqlDbType.Integer);
            writer.Write((int)InboxMessageType.Migration, NpgsqlTypes.NpgsqlDbType.Integer);
            writer.WriteNull();
            writer.WriteNull();
            writer.WriteNull();
            writer.Write(Proto.VersionName, NpgsqlTypes.NpgsqlDbType.Text);
        }
    }
}
