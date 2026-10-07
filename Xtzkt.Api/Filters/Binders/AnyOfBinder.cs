using Microsoft.AspNetCore.Mvc.ModelBinding;
using Xtzkt.Api.Extensions;
using Xtzkt.Api.Filters.Parameters;
using Xtzkt.Api.Services.Cache;

namespace Xtzkt.Api.Filters.Binders;

public class AnyOfBinder(AddressCache _addressCache) : IModelBinder
{
    public async Task BindModelAsync(ModelBindingContext bindingContext)
    {
        var root = $"{bindingContext.ModelName}.";
        var key = bindingContext.HttpContext.Request.Query.Keys.FirstOrDefault(x => x.StartsWith(root, StringComparison.OrdinalIgnoreCase));
        if (key == null)
        {
            bindingContext.Result = ModelBindingResult.Success(null);
            return;
        }

        // anyof.field1.field2[.id|.hash][.eq|.in]
        var ss = key.Split('.');
        if (ss.Contains(""))
        {
            bindingContext.ModelState.TryAddModelError(key, "Invalid syntax of `anyof` parameter. Field names must not be empty.");
            return;
        }

        var fields = ss.Skip(1).ToList();
        if (fields.Count != 0 && fields[^1] is "eq" or "in")
            fields.RemoveAt(fields.Count - 1);
        if (fields.Count != 0 && fields[^1] is "id" or "hash")
            fields.RemoveAt(fields.Count - 1);

        key = $"{ss[0]}.{string.Join('.', fields)}";

        if (fields.Count < 2)
        {
            bindingContext.ModelState.TryAddModelError(key, "Invalid syntax of `anyof` parameter. At least two fields must be specified, e.g. `anyof.field1.field2=value`.");
            return;
        }

        var hasIds = false;
        if (!TryGetIds(bindingContext, $"{key}.id", ref hasIds, out var ids))
            return;

        if (!hasIds && !TryGetIds(bindingContext, key, ref hasIds, out ids))
            return;

        var hasHashes = false;
        if (!TryGetHashes(bindingContext, $"{key}.hash", ref hasHashes, out var hashes))
            return;

        if (!hasIds && !hasHashes)
        {
            bindingContext.Result = ModelBindingResult.Success(null);
            return;
        }

        if (hasHashes)
        {
            var resolved = new HashSet<int>();

            if (hashes!.Remove(AddressHashNullParameter.Null))
                resolved.Add(Int32NullParameter.Null);

            if (hashes.Count != 0)
                foreach (var address in await _addressCache.GetAsync([.. hashes]))
                    resolved.Add(address.Id);

            if (hasIds)
                ids!.IntersectWith(resolved);
            else
                ids = resolved;
        }

        bindingContext.Result = ModelBindingResult.Success(new AnyOfParameter
        {
            Fields = fields,
            Eq = ids!.Count switch
            {
                0 => -1, // nothing matches
                1 => ids.First(),
                _ => null,
            },
            In = ids.Count > 1 ? [.. ids] : null,
        });
    }

    static bool TryGetIds(ModelBindingContext bindingContext, string name, ref bool hasValue, out HashSet<int>? result)
    {
        result = null;

        if (!bindingContext.TryGetInt32Null(name, ref hasValue, out var value))
            return false;

        if (!bindingContext.TryGetInt32Null($"{name}.eq", ref hasValue, out var eq))
            return false;

        if (!bindingContext.TryGetInt32NullList($"{name}.in", ref hasValue, out var @in))
            return false;

        if (!hasValue)
            return true;

        if ((value ?? eq) is int _eq)
        {
            if (@in != null && !@in.Contains(_eq))
            {
                bindingContext.ModelState.TryAddModelError($"{name}.in", "Conflicts with `.eq`.");
                return false;
            }
            result = [_eq];
        }
        else
        {
            result = [.. @in!];
        }

        return true;
    }

    static bool TryGetHashes(ModelBindingContext bindingContext, string name, ref bool hasValue, out HashSet<string>? result)
    {
        result = null;

        if (!bindingContext.TryGetAddressHashNull(name, ref hasValue, out var value))
            return false;

        if (!bindingContext.TryGetAddressHashNull($"{name}.eq", ref hasValue, out var eq))
            return false;

        if (!bindingContext.TryGetAddressHashNullList($"{name}.in", ref hasValue, out var @in))
            return false;

        if (!hasValue)
            return true;

        if ((value ?? eq) is string _eq)
        {
            if (@in != null && !@in.Contains(_eq))
            {
                bindingContext.ModelState.TryAddModelError($"{name}.in", "Conflicts with `.eq`.");
                return false;
            }
            result = [_eq];
        }
        else
        {
            result = [.. @in!];
        }

        return true;
    }
}
