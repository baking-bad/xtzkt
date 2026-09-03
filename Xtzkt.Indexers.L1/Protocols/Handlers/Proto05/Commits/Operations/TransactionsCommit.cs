using System.Text.Json;
using Netezos.Contracts;
using Netezos.Encoding;
using Xtzkt.Data.Models;
using Xtzkt.Data.Models.Operations.Abstract;
using Xtzkt.Indexers.Common.Extensions;
using Xtzkt.Indexers.Common.Helpers;
using Xtzkt.Utils;

namespace Xtzkt.Indexers.L1.Protocols.Proto05
{
    class TransactionsCommit(ProtocolHandler protocol) : Proto04.TransactionsCommit(protocol)
    {
        protected override async Task ProcessParameters(L1TransactionOperation transaction, L1Address target, JsonElement param)
        {
            string? rawEp = null;
            IMicheline? rawParam;
            try
            {
                rawEp = param.RequiredString("entrypoint");
                rawParam = Micheline.FromJson(param.Required("value"))!;
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to parse tx parameters");
                transaction.Entrypoint = rawEp ?? string.Empty;
                transaction.ParametersRaw = new MichelineArray().ToBytes();
                return;
            }

            if (target is L1Contract contract)
            {
                var schema = contract.Kind > L1ContractKind.DelegatorContract
                    ? (await Cache.Schemas.GetAsync(contract))
                    : MichelsonScript.ManagerTz;

                try
                {
                    var (normEp, normParam) = schema.NormalizeParameter(rawEp, rawParam);

                    transaction.Entrypoint = normEp;
                    transaction.ParametersRaw = schema.OptimizeParameter(normEp, normParam).ToBytes();
                    transaction.Parameters = Regexes.RestrictedUnicode().Replace(schema.HumanizeParameter(normEp, normParam), Regexes.NullEscapeString);
                    transaction.Guessed = false;
                }
                catch (Exception ex)
                {
                    transaction.Entrypoint ??= rawEp;
                    transaction.ParametersRaw ??= rawParam.ToBytes();
                    transaction.Guessed = false;

                    if (transaction.Status == OperationStatus.Applied)
                        Logger.LogError(ex, "Failed to humanize tx parameters");
                }
            }
            else if (target is L1SmartRollup smartRollup)
            {
                var schema = await Cache.Schemas.GetAsync(smartRollup);

                try
                {
                    var (normEp, normParam) = schema.NormalizeParameter(rawEp, rawParam);

                    transaction.Entrypoint = normEp;
                    transaction.ParametersRaw = schema.OptimizeParameter(normEp, normParam).ToBytes();
                    transaction.Parameters = Regexes.RestrictedUnicode().Replace(schema.HumanizeParameter(normEp, normParam), Regexes.NullEscapeString);
                    transaction.Guessed = false;
                }
                catch (Exception ex)
                {
                    transaction.Entrypoint ??= rawEp;
                    transaction.ParametersRaw ??= rawParam.ToBytes();
                    transaction.Guessed = false;

                    if (transaction.Status == OperationStatus.Applied)
                        Logger.LogError(ex, "Failed to humanize tx parameters");
                }
            }
            else
            {
                transaction.Entrypoint = rawEp;
                transaction.ParametersRaw = rawParam.ToBytes();
            }
        }

        protected override IMicheline NormalizeStorage(L1TransactionOperation transaction, IMicheline storage, ContractScript schema)
        {
            return storage;
        }

        protected override IEnumerable<BigMapDiff>? ParseBigMapDiffs(L1TransactionOperation transaction, JsonElement result)
        {
            return result.TryGetProperty("big_map_diff", out var diffs)
                ? diffs.RequiredArray().EnumerateArray().Select(BigMapDiff.Parse)
                : null;
        }
    }
}
