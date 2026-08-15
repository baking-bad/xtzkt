using Microsoft.AspNetCore.Mvc.ModelBinding;
using Xtzkt.Api.Extensions;
using Xtzkt.Api.Filters.Parameters;

namespace Xtzkt.Api.Filters.Binders;

public class AddressHashEqBinder : IModelBinder
{
    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        var param = bindingContext.ModelName;
        var hasValue = false;

        if (!bindingContext.TryGetAddressHash($"{param}", ref hasValue, out var value))
            return Task.CompletedTask;

        if (!bindingContext.TryGetAddressHash($"{param}.eq", ref hasValue, out var eq))
            return Task.CompletedTask;

        if (!bindingContext.TryGetAddressHashList($"{param}.in", ref hasValue, out var @in))
            return Task.CompletedTask;

        var _eq = value ?? eq;
        if (_eq != null)
        {
            if (@in != null)
            {
                if (!@in.Contains(_eq))
                {
                    bindingContext.ModelState.TryAddModelError($"{param}.in", "Conflicts with `.eq`.");
                    return Task.CompletedTask;
                }
                @in = null;
            }
        }

        if (@in != null)
        {
            if (@in.Count == 1)
            {
                _eq = @in[0];
                @in = null;
            }
        }

        bindingContext.Result = ModelBindingResult.Success(!hasValue ? null : new AddressHashEqParameter
        {
            Eq = _eq,
            In = @in,
        });

        return Task.CompletedTask;
    }
}
