using Microsoft.AspNetCore.Mvc.ModelBinding;
using Xtzkt.Api.Extensions;
using Xtzkt.Api.Filters.Parameters;
using Xtzkt.Api.Models.Enums;

namespace Xtzkt.Api.Filters.Binders;

public class RuntimeBinder : IModelBinder
{
    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        var param = bindingContext.ModelName;
        var hasValue = false;

        if (!bindingContext.TryGetEnum(Runtimes.Mapping, $"{param}", ref hasValue, out var value))
            return Task.CompletedTask;

        if (!bindingContext.TryGetEnum(Runtimes.Mapping, $"{param}.eq", ref hasValue, out var eq))
            return Task.CompletedTask;

        if (!bindingContext.TryGetEnum(Runtimes.Mapping, $"{param}.ne", ref hasValue, out var ne))
            return Task.CompletedTask;

        bindingContext.Result = ModelBindingResult.Success(!hasValue ? null : new RuntimeParameter
        {
            Eq = value ?? eq,
            Ne = ne,
        });

        return Task.CompletedTask;
    }
}
