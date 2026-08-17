using System.Text;
using Microsoft.AspNetCore.Mvc;
using Xtzkt.Api.Filters.Base;
using Xtzkt.Api.Filters.Binders;
using Xtzkt.Api.Services.Cache;

namespace Xtzkt.Api.Filters.Parameters;

[ModelBinder(BinderType = typeof(ChainIdBinder))]
public class ChainIdParameter : INormalizable
{
    /// <summary>
    /// **Equal** mode (default mode, so `param.eq=value` is the same as `param=value`).
    /// Returns items where 'param' is equal to 'value'.
    ///
    /// Example: `?chainId=0x1f094`.
    /// </summary>
    public string? Eq { get; set; }

    /// <summary>
    /// **Not equal** mode.
    /// Returns items where 'param' is not equal to 'value'.
    ///
    /// Example: `?chainId.ne=NetY1Bqj2mNr74r`.
    /// </summary>
    public string? Ne { get; set; }

    /// <summary>
    /// **In list** mode.
    /// Returns items where 'param' is equal to any of comma-separated 'values'.
    ///
    /// Example: `?chainId.in=0x1f094,NetY1Bqj2mNr74r`.
    /// </summary>
    public List<string>? In { get; set; }

    /// <summary>
    /// **Not in list** mode.
    /// Returns items where 'param' is not equal to any of comma-separated 'values'.
    ///
    /// Example: `?chainId.ni=0x1f094,NetY1Bqj2mNr74r`.
    /// </summary>
    public List<string>? Ni { get; set; }

    public bool Matches(string value) =>
        (Eq == null || value == Eq) &&
        (Ne == null || value != Ne) &&
        (In == null || In.Contains(value)) &&
        (Ni == null || !Ni.Contains(value));

    public Int32Parameter ToIdParameter(ChainCache cache)
    {
        var id = new Int32Parameter();
        var chains = cache.Get();

        if (Eq is string eq)
        {
            id.Eq = chains.FirstOrDefault(x => x.ChainId == eq)?.Id ?? -1;
            return id;
        }

        if (Ne is string ne)
        {
            id.Ne = chains.FirstOrDefault(x => x.ChainId == ne)?.Id;
            return id;
        }

        if (In is List<string> @in)
        {
            var set = @in.ToHashSet();
            var ids = chains.Where(x => set.Contains(x.ChainId)).Select(x => x.Id).ToList();
            if (ids.Count == 0)
            {
                id.Eq = -1;
                return id;
            }

            if (ids.Count == 1)
            {
                id.Eq = ids[0];
                return id;
            }

            id.In = ids;
            return id;
        }

        if (Ni is List<string> ni)
        {
            var set = ni.ToHashSet();
            var ids = chains.Where(x => set.Contains(x.ChainId)).Select(x => x.Id).ToList();
            if (ids.Count == 0)
            {
                id.Ne = null;
                return id;
            }

            if (ids.Count == 1)
            {
                id.Ne = ids[0];
                return id;
            }

            id.Ni = ids;
            return id;
        }

        return id;
    }

    public string Normalize(string name)
    {
        var sb = new StringBuilder();

        if (Eq != null)
            sb.Append($"{name}.eq={Eq}&");

        if (Ne != null)
            sb.Append($"{name}.ne={Ne}&");

        if (In?.Count > 0)
            sb.Append($"{name}.in={string.Join(",", In.OrderBy(x => x))}&");

        if (Ni?.Count > 0)
            sb.Append($"{name}.ni={string.Join(",", Ni.OrderBy(x => x))}&");

        return sb.ToString();
    }
}
