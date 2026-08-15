using Microsoft.AspNetCore.Mvc.ModelBinding;
using Xtzkt.Api.Extensions;
using Xtzkt.Api.Filters.Parameters;

namespace Xtzkt.Api.Filters.Binders;

public class MichelineBinder : IModelBinder
{
    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        var param = bindingContext.ModelName;
        var hasValue = false;

        if (!bindingContext.TryGetMicheline($"{param}", ref hasValue, out var value))
            return Task.CompletedTask;

        if (!bindingContext.TryGetMicheline($"{param}.eq", ref hasValue, out var eq))
            return Task.CompletedTask;

        if (!bindingContext.TryGetMicheline($"{param}.ne", ref hasValue, out var ne))
            return Task.CompletedTask;

        bindingContext.Result = ModelBindingResult.Success(!hasValue ? null : new MichelineParameter
        {
            Eq = value ?? eq,
            Ne = ne,
        });

        return Task.CompletedTask;
    }
}
