using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xtzkt.Data;
using Xtzkt.Data.Models;

namespace Xtzkt.Indexers.TezosX.Protocols
{
    public class BatchContext
    {
        public HashSet<XBlock> Blocks { get; } = [];
        public List<TransactionOperation> TransactionOps { get; } = [];
        public List<Log> Logs { get; } = [];
        public List<TokenTransfer> TokenTransfers { get; } = [];
        public List<XStatistics> Statistics { get; } = [];

        public bool Contains(XBlock block)
        {
            return Blocks.Contains(block);
        }

        public bool Contains(XStatistics statistics)
        {
            return Statistics.Contains(statistics);
        }

        public void Apply(XtzktContext db)
        {
            if (Blocks.Count == 0)
                return;

            var conn = (db.Database.GetDbConnection() as NpgsqlConnection)!;

            XBlock.Write(conn, Blocks);
            Blocks.Clear();

            if (TransactionOps.Count != 0)
            {
                List<XEvmTransactionOperation>? evmOps = null;
                List<XMichelsonTransactionOperation>? michelsonOps = null;
                List<XEvmMichelsonTransactionOperation>? evmMichelsonOps = null;
                List<XMichelsonEvmTransactionOperation>? michelsonEvmOps = null;

                foreach (var op in TransactionOps)
                {
                    switch (op)
                    {
                        case XEvmTransactionOperation x: (evmOps ??= []).Add(x); break;
                        case XMichelsonTransactionOperation x: (michelsonOps ??= []).Add(x); break;
                        case XEvmMichelsonTransactionOperation x: (evmMichelsonOps ??= []).Add(x); break;
                        case XMichelsonEvmTransactionOperation x: (michelsonEvmOps ??= []).Add(x); break;
                        default: throw new NotImplementedException($"'{op.GetType()}' is not implemented");
                    }
                }

                if (evmOps != null) XEvmTransactionOperation.Write(conn, evmOps);
                if (michelsonOps != null) XMichelsonTransactionOperation.Write(conn, michelsonOps);
                if (evmMichelsonOps != null) XEvmMichelsonTransactionOperation.Write(conn, evmMichelsonOps);
                if (michelsonEvmOps != null) XMichelsonEvmTransactionOperation.Write(conn, michelsonEvmOps);

                TransactionOps.Clear();
            }

            if (Logs.Count != 0)
            {
                List<EvmLog>? evmLogs = null;
                List<MichelsonLog>? michelsonLogs = null;

                foreach (var log in Logs)
                {
                    switch (log)
                    {
                        case EvmLog x: (evmLogs ??= []).Add(x); break;
                        case MichelsonLog x: (michelsonLogs ??= []).Add(x); break;
                        default: throw new NotImplementedException($"'{log.GetType()}' is not implemented");
                    }
                }

                if (evmLogs != null) EvmLog.Write(conn, evmLogs);
                if (michelsonLogs != null) MichelsonLog.Write(conn, michelsonLogs);

                Logs.Clear();
            }

            if (TokenTransfers.Count != 0)
            {
                TokenTransfer.Write(conn, TokenTransfers);
                TokenTransfers.Clear();
            }

            if (Statistics.Count != 0)
            {
                XStatistics.Write(conn, Statistics);
                Statistics.Clear();
            }
        }
    }
}
