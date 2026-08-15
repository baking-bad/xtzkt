using System.Text;
using Microsoft.AspNetCore.Mvc;
using Xtzkt.Api.Filters.Base;
using Xtzkt.Api.Filters.Binders;
using Xtzkt.Api.Services.Cache;

namespace Xtzkt.Api.Filters.Parameters;

[ModelBinder(BinderType = typeof(AddressHashNullBinder))]
public class AddressHashNullParameter : INormalizable
{
    /// <summary>
    /// Sentinel value used to represent a `null` filter.
    /// </summary>
    public const string Null = "";

    /// <summary>
    /// **Equal** mode (default mode, so `param.eq=value` is the same as `param=value`).
    /// Returns items where 'param' is equal to 'value'. Use `null` to get items where 'param' is not set.
    ///
    /// Example: `?target=KT1...` or `?target=null`.
    /// </summary>
    public string? Eq { get; set; }

    /// <summary>
    /// **Not equal** mode.
    /// Returns items where 'param' is not equal to 'value'. Use `null` to get items where 'param' is set.
    ///
    /// Example: `?target.ne=KT1...` or `?target.ne=null`.
    /// </summary>
    public string? Ne { get; set; }

    /// <summary>
    /// **In list** mode.
    /// Returns items where 'param' is equal to any of comma-separated 'values'. Use `null` to include items where 'param' is not set.
    ///
    /// Example: `?target.in=KT1...,tz1...` or `?target.in=KT1...,null`.
    /// </summary>
    public List<string>? In { get; set; }

    /// <summary>
    /// **Not in list** mode.
    /// Returns items where 'param' is not equal to any of comma-separated 'values'. Use `null` to exclude items where 'param' is not set.
    ///
    /// Example: `?target.ni=KT1...,tz1...` or `?target.ni=KT1...,null`.
    /// </summary>
    public List<string>? Ni { get; set; }

    public async Task<Int32NullParameter?> ToIdParameter(AddressCache cache, int? chainId)
    {
        var id = new Int32NullParameter();

        if (chainId is int _chainId)
        {
            if (Eq is string eq)
            {
                id.Eq = eq == Null ? Int32NullParameter.Null : ((await cache.GetAsync(_chainId, eq))?.Id ?? -1);
                return id;
            }

            if (Ne is string ne)
            {
                id.Ne = ne == Null ? Int32NullParameter.Null : (await cache.GetAsync(_chainId, ne))?.Id;
                return id;
            }

            if (In is List<string> @in)
            {
                var hasNull = @in.Contains(Null);
                var addresses = await cache.GetAsync(_chainId, hasNull ? [.. @in.Where(x => x != Null)] : @in);
                var ids = addresses.Select(x => x.Id).ToList();
                if (hasNull) ids.Add(Int32NullParameter.Null);

                if (ids.Count == 0) { id.Eq = -1; return id; }
                if (ids.Count == 1) { id.Eq = ids[0]; return id; }
                id.In = ids;
                return id;
            }

            if (Ni is List<string> ni)
            {
                var hasNull = ni.Contains(Null);
                var addresses = await cache.GetAsync(_chainId, hasNull ? [.. ni.Where(x => x != Null)] : ni);
                var ids = addresses.Select(x => x.Id).ToList();
                if (hasNull) ids.Add(Int32NullParameter.Null);

                if (ids.Count == 0) { id.Ne = null; return id; }
                if (ids.Count == 1) { id.Ne = ids[0]; return id; }
                id.Ni = ids;
                return id;
            }
        }
        else
        {
            if (Eq is string eq)
            {
                if (eq == Null) { id.Eq = Int32NullParameter.Null; return id; }
                var addresses = await cache.GetAsync(eq);
                if (addresses.Count == 0) { id.Eq = -1; return id; }
                if (addresses.Count == 1) { id.Eq = addresses[0].Id; return id; }
                id.In = [.. addresses.Select(x => x.Id)];
                return id;
            }

            if (Ne is string ne)
            {
                if (ne == Null) { id.Ne = Int32NullParameter.Null; return id; }
                var addresses = await cache.GetAsync(ne);
                if (addresses.Count == 0) { id.Ne = null; return id; }
                if (addresses.Count == 1) { id.Ne = addresses[0].Id; return id; }
                id.Ni = [.. addresses.Select(x => x.Id)];
                return id;
            }

            if (In is List<string> @in)
            {
                var hasNull = @in.Contains(Null);
                var addresses = await cache.GetAsync(hasNull ? [.. @in.Where(x => x != Null)] : @in);
                var ids = addresses.Select(x => x.Id).ToList();
                if (hasNull) ids.Add(Int32NullParameter.Null);

                if (ids.Count == 0) { id.Eq = -1; return id; }
                if (ids.Count == 1) { id.Eq = ids[0]; return id; }
                id.In = ids;
                return id;
            }

            if (Ni is List<string> ni)
            {
                var hasNull = ni.Contains(Null);
                var addresses = await cache.GetAsync(hasNull ? [.. ni.Where(x => x != Null)] : ni);
                var ids = addresses.Select(x => x.Id).ToList();
                if (hasNull) ids.Add(Int32NullParameter.Null);

                if (ids.Count == 0) { id.Ne = null; return id; }
                if (ids.Count == 1) { id.Ne = ids[0]; return id; }
                id.Ni = ids;
                return id;
            }
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
