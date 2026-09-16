using Microsoft.AspNetCore.Mvc.ModelBinding;
using Xtzkt.Api.Extensions;
using Xtzkt.Api.Filters.Parameters;

namespace Xtzkt.Api.Filters.Binders;

public class ChainIdEqBinder : IModelBinder
{
    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        var param = bindingContext.ModelName;
        var hasValue = false;

        if (bindingContext.ThrowUnsupportedMode($"{param}.ne") ||
            bindingContext.ThrowUnsupportedMode($"{param}.in") ||
            bindingContext.ThrowUnsupportedMode($"{param}.ni"))
            return Task.CompletedTask;

        if (!bindingContext.TryGetHexOrBase58($"{param}", ref hasValue, out var value, "Net", 15))
            return Task.CompletedTask;

        if (!bindingContext.TryGetHexOrBase58($"{param}.eq", ref hasValue, out var eq, "Net", 15))
            return Task.CompletedTask;

        bindingContext.Result = ModelBindingResult.Success(!hasValue ? null : new ChainIdEqParameter
        {
            Eq = value ?? eq,
        });

        return Task.CompletedTask;
    }
}
