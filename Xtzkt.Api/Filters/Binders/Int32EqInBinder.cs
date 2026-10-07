using Microsoft.AspNetCore.Mvc.ModelBinding;
using Xtzkt.Api.Extensions;
using Xtzkt.Api.Filters.Parameters;

namespace Xtzkt.Api.Filters.Binders;

public class Int32EqInBinder : IModelBinder
{
    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        var param = bindingContext.ModelName;
        var hasValue = false;

        if (bindingContext.ThrowUnsupportedMode($"{param}.ne") ||
            bindingContext.ThrowUnsupportedMode($"{param}.ni") ||
            bindingContext.ThrowUnsupportedMode($"{param}.gt") ||
            bindingContext.ThrowUnsupportedMode($"{param}.ge") ||
            bindingContext.ThrowUnsupportedMode($"{param}.lt") ||
            bindingContext.ThrowUnsupportedMode($"{param}.le"))
            return Task.CompletedTask;

        if (!bindingContext.TryGetInt32($"{param}", ref hasValue, out var value))
            return Task.CompletedTask;

        if (!bindingContext.TryGetInt32($"{param}.eq", ref hasValue, out var eq))
            return Task.CompletedTask;

        if (!bindingContext.TryGetInt32List($"{param}.in", ref hasValue, out var @in))
            return Task.CompletedTask;

        var _eq = value ?? eq;
        if (_eq is int eqValue && @in != null)
        {
            if (!@in.Contains(eqValue))
            {
                bindingContext.ModelState.TryAddModelError($"{param}.in", "Conflicts with `.eq`.");
                return Task.CompletedTask;
            }
            @in = null;
        }

        if (@in?.Count == 1)
        {
            _eq = @in[0];
            @in = null;
        }

        bindingContext.Result = ModelBindingResult.Success(!hasValue ? null : new Int32EqInParameter
        {
            Eq = _eq,
            In = @in,
        });

        return Task.CompletedTask;
    }
}
