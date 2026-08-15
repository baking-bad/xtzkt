using System.Text;
using Microsoft.AspNetCore.Mvc;
using Xtzkt.Api.Filters.Base;
using Xtzkt.Api.Filters.Binders;
using Xtzkt.Api.Services.Cache;

namespace Xtzkt.Api.Filters.Parameters;

[ModelBinder(BinderType = typeof(AddressHashBinder))]
public class AddressHashParameter : INormalizable
{
    /// <summary>
    /// **Equal** mode (default mode, so `param.eq=value` is the same as `param=value`).
    /// Returns items where 'param' is equal to 'value'.
    ///
    /// Example: `?type=l1_baker`.
    /// </summary>
    public string? Eq { get; set; }

    /// <summary>
    /// **Not equal** mode.
    /// Returns items where 'param' is not equal to 'value'.
    ///
    /// Example: `?type.ne=l1_ghost`.
    /// </summary>
    public string? Ne { get; set; }

    /// <summary>
    /// **In list** mode.
    /// Returns items where 'param' is equal to any of comma-separated 'values'.
    ///
    /// Example: `?type.in=l1_user,l1_baker`.
    /// </summary>
    public List<string>? In { get; set; }

    /// <summary>
    /// **Not in list** mode.
    /// Returns items where 'param' is not equal to any of comma-separated 'values'.
    ///
    /// Example: `?type.ni=l1_ghost,x_michelson_ghost`.
    /// </summary>
    public List<string>? Ni { get; set; }

    public async Task<Int32Parameter?> ToIdParameter(AddressCache cache, int? chainId)
    {
        var id = new Int32Parameter();

        if (chainId is int _chainId)
        {
            if (Eq is string eq)
            {
                id.Eq = (await cache.GetAsync(_chainId, eq))?.Id ?? -1;
                return id;
            }

            if (Ne is string ne)
            {
                id.Ne = (await cache.GetAsync(_chainId, ne))?.Id;
                return id;
            }

            if (In is List<string> @in)
            {
                var addresses = await cache.GetAsync(_chainId, @in);
                if (addresses.Count == 0)
                {
                    id.Eq = -1;
                    return id;
                }

                if (addresses.Count == 1)
                {
                    id.Eq = addresses[0].Id;
                    return id;
                }

                id.In = [.. addresses.Select(x => x.Id)];
                return id;
            }

            if (Ni is List<string> ni)
            {
                var addresses = await cache.GetAsync(_chainId, ni);
                if (addresses.Count == 0)
                {
                    id.Ne = null;
                    return id;
                }

                if (addresses.Count == 1)
                {
                    id.Ne = addresses[0].Id;
                    return id;
                }

                id.Ni = [.. addresses.Select(x => x.Id)];
                return id;
            }
        }
        else
        {
            if (Eq is string eq)
            {
                var addresses = await cache.GetAsync(eq);
                if (addresses.Count == 0)
                {
                    id.Eq = -1;
                    return id;
                }

                if (addresses.Count == 1)
                {
                    id.Eq = addresses[0].Id;
                    return id;
                }

                id.In = [.. addresses.Select(x => x.Id)];
                return id;
            }

            if (Ne is string ne)
            {
                var addresses = await cache.GetAsync(ne);
                if (addresses.Count == 0)
                {
                    id.Ne = null;
                    return id;
                }

                if (addresses.Count == 1)
                {
                    id.Ne = addresses[0].Id;
                    return id;
                }

                id.Ni = [.. addresses.Select(x => x.Id)];
                return id;
            }

            if (In is List<string> @in)
            {
                var addresses = await cache.GetAsync(@in);
                if (addresses.Count == 0)
                {
                    id.Eq = -1;
                    return id;
                }

                if (addresses.Count == 1)
                {
                    id.Eq = addresses[0].Id;
                    return id;
                }

                id.In = [.. addresses.Select(x => x.Id)];
                return id;
            }

            if (Ni is List<string> ni)
            {
                var addresses = await cache.GetAsync(ni);
                if (addresses.Count == 0)
                {
                    id.Ne = null;
                    return id;
                }

                if (addresses.Count == 1)
                {
                    id.Ne = addresses[0].Id;
                    return id;
                }

                id.Ni = [.. addresses.Select(x => x.Id)];
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
