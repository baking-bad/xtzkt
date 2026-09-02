using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xtzkt.Data.Models;
using Xtzkt.Indexers.Common.Exceptions;
using Xtzkt.Indexers.Common.Extensions;

namespace Xtzkt.Indexers.L1.Protocols.Proto01
{
    class BakingRightsCommit(ProtocolHandler protocol) : ProtocolCommit(protocol)
    {
        public List<BakingRight> CurrentRights { get; protected set; } = null!;
        public IEnumerable<JsonElement>? FutureBakingRights { get; protected set; }
        public IEnumerable<JsonElement>? FutureAttestationRights { get; protected set; }

        public virtual async Task Apply(L1Block block)
        {
            #region current rights
            CurrentRights = await Cache.BakingRights.GetAsync(block.Level);
            var sql = string.Empty;

            if (block.BlockRound == 0 && block.AttestationPower == block.AttestationCommittee)
            {
                CurrentRights.RemoveAll(x => x.Type == BakingRightType.Baking && x.Round > 0);
                CurrentRights.ForEach(x => x.Status = BakingRightStatus.Realized);

                sql = $@"
                    DELETE  FROM ""BakingRights""
                    WHERE   ""ChainId"" = {block.ChainId}
                    AND     ""Level"" = {block.Level}
                    AND     ""Type"" = {(int)BakingRightType.Baking}
                    AND     ""Round"" > 0;

                    UPDATE  ""BakingRights""
                    SET     ""Status"" = {(int)BakingRightStatus.Realized}
                    WHERE   ""ChainId"" = {block.ChainId}
                    AND     ""Level"" = {block.Level};";
            }
            else
            {
                #region load missed rounds
                var maxExistedRound = CurrentRights
                    .Where(x => x.Type == BakingRightType.Baking)
                    .Select(x => x.Round)
                    .Max();

                if (maxExistedRound < block.BlockRound)
                {
                    var bakingRights = await Proto.Rpc.GetLevelBakingRightsAsync(block.Level, block.Level, block.BlockRound);
                    //bakingRights = bakingRights.OrderBy(x => x.Round);

                    var sqlInsert = @"
                        INSERT INTO ""BakingRights"" (""ChainId"", ""Cycle"", ""Level"", ""BakerId"", ""Type"", ""Status"", ""Round"", ""Slots"") VALUES ";

                    foreach (var bakingRight in bakingRights.EnumerateArray().SkipWhile(x => x.RequiredInt32("priority") <= maxExistedRound))
                    {
                        var baker = Cache.Addresses.GetBakerOrDefault(bakingRight.RequiredString("delegate"));
                        if (baker == null) continue; // WTF: [level:28680] - Baking rights were given to non-baker address

                        sqlInsert += $@"
                            ({block.ChainId}, {block.Cycle}, {block.Level}, {baker.Id}, {(int)BakingRightType.Baking}, {(int)BakingRightStatus.Future}, {bakingRight.RequiredInt32("priority")}, null),";
                    }

                    await Db.Database.ExecuteSqlRawAsync(sqlInsert[..^1]);

                    //TODO: execute sql with RETURNS to get identity
                    var addedRights = await Db.BakingRights
                        .Where(x => x.ChainId == block.ChainId && x.Level == block.Level && x.Type == BakingRightType.Baking && x.Round > maxExistedRound)
                        .ToListAsync();

                    CurrentRights.AddRange(addedRights);
                }
                #endregion

                #region remove excess
                if (CurrentRights.RemoveAll(x => x.Type == BakingRightType.Baking && x.Round > block.BlockRound) > 0)
                {
                    sql += $@"
                        DELETE  FROM ""BakingRights""
                        WHERE   ""ChainId"" = {block.ChainId}
                        AND     ""Level"" = {block.Level}
                        AND     ""Type"" = {(int)BakingRightType.Baking}
                        AND     ""Round"" > {block.BlockRound};";
                }
                #endregion

                #region remove weird
                var weirdRights = CurrentRights
                    .Where(x => !Cache.Addresses.BakerExists(x.BakerId))
                    .ToList();

                if (weirdRights.Count > 0)
                {
                    foreach (var wr in weirdRights)
                        CurrentRights.Remove(wr);

                    sql += $@"
                        DELETE  FROM ""BakingRights""
                        WHERE   ""Id"" = ANY(ARRAY[{string.Join(',', weirdRights.Select(x => x.Id))}]);";
                }
                #endregion

                foreach (var cr in CurrentRights)
                    cr.Status = BakingRightStatus.Missed;

                CurrentRights.First(x => x.Round == block.BlockRound).Status = BakingRightStatus.Realized;

                if (Context.AttestationOps.Count != 0)
                {
                    var attesters = new HashSet<int>(Context.AttestationOps.Select(x => x.BakerId));
                    foreach (var ar in CurrentRights.Where(x => x.Type == BakingRightType.Attestation && attesters.Contains(x.BakerId)))
                        ar.Status = BakingRightStatus.Realized;
                }

                var realized = CurrentRights.Where(x => x.Status == BakingRightStatus.Realized);
                var missed = CurrentRights.Where(x => x.Status == BakingRightStatus.Missed);

                sql += $@"
                    UPDATE  ""BakingRights""
                    SET     ""Status"" = {(int)BakingRightStatus.Realized}
                    WHERE   ""Id"" = ANY(ARRAY[{string.Join(',', realized.Select(x => x.Id))}]);";

                if (missed.Any())
                {
                    sql += $@"
                        UPDATE  ""BakingRights""
                        SET     ""Status"" = {(int)BakingRightStatus.Missed}
                        WHERE   ""Id"" = ANY(ARRAY[{string.Join(',', missed.Select(x => x.Id))}]);";
                }
            }

            await Db.Database.ExecuteSqlRawAsync(sql);
            #endregion

            #region new cycle
            if (block.Events.HasFlag(L1BlockEvents.CycleBegin))
            {
                var futureCycle = block.Cycle + Context.Protocol.ConsensusRightsDelay;

                FutureBakingRights = await GetBakingRights(block, Context.Protocol, futureCycle);
                FutureAttestationRights = await GetAttestationRights(block, Context.Protocol, futureCycle);

                foreach (var ar in FutureAttestationRights)
                    if (!await Cache.Addresses.ExistsAsync(ar.RequiredString("delegate")))
                        throw new Exception($"Address {ar.RequiredString("delegate")} doesn't exist");

                foreach (var br in FutureBakingRights)
                    if (!await Cache.Addresses.ExistsAsync(br.RequiredString("delegate")))
                        throw new Exception($"Address {br.RequiredString("delegate")} doesn't exist");

                var conn = (Db.Database.GetDbConnection() as NpgsqlConnection)!;
                using var writer = conn.BeginBinaryImport(@"COPY ""BakingRights"" (""ChainId"", ""Cycle"", ""Level"", ""BakerId"", ""Type"", ""Status"", ""Round"", ""Slots"") FROM STDIN (FORMAT BINARY)");

                foreach (var ar in FutureAttestationRights)
                {
                    // WTF: [level:28680] - Baking rights were given to non-baker address
                    var acc = await Cache.Addresses.GetExistingAsync(ar.RequiredString("delegate"));
                    
                    writer.StartRow();
                    writer.Write(block.ChainId, NpgsqlTypes.NpgsqlDbType.Integer);
                    writer.Write(Context.Protocol.GetCycle(ar.RequiredInt32("level") + 1), NpgsqlTypes.NpgsqlDbType.Integer); // level + 1 (shifted)
                    writer.Write(ar.RequiredInt32("level") + 1, NpgsqlTypes.NpgsqlDbType.Integer);                             // level + 1 (shifted)
                    writer.Write(acc.Id, NpgsqlTypes.NpgsqlDbType.Integer);
                    writer.Write((short)BakingRightType.Attestation, NpgsqlTypes.NpgsqlDbType.Smallint);
                    writer.Write((short)BakingRightStatus.Future, NpgsqlTypes.NpgsqlDbType.Smallint);
                    writer.WriteNull();
                    writer.Write(ar.RequiredArray("slots").Count(), NpgsqlTypes.NpgsqlDbType.Integer);
                }

                foreach (var br in FutureBakingRights)
                {
                    // WTF: [level:28680] - Baking rights were given to non-baker address
                    var acc = await Cache.Addresses.GetExistingAsync(br.RequiredString("delegate"));

                    writer.StartRow();
                    writer.Write(block.ChainId, NpgsqlTypes.NpgsqlDbType.Integer);
                    writer.Write(futureCycle, NpgsqlTypes.NpgsqlDbType.Integer);
                    writer.Write(br.RequiredInt32("level"), NpgsqlTypes.NpgsqlDbType.Integer);
                    writer.Write(acc.Id, NpgsqlTypes.NpgsqlDbType.Integer);
                    writer.Write((short)BakingRightType.Baking, NpgsqlTypes.NpgsqlDbType.Smallint);
                    writer.Write((short)BakingRightStatus.Future, NpgsqlTypes.NpgsqlDbType.Smallint);
                    writer.Write(br.RequiredInt32("priority"), NpgsqlTypes.NpgsqlDbType.Integer);
                    writer.WriteNull();
                }

                writer.Complete();
            }
            #endregion
        }

        public virtual async Task Revert(L1Block block)
        {
            #region current rights
            CurrentRights = await Cache.BakingRights.GetAsync(block.Level);

            foreach (var cr in CurrentRights)
                cr.Status = BakingRightStatus.Future;

            await Db.Database.ExecuteSqlRawAsync("""
                UPDATE "BakingRights"
                SET "Status" = {0}
                WHERE "ChainId" = {1}
                AND "Level" = {2}
                """, (int)BakingRightStatus.Future, block.ChainId, block.Level);
            #endregion

            #region new cycle
            if (block.Events.HasFlag(L1BlockEvents.CycleBegin))
            {
                await Db.Database.ExecuteSqlRawAsync("""
                    DELETE FROM "BakingRights"
                    WHERE "ChainId" = {0}
                    AND ("Level" = {1} AND "Type" = {2} OR "Level" > {1})
                    """,
                    block.ChainId,
                    Context.Protocol.GetCycleStart(block.Cycle + Context.Protocol.ConsensusRightsDelay),
                    (int)BakingRightType.Baking);
            }
            #endregion
        }

        protected virtual async Task<IEnumerable<JsonElement>> GetBakingRights(L1Block block, L1Protocol protocol, int cycle)
        {
            var rights = (await Proto.Rpc.GetBakingRightsAsync(block.Level, cycle)).RequiredArray().EnumerateArray();
            if (!rights.Any() || rights.Count(x => x.RequiredInt32("priority") == 0) != protocol.BlocksPerCycle)
                throw new ValidationException("Rpc returned less baking rights (with priority 0) than it should be");

            return rights;
        }

        protected virtual async Task<IEnumerable<JsonElement>> GetAttestationRights(L1Block block, L1Protocol protocol, int cycle)
        {
            var rights = (await Proto.Rpc.GetAttestationRightsAsync(block.Level, cycle)).RequiredArray().EnumerateArray();
            if (!rights.Any() || rights.Sum(x => x.RequiredArray("slots").Count()) != protocol.BlocksPerCycle * protocol.AttestersPerBlock)
                throw new ValidationException("Rpc returned less attestation rights (slots) than it should be");

            return rights;
        }
    }
}
