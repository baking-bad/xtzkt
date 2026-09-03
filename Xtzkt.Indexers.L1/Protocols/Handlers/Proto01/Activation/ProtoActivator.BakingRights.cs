using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xtzkt.Data.Models;
using Xtzkt.Indexers.Common.Extensions;

namespace Xtzkt.Indexers.L1.Protocols.Proto01
{
    partial class ProtoActivator : ProtocolCommit
    {
        public async Task<(List<IEnumerable<RightsGenerator.BR>>, List<IEnumerable<RightsGenerator.AR>>)> BootstrapBakingRights(
            L1Protocol protocol,
            List<L1Address> addresses,
            List<Cycle> cycles)
        {
            var bakingRights = new List<IEnumerable<RightsGenerator.BR>>(protocol.ConsensusRightsDelay + 1);
            var attestationRights = new List<IEnumerable<RightsGenerator.AR>>(protocol.ConsensusRightsDelay + 1);

            foreach (var cycle in cycles)
            {
                var (futureBakingRights, futureAttestationRights) = await GetRights(protocol, addresses, cycle);

                bakingRights.Add(futureBakingRights);
                attestationRights.Add(futureAttestationRights);

                var conn = (Db.Database.GetDbConnection() as NpgsqlConnection)!;
                using var writer = conn.BeginBinaryImport(@"
                    COPY ""BakingRights"" (""ChainId"", ""Cycle"", ""Level"", ""BakerId"", ""Type"", ""Status"", ""Round"", ""Slots"")
                    FROM STDIN (FORMAT BINARY)");

                foreach (var ar in futureAttestationRights)
                {
                    writer.StartRow();
                    writer.Write(protocol.ChainId, NpgsqlTypes.NpgsqlDbType.Integer);
                    writer.Write(protocol.GetCycle(ar.Level + 1), NpgsqlTypes.NpgsqlDbType.Integer); // level + 1 (shifted)
                    writer.Write(ar.Level + 1, NpgsqlTypes.NpgsqlDbType.Integer);                    // level + 1 (shifted)
                    writer.Write(ar.Baker, NpgsqlTypes.NpgsqlDbType.Integer);
                    writer.Write((short)BakingRightType.Attestation, NpgsqlTypes.NpgsqlDbType.Smallint);
                    writer.Write((short)BakingRightStatus.Future, NpgsqlTypes.NpgsqlDbType.Smallint);
                    writer.WriteNull();
                    writer.Write(ar.Slots, NpgsqlTypes.NpgsqlDbType.Integer);
                }

                foreach (var br in futureBakingRights.SkipWhile(x => x.Level == 1)) // skip bootstrap block rights
                {
                    writer.StartRow();
                    writer.Write(protocol.ChainId, NpgsqlTypes.NpgsqlDbType.Integer);
                    writer.Write(cycle.Index, NpgsqlTypes.NpgsqlDbType.Integer);
                    writer.Write(br.Level, NpgsqlTypes.NpgsqlDbType.Integer);
                    writer.Write(br.Baker, NpgsqlTypes.NpgsqlDbType.Integer);
                    writer.Write((short)BakingRightType.Baking, NpgsqlTypes.NpgsqlDbType.Smallint);
                    writer.Write((short)BakingRightStatus.Future, NpgsqlTypes.NpgsqlDbType.Smallint);
                    writer.Write(br.Round, NpgsqlTypes.NpgsqlDbType.Integer);
                    writer.WriteNull();
                }

                writer.Complete();
            }

            return (bakingRights, attestationRights);
        }

        public async Task ClearBakingRights()
        {
            await Db.Database.ExecuteSqlRawAsync("""
                DELETE FROM "BakingRights"
                WHERE "ChainId" = {0}
                """, Cache.Chain.Get().Id);
            Cache.BakingRights.Reset();
        }

        protected virtual async Task<(IEnumerable<RightsGenerator.BR>, IEnumerable<RightsGenerator.AR>)> GetRights(L1Protocol protocol, List<L1Address> addresses, Cycle cycle)
        {
            var bakingRights = (await Proto.Rpc.GetBakingRightsAsync(1, cycle.Index))
                .EnumerateArray()
                .Select(x => new RightsGenerator.BR
                {
                    Baker = Cache.Addresses.GetExistingBaker(x.RequiredString("delegate")).Id,
                    Level = x.RequiredInt32("level"),
                    Round = x.RequiredInt32("priority")
                });

            var attestationRights = (await Proto.Rpc.GetAttestationRightsAsync(1, cycle.Index))
                .EnumerateArray()
                .Select(x => new RightsGenerator.AR
                {
                    Baker = Cache.Addresses.GetExistingBaker(x.RequiredString("delegate")).Id,
                    Level = x.RequiredInt32("level"),
                    Slots = x.RequiredArray("slots").Count()
                });

            return (bakingRights, attestationRights);
        }
    }
}
