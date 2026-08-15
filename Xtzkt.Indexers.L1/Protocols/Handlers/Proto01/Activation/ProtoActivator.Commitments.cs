using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Linq;
using Npgsql;
using Xtzkt.Data.Models;

namespace Xtzkt.Indexers.L1.Protocols.Proto01
{
    partial class ProtoActivator : ProtocolCommit
    {
        void BootstrapCommitments(JToken parameters)
        {
            var chain = Cache.Chain.Get();

            var commitments = parameters["commitments"]?.Select(x => new Commitment
            {
                ChainId = chain.Id,
                Hash = x[0]!.Value<string>()!,
                Balance = x[1]!.Value<long>()
            });

            if (commitments != null)
            {
                var statistics = Cache.Statistics.Current;

                var conn = (Db.Database.GetDbConnection() as NpgsqlConnection)!;
                using var writer = conn.BeginBinaryImport(@"COPY ""Commitments"" (""ChainId"", ""Hash"", ""Balance"") FROM STDIN (FORMAT BINARY)");

                foreach (var commitment in commitments)
                {
                    writer.StartRow();
                    writer.Write(commitment.ChainId);
                    writer.Write(commitment.Hash);
                    writer.Write(commitment.Balance);

                    chain.CommitmentsCount++;
                    statistics.TotalCommitments += commitment.Balance;
                }

                writer.Complete();
            }
        }

        async Task ClearCommitments()
        {
            var chain = Cache.Chain.Get();

            await Db.Database.ExecuteSqlRawAsync("""
                DELETE FROM "Commitments"
                WHERE "ChainId" = {0}
                """, chain.Id);

            chain.CommitmentsCount = 0;
        }
    }
}
