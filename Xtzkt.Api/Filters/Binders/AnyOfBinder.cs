using Microsoft.AspNetCore.Mvc.ModelBinding;
using Xtzkt.Api.Extensions;
using Xtzkt.Api.Filters.Parameters;
using Xtzkt.Api.Services.Cache;

namespace Xtzkt.Api.Filters.Binders;

public class AnyOfBinder(AddressCache _addressCache) : IModelBinder
{
    public async Task BindModelAsync(ModelBindingContext bindingContext)
    {
        var key = bindingContext.HttpContext.Request.Query.Keys.FirstOrDefault(x => x.StartsWith("anyof."));
        if (key == null)
        {
            bindingContext.Result = ModelBindingResult.Success(null);
            return;
        }

        var ss = key.Split(".", StringSplitOptions.RemoveEmptyEntries);
        var (mode, skip) = ss[^1] switch
        {
            "eq" => (3, 1),
            "in" => (3, 1),
            _ => (0, 0)
        };
        key = key[..^mode];

        var fields = ss.Skip(1).SkipLast(skip);
        if (fields.Count() < 2)
        {
            bindingContext.ModelState.TryAddModelError(key, "Invalid syntax of `anyof` parameter. At least two fields must be specified, e.g. `anyof.field1.field2=value`.");
            return;
        }

        var hasValue = false;

        if (!bindingContext.TryGetAddressHashNull($"{key}", ref hasValue, out var value))
            return;

        if (!bindingContext.TryGetAddressHashNull($"{key}.eq", ref hasValue, out var eq))
            return;

        if (!bindingContext.TryGetAddressHashNullList($"{key}.in", ref hasValue, out var @in))
            return;

        if (!hasValue)
        {
            bindingContext.Result = ModelBindingResult.Success(null);
            return;
        }

        var anyof = new AnyOfParameter { Fields = fields };

        if ((value ?? eq) is string _eq)
        {
            if (@in != null)
            {
                if (!@in.Contains(_eq))
                {
                    bindingContext.ModelState.TryAddModelError($"{key}.in", "Conflicts with `.eq`.");
                    return;
                }
                @in = null;
            }

            if (_eq == AddressHashNullParameter.Null)
            {
                anyof.Eq = Int32NullParameter.Null;
            }
            else
            {
                var addresses = await _addressCache.GetAsync(_eq);
                if (addresses.Count == 0) anyof.Eq = -1;
                else if (addresses.Count == 1) anyof.Eq = addresses[0].Id;
                else anyof.In = [.. addresses.Select(x => x.Id)];
            }
        }
        
        if (@in is List<string> _in)
        {
            if (_in.Contains(AddressHashNullParameter.Null))
            {
                var addresses = await _addressCache.GetAsync([.. _in.Where(x => x != AddressHashNullParameter.Null)]);
                if (addresses.Count == 0) anyof.Eq = Int32NullParameter.Null;
                else anyof.In = [.. addresses.Select(x => x.Id).Append(Int32NullParameter.Null)];
            }
            else
            {
                var addresses = await _addressCache.GetAsync(_in);
                if (addresses.Count == 0) anyof.Eq = -1;
                else if (addresses.Count == 1) anyof.Eq = addresses[0].Id;
                else anyof.In = [.. addresses.Select(x => x.Id)];
            }
        }

        bindingContext.Result = ModelBindingResult.Success(anyof);
    }
}
