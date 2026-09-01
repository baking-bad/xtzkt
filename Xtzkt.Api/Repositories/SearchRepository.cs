using Dapper;
using Netezos;
using Npgsql;
using Xtzkt.Api.Filters;
using Xtzkt.Api.Filters.Parameters;
using Xtzkt.Api.Models.Enums;
using Xtzkt.Api.Models.Search;
using Xtzkt.Api.Services.Cache;
using Xtzkt.Api.Services.Database;
using Xtzkt.Api.Utils;
using Xtzkt.Data.Utils;
using Xtzkt.Utils;
using Xtzkt.Utils.Encoding;

namespace Xtzkt.Api.Repositories;

public class SearchRepository(
    ChainCache _chainCache,
    AddressCache _addressCache,
    AliasCache _aliasCache,
    DbInitService _dbInit,
    NpgsqlDataSource _dataSource)
{
    public async Task<IEnumerable<SearchResult>> Search(SearchFilter filter)
    {
        var query = NormalizeQuery(filter.Query);
        if (query.Length == 0)
            return [];

        var (chains, xChains) = ResolveChains(filter.Chain);
        if (chains.Length == 0)
            return [];

        var scopes = filter.Scopes?.Scopes ?? SearchScopes.Default;
        if (scopes.Count == 0)
            return [];

        await using var db = await _dataSource.OpenConnectionAsync();
        var results = new List<(double score, SearchResult result)>(filter.Limit);

        #region helpers
        async Task HandleTokensByContracts(List<int>? ids)
        {
            if (ids == null) return;
            foreach (var id in ids)
                // it's hardly possible to have more than 1 item in list, so we do targeted sql query
                results.AddRange(await SearchTokensByContract(db, id, filter.Limit));
        }

        async Task HandleAddressByHash(int[] relevantChains)
        {
            if (scopes.Contains(SearchScopes.Address))
            {
                var (r, c) = await SearchAddressesByHash(relevantChains, query, filter.Limit);
                results.AddRange(r);

                if (scopes.Contains(SearchScopes.Token))
                    await HandleTokensByContracts(c);
            }
            else if (scopes.Contains(SearchScopes.Token))
            {
                var (_, c) = await SearchAddressesByHash(relevantChains, query, filter.Limit);
                await HandleTokensByContracts(c);
            }
        }
        #endregion

        #region address + token by mich hash
        if (Regexes.MichelsonAddress().IsMatch(query))
        {
            await HandleAddressByHash(chains);
        }
        #endregion

        #region address + token by evm hash
        else if (Regexes.EvmAddress().IsMatch(query))
        {
            await HandleAddressByHash(xChains);
        }
        #endregion

        #region operation by mich hash
        else if (Regexes.MichelsonOperationHash().IsMatch(query))
        {
            if (scopes.Contains(SearchScopes.Operation) && Base58.TryDecode(query, Prefixes.o, out var operationHash))
                results.AddRange(await SearchOperationsByHash(db, chains, operationHash, query, filter.Limit));
        }
        #endregion

        #region operation + block by evm hash
        else if (Regexes.EvmHash().IsMatch(query) && Hex.TryGetBytes(query, out var evmHash))
        {
            if (scopes.Contains(SearchScopes.Operation))
                results.AddRange(await SearchOperationsByHash(db, xChains, evmHash, query, filter.Limit));

            if (scopes.Contains(SearchScopes.Block))
                results.AddRange(await SearchBlocksByEvmHash(db, xChains, evmHash, query, filter.Limit));
        }
        #endregion

        #region block by mich hash
        else if (Regexes.MichelsonBlockHash().IsMatch(query))
        {
            if (scopes.Contains(SearchScopes.Block) && Base58.TryDecode(query, Prefixes.B, out var blockHash))
                results.AddRange(await SearchBlocksByMichHash(db, chains, blockHash, filter.Limit));
        }
        #endregion

        #region block by level
        else if (Regexes.Number().IsMatch(query) && int.TryParse(query, out var level))
        {
            if (scopes.Contains(SearchScopes.Block))
                results.AddRange(await SearchBlocksByLevel(db, chains, level, filter.Limit));
        }
        #endregion

        #region known, but not yet searchable
        else if (Regexes.MichelsonProtocolHash().IsMatch(query) || Regexes.MichelsonExpressionHash().IsMatch(query))
        {
            // TODO: extend search with protocols, constants and bigmap keys
        }
        #endregion

        #region address + token by string
        else
        {
            if (scopes.Contains(SearchScopes.Address))
                results.AddRange(await SearchAddressesByAlias(chains, query, filter.Limit));

            if (scopes.Contains(SearchScopes.Token))
                results.AddRange(await SearchTokensByNameOrSymbol(db, chains, query, filter.Limit));
        }
        #endregion

        return results
            .OrderByDescending(x => x.score)
            .ThenBy(x => x.result.Priority)
            .Select(x => x.result)
            .Take(filter.Limit);
    }

    #region addresses
    async Task<(IEnumerable<(double, SearchResult)>, List<int>?)> SearchAddressesByHash(int[] chains, string hash, int limit)
    {
        var searchResults = new List<(double, SearchResult)>();
        List<int>? contractIds = null;

        foreach (var chainId in chains)
        {
            if (await _addressCache.GetAsync(chainId, hash) is Data.Models.Address address)
            {
                searchResults.Add((1, new AddressSearchResult
                {
                    Chain = _chainCache.GetInfo(address.ChainId),
                    Hash = address.Hash,
                    Type = AddressTypes.ToString((int)address.Type),
                    Alias = _aliasCache.Get(address.Id),
                }));

                if (address is Data.Models.L1Contract l1c && l1c.TokensCount != 0)
                {
                    contractIds ??= [];
                    contractIds.Add(l1c.Id);
                }
                else if (address is Data.Models.XMichelsonContract xmc && xmc.TokensCount != 0)
                {
                    contractIds ??= [];
                    contractIds.Add(xmc.Id);
                }
                else if (address is Data.Models.XEvmContract xec && xec.TokensCount != 0)
                {
                    contractIds ??= [];
                    contractIds.Add(xec.Id);
                }

                if (searchResults.Count == limit) break;
            }
        }

        return (searchResults, contractIds);
    }

    async Task<IEnumerable<(double, SearchResult)>> SearchAddressesByAlias(int[] chains, string query, int limit)
    {
        var idsWithScores = _aliasCache.Search(chains, query, limit);
        if (idsWithScores.Length == 0)
            return [];

        await _addressCache.PreloadAsync(idsWithScores.Select(x => x.Id));

        var res = new List<(double, SearchResult)>(idsWithScores.Length);
        foreach (var (id, score) in idsWithScores)
            if (await _addressCache.GetAsync(id) is Data.Models.Address address)
                res.Add((score, new AddressSearchResult
                {
                    Chain = _chainCache.GetInfo(address.ChainId),
                    Hash = address.Hash,
                    Type = AddressTypes.ToString((int)address.Type),
                    Alias = _aliasCache.Get(address.Id),
                }));

        return res;
    }
    #endregion

    #region blocks
    async Task<IEnumerable<(double, SearchResult)>> SearchBlocksByEvmHash(NpgsqlConnection db, int[] chains, byte[] hash, string hashStr, int limit)
    {
        var rows = await db.QueryAsync("""
            SELECT "ChainId", "Level", "Timestamp", "MichelsonHash"
            FROM "Blocks"
            WHERE "ChainId" = ANY (@chains) AND "Hash" = @hash
            ORDER BY "Id"
            LIMIT @limit
            """, new { chains, hash, limit });

        return rows.Select<dynamic, (double, SearchResult)>(row => (1, new BlockSearchResult
        {
            Chain = _chainCache.GetInfo((int)row.ChainId),
            Level = row.Level,
            Timestamp = row.Timestamp,
            Hash = hashStr,
            MichelsonHash = row.MichelsonHash is byte[] mh ? Hashes.FormatMichelsonBlockHash(mh) : null,
        }));
    }

    async Task<IEnumerable<(double, SearchResult)>> SearchBlocksByMichHash(NpgsqlConnection db, int[] chains, byte[] hash, int limit)
    {
        var rows = await db.QueryAsync("""
            SELECT "ChainId", "Level", "Timestamp", "Layer", "Hash", "MichelsonHash"
            FROM "Blocks"
            WHERE "ChainId" = ANY (@chains) AND ("Hash" = @hash OR "MichelsonHash" = @hash)
            ORDER BY "Id"
            LIMIT @limit
            """, new { chains, hash, limit });

        return rows.Select<dynamic, (double, SearchResult)>(row => (1, new BlockSearchResult
        {
            Chain = _chainCache.GetInfo((int)row.ChainId),
            Level = row.Level,
            Timestamp = row.Timestamp,
            Hash = Hashes.FormatBlockHash(row.Hash, (Data.Models.Layer)row.Layer),
            MichelsonHash = row.MichelsonHash is byte[] mh ? Hashes.FormatMichelsonBlockHash(mh) : null,
        }));
    }

    async Task<IEnumerable<(double, SearchResult)>> SearchBlocksByLevel(NpgsqlConnection db, int[] chains, int level, int limit)
    {
        var rows = await db.QueryAsync("""
            SELECT "ChainId", "Timestamp", "Layer", "Hash", "MichelsonHash"
            FROM "Blocks"
            WHERE "ChainId" = ANY (@chains) AND "Level" = @level
            ORDER BY "Id"
            LIMIT @limit
            """, new { chains, level, limit });

        return rows.Select<dynamic, (double, SearchResult)>(row => (1, new BlockSearchResult
        {
            Chain = _chainCache.GetInfo((int)row.ChainId),
            Level = level,
            Timestamp = row.Timestamp,
            Hash = Hashes.FormatBlockHash(row.Hash, (Data.Models.Layer)row.Layer),
            MichelsonHash = row.MichelsonHash is byte[] mh ? Hashes.FormatMichelsonBlockHash(mh) : null,
        }));
    }
    #endregion

    #region operations
    async Task<IEnumerable<(double, SearchResult)>> SearchOperationsByHash(NpgsqlConnection db, int[] chains, byte[] hash, string hashStr, int limit)
    {
        var rows = await db.QueryAsync("""
            -- manager
            SELECT "ChainId", "Level", "Timestamp" FROM "DepositOps"                WHERE "ChainId" = ANY (@chains) AND "Hash" = @hash UNION
            SELECT "ChainId", "Level", "Timestamp" FROM "IncreasePaidStorageOps"    WHERE "ChainId" = ANY (@chains) AND "Hash" = @hash UNION
            SELECT "ChainId", "Level", "Timestamp" FROM "OriginationOps"            WHERE "ChainId" = ANY (@chains) AND "Hash" = @hash UNION
            SELECT "ChainId", "Level", "Timestamp" FROM "RegisterConstantOps"       WHERE "ChainId" = ANY (@chains) AND "Hash" = @hash UNION
            SELECT "ChainId", "Level", "Timestamp" FROM "RevealOps"                 WHERE "ChainId" = ANY (@chains) AND "Hash" = @hash UNION
            SELECT "ChainId", "Level", "Timestamp" FROM "TransactionOps"            WHERE "ChainId" = ANY (@chains) AND "Hash" = @hash UNION
            SELECT "ChainId", "Level", "Timestamp" FROM "TransferTicketOps"         WHERE "ChainId" = ANY (@chains) AND "Hash" = @hash UNION
            SELECT "ChainId", "Level", "Timestamp" FROM "DalPublishCommitmentOps"   WHERE "ChainId" = ANY (@chains) AND "Hash" = @hash UNION
            SELECT "ChainId", "Level", "Timestamp" FROM "DelegationOps"             WHERE "ChainId" = ANY (@chains) AND "Hash" = @hash UNION
            SELECT "ChainId", "Level", "Timestamp" FROM "SetDelegateParametersOps"  WHERE "ChainId" = ANY (@chains) AND "Hash" = @hash UNION
            SELECT "ChainId", "Level", "Timestamp" FROM "SetDepositsLimitOps"       WHERE "ChainId" = ANY (@chains) AND "Hash" = @hash UNION
            SELECT "ChainId", "Level", "Timestamp" FROM "SmartRollupOriginateOps"   WHERE "ChainId" = ANY (@chains) AND "Hash" = @hash UNION
            SELECT "ChainId", "Level", "Timestamp" FROM "SmartRollupAddMessagesOps" WHERE "ChainId" = ANY (@chains) AND "Hash" = @hash UNION
            SELECT "ChainId", "Level", "Timestamp" FROM "SmartRollupCementOps"      WHERE "ChainId" = ANY (@chains) AND "Hash" = @hash UNION
            SELECT "ChainId", "Level", "Timestamp" FROM "SmartRollupExecuteOps"     WHERE "ChainId" = ANY (@chains) AND "Hash" = @hash UNION
            SELECT "ChainId", "Level", "Timestamp" FROM "SmartRollupPublishOps"     WHERE "ChainId" = ANY (@chains) AND "Hash" = @hash UNION
            SELECT "ChainId", "Level", "Timestamp" FROM "SmartRollupRecoverBondOps" WHERE "ChainId" = ANY (@chains) AND "Hash" = @hash UNION
            SELECT "ChainId", "Level", "Timestamp" FROM "SmartRollupRefuteOps"      WHERE "ChainId" = ANY (@chains) AND "Hash" = @hash UNION
            SELECT "ChainId", "Level", "Timestamp" FROM "StakingOps"                WHERE "ChainId" = ANY (@chains) AND "Hash" = @hash UNION
            SELECT "ChainId", "Level", "Timestamp" FROM "UpdateSecondaryKeyOps"     WHERE "ChainId" = ANY (@chains) AND "Hash" = @hash UNION
            -- anonymous
            SELECT "ChainId", "Level", "Timestamp" FROM "ActivationOps"             WHERE "ChainId" = ANY (@chains) AND "Hash" = @hash UNION
            SELECT "ChainId", "Level", "Timestamp" FROM "DalEntrapmentEvidenceOps"  WHERE "ChainId" = ANY (@chains) AND "Hash" = @hash UNION
            SELECT "ChainId", "Level", "Timestamp" FROM "DoubleBakingOps"           WHERE "ChainId" = ANY (@chains) AND "Hash" = @hash UNION
            SELECT "ChainId", "Level", "Timestamp" FROM "DoubleConsensusOps"        WHERE "ChainId" = ANY (@chains) AND "Hash" = @hash UNION
            SELECT "ChainId", "Level", "Timestamp" FROM "DrainDelegateOps"          WHERE "ChainId" = ANY (@chains) AND "Hash" = @hash UNION
            SELECT "ChainId", "Level", "Timestamp" FROM "NonceRevelationOps"        WHERE "ChainId" = ANY (@chains) AND "Hash" = @hash UNION
            SELECT "ChainId", "Level", "Timestamp" FROM "VdfRevelationOps"          WHERE "ChainId" = ANY (@chains) AND "Hash" = @hash UNION
            -- governance
            SELECT "ChainId", "Level", "Timestamp" FROM "BallotOps"                 WHERE "ChainId" = ANY (@chains) AND "Hash" = @hash UNION
            SELECT "ChainId", "Level", "Timestamp" FROM "ProposalOps"               WHERE "ChainId" = ANY (@chains) AND "Hash" = @hash UNION
            -- consensus
            SELECT "ChainId", "Level", "Timestamp" FROM "AttestationOps"            WHERE "ChainId" = ANY (@chains) AND "Hash" = @hash UNION
            SELECT "ChainId", "Level", "Timestamp" FROM "PreattestationOps"         WHERE "ChainId" = ANY (@chains) AND "Hash" = @hash
            -- 
            ORDER BY "Timestamp" DESC, "ChainId"
            LIMIT @limit
            """, new { chains, hash, limit });

        return rows.Select<dynamic, (double, SearchResult)>(row => (1, new OperationSearchResult
        {
            Chain = _chainCache.GetInfo((int)row.ChainId),
            Level = row.Level,
            Timestamp = row.Timestamp,
            Hash = hashStr,
        }));
    }
    #endregion

    #region tokens
    async Task<IEnumerable<(double, SearchResult)>> SearchTokensByContract(NpgsqlConnection db, int contractId, int limit)
    {
        var rows = await db.QueryAsync("""
            SELECT "Id", "ChainId", "ContractId", "TokenId", "Tags", "Name", "Symbol", "Decimals"
            FROM "Tokens"
            WHERE "ContractId" = @contractId
            ORDER BY "TokenId" DESC
            LIMIT @limit
            """, new { contractId, limit });

        return rows.Select<dynamic, (double, SearchResult)>(row => (1, new TokenSearchResult
        {
            Id = row.Id,
            Chain = _chainCache.GetInfo((int)row.ChainId),
            Contract = _addressCache.GetInfo((int)row.ContractId),
            TokenId = row.TokenId,
            Standard = TokenStandards.ToString((int)row.Tags),
            Name = row.Name,
            Symbol = row.Symbol,
            Decimals = row.Decimals,
        }));
    }

    async Task<IEnumerable<(double, SearchResult)>> SearchTokensByNameOrSymbol(NpgsqlConnection db, int[] chains, string query, int limit)
    {
        var lowered = query.ToLowerInvariant();
        var escaped = lowered.Replace(@"\", @"\\").Replace("%", @"\%").Replace("_", @"\_");

        IEnumerable<dynamic> rows;
        if (lowered.Length < FuzzyMatcher.MinFuzzyLength)
        {
            // Queries too short for a trigram are matched against the symbol only, by equality and prefix.
            // Note 'lower(col) LIKE' rather than 'col ILIKE': btree can serve the former, while
            // case-insensitive matching is something it can't do at all.
            rows = await db.QueryAsync("""
                SELECT
                    CASE WHEN lower("Symbol") = @lowered THEN 1.0::real ELSE 0.9::real END AS score,
                    "Id", "ChainId", "ContractId", "TokenId", "Tags", "Name", "Symbol", "Decimals"
                FROM "Tokens"
                WHERE "ChainId" = ANY (@chains)
                AND lower("Symbol") LIKE @prefix
                ORDER BY score DESC, "Id" DESC
                LIMIT @limit
                """, new { prefix = $"{escaped}%", chains, lowered, limit });
        }
        else if (_dbInit.PgTrgm)
        {
            rows = await db.QueryAsync("""
                SELECT
                    CASE
                        WHEN lower("Symbol") = @lowered OR lower("Name") = @lowered THEN 1.0::real
                        WHEN "Symbol" ILIKE @prefix   OR "Name" ILIKE @prefix       THEN 0.9::real
                        WHEN "Symbol" ILIKE @contains OR "Name" ILIKE @contains     THEN 0.7::real
                        ELSE GREATEST(word_similarity(@lowered, "Symbol"), word_similarity(@lowered, "Name")) * 0.6::real
                    END AS score,
                    "Id", "ChainId", "ContractId", "TokenId", "Tags", "Name", "Symbol", "Decimals"
                FROM "Tokens"
                WHERE "ChainId" = ANY (@chains)
                AND ("Symbol" ILIKE @contains OR "Name" ILIKE @contains OR
                    @lowered % "Symbol" OR @lowered % "Name" OR
                    @lowered <% "Symbol" OR @lowered <% "Name")
                ORDER BY score DESC, "Id" DESC
                LIMIT @limit
                """, new { contains = $"%{escaped}%", prefix = $"{escaped}%", chains, lowered, limit });
        }
        else
        {
            // no fuzzy tier without pg_trgm
            rows = await db.QueryAsync("""
                SELECT
                    CASE
                        WHEN lower("Symbol") = @lowered OR lower("Name") = @lowered THEN 1.0::real
                        WHEN "Symbol" ILIKE @prefix     OR "Name" ILIKE @prefix     THEN 0.9::real
                        ELSE 0.7::real
                    END AS score,
                    "Id", "ChainId", "ContractId", "TokenId", "Tags", "Name", "Symbol", "Decimals"
                FROM "Tokens"
                WHERE "ChainId" = ANY (@chains)
                AND ("Symbol" ILIKE @contains OR "Name" ILIKE @contains)
                ORDER BY score DESC, "Id" DESC
                LIMIT @limit
                """, new { contains = $"%{escaped}%", prefix = $"{escaped}%", chains, lowered, limit });
        }

        var contractIds = new List<int>(rows.Count());
        foreach (var row in rows)
            contractIds.Add((int)row.ContractId);

        await _addressCache.PreloadAsync(contractIds);

        return rows.Select<dynamic, (double, SearchResult)>(row => ((double)row.score, new TokenSearchResult
        {
            Id = row.Id,
            Chain = _chainCache.GetInfo((int)row.ChainId),
            Contract = _addressCache.GetInfo((int)row.ContractId),
            TokenId = row.TokenId,
            Standard = TokenStandards.ToString((int)row.Tags),
            Name = row.Name,
            Symbol = row.Symbol,
            Decimals = row.Decimals,
        }));
    }
    #endregion

    static string NormalizeQuery(string query)
    {
        var res = query.Trim();

        // postgres text params can't contain NUL, and no searchable value contains control chars
        if (res.Any(char.IsControl))
            res = new string([.. res.Where(x => !char.IsControl(x))]).Trim();

        // hex values are always stored lowercase, while base58 ones are case-sensitive
        return res.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? res.ToLowerInvariant() : res;
    }

    (int[], int[]) ResolveChains(ChainInfoParameter? filter)
    {
        var chains = _chainCache.Get();
        var xChains = chains.Where(x => x.Layer == Data.Models.Layer.TezosX);

        if (filter == null || filter.IsEmpty())
            return ([.. chains.Select(x => x.Id)], [..xChains.Select(x => x.Id)]);

        var id = filter.Id + filter.ChainId?.ToIdParameter(_chainCache);
        if (id == null)
            return ([.. chains.Select(x => x.Id)], [.. xChains.Select(x => x.Id)]);

        return ([.. chains.Select(x => x.Id).Where(id.Matches)], [.. xChains.Select(x => x.Id).Where(id.Matches)]);
    }
}
