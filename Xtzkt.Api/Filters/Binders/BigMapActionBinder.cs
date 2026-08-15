using Microsoft.AspNetCore.Mvc.ModelBinding;
using Xtzkt.Api.Extensions;
using Xtzkt.Api.Filters.Parameters;
using Xtzkt.Api.Models.Enums;

namespace Xtzkt.Api.Filters.Binders;

public class BigMapActionBinder : IModelBinder
{
    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        var param = bindingContext.ModelName;
        var hasValue = false;

        if (!bindingContext.TryGetEnum(BigMapActions.Mapping, $"{param}", ref hasValue, out var value))
            return Task.CompletedTask;

        if (!bindingContext.TryGetEnum(BigMapActions.Mapping, $"{param}.eq", ref hasValue, out var eq))
            return Task.CompletedTask;

        if (!bindingContext.TryGetEnum(BigMapActions.Mapping, $"{param}.ne", ref hasValue, out var ne))
            return Task.CompletedTask;

        if (!bindingContext.TryGetEnumList(BigMapActions.Mapping, $"{param}.in", ref hasValue, out var @in))
            return Task.CompletedTask;

        if (!bindingContext.TryGetEnumList(BigMapActions.Mapping, $"{param}.ni", ref hasValue, out var ni))
            return Task.CompletedTask;

        bindingContext.Result = ModelBindingResult.Success(!hasValue ? null : new BigMapActionParameter
        {
            Eq = value ?? eq,
            Ne = ne,
            In = @in,
            Ni = ni
        });

        return Task.CompletedTask;
    }
}
